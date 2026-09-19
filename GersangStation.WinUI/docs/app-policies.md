# WinUI app policies

Applies only to the matching feature in `GersangStation.WinUI/GersangStation/`. Core and Shared do not use these UI helpers.

## Failures and dialogs

- Reuse established handling for equivalent failures. Ask only when recovery, retry, fallback, termination, or presentation requires an unresolved product decision.
- Preserve original exception context. Expected failures need a meaningful result or recovery; cancellation is normally not an error.
- Recoverable exceptions use the detailed exception window unless the user has chosen narrower UI for the case. Translate `AppDataOperationResult` with existing app-layer formatter/dialog helpers.
- At throwing app boundaries, use `TryEnqueueHandled`, `FireAndForgetHandled`, `SafeExecution.RunHandledAsync`, or `SafeExecution.StartHandledTimer` as appropriate.
- Prefer explicit `AppExceptionHandler` intent methods over the legacy boolean API. Global unhandled hooks report crashes and terminate; `AppDomain` handling avoids XAML and blocking async UI.
- Show dialogs with `ShowManagedAsync` through `App.DialogCoordinator`; reusable dialogs derive from `AppContentDialog`.

## Navigation and startup

- App web links normally open in the internal WebView unless OS browser handoff is required.
- Admin startup and desktop shortcuts share one elevated task through `AdminStartupRegistrationService`; startup controls its logon trigger. Keep the launch script in PowerShell `-EncodedCommand`, not an AppData/package support file.
- When changing this launch flow, verify the actual process/window; scheduled-task success does not prove app launch or standard-user support.

## Packaged links and policy assets

Paths below are repository-relative.

- Privacy text: `GersangStation.WinUI/GersangStation/Assets/Policies/`; keep the app link aligned when it changes.
- Help/policy/license URLs: `metadata/winui-links-manifest.json`. Remote values override the packaged default; missing remote keys fall back to it, then to the HTML error page if neither supplies a value.
- Store fallback: `metadata/winui-store-update-version-manifest.json` is a cumulative history of `"{version, required}"` strings, read remotely for manual-help fallback after Store detection/install failures.
- Shell-visible package names use resources under `GersangStation.WinUI/GersangStation/Strings/`.

Package builds and Store draft edits use the [release procedure](../../docs/winui-store-release.md); ordinary asset edits do not start that workflow.
