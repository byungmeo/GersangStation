# WinUI subtree

Applies to `GersangStation.WinUI/`, including `Core/`.

- `Core/` returns structured results or exceptions with original context; it does not choose app UI or call `App.ExceptionHandler`. Prefer structured APIs over compatibility wrappers when failures matter.
- For game launch, client install/patch/clone, or game-window changes, read the relevant section of [game policies](docs/game-policies.md).
