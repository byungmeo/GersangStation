# WinUI Store release preparation

The default release path builds the MSIX upload package, preserves the current
Store draft, adds the new package, and saves requested metadata changes.
**It never submits the app for certification.** The `publish` skill compares the
planned release with the last published WinUI release and prepares the text.

## What "upload" completes

For this MSIX app, the official API has distinct stages:

1. Create a draft copied from the last published submission, **only if no draft exists**.
2. Save submission JSON, including new package references and listing changes.
3. Transfer a ZIP containing pending files to Store's upload storage.
4. Commit the submission, which starts Store processing and the submission flow.

This tooling stops after **step 3**. `PendingUpload` is expected until Store
processes the files. `result.json` separately records `metadataSaved`,
`filesTransferred`, `storeProcessingStarted: false`, and `submitted: false`.
An API save/file transfer is **not proof of package validation or a fully updated
Partner Center screen**. Microsoft's documentation says changes are displayed
after commit. Actual portal behavior before commit still needs account testing.

Microsoft also warns that editing an API-created submission in Partner Center
can prevent subsequent API updates/commit and can leave a submission in an error
state. Viewing for review and editing are different actions. Do not assume that
API staging followed by portal edits/manual submission is a supported handoff.
Do not use `targetPublishMode: Manual` as a substitute for stopping before commit:
it delays publication, not certification.

If the requirement is a package processed in the web portal plus freely editable
web fields before manually submitting, use the **browser path for the entire
draft**. The skill can operate the existing Partner Center forms. Do not silently
switch an API-created draft to browser editing or delete it to recover. These
limits belong to Microsoft's API, not to the build workflow.

## GitHub Actions

Run **Prepare WinUI Store release** (`publish-winui-store.yml`).

| mode | Behavior |
| --- | --- |
| `build` | Build Release/x64 with VS MSBuild; inspect actual nested MSIX identities; retain artifacts. Requires `version`. |
| `upload` | Build, then add the package to the existing API-editable draft (or create one if absent). Duplicate packages fail. Optional `changes_file` or `release_content_file` applies metadata in the same operation. No commit. |
| `edit` | Save metadata from `changes_file` or `release_content_file` to an existing draft, without building or uploading another package. `version` is unused. No commit. |

`draft` and `submit` have been removed to avoid ambiguous or unintended actions.
The previous `msstore publish` implementation has been removed because it can
replace an existing pending draft with a copy of the last published submission.

Open the completed run's **Artifacts** section:

- `winui-store-<run_id>-<run_attempt>`: `.msixupload`/`.appxupload`,
  `SHA256SUMS.txt`, `release-build.json`, `package-inspection.json`, `build.binlog`.
- `winui-store-result-<run_id>-<run_attempt>`: small Store-operation receipt.

`release-build.json` identifies the version, commit, dirty checkout status,
package identity, hash, and run. Re-running a job gets a separate artifact name.
Full draft snapshots are deliberately **not uploaded as Actions artifacts**:
certification notes can contain test credentials. Build logs can expose build
paths/properties; never inject credentials into the packaging job.

The build uses `windows-2025-vs2026` and the .NET SDK from `global.json`.
It builds the root `GersangStation.slnx` with Visual Studio MSBuild for the
WinForms COM reference. Only the WinUI Store manifest version is temporarily
changed and restored byte-for-byte. Dev identity, WinForms version/settings,
WinUI self-contained delivery, and disabled trimming remain unchanged.
Packaging uses `Rebuild` so switching versions cannot reuse an earlier generated
upload-bundle manifest. Always inspect the nested package version, not only its
filename or the outer bundle version. The SDK's `_Test` output folder contains
the sideload package produced alongside `StoreUpload`; it is not a build configuration.

### One-time setup

1. Link the Microsoft Entra application to the Partner Center developer account
   with the required Store submission permissions.
2. Create the GitHub environment `microsoft-store` with secrets:
   `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`,
   `AZURE_AD_APPLICATION_SECRET` (the secret **value**, not its ID).
3. Add environment variable `STORE_PRODUCT_ID`: the Store **product ID**,
   not `Package/Identity/Name` and not the Entra application ID.
4. The old `SELLER_ID` secret is no longer used by this MSIX REST implementation;
   it may remain for other tools. This code checks product identity and publisher.
5. Commit/push the workflow and scripts to the default branch so manual dispatch
   is available. Run `build` first. A GitHub run only contains the selected remote
   checkout, not local uncommitted changes.

The API route requires at least one already published submission. Unsupported
account/product features can cause API rejection; no automatic draft replacement
or certification submission is attempted.

## Local build and package inspection

Use an empty output directory and a new four-part version ending in `.0`:

```powershell
./scripts/Build-WinUIStorePackage.ps1 -Version 2.0.10.0 `
  -OutputDirectory C:/Temp/GersangStation-Store-2.0.10 `
  -MSBuildPath 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe'

python scripts/winui_store_package.py `
  --package C:/Temp/GersangStation-Store-2.0.10/GersangStation_2.0.10.0_x64_bundle.msixupload `
  --version 2.0.10.0
```

Python 3.11+ is required; no third-party Python packages are needed. In Codex,
use its bundled Python runtime if `python` is not installed on PATH.
Inspection checks the nested bundle and application manifest, version, publisher,
Store identity, x64 architecture, and SHA-256. It does not install or launch the app.

## Inspect and edit draft fields

### Credentials only in GitHub

For ordinary release preparation, provide `release_content_file` with a public
JSON file in the selected checkout (see `store-releases/2.0.9.0.json`). It contains
`version`, `expectedPublishedVersion`, `locale`, `releaseNotes`, and optionally
`certificationNotesAppend`. The runner checks the published version against the
release-note baseline, uses the actual locale key, changes only release notes,
and appends the new review instructions to existing certification notes. Existing
private instructions never leave the runner or enter the repository. Repeated
metadata edits do not append an identical addendum twice.

Use this input with `upload` for a new package. With `edit`, Store must already
return the target version on a draft package; unprocessed packages without a
version require a snapshot-based edit plan. This input and `changes_file` are
mutually exclusive. The runner still compares the current draft again before
writing and never submits it. A baseline mismatch stops before remote writes.

Locally, the equivalent option is `--release-content <file>`. For broader edits,
use the full snapshot-based plan below.

### Snapshot-based edits

Set the same three credential environment variables and `STORE_PRODUCT_ID` in the
process running the script. Do not paste secrets into commands, chat, or committed
files. GitHub secrets cannot be downloaded into the local machine; local use needs
its own authorized environment. A new output directory is required each time.

```powershell
# Read-only: save current/published metadata and an edit plan skeleton.
python scripts/winui_store_draft.py snapshot --output artifacts/winui-store/snapshot-01

# Add a package, preserving all unrelated existing draft data.
python scripts/winui_store_draft.py upload --version 2.0.10.0 `
  --package C:/Temp/GersangStation-Store-2.0.10/GersangStation_2.0.10.0_x64_bundle.msixupload `
  --output artifacts/winui-store/upload-01

# Save only the edited metadata; no rebuild or package re-upload.
python scripts/winui_store_draft.py edit `
  --changes artifacts/winui-store/snapshot-01/edit-plan.json `
  --output artifacts/winui-store/edit-01
```

Edit the `changes` member of the generated `edit-plan.json`, retaining its real
`productId`, `submissionId`, and `baselineFingerprint`. For example:

```json
{
  "productId": "YOUR_STORE_PRODUCT_ID",
  "submissionId": "CURRENT_SUBMISSION_ID_OR_NULL_FOR_NO_DRAFT",
  "baselineFingerprint": "KEEP_THE_VALUE_FROM_SNAPSHOT",
  "changes": {
    "listings": {
      "ko-kr": {
        "baseListing": {
          "releaseNotes": "이번 버전에서 변경된 사용자 기능과 수정 사항"
        }
      }
    },
    "notesForCertification": "기존 테스트 안내를 보존하고 이번 변경에 필요한 검증 절차를 반영한 전체 문구"
  }
}
```

Use the locale key actually returned by Store. `submissionId` is JSON `null` when
there is no draft; do not copy the example placeholder literally. An upload can
also take `--changes` to stage the package and metadata together. The script
refuses a plan if the current draft no longer matches its fingerprint, and checks
again before writing. The API has no documented conditional-write contract here:
avoid simultaneous portal/API editors during a run.

This is **JSON Merge Patch**: objects merge recursively, arrays replace in full,
and `null` removes a member. Omitted fields remain as they were. For example,
updating `releaseNotes` leaves description, screenshots, other locales, and pricing
unchanged. Preserve old certification instructions when generating the new full
`notesForCertification` string. The API still validates whether a field can change.

`EDITABLE` in the script lists supported top-level metadata fields. It includes
listings, certification notes, pricing, visibility, scheduling, category,
declarations, trailers, and package delivery settings. Read-only fields and
`applicationPackages` are protected; adding a package uses `upload`.
Fields omitted/ignored by the API (such as privacy/support URLs in old listing
fields), age-rating questionnaires, and package removal use Partner Center UI.
Choose that route before writing the draft through API. Thus **all portal-editable
fields have a UI route**, but this tool does not claim the API covers every field.

To use Actions `edit`, put a **non-secret** edit plan at a repository-relative
path in the selected checkout and enter it as `changes_file`. If notes contain
credentials, run the local command with a private plan instead. The skill should
not commit complete snapshots or credential-bearing notes merely to run CI.

## Pending assets, duplicates, and interrupted runs

- A matching package filename or version/architecture in the draft or published
  submission fails **before any Store write**. Renaming the file does not bypass
  this check. Existing packages are not removed or replaced. Versions must also
  be newer than the published packages.
- Because all pending files share an upload ZIP, another package/image/trailer
  already marked `PendingUpload` requires its local file too. Use local
  `--assets-dir <directory>` with all pending relative paths. New image/trailer
  references can be supplied via `--changes` plus this directory. The workflow
  intentionally fails if required existing pending assets are unavailable.
- Metadata-only `edit` does not re-upload an existing pending ZIP. New pending
  files, or explicitly providing `--assets-dir`, require all pending files.
- A failure after PUT can leave saved metadata with incomplete file transfer.
  Inspect `result.json`'s stage, take a fresh snapshot, and inspect Partner Center
  before recovery. Do not blindly rerun `upload`: the duplicate check will reject
  the newly saved package reference. Preserve the local package, ZIP, and receipts
  for an explicit recovery action. This tool does not delete or roll back a draft.
- API rejection of a portal-edited draft is a failure, not permission to recreate
  it. Network failures are not automatically retried because the remote outcome
  can be uncertain.

For a browser upload, export the complete current package list to this minimal
private inventory and pass `--inventory <file>` to `winui_store_package.py` before
selecting the file. Include all existing rows, including pending/deleted rows still
present in the draft; do not mark an incomplete paginated list as complete.

```json
{
  "complete": true,
  "packageIdentityName": "Byungmeo.642537A4A3EB7",
  "packages": [{"fileName": "old.msixupload", "version": "2.0.9.0", "architecture": "x64"}]
}
```

## Using the publish skill

The personal skill is installed at `~/.codex/skills/publish/SKILL.md`.
Example: **`$publish 2.0.10.0 출시 준비해줘`**.

It verifies the last *published WinUI* version, resolves the corresponding source
commit, examines history and the final diff only under `GersangStation.WinUI/`
and `GersangStation.Shared/`, drafts Korean
release notes and any needed certification instructions, builds/verifies the
package, then saves the draft and stops before submission. It can also edit an
existing draft without adding its package again. If the source mapping cannot be
proven, it asks for the baseline commit rather than using a WinForms tag or
claiming unreleased changes were in the previous Store version.

Release notes describe only effects users can see or experience, using commit
messages and code as evidence. Internal refactors and implementation details are
omitted. For example, describe the mouse-confinement failure after the 26-09-14
game patch as fixed, rather than naming the low-level hook implementation.
Three-part release requests such as `2.0.9` mean Store package version `2.0.9.0`.

The skill keeps a local review report (source range, changes, validation evidence,
field-level edits, package hash, and exact upload status) under ignored
`artifacts/winui-store/`. It never invents passed tests or writes internal developer
details into customer release notes. Additional fields change only when required
by the release or explicitly requested. Pricing/legal declarations need facts
from the user when those facts cannot be established from the project.

## Sources

- [MSIX submission lifecycle, fields, and API/portal mixing warning](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions)
- [Commit semantics and when changes appear in Partner Center](https://learn.microsoft.com/en-us/windows/uwp/monetize/commit-an-app-submission)
- [Update an existing submission](https://learn.microsoft.com/en-us/windows/uwp/monetize/update-an-app-submission)
- [Store CLI commands and pending-submission replacement](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/commands)
- [Store GitHub Actions account prerequisites](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions)
