# WinForms runtime and packaging

Read only the section relevant to the change. Paths below are relative to `GersangStation.Winform/`.

## Client creation and patching

`GersangStation/Modules/ClientCreator.cs` selects the layout from the local client version.

- Preserve the pre-`34100` layout and the `34100+` layout. Patching across that boundary requires the reinstall guide rather than in-place migration.
- `Online/KeySetting.dat`, `PetSetting.dat`, `AKinteractive.cfg`, and `CombineInfo.txt` are real copies, not shared symbolic files.
- Preserve the `Assets/Config` overwrite choice and report real-directory/link conflicts instead of deleting user data.

## Mouse adapter

`GersangStation/Modules/ClipMouse.cs` owns persisted hotkeys and tray notifications. Preserve F11/custom toggle and optional Alt release/first-window confinement. Engine behavior and runtime validation are defined in the [shared contract](../../GersangStation.Shared/README.md).

## Updater

`GersangStationMiniUpdator/UpdateRunner.cs` skips `*.config` and its own running files. Keep that protection.
`MiniUpdaterMaintenance` uses embedded updater resources to repair/update the external `GersangStationMini/Updator` installation; both copies are intentional.
Maintenance is silent, has a 30-second budget and app-exit cancellation, and must not delay startup/shutdown.
Updater behavior changes require its own project version increment. Successful updater runs show a 10-second countdown before closing.

## Release ZIP

From the repository root, in a Visual Studio developer shell:

```powershell
./GersangStation.Winform/scripts/Publish-GersangStationMiniRelease.ps1 `
  -Version <requested-version> -MsBuildPath (Get-Command MSBuild.exe).Source -NoPause
```

The script uses `FolderRelease_win-x64.pubxml`, includes the root license and guide shortcuts, removes shipped app config files, and writes `GersangStation.Winform/Publish/GersangStation_mini_v.<version>.zip`. It replaces staging/output for that version; inspect existing artifacts before reusing a version.

Verify the actual ZIP entries and EXE version, updater files, license/guide files, and absence of shipped app configs. Report the ZIP path, SHA-256, and GitHub upload status separately: this script only packages locally.
Tag and legacy update compatibility rules are in the [manifest contract](winforms-manifest-design.md).
