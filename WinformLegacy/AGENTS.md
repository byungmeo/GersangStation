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
- `GersangStation/Modules/WinFormsManifestLoader.cs`: WinForms manifest DTOs and JSON fetch helper for manifest-first release metadata loading.
- `GersangStation/Modules/ClientCreator.cs`: path validation, drive-format checks, symbolic-link-based client cloning.
- `GersangStation/Modules/ClipMouse.cs`: Win32 cursor clipping thread, hotkey registration, game window detection.
- `GersangStation/Properties/App.config`: shipped default config keys and values.
- `GersangStation/Properties/PublishProfiles/FolderRelease_win-x64.pubxml`: current single-file release publish settings.
- `..\README.md`: repository-level content source for in-app announcements and sponsor list parsing.

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
- Preserve the symbolic-client safety rules in `ClientCreator`: `Online\\KeySetting.dat`, `PetSetting.dat`, `AKinteractive.cfg`, and `CombineInfo.txt` are intentionally copied as real files instead of symlinked files.
- Be conservative around WebView2 automation, registry edits, shell shortcuts, Win32 interop, symlinks, and background-thread UI access. These areas need Windows-specific smoke testing after changes.
- Do not replace legacy APIs only to satisfy warnings if runtime behavior is uncertain. Stability matters more than analyzer cleanliness in this repo.
- Do not treat update/version/announcement behavior as local-only logic. Several user-visible features currently depend on GitHub repository content and URL conventions.
- The current version/update/repository coupling may be intentionally redesigned later because WinUI and WinForms now coexist in the same Git repository. When asked, prefer implementing the new design over preserving the current coupling.

## Current GitHub Integration Contract

- This section documents the current implementation, not a permanent rule. It is expected to change if versioning or release architecture is redesigned.
- `Form1.LoadComponent()` uses `Octokit` against the `byungmeo/GersangStation` GitHub repository.
- Current bridge implementation tries `winforms_manifest_url` first for release/announcement/sponsor data, then falls back per section to the old GitHub Release + root `README.md` parsing path when manifest data is unavailable.
- Program update detection still has a GitHub Releases fallback path and now skips unsupported tag formats until it finds a parseable stable release tag.
- Because the current legacy client only understands numeric `TagName` values, compatibility releases for legacy users must keep a numeric non-prerelease tag until the old update path can be retired safely.
- The in-app version labels and update prompt come from:
  - local assembly version in `GersangStation.csproj`
  - release `TagName`
  - release `Body`
- Release notes may contain a dialog-only block delimited by `<!--DIALOG-->` and `<!--END-->`. The updater extracts only that block for the user-facing message when present.
- The patch note button and manual update flow open `https://github.com/byungmeo/GersangStation/releases/latest`.
- Announcement text is parsed from the repository root `README.md`, starting at the `# 공지사항` marker.
- The current parser expects announcement lines shaped like:
  - `['YY.MM.DD] 제목 {discussionNumber}`
- The discussion number inside `{...}` is turned into a GitHub Discussions URL under `https://github.com/byungmeo/GersangStation/discussions/<number>`.
- `prev_announcement` in config stores the last seen discussion URL and drives the legacy "new announcement" popup behavior.
- `last_seen_announcement_id` is the new manifest-friendly popup marker; bridge code writes both when possible for backward compatibility.
- Sponsor data is also parsed from the repository root `README.md`, starting at `<summary>후원해주신 분들</summary>` and splitting on `<br>`.
- If the repository owner/name, README marker strings, release tag format, release body marker format, or GitHub Discussions link structure changes, this WinForms app will need code updates.

## Build And Publish

- `dotnet build` is not sufficient for this solution because the project uses a COM reference (`IWshRuntimeLibrary`) and fails under .NET Core MSBuild with `MSB4803`.
- Use Visual Studio MSBuild instead:
  - `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" GersangStation.sln /t:Build /p:Configuration=Debug`
- Expect existing warnings for:
  - `NETSDK1138` because `net6.0-windows7.0` is out of support
  - `System.IO.Hashing 10.0.2` not officially supporting `net6.0-windows7.0`
  - `MSB3277` `WindowsBase` version conflict from `WebView2.Wpf`
- Publish configuration currently lives in `GersangStation/Properties/PublishProfiles/FolderRelease_win-x64.pubxml`.

## Verification Checklist

- Main form, tab navigation, tray minimize/restore, and notify icon behavior after shell changes.
- WebView2 startup and login flow after any browser, registry, or login automation change.
- Client path save/load plus multi-client creation after filesystem or config changes.
- Patch download/extract/apply after patching or version-check changes.
- Mouse clip toggle and hotkeys after `ClipMouse` or input-setting changes.
- GitHub-backed version label, update prompt, announcement label, and sponsor list after any repository/API/parsing change.
- Output artifacts (`.url` guide files, config, single-file publish layout) after build/publish edits.
