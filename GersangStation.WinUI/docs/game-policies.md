# WinUI game policies

Applies to WinUI launch, client install/patch/clone, and game-window behavior. Read only the relevant section. WinForms compatibility rules are separate.

## Launch state and limits

- At most 3 clients across all servers; account uniqueness is global and launch buttons represent global slots.
- Mark the slot `Starting` on click; restore `Available` if preconditions fail before game startup.

## Install, patch, and cloning

- Require a fully qualified install path containing `Run.exe`, `Online/Map`, and `Online/vsn.dat` so the local version can be determined.
- Probe target write access before full-client installation; the app owns permission guidance. Patch/clone writes run only in the on-demand elevated GameMaintenance worker, without a write-permission probe. Keep install-path validation and filesystem/symbolic-link support checks.
- Select clone layout from the local version; station-managed folders use `_CreatedByStation`.
- Sidebar-triggered clone creation and installation settings both create/synchronize clients 2 and 3 in one maintenance request through `MultiClientCreationService`. The triggering launch slot does not limit the destinations. Confirm this two-client scope before sidebar creation; preserve existing per-client settings as usual.
- Patch every already-created clone automatically, using the same server/path validation as the installation page. Capture eligible fixed clone paths before patch writes; do not create missing or invalid clones during patching.
- For `34100+`, copy direct `Online` files and link its subdirectories. Preserve existing clone settings in `Assets/Config`, legacy `Online` settings, and `config.ln` regardless of layout; copy only missing settings files. There is no settings-overwrite option.
- Skip top-level `PatchTemp`, `GersangDown`, and `ScreenShots`; preserve real clone screenshots and remove old screenshot links.
- Treat paths as non-symbolic when symbolic support is unavailable. A real directory where a link is required is a conflict, not permission to delete it.
- Unlink a symbolic destination file before replacing it with a copy.
- Successful patch application is not undone by post-patch temporary cleanup failure or a late cancellation during cleanup. Keep the patch result successful and send cleanup diagnostics to the app for silent logging; pre-patch cleanup required for a fresh download remains a blocking step.

## Game-window control

- WinUI mouse confinement and the first window-switch mode currently require administrator execution. This is a host policy, not a shared-engine requirement.
- `ClipMouseService` uses shared defaults and suspends the engine during window browsing. Engine safety and adapter validation are in the [shared contract](../../GersangStation.Shared/README.md).
- `WindowSwitchService` polls at 5ms; fixed Alt + backtick cycles running launch slots using brief z-order raises, never persistent TopMost.
- During browsing, the same chord cycles again; first left-click ends browsing. Track stable top-level root-owner window handles.
