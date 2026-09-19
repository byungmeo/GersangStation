# WinUI game policies

Applies to WinUI launch, client install/patch/clone, and game-window behavior. Read only the relevant section. WinForms compatibility rules are separate.

## Launch state and limits

- At most 3 clients across all servers; account uniqueness is global and launch buttons represent global slots.
- Mark the slot `Starting` on click; restore `Available` if preconditions fail before game startup.

## Install, patch, and cloning

- Require a fully qualified install path containing `Run.exe`, `Online/Map`, and `Online/vsn.dat` so the local version can be determined.
- Probe target write access before install/patch/clone operations; the app owns permission guidance. Write permission and symbolic-link support are separate checks.
- Select clone layout from the local version; station-managed folders use `_CreatedByStation`.
- For `34100+`, copy direct `Online` files, link its subdirectories, and copy `Assets/Config` using the config-overwrite choice.
- Skip top-level `PatchTemp`, `GersangDown`, and `ScreenShots`; preserve real clone screenshots and remove old screenshot links.
- Treat paths as non-symbolic when symbolic support is unavailable. A real directory where a link is required is a conflict, not permission to delete it.
- Unlink a symbolic destination file before replacing it with a copy.

## Game-window control

- WinUI mouse confinement and the first window-switch mode currently require administrator execution. This is a host policy, not a shared-engine requirement.
- `ClipMouseService` uses shared defaults and suspends the engine during window browsing. Engine safety and adapter validation are in the [shared contract](../../GersangStation.Shared/README.md).
- `WindowSwitchService` polls at 5ms; fixed Alt + backtick cycles running launch slots using brief z-order raises, never persistent TopMost.
- During browsing, the same chord cycles again; first left-click ends browsing. Track stable top-level root-owner window handles.
