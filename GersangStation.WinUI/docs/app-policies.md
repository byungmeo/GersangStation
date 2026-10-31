# WinUI app contracts

Applies to `GersangStation.WinUI/GersangStation/`. Core returns results or exceptions; presentation belongs to the app.

## Error handling

- Recoverable failures use `AppExceptionHandler`. Global notices use an InfoBar with on-demand diagnostic details; feature-owned feedback records through `RecordRecoverable` without a duplicate global notice.
- Preserve original exceptions and operation/target context. Logging and notice suppression are independent: forwarding one exception shares its record, while a new failure can be logged even when its notice is suppressed.
- Fatal UI/startup failures use `FatalErrorHandler`: bounded best-effort recording followed by termination, without depending on XAML, dispatcher scheduling, or normal cleanup. The terminating AppDomain hook leaves termination to the CLR.
- `AppSessionJournal` distinguishes an incomplete previous session from a captured exception. Tray hiding, cancelled exit, and secondary-instance redirection are not completed primary sessions.
- Native/WinRT callbacks contain managed exceptions. UI dispatch and fire-and-forget boundaries use the handled helpers in `Diagnostics`.
- Diagnostic viewer/reporting failures are recorded without recursively presenting another error.
- `App.DialogCoordinator` serializes dialogs through `ShowManagedAsync`; reusable dialogs derive from `AppContentDialog`. Cancellation belongs to the operation that requested the dialog.

## Stored data

- Failed reads retain existing data and do not expose fallback defaults as editable settings. Failed writes retain the user's draft and the last committed data.
- Account metadata and PasswordVault credentials have separate failure modes. Missing credentials or a vault service failure do not imply account deletion.
- Mutations and recovery for the same store are serialized. Corrupt data is distinguished from transient access failures; resetting it is a separate destructive operation.

## Navigation and browser lifetime

- Shell navigation and exit share the content-transition gate. Leave/save decisions complete before changing content; `OnNavigatingFrom` is only a synchronous safety guard. A pending exit invalidates queued navigation.
- Tray Exit restores the main window and uses the same pending-work/unsaved-edit decisions as normal exit. Close-to-tray and Store-update termination are distinct paths.
- Async results belong to their originating page, document, or operation. Late callbacks cannot replace newer state or revive dismissed UI.
- Browser document faults retain their cause and are distinct from request replacement, maintenance suspension, and disposal cancellation. See [browser session](browser-session.md) and [data maintenance](browser-maintenance.md).
- Automatic startup uses the package's `GersangStationStartup` task through `Windows.ApplicationModel.StartupTask`, including Windows' user-disabled and policy-controlled states. The main app and maintenance worker run asInvoker.

## Assets and links

- Privacy text: `GersangStation.WinUI/GersangStation/Assets/Policies/`.
- Help/policy/license URLs: `metadata/winui-links-manifest.json`. Remote entries override packaged defaults; unresolved internal links use the HTML fallback.
- Shell-visible package names: `GersangStation.WinUI/GersangStation/Strings/`.
