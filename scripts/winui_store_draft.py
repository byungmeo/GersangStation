"""Preserve a Microsoft Store MSIX draft, stage files, and NEVER submit it.

Uses only Python's standard library. See docs/winui-store-release.md for the
important distinction between API staging and Store package ingestion.
"""

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

from winui_store_package import identity_from_xml, inspect_archive, inspect_package, reject_duplicate

API_ROOT = "https://manage.devcenter.microsoft.com/v1.0/my"
MANIFEST = Path(__file__).resolve().parents[1] / "GersangStation.WinUI/GersangStation/Package.appxmanifest"
# The complete submission is round-tripped, but only documented editable
# metadata can be deliberately changed. Package additions use the upload command.
EDITABLE = {
    "applicationCategory", "pricing", "visibility", "targetPublishMode",
    "targetPublishDate", "listings", "hardwarePreferences", "automaticBackupEnabled",
    "canInstallOnRemovableMedia", "isGameDvrEnabled", "gamingOptions",
    "hasExternalInAppProducts", "meetAccessibilityGuidelines", "notesForCertification",
    "packageDeliveryOptions", "enterpriseLicensing",
    "allowMicrosoftDecideAppAvailabilityToFutureDeviceFamilies",
    "allowTargetFutureDeviceFamilies", "trailers",
}


def save_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sanitized(submission):
    """Exclude upload credentials and transient processing details from snapshots."""
    return {key: value for key, value in submission.items()
            if key not in ("fileUploadUrl", "statusDetails")}


def fingerprint(submission):
    encoded = json.dumps(sanitized(submission), sort_keys=True,
                         separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def merge_patch(original, changes):
    """Apply JSON Merge Patch: objects merge, arrays replace, null deletes a key."""
    if not isinstance(changes, dict):
        return copy.deepcopy(changes)
    result = copy.deepcopy(original) if isinstance(original, dict) else {}
    for key, value in changes.items():
        if value is None:
            result.pop(key, None)
        else:
            result[key] = merge_patch(result.get(key), value)
    return result


def verify_patch(actual, changes):
    """Check intended values, allowing IDs/default fields added by the server."""
    def matches_value(value, expected):
        if isinstance(expected, dict):
            return isinstance(value, dict) and all(
                key in value and matches_value(value[key], item) for key, item in expected.items())
        if isinstance(expected, list):
            return isinstance(value, list) and len(value) == len(expected) and all(
                matches_value(left, right) for left, right in zip(value, expected))
        return value == expected

    if not isinstance(changes, dict):
        return matches_value(actual, changes)
    return isinstance(actual, dict) and all(
        (key not in actual if value is None else verify_patch(actual.get(key), value))
        for key, value in changes.items())


def pending_files(value):
    if isinstance(value, dict):
        if value.get("fileStatus") == "PendingUpload":
            if not isinstance(value.get("fileName"), str) or not value["fileName"]:
                raise ValueError("PendingUpload asset has no fileName.")
            yield value["fileName"]
        for child in value.values():
            yield from pending_files(child)
    elif isinstance(value, list):
        for child in value:
            yield from pending_files(child)


def create_payload(submission, package, assets_dir, destination):
    """Supply every PendingUpload asset; never overwrite a ZIP with missing assets."""
    sources = {}
    names = set()
    for raw_name in pending_files(submission):
        name = raw_name.replace("\\", "/")
        relative = PurePosixPath(name)
        if relative.is_absolute() or ".." in relative.parts or ":" in name:
            raise ValueError("Unsafe pending asset path in draft.")
        if name.casefold() in names:
            if name not in sources:
                raise ValueError("Pending asset paths differ only in case.")
            continue
        names.add(name.casefold())
        if package and name == package.name:
            source = package
        elif assets_dir:
            source = (assets_dir / name).resolve()
            if not source.is_relative_to(assets_dir.resolve()):
                raise ValueError("Pending asset path leaves assets directory.")
        else:
            raise ValueError(f"Supply --assets-dir with existing pending asset: {name}")
        if not source.is_file():
            raise ValueError(f"Missing pending asset: {name}")
        sources[name] = source
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_STORED) as archive:
        for name, source in sources.items():
            archive.write(source, name)
    return len(sources)


def package_inventory(submission, report, assets_dir):
    """Resolve unprocessed package versions from the supplied pending files."""
    packages = []
    for item in submission.get("applicationPackages", []):
        name = item.get("fileName", "")
        if item.get("version") or not assets_dir or name.casefold() == report["fileName"].casefold():
            packages.append(item)
            continue
        relative = PurePosixPath(name.replace("\\", "/"))
        if not name or relative.is_absolute() or ".." in relative.parts or ":" in name:
            raise ValueError("Unsafe pending package path.")
        source = (assets_dir / str(relative)).resolve()
        if not source.is_relative_to(assets_dir.resolve()) or not source.is_file():
            raise ValueError("Supply the unprocessed package in --assets-dir to determine its version.")
        entries = [entry for entry in inspect_archive(source, name) if entry["kind"] == "package"
                   and not entry["identity"].get("ResourceId")]
        if not entries:
            raise ValueError("Cannot determine the existing pending package version.")
        for entry in entries:
            identity = entry["identity"]
            if (identity.get("Name"), identity.get("Publisher")) != (report["packageIdentityName"], report["publisher"]):
                raise ValueError("Supplied pending package belongs to a different identity/publisher.")
            packages.append({**item, "version": identity.get("Version"),
                             "architecture": identity.get("ProcessorArchitecture")})
    return {"complete": True, "packageIdentityName": report["packageIdentityName"], "packages": packages}


class StoreApi:
    def __init__(self):
        required = ("AZURE_AD_TENANT_ID", "AZURE_AD_APPLICATION_CLIENT_ID",
                    "AZURE_AD_APPLICATION_SECRET")
        missing = [key for key in required if not os.environ.get(key)]
        if missing:
            raise ValueError("Missing environment variables: " + ", ".join(missing))
        tenant = urllib.parse.quote(os.environ[required[0]], safe="")
        form = urllib.parse.urlencode({
            "grant_type": "client_credentials",
            "client_id": os.environ[required[1]],
            "client_secret": os.environ[required[2]],
            "resource": "https://manage.devcenter.microsoft.com",
        }).encode()
        request = urllib.request.Request(
            f"https://login.microsoftonline.com/{tenant}/oauth2/token", data=form,
            headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST")
        self.token = self._send(request, "authentication")["access_token"]

    @staticmethod
    def _send(request, operation):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                body = response.read()
                return json.loads(body) if body else {}
        except urllib.error.HTTPError as error:
            # Responses/URLs may contain SAS tokens or certification credentials.
            correlation = error.headers.get("MS-CorrelationId", error.headers.get("x-ms-request-id", "unknown"))
            correlation = re.sub(r"[^a-zA-Z0-9-]", "", correlation)[:100]
            raise RuntimeError(f"{operation}: HTTP {error.code}; correlation {correlation}. "
                               "No automatic retry. Check current draft before continuing.") from error
        except (urllib.error.URLError, TimeoutError) as error:
            raise RuntimeError(f"{operation}: network failure; remote outcome may be uncertain. "
                               "Inspect the draft before retrying.") from error

    def request(self, method, path, data=None):
        # Even an accidental future call cannot certify or delete a submission.
        allowed = (
            method == "GET" and re.fullmatch(r"/applications/[A-Za-z0-9]+(?:/submissions/[0-9]+)?", path)
            or method == "POST" and re.fullmatch(r"/applications/[A-Za-z0-9]+/submissions", path)
            or method == "PUT" and re.fullmatch(r"/applications/[A-Za-z0-9]+/submissions/[0-9]+", path)
            or method == "POST" and re.fullmatch(r"/applications/[A-Za-z0-9]+/submissions/[0-9]+/commit", path)
        )
        if not allowed:
            raise ValueError("This tool only reads, creates, and saves drafts. Operation refused.")
        encoded = json.dumps(data).encode("utf-8") if data is not None else None
        request = urllib.request.Request(API_ROOT + path, data=encoded, method=method,
                                        headers={"Authorization": "Bearer " + self.token,
                                                 "Content-Type": "application/json"})
        return self._send(request, method + " Store draft")

    def upload(self, url, path):
        parsed = urllib.parse.urlsplit(url)
        if (parsed.scheme != "https" or not (parsed.hostname or "").endswith(".blob.core.windows.net")
                or not urllib.parse.parse_qs(parsed.query).get("sig")):
            raise ValueError("Store did not return a recognized Azure Blob upload URL.")
        with path.open("rb") as stream:
            request = urllib.request.Request(url, data=stream, method="PUT", headers={
                "Content-Length": str(path.stat().st_size), "Content-Type": "application/zip",
                "x-ms-blob-type": "BlockBlob", "x-ms-version": "2021-12-02",
            })
            self._send(request, "upload staged files")


def submission_path(product_id, submission_id):
    return f"/applications/{product_id}/submissions/{submission_id}"


def load_state(api, product_id):
    app = api.request("GET", f"/applications/{product_id}")
    expected = identity_from_xml(MANIFEST.read_bytes())
    if (app.get("packageIdentityName"), app.get("publisherName")) != (expected["Name"], expected["Publisher"]):
        raise ValueError("STORE_PRODUCT_ID does not match this repository's Store identity/publisher.")
    published_id = (app.get("lastPublishedApplicationSubmission") or {}).get("id")
    if not published_id:
        raise ValueError("This tool updates an existing published app. Complete the first submission in Partner Center.")
    published = api.request("GET", submission_path(product_id, published_id))
    pending_id = (app.get("pendingApplicationSubmission") or {}).get("id")
    draft = api.request("GET", submission_path(product_id, pending_id)) if pending_id else None
    return app, published, draft


def apply_changes(baseline, draft, product_id, plan):
    if plan is None:
        return copy.deepcopy(baseline)
    if plan.get("productId") != product_id or plan.get("submissionId") != (draft or {}).get("id"):
        raise ValueError("Edit plan targets a different product/draft; take a new snapshot.")
    if plan.get("baselineFingerprint") != fingerprint(baseline):
        raise ValueError("Draft changed since the edit plan snapshot; regenerate and review the plan.")
    changes = plan.get("changes")
    if not isinstance(changes, dict) or not changes.keys() <= EDITABLE:
        raise ValueError("Edit plan contains unsupported top-level fields; see EDITABLE and the release guide.")
    return merge_patch(baseline, changes)


def execute_commit(api, args, result):
    """Commit exactly one verified PendingCommit draft when explicitly requested."""
    app, published, draft = load_state(api, args.product_id)
    if not draft or str(draft.get("id")) != str(args.submission_id):
        raise ValueError("The requested submission is not the current Store draft; nothing was committed.")
    if draft.get("status") != "PendingCommit":
        result.update(submissionStatus=draft.get("status"))
        raise ValueError("The requested submission is not PendingCommit; nothing was committed.")
    path = submission_path(args.product_id, args.submission_id)
    result.update(submissionId=draft["id"], statusBeforeCommit=draft["status"],
                  packageCount=len(draft.get("applicationPackages", [])))
    result["stage"] = "committing"
    api.request("POST", path + "/commit")
    result["stage"] = "verifying-submission"
    after = api.request("GET", path)
    status = after.get("status")
    if status in ("PendingCommit", "Canceled"):
        raise ValueError("Store did not accept the commit; inspect the submission in Partner Center.")
    result.update(stage="submitted", status=status, storeProcessingStarted=True, submitted=True)


def release_content_plan(content, baseline, published, draft, product_id, version):
    """Merge public release text on the credentialed runner, keeping private notes there."""
    required = {"version", "expectedPublishedVersion", "locale", "releaseNotes"}
    if (not isinstance(content, dict) or not required <= content.keys()
            or not content.keys() <= required | {"certificationNotesAppend"}):
        raise ValueError("Release content has missing or unsupported fields.")
    if any(not isinstance(value, str) or not value.strip() for value in content.values()):
        raise ValueError("Release content fields must be nonempty strings.")
    def version_tuple(value):
        if not re.fullmatch(r"[1-9][0-9]*\.[0-9]+\.[0-9]+\.0", value):
            raise ValueError("Release content versions must be four-part Store versions ending in .0.")
        parts = tuple(map(int, value.split(".")))
        if any(part > 65535 for part in parts):
            raise ValueError("Release content version fields must not exceed 65535.")
        return parts
    target = version_tuple(content["version"])
    expected = version_tuple(content["expectedPublishedVersion"])
    versions = [item.get("version") for item in published.get("applicationPackages", [])]
    if not versions or any(not value for value in versions):
        raise ValueError("Cannot establish the last published version for the release content.")
    # Published packages may have a nonzero revision assigned by Store.
    try:
        actual = max(tuple(map(int, value.split("."))) for value in versions)
    except (AttributeError, ValueError) as error:
        raise ValueError("Invalid published package version.") from error
    if actual != expected:
        raise ValueError("Published version differs from the release-note baseline. Review the source range first.")
    if target <= expected or (version and content["version"] != version):
        raise ValueError("Release content does not match the new package version.")
    if not version and not any(item.get("version") == content["version"]
                               for item in baseline.get("applicationPackages", [])):
        raise ValueError("Cannot verify release content against a processed draft package; use a snapshot edit plan.")
    locales = [key for key in baseline.get("listings", {})
               if key.casefold() == content["locale"].casefold()]
    if len(locales) != 1 or not isinstance(baseline["listings"][locales[0]].get("baseListing"), dict):
        raise ValueError("Release content locale is missing or ambiguous in the current Store listing.")
    changes = {"listings": {locales[0]: {"baseListing": {"releaseNotes": content["releaseNotes"]}}}}
    addendum = content.get("certificationNotesAppend")
    if addendum:
        existing = baseline.get("notesForCertification") or ""
        if not isinstance(existing, str):
            raise ValueError("Existing certification notes are not text; use a reviewed edit plan.")
        changes["notesForCertification"] = (existing if addendum in existing else
                                             existing + ("\n\n" if existing else "") + addendum)
    return {"productId": product_id, "submissionId": (draft or {}).get("id"),
            "baselineFingerprint": fingerprint(baseline), "changes": changes}


def execute(api, args, result):
    app, published, draft = load_state(api, args.product_id)
    baseline = draft or published
    for name, value in (("published", published), ("draft", draft)):
        if value:
            save_json(args.output / f"{name}-before.json", sanitized(value))
    template = {"productId": args.product_id, "submissionId": (draft or {}).get("id"),
                "baselineFingerprint": fingerprint(baseline), "changes": {}}
    save_json(args.output / "edit-plan.json", template)
    result.update(productId=args.product_id, submissionId=(draft or {}).get("id"),
                  baselineFingerprint=fingerprint(baseline), stage="snapshot-saved")
    if args.command == "snapshot":
        return
    if draft and draft.get("status") != "PendingCommit":
        result.update(submissionStatus=draft.get("status"),
                      submissionStatusDetailsKeys=sorted((draft.get("statusDetails") or {}).keys()))
        raise ValueError("Existing submission is not PendingCommit; leave the active submission untouched.")
    if args.command == "edit" and not draft:
        raise ValueError("No existing draft. Create one by uploading a package first.")
    plan = json.loads(args.changes.read_text(encoding="utf-8-sig")) if args.changes else None
    if args.release_content:
        content = json.loads(args.release_content.read_text(encoding="utf-8-sig"))
        plan = release_content_plan(content, baseline, published, draft, args.product_id, args.version)
        save_json(args.output / "edit-plan.json", plan)
    desired = apply_changes(baseline, draft, args.product_id, plan)
    package = args.package.resolve() if args.command == "upload" else None
    if package:
        report = inspect_package(package, MANIFEST, args.version)
        for state in (published, draft):
            if state:
                reject_duplicate(report, package_inventory(state, report, args.assets_dir))
        latest = [item.get("version") for item in published.get("applicationPackages", []) if item.get("version")]
        if latest and tuple(map(int, args.version.split("."))) <= max(tuple(map(int, v.split("."))) for v in latest):
            raise ValueError("Release version must be newer than the published packages.")
        desired.setdefault("applicationPackages", []).append({"fileName": package.name, "fileStatus": "PendingUpload"})
        save_json(args.output / "package-inspection.json", report)
        result.update(packageFile=package.name, packageSha256=report["sha256"], version=report["version"])
    new_pending = set(pending_files(desired)) - set(pending_files(baseline))
    needs_upload = bool(package or new_pending or args.assets_dir)
    payload = args.output / "store-upload.zip"
    if needs_upload:
        result["stagedFileCount"] = create_payload(desired, package, args.assets_dir, payload)
        with payload.open("rb") as stream:
            result["payloadSha256"] = hashlib.file_digest(stream, "sha256").hexdigest()
    # Compare again immediately before mutation. The API has no documented
    # conditional update contract; avoid simultaneous portal/API editors.
    _, current_published, current_draft = load_state(api, args.product_id)
    if ((current_draft or {}).get("id") != (draft or {}).get("id")
            or fingerprint(current_draft or current_published) != fingerprint(baseline)):
        raise ValueError("Submission changed during preparation; nothing was written. Take a new snapshot.")
    if not draft:
        result["stage"] = "creating-draft"
        draft = api.request("POST", f"/applications/{args.product_id}/submissions")
        result["submissionId"] = draft.get("id")
        if draft.get("status") != "PendingCommit" or not draft.get("id"):
            raise ValueError("Created submission has unexpected state. Inspect it before continuing.")
        desired = merge_patch(draft, plan["changes"] if plan else {})
        desired.setdefault("applicationPackages", []).append({"fileName": package.name, "fileStatus": "PendingUpload"})
        if needs_upload:
            result["stagedFileCount"] = create_payload(desired, package, args.assets_dir, payload)
            with payload.open("rb") as stream:
                result["payloadSha256"] = hashlib.file_digest(stream, "sha256").hexdigest()
    result["stage"] = "saving-metadata"
    path = submission_path(args.product_id, draft["id"])
    saved = api.request("PUT", path, desired)
    result["metadataSaved"] = True
    save_json(args.output / "draft-saved.json", sanitized(saved))
    if needs_upload:
        result["stage"] = "uploading-files"
        api.upload(saved.get("fileUploadUrl") or draft.get("fileUploadUrl", ""), payload)
        result["filesTransferred"] = True
    result["stage"] = "verifying-draft"
    after = api.request("GET", path)
    if after.get("status") != "PendingCommit":
        raise ValueError("Draft status changed during the operation; inspect it in Partner Center.")
    # Verify all intended edits and package additions without requiring volatile
    # server fields (asset IDs, URLs, timestamps) to remain byte-identical.
    if plan:
        if not verify_patch(after, plan["changes"]):
            raise ValueError("Store did not retain every requested metadata change. Inspect saved draft.")
    if package and not any(item.get("fileName") == package.name for item in after.get("applicationPackages", [])):
        raise ValueError("Store did not retain the new package reference.")
    for old in draft.get("applicationPackages", []):
        if not any((item.get("id") == old["id"] if old.get("id") else item.get("fileName") == old.get("fileName"))
                   for item in after.get("applicationPackages", [])):
            raise ValueError("An existing package reference is missing after saving; inspect the draft.")
    save_json(args.output / "draft-after.json", sanitized(after))
    result.update(stage="staged-not-submitted", submissionId=after["id"],
                  baselineFingerprint=fingerprint(after), status=after["status"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("snapshot", "upload", "edit", "commit"))
    parser.add_argument("--product-id", default=os.environ.get("STORE_PRODUCT_ID"))
    parser.add_argument("--package", type=Path)
    parser.add_argument("--version")
    metadata = parser.add_mutually_exclusive_group()
    metadata.add_argument("--changes", type=Path, help="Edit plan generated by snapshot (JSON Merge Patch)")
    metadata.add_argument("--release-content", type=Path, help="Public release text; merge with current notes on the runner")
    parser.add_argument("--submission-id", help="Exact current PendingCommit submission ID for commit")
    parser.add_argument("--assets-dir", type=Path, help="Local files for ALL pending assets, preserving relative paths")
    parser.add_argument("--output", type=Path, required=True, help="New private directory; snapshots can contain test credentials")
    args = parser.parse_args()
    if not args.product_id or not re.fullmatch(r"[A-Za-z0-9]+", args.product_id):
        parser.error("Provide the Store product ID, not Package/Identity/Name.")
    if args.command == "upload" and (not args.package or not args.version):
        parser.error("upload needs --package and --version")
    if args.version and (not re.fullmatch(r"[1-9][0-9]*\.[0-9]+\.[0-9]+\.0", args.version)
                         or any(int(part) > 65535 for part in args.version.split("."))):
        parser.error("Use a four-part Store version ending in .0, with fields no greater than 65535")
    if args.command == "edit" and not (args.changes or args.release_content):
        parser.error("edit needs --changes or --release-content")
    if args.command == "commit" and (not args.submission_id or args.changes or args.release_content
                                      or args.package or args.version or args.assets_dir):
        parser.error("commit needs only --submission-id")
    if args.command == "snapshot" and (args.package or args.changes or args.release_content or args.assets_dir):
        parser.error("snapshot is read-only and accepts no update inputs")
    if args.command != "upload" and (args.package or args.version):
        parser.error("Only upload accepts package/version")
    if args.command != "commit" and args.submission_id:
        parser.error("Only commit accepts --submission-id")
    if args.output.exists() and any(args.output.iterdir()):
        parser.error("Use an empty output directory; do not overwrite evidence from an earlier operation")
    args.output.mkdir(parents=True, exist_ok=True)
    result = {"stage": "initializing", "metadataSaved": False, "filesTransferred": False,
              "storeProcessingStarted": False, "submitted": False}
    try:
        if args.command == "commit":
            execute_commit(StoreApi(), args, result)
        else:
            execute(StoreApi(), args, result)
        save_json(args.output / "result.json", result)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except Exception as error:
        # CLI boundary: preserve a small receipt, never print authenticated HTTP
        # objects, raw API responses, tokens, or certification note contents.
        result["failed"] = True
        result["errorType"] = type(error).__name__
        save_json(args.output / "result.json", result)
        if isinstance(error, (ValueError, RuntimeError, FileNotFoundError)):
            print(str(error), file=sys.stderr)
        else:
            print(f"{type(error).__name__}: operation stopped; see result.json and inspect the draft.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
