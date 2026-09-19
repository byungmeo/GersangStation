"""Inspect the actual Store archive and reject duplicates before a browser upload.

This tool is offline. Inventory JSON must come from a fresh, complete Partner
Center package listing for this product; it is not a release history cache.
"""

import argparse
import hashlib
import io
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile


def identity_from_xml(data):
    root = ET.fromstring(data)
    identity = root.find("{*}Identity")
    if identity is None:
        raise ValueError("Package manifest has no Identity.")
    return dict(identity.attrib)


def inspect_archive(source, label, depth=0):
    """Read bundle/application identities without executing or extracting files."""
    if depth > 3:
        raise ValueError("Unexpected package archive nesting.")
    result = []
    with zipfile.ZipFile(source) as archive:
        names = archive.namelist()
        if len(names) != len(set(name.casefold() for name in names)):
            raise ValueError("Archive has ambiguous duplicate entry names.")
        for name in names:
            lowered = name.lower()
            if lowered in ("appxmanifest.xml", "appxmetadata/appxbundlemanifest.xml"):
                if archive.getinfo(name).file_size > 4 * 1024 * 1024:
                    raise ValueError("Unexpectedly large package manifest.")
                result.append({
                    "archive": label,
                    "kind": "bundle" if "bundlemanifest" in lowered else "package",
                    "identity": identity_from_xml(archive.read(name)),
                })
            elif lowered.endswith((".msix", ".appx", ".msixbundle", ".appxbundle")):
                result.extend(inspect_archive(io.BytesIO(archive.read(name)),
                                              f"{label}/{name}", depth + 1))
    return result


def inspect_package(package, manifest, expected_version):
    package = Path(package)
    if package.suffix.lower() not in (".msixupload", ".appxupload"):
        raise ValueError("Expected the Store .msixupload or .appxupload artifact.")
    expected = identity_from_xml(Path(manifest).read_bytes())
    entries = inspect_archive(package, package.name)
    applications = [item for item in entries if item["kind"] == "package"
                    and not item["identity"].get("ResourceId")]
    if not applications:
        raise ValueError("Archive contains no application package.")
    for item in entries:
        identity = item["identity"]
        if (identity.get("Name"), identity.get("Publisher")) != (
                expected["Name"], expected["Publisher"]):
            raise ValueError("Archive identity/publisher differs from the Store manifest.")
        if identity.get("Version") != expected_version:
            raise ValueError("Archive version differs from the requested release version.")
    if any(item["identity"].get("ProcessorArchitecture", "").lower() != "x64"
           for item in applications):
        raise ValueError("Expected x64 application packages only.")
    with package.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest().upper()
    return {
        "schemaVersion": 1,
        "fileName": package.name,
        "sha256": digest,
        "sizeBytes": package.stat().st_size,
        "packageIdentityName": expected["Name"],
        "publisher": expected["Publisher"],
        "version": expected_version,
        "architectures": sorted({item["identity"]["ProcessorArchitecture"].lower()
                                 for item in applications}),
        "entries": entries,
    }


def reject_duplicate(report, inventory):
    """Treat an existing version/architecture as a duplicate even if renamed."""
    if inventory.get("complete") is not True:
        raise ValueError("Inventory must cover the entire current draft package list.")
    if inventory.get("packageIdentityName") != report["packageIdentityName"]:
        raise ValueError("Inventory belongs to a different Store product.")
    packages = inventory.get("packages")
    if not isinstance(packages, list):
        raise ValueError("Inventory needs a packages array, including when empty.")
    known_names = {report["fileName"].casefold()}
    known_names.update(item["archive"].rsplit("/", 1)[-1].casefold()
                       for item in report["entries"])
    for item in packages:
        if not isinstance(item, dict):
            raise ValueError("Invalid package inventory entry.")
        if str(item.get("fileName", "")).casefold() in known_names:
            raise ValueError("Duplicate package file name already exists in the draft.")
        version = item.get("version")
        if not version:
            raise ValueError("An existing package has no known version; inspect it before upload.")
        if version != report["version"]:
            continue
        architecture = str(item.get("architecture", "")).lower()
        # Bundles may be shown as neutral or without an architecture. Be
        # conservative: same version cannot safely be considered a new package.
        if architecture in report["architectures"] or architecture in ("", "neutral", "bundle"):
            raise ValueError("Duplicate package version/architecture already exists in the draft.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--manifest", type=Path, default=Path(__file__).resolve().parents[1]
                        / "GersangStation.WinUI/GersangStation/Package.appxmanifest")
    parser.add_argument("--inventory", type=Path,
                        help="Current Partner Center package inventory JSON; fail on duplicates")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        report = inspect_package(args.package, args.manifest, args.version)
        if args.inventory:
            reject_duplicate(report, json.loads(args.inventory.read_text(encoding="utf-8-sig")))
            report["duplicateCheck"] = "passed-against-supplied-inventory"
        else:
            report["duplicateCheck"] = "not-performed"
        serialized = json.dumps(report, ensure_ascii=False, indent=2)
        if args.output:
            args.output.write_text(serialized + "\n", encoding="utf-8")
        print(serialized)
    except (ValueError, OSError, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"Package check failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
