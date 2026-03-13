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
- The repository opts `dotnet test` into `Microsoft.Testing.Platform` via the root `global.json`. Keep that file in place when updating test infrastructure for .NET 10 SDK or later.
- Run core tests with:
  - `dotnet test --project .\Core\Core.Test\Core.Test.csproj -c Debug`

## Repository Skills
- Use `.codex\skills\gersangstation-winui-policy\SKILL.md` when changing WinUI page or service responsibilities, game launch rules, multi-client layout behavior, privacy-policy assets or links, or release packaging settings.
- Keep this file limited to cross-cutting repository rules. Keep detailed app policy in the repository skill and its reference files.

## Editing Rules
- When adding or materially changing methods, proactively add, update, or remove XML `summary` comments where they improve maintainability; do not wait for an explicit user request.
- When a cross-cutting repository rule changes, update this `AGENTS.md`.
- When a detailed app policy changes, update `.codex\skills\gersangstation-winui-policy\SKILL.md` or one of its reference files in the same change.

## Git Workflow Rules
- Default issue workflow is: create a GitHub issue, create a related branch, then make all commits on that branch with the issue reference in the commit message.
- When working on a branch that is clearly tied to an issue, include the issue reference in every commit message using the `[#{issueNumber}]` prefix format unless the user explicitly asks for a different convention.
- If the current branch is not clearly tied to an issue, do not invent an issue number. Ask the user or proceed without the issue prefix if necessary.
- At the end of every coding task, always recommend a commit message. Base the recommendation on the full set of uncommitted changes at that moment, so if the user skipped a commit for the previous task, the next recommendation must cover the accumulated work.

## Exception Handling Rules
- Treat exception handling as a cross-cutting repository policy. When Codex or another AI adds or changes code that can fail, prefer routing failures into the centralized exception pipeline instead of adding ad hoc `catch (Exception)` blocks.
- Do not silently swallow exceptions. Avoid empty `catch` blocks and avoid patterns that only log to `Debug.WriteLine` without either recovering, returning a meaningful result, or forwarding the exception to `App.ExceptionHandler`.
- Catch broad exceptions only at execution boundaries such as WinUI event handlers, app startup, background work entry points, timer callbacks, dispatcher callbacks, or external I/O boundaries.
- Treat `OperationCanceledException` as a normal cancellation path unless the user explicitly wants cancellation surfaced as an error.
- For `DispatcherQueue` work, prefer `DispatcherQueue.TryEnqueueHandled(...)` over raw `TryEnqueue(...)` when the callback can throw.
- For fire-and-forget tasks, do not leave `_ = Task.Run(...)` or other unobserved tasks without centralized handling. Prefer `FireAndForgetHandled(...)` or `SafeExecution.RunHandledAsync(...)`.
- For timers, prefer `SafeExecution.StartHandledTimer(...)` over raw `new Timer(...)` when the callback can throw.
- For non-UI async/sync entry points that may fail, prefer `SafeExecution.RunHandledAsync(...)` so exceptions are routed to `AppExceptionHandler`.
- When calling `AppExceptionHandler` directly, prefer explicit intent methods such as `ShowRecoverableAsync(...)`, `HandleFatalUiExceptionAsync(...)`, or `HandleFatalProcessException(...)` over the legacy boolean-based `HandleAsync(..., isFatal)`.
- Treat global unhandled exception hooks as crash-reporting boundaries, not recovery points. Prefer logging, minimal final user notification, and termination over trying to continue after `Application.UnhandledException` or `AppDomain.CurrentDomain.UnhandledException`.
- For `AppDomain.CurrentDomain.UnhandledException`, avoid WinUI/XAML work and avoid blocking on async UI code. Prefer low-level fallback handling only.
- If a method can validate input or environment without exceptions, prefer explicit checks and meaningful return values before relying on exception handling.
- When code must catch a specific recoverable exception such as `IOException`, `UnauthorizedAccessException`, or `HttpRequestException`, either recover locally with a clear user-facing outcome or re-route the failure into the centralized exception handler.
- Preserve original exception context. Do not use `throw ex;`. Use `throw;` for rethrow, or wrap with a domain-specific exception that keeps the original exception as `InnerException`.
- When adding new background or scheduling mechanisms, extend the centralized exception utilities rather than inventing a new local handling pattern.
- Keep `Core` free of app-specific UI wording. When `AppDataManager` returns `AppDataOperationResult`, translate it in the `GersangStation` app layer with the shared formatter/dialog helper instead of showing raw `Exception.Message` or generic one-line failure text.
- Prefer contextual `ContentDialog`/`InfoBar` style feedback for recoverable UI errors. Reserve the detailed exception window for crash reporting, developer tooling, or explicit support diagnostics.
- If a change introduces a new exception-handling pattern, update this file in the same change so future AI-assisted edits follow the same rule set.

## Validation
- After code changes affecting the app, run:
  - `dotnet build .\GersangStation\GersangStation.csproj -c Debug -p:Platform=x64`
- Do not launch the WinUI app for validation unless the user explicitly asks for it.
- Default app-change validation to build-only verification.
