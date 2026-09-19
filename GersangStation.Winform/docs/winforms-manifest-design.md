# WinForms manifest contract

Applies to WinForms update checks, announcements, sponsors, and the three `publish-winforms-*-manifest.yml` workflows. Not a WinUI release procedure.

## Sources and loading

Paths below are relative to the repository root. Defaults use GitHub raw under `byungmeo/GersangStation/master/`.

| Purpose | File under `metadata/` | Config URL key | On load failure |
| --- | --- | --- | --- |
| Release | `winforms-release-manifest.json` | `winforms_release_manifest_url` | GitHub Releases |
| Announcement | `winforms-announcements-manifest.json` | `winforms_announcement_manifest_url` | No content fallback |
| Sponsors | `winforms-sponsors-manifest.json` | `winforms_sponsors_manifest_url` | No content fallback |

An empty release/announcement URL uses the old `winforms_manifest_url` key. Announcements and sponsors no longer parse the root README.

Schema/DTO source: `GersangStation.Winform/GersangStation/Modules/WinFormsManifestLoader.cs`.
Consumption/UI source: `GersangStation.Winform/GersangStation/Forms/Form1.cs`.
Use the checked-in manifests as examples rather than copying historical release values.

## Schema and behavior

All three documents carry `schema_version: 1`, `product: "winforms"`, `channel: "stable"`, and UTC ISO-8601 `generated_at`.

- `release`: `version`, `tag`, `compatibility_tag`, `published_at`, `is_mandatory`, `title`, `message`, `notes_url`, and `download` (`asset_name`, `url`, `sha256`, `size`). Drives version/update UI and download selection.
- `announcement`: one current entry with `id`, `title`, `url`, `published_at`, and `show_popup`. Use the Discussion number as the ID. `last_seen_announcement_id` and legacy `prev_announcement` URL track popup state.
- `sponsors`: `last_updated_at` and string `items` in the format `{date} [{name}] {message}`. Store oldest first; the app displays newest first. The DTO accepts older item shapes for compatibility.

## Workflow ownership

Use the matching workflow under `.github/workflows/`; its `workflow_dispatch` definition is the source of truth for inputs.

- `publish-winforms-release-manifest.yml`: resolves release tags/assets, publication time, URL, size, and SHA-256; takes version/title/message/mandatory flag and asset name.
- `publish-winforms-announcements-manifest.yml`: takes Discussion number/title/popup flag, derives the Discussion URL, and stamps the run time.
- `publish-winforms-sponsors-manifest.yml`: takes date/name/message, validates `yyyy-mm-dd`, and appends a line unless already present.
- Each has `dry_run`; preview output before publishing a metadata change when validation is needed.

## Legacy release compatibility

- Source-management tags use `winforms-v{version}`. Keep a bare numeric, non-prerelease GitHub Release for older clients during the compatibility period. Historical numeric tags belong to WinForms, not WinUI.
- Current release fallback skips unsupported tags until a parseable stable release is found. Keep compatibility for older numeric-only clients when changing this code.
- Local version comes from `GersangStation.Winform.csproj`; release metadata is preferred over GitHub release tag/body fallback.
- Fallback release bodies may delimit popup text with `<!--DIALOG-->` and `<!--END-->`.
- The patch-note/manual-update link uses `https://github.com/byungmeo/GersangStation/releases/latest`; repository/tag/link changes require checking this path too.

ZIP packaging and updater behavior are in [runtime and packaging](runtime-and-packaging.md).
