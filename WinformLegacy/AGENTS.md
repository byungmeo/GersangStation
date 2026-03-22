# GersangStation WinForms Legacy

## Purpose

Maintain this legacy Windows Forms build as a stable production line even though the WinUI 3 rewrite lives at `E:\Projects\dotnet\GersangStation\GersangStation.WinUI`.
Use the WinUI project as a comparison point for behavior or copy, but do not assume the legacy app should be structurally rewritten to match it unless the user asks.
Treat this document as working guidance for the current codebase state, not as an immutable architecture contract. If the user requests a redesign of versioning, release flow, repository integration, or update behavior, follow the request and then update this document to match the new design.

## Repository Layout

- Git root is `E:\Projects\dotnet\GersangStation`, not the `WinformLegacy` folder.
- This repository currently contains at least:
  - `GersangStation.WinUI`: the WinUI 3 rewrite
  - `WinformLegacy`: this maintained Windows Forms line
  - `Website`: related site assets/content
  - `.github`: shared repository automation/configuration
- When checking history, branches, CI, release notes, README content, or repository-wide assets, work from the repo root context rather than assuming the legacy project is isolated.

## Use This Skill

Use `$winforms-app` for WinForms-specific work in this repository.

## Delivery Model

- `GersangStation.WinUI` is distributed through Microsoft Store as a Release/MSIX package.
- `WinformLegacy` is distributed as a WinExe build, then packaged and uploaded through GitHub Releases.
- Do not assume both apps share the same versioning scheme, release cadence, packaging metadata, or update channel unless the user explicitly asks to unify them.
- New WinForms release tags should use `winforms-v{version}`.
- New WinUI release tags should use `winui-v{version}`.
- Historical bare numeric tags such as `1.6.3` should be interpreted as WinForms legacy tags when historical release behavior needs to be analyzed.
- During the compatibility period, WinForms should continue to publish a bare numeric non-prerelease GitHub Release for legacy clients, even if `winforms-v{version}` is also created as a source-management tag.

## Project Map

- `GersangStation/GersangStation.csproj`: `net6.0-windows7.0` Windows Forms app with `MaterialSkinKR`, `WebView2`, `Octokit`, `System.IO.Hashing`, and a COM reference to `IWshRuntimeLibrary`.
- `GersangStation/Program.cs`: single-instance startup, tray-window restore, DPI comments, application entry point.
- `GersangStation/Forms/Form1.cs`: main shell, WebView2 login automation, preset/account selection, tray menu, announcements, sponsor list, update check, mouse-clip integration.
- `GersangStation/Forms/Form_ClientSetting.cs`: per-server client path management, auto-update option, symbolic multi-client creation, patch dialog entry.
- `GersangStation/Forms/Form_Patcher.cs`: version detection, patch list download, archive extraction, optional multi-client propagation.
- `GersangStation/Forms/Form_AccountSetting.cs`: account CRUD. Passwords are stored with DPAPI through `EncryptionSupporter`.
- `GersangStation/Forms/Form_Browser.cs`: WebView2 popup browser and shortcut save flow.
- `GersangStation/Forms/Form_ShortcutSetting.cs`: four shortcut slots and titles.
- `GersangStation/Modules/ConfigManager.cs`: appSettings bootstrap, migration from older config files, runtime save helpers.
- `GersangStation/Modules/WinFormsManifestLoader.cs`: WinForms release/announcement/sponsors manifest DTOs and JSON fetch helpers.
- `GersangStation/Modules/ClientCreator.cs`: WinUI-aligned path validation, `v34100` patch reinstall guidance gate, and symbolic-link-based client cloning with legacy/pre-`34100` and post-`34100` layout policies.
- `GersangStation/Modules/ClipMouse.cs`: Win32 cursor clipping thread, hotkey registration, game window detection.
- `GersangStation/Properties/App.config`: shipped default config keys and values.
- `GersangStation/Properties/PublishProfiles/FolderRelease_win-x64.pubxml`: current single-file release publish settings.
- `GersangStationMiniUpdator/GersangStationMiniUpdator.csproj`: standalone WinForms updater for `GersangStationMini`, using a temp extraction folder and selective file apply.
- `GersangStationMiniUpdator/UpdateRunner.cs`: zip acquisition, temp extraction, config-file skip policy, updater self-skip policy, and target app restart flow.
- `..\README.md`: historical repository content reference only. Runtime announcement/sponsor loading no longer depends on it.

## Domain Terms

- `본클라`: primary/original Gersang client path.
- `다클라`: second/third cloned client paths.
- `천라`: RnD server variant.

## Working Rules

- Keep user-facing strings in Korean unless the existing screen already uses English branding or URLs.
- Treat each form as a synchronized trio: `FormName.cs`, `FormName.Designer.cs`, and `FormName.resx`.
- Prefer code-behind edits over broad layout rewrites. This UI still assumes 100% DPI in several places; do not casually resize or reposition controls.
- If you must edit a designer file manually, change only the specific serialized properties you understand and keep field names, event hookups, and resource keys aligned.
- Do not rename controls or event handlers casually. Search both code-behind and designer files before and after any rename.
- If you add or rename a config key, update both `ConfigManager.Validation()` and `Properties/App.config`.
- Preserve the existing semicolon-delimited formats for `account_list`, `shortcut_name`, and `current_comboBox_index_preset_*`.
- Do not change account encryption/storage format without an explicit migration plan. Passwords are DPAPI-protected per current Windows user.
- `GersangStationMiniUpdator` intentionally skips overwriting `*.config` files and its own files while running. Do not weaken that policy unless the user explicitly asks for a different updater model.
- Preserve the symbolic-client safety rules in `ClientCreator`: `Online\\KeySetting.dat`, `PetSetting.dat`, `AKinteractive.cfg`, and `CombineInfo.txt` are intentionally copied as real files instead of symlinked files.
- Current legacy policy keeps the old multi-client layout for pre-`v34100` clients, switches to the new layout at `v34100` and later, and still blocks patching from `<34100` to `>=34100` with the reinstall guide instead of attempting in-place migration.
- Be conservative around WebView2 automation, registry edits, shell shortcuts, Win32 interop, symlinks, and background-thread UI access. These areas need Windows-specific smoke testing after changes.
- Do not replace legacy APIs only to satisfy warnings if runtime behavior is uncertain. Stability matters more than analyzer cleanliness in this repo.
- Do not treat update/version/announcement behavior as local-only logic. Several user-visible features currently depend on GitHub repository content and URL conventions.
- The current version/update/repository coupling may be intentionally redesigned later because WinUI and WinForms now coexist in the same Git repository. When asked, prefer implementing the new design over preserving the current coupling.

## Current GitHub Integration Contract

- This section documents the current implementation, not a permanent rule. It is expected to change if versioning or release architecture is redesigned.
- `Form1.LoadComponent()` uses `Octokit` against the `byungmeo/GersangStation` GitHub repository.
- Current bridge implementation tries separate URLs first:
  - `winforms_release_manifest_url`
  - `winforms_announcement_manifest_url`
  - `winforms_sponsors_manifest_url`
- `winforms_manifest_url` remains only as a legacy fallback key for release/announcement loading.
- Shared repo workflows are split by responsibility:
  - `.github/workflows/publish-winforms-release-manifest.yml`
  - `.github/workflows/publish-winforms-announcements-manifest.yml`
  - `.github/workflows/publish-winforms-sponsors-manifest.yml`
- WinForms announcements manifest is intentionally single-entry. The announcement workflow accepts only the GitHub Discussions number, title, and popup flag; it derives the URL as `https://github.com/byungmeo/GersangStation/discussions/<number>` and uses the workflow run time as `published_at`.
- WinForms sponsors manifest stores README-compatible display lines. The sponsors workflow accepts `sponsor_date`, `sponsor_name`, and `sponsor_message`, validates the date as `yyyy-mm-dd`, and appends lines in the format `{후원 날짜} [{후원자명}] {후원내용}`.
- Program update detection still has a GitHub Releases fallback path and now skips unsupported tag formats until it finds a parseable stable release tag.
- Because the current legacy client only understands numeric `TagName` values, compatibility releases for legacy users must keep a numeric non-prerelease tag until the old update path can be retired safely.
- The in-app version labels and update prompt come from:
  - local assembly version in `GersangStation.csproj`
  - release `TagName`
  - release `Body`
- Release notes may contain a dialog-only block delimited by `<!--DIALOG-->` and `<!--END-->`. The updater extracts only that block for the user-facing message when present.
- The patch note button and manual update flow open `https://github.com/byungmeo/GersangStation/releases/latest`.
- Announcement text is now loaded only from `winforms-announcements-manifest.json`.
- `prev_announcement` in config stores the last seen discussion URL and drives the legacy "new announcement" popup behavior.
- `last_seen_announcement_id` is the new manifest-friendly popup marker; bridge code writes both when possible for backward compatibility.
- Sponsor data is now loaded only from `winforms-sponsors-manifest.json`. Root `README.md` sponsor parsing is no longer part of the runtime path.
- If the repository owner/name, release tag format, release body marker format, or GitHub Discussions link structure changes, this WinForms app will need code updates.

## Build And Publish

- `dotnet build` is not sufficient for this solution because the project uses a COM reference (`IWshRuntimeLibrary`) and fails under .NET Core MSBuild with `MSB4803`.
- Use Visual Studio MSBuild instead:
  - `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" GersangStation.sln /t:Build /p:Configuration=Debug`
- Expect existing warnings for:
  - `NETSDK1138` because `net6.0-windows7.0` is out of support
  - `System.IO.Hashing 10.0.2` not officially supporting `net6.0-windows7.0`
  - `MSB3277` `WindowsBase` version conflict from `WebView2.Wpf`
- Publish configuration currently lives in `GersangStation/Properties/PublishProfiles/FolderRelease_win-x64.pubxml`.
- Final release packaging can be generated with `scripts/Publish-GersangStationMiniRelease.ps1`, which publishes the app, injects the root `LICENSE`, removes shipped config files, and creates `GersangStation_v.<version>.zip` in the legacy release format.

## Verification Checklist

- Main form, tab navigation, tray minimize/restore, and notify icon behavior after shell changes.
- WebView2 startup and login flow after any browser, registry, or login automation change.
- Client path save/load plus multi-client creation after filesystem or config changes.
- Patch download/extract/apply after patching or version-check changes.
- Mouse clip toggle and hotkeys after `ClipMouse` or input-setting changes.
- GitHub-backed version label, update prompt, announcement label, and sponsor list after any repository/API/parsing change.
- Output artifacts (`.url` guide files, config, single-file publish layout) after build/publish edits.
