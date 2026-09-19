# GersangStation.Shared

UI-independent code shared by the Windows 10 1809+ x64 applications. Applies to this library and its host adapters only. It must not depend on either UI project; hosts choose presentation and logging.

## Mouse confinement

`Input/MouseConfinementService` uses the existing WinUI engine: a low-level mouse hook and a 5ms polling fallback, client-area edge correction, immediate Alt release, re-entry activation, and outside-drag bypass. It never calls `ClipCursor` or changes the game's OS clip ownership.

Create it on a thread with a Windows message loop, pass an asynchronous `IProgress<MouseConfinementDiagnostic>` (for example `Progress<T>`), then call `SetEnabled`. Start/stop, compatibility configuration, and disposal belong to that thread. `SetExternalSuspended` is thread-safe so a background game-window switcher can pause correction immediately.

WinUI uses default options. WinForms calls `ConfigureCompatibility` for its optional Alt release and first-window restriction; `ResetFirstWindow` clears that selection. The first-window identity includes HWND and PID. Global toggle hotkey registration and persisted key syntax belong to the WinForms adapter; toggling uses the same shared `SetEnabled` API.

## Failure contract

- Invalid lifecycle/thread use throws synchronously to the caller.
- A vanished/invisible/minimized/non-game window means no target, not an application failure.
- Failed geometry/cursor reads skip that observation. A failed cursor write is eligible for the next poll, preserving the WinUI behavior.
- Hook installation failure reports a diagnostic and leaves polling available.
- Unexpected managed timer/hook exceptions disable monitoring and report the original exception. The callback boundary does not choose app UI or terminate the process.
- Hook removal failure reports a diagnostic and retains the delegate to prevent native callbacks into collected memory. The stopped callback passes input through; another `Dispose` call retries removal. A persistent failure retains the registration until process exit.
- Diagnostic observers must dispatch asynchronously and must not throw. A synchronous observer failure is written to `Trace` so it cannot escape through native input callbacks.

Both hosts ignore delayed stop diagnostics after monitoring has already restarted. Neither host changes the shared module's failure classification.

## Input safety and validation

Keep hook callbacks nonblocking and pass input through while state is busy. Do not hold the service lock while invoking the next hook or writing cursor position. Revalidate HWND, PID, and service state before correction; release/stop invalidates pending observations. Rearm after outside drags only when dragging ends and the cursor re-enters.

Geometry and cursor reads/writes use a consistent per-monitor-aware DPI context. For engine changes, verify both hosts; for adapter-only changes, verify the affected host. Check the actual loaded binary and client edges at the affected DPI, Alt escape, outside/title-bar dragging, foreground changes, and WinForms hotkeys/first-window mode as applicable. Build success alone does not verify cursor behavior.
