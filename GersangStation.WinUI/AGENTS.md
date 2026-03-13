# AGENTS.md

## Scope
- This file applies to the entire worktree rooted at `E:\Projects\dotnet\GersangStation\GersangStation.WinUI`.
- If a subdirectory defines its own `AGENTS.md`, the more specific file takes precedence for that subtree.

## Solution Layout
- `GersangStation/`: WinUI 3 desktop app
- `Core/`: shared library
- `Core/Core.Test/`: tests for `Core`

## Build
- Preferred full build:
  - `dotnet build .\GersangStation\GersangStation.csproj -c Debug -p:Platform=x64`
- Solution-level build if needed:
  - `dotnet build .\GersangStation.WinUI.slnx -c Debug -p:Platform=x64`

## Test
- Run core tests with:
  - `dotnet test .\Core\Core.Test\Core.Test.csproj -c Debug`

## Repository Skills
- Use `.codex\skills\gersangstation-winui-policy\SKILL.md` when changing WinUI page or service responsibilities, game launch rules, multi-client layout behavior, privacy-policy assets or links, or release packaging settings.
- Keep this file limited to cross-cutting repository rules. Keep detailed app policy in the repository skill and its reference files.

## Editing Rules
- When adding or materially changing methods, proactively add, update, or remove XML `summary` comments where they improve maintainability; do not wait for an explicit user request.
- When a cross-cutting repository rule changes, update this `AGENTS.md`.
- When a detailed app policy changes, update `.codex\skills\gersangstation-winui-policy\SKILL.md` or one of its reference files in the same change.

## Exception Handling Rules
- Treat exception handling as a cross-cutting repository policy. When Codex or another AI adds or changes code that can fail, prefer routing failures into the centralized exception pipeline instead of adding ad hoc `catch (Exception)` blocks.
- Do not silently swallow exceptions. Avoid empty `catch` blocks and avoid patterns that only log to `Debug.WriteLine` without either recovering, returning a meaningful result, or forwarding the exception to `App.ExceptionHandler`.
- Catch broad exceptions only at execution boundaries such as WinUI event handlers, app startup, background work entry points, timer callbacks, dispatcher callbacks, or external I/O boundaries.
- Treat `OperationCanceledException` as a normal cancellation path unless the user explicitly wants cancellation surfaced as an error.
- For `DispatcherQueue` work, prefer `DispatcherQueue.TryEnqueueHandled(...)` over raw `TryEnqueue(...)` when the callback can throw.
- For fire-and-forget tasks, do not leave `_ = Task.Run(...)` or other unobserved tasks without centralized handling. Prefer `FireAndForgetHandled(...)` or `SafeExecution.RunHandledAsync(...)`.
- For timers, prefer `SafeExecution.StartHandledTimer(...)` over raw `new Timer(...)` when the callback can throw.
- For non-UI async/sync entry points that may fail, prefer `SafeExecution.RunHandledAsync(...)` so exceptions are routed to `AppExceptionHandler`.
- If a method can validate input or environment without exceptions, prefer explicit checks and meaningful return values before relying on exception handling.
- When code must catch a specific recoverable exception such as `IOException`, `UnauthorizedAccessException`, or `HttpRequestException`, either recover locally with a clear user-facing outcome or re-route the failure into the centralized exception handler.
- Preserve original exception context. Do not use `throw ex;`. Use `throw;` for rethrow, or wrap with a domain-specific exception that keeps the original exception as `InnerException`.
- When adding new background or scheduling mechanisms, extend the centralized exception utilities rather than inventing a new local handling pattern.
- If a change introduces a new exception-handling pattern, update this file in the same change so future AI-assisted edits follow the same rule set.

## Validation
- After code changes affecting the app, run:
  - `dotnet build .\GersangStation\GersangStation.csproj -c Debug -p:Platform=x64`
- Do not launch the WinUI app for validation unless the user explicitly asks for it.
- Default app-change validation to build-only verification.
