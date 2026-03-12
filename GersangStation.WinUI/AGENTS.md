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

## Search And Inspection
- Prefer `rg` / `rg --files` for text and file search.
- If `rg` is unavailable in the shell, fall back to PowerShell `Get-ChildItem` + `Select-String`.

## Editing Rules
- Use `apply_patch` for manual file edits.
- Preserve existing architecture unless the task explicitly requires refactoring.
- Do not revert unrelated user changes.
- Keep comments concise and technical.
- When adding or materially changing methods, proactively add, update, or remove XML `summary` comments where they improve maintainability; do not wait for an explicit user request.
- When a policy is decided, changed, or retired through discussion with the user, update this `AGENTS.md` so the current agreement remains explicit.

## WinUI Rules
- Keep `WebViewManager` focused on WebView2/browser orchestration.
- Keep launcher execution and game-process tracking logic in `GameStarter`.
- Prefer keeping UI policy in `StationPage` and process/runtime policy in services.
- Avoid unnecessary XAML structure changes when the requirement can be satisfied from code-behind or bindings.
- When app UI opens web URLs, prefer navigating to the internal WebView page instead of launching the external browser unless OS-level browser handoff is explicitly required.
- Keep the user-facing privacy policy in a version-controlled text file under `GersangStation\Assets\Policies\` and update the in-app link when the policy changes.

## Current Game Policies
- A maximum of 3 game clients may run at the same time across all servers.
- Account uniqueness is global: the same account cannot run on multiple clients at once, even across different servers.
- Client-slot state is server-specific: the same slot number may be used independently on different servers.
- A launch attempt should switch the slot to `Starting` immediately on button click.
- If launch preconditions fail before the game process starts, the slot must return to `Available`.
- Client install-path validation should look for `\Online\Map` instead of `\char`.
- For multi-client creation on `v34100+` layouts, direct files under `Online` must always be hard-copied with overwrite, while subdirectories under `Online` remain symbolic links.
- For multi-client creation on `v34100+` layouts, `\Assets\Config` and everything under it must follow the config overwrite policy instead of being symbolically linked.

## Validation
- After code changes affecting the app, run:
  - `dotnet build .\GersangStation\GersangStation.csproj -c Debug -p:Platform=x64`
- Do not launch the WinUI app for validation unless the user explicitly asks for it.
- Default app-change validation to build-only verification.

## Packaging
- Keep `PublishTrimmed` disabled for release builds. This app has shown release-only WinUI runtime failures when trimming is enabled.
- `PublishReadyToRun` may remain enabled for release builds.
