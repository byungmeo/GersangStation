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

## Validation
- After code changes affecting the app, run:
  - `dotnet build .\GersangStation\GersangStation.csproj -c Debug -p:Platform=x64`
- Do not launch the WinUI app for validation unless the user explicitly asks for it.
- Default app-change validation to build-only verification.
