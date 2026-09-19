# WinUI subtree

Applies to `GersangStation.WinUI/`, including `Core/`.

- `Core/` returns structured results or exceptions with original context; it does not choose app UI or call `App.ExceptionHandler`. Prefer structured APIs over compatibility wrappers when failures matter.
- Do not add committed test projects or test source files without an explicit user request. Use temporary validation code when needed.
- For game launch, client install/patch/clone, or game-window changes, read the relevant section of [game policies](docs/game-policies.md).
