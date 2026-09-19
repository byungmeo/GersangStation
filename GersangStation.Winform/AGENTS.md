# WinForms subtree

Applies to `GersangStation.Winform/`. Use the root solution and build instructions.

- Keep `SelfContained=false`, the `GersangStation` assembly/config names, and the independent GitHub release channel.
- For form edits, keep `.cs`, `.Designer.cs`, and `.resx` names, events, and resources aligned. Preserve Korean UI text and existing DPI/layout assumptions unless the task changes them.
- For settings changes, update both `ConfigManager.Validation()` and `Properties/App.config`; preserve existing semicolon-delimited values and per-user DPAPI account storage unless a migration is requested.
- For client cloning/patching, mouse hotkeys, updater changes, or ZIP packaging, read only the relevant section of [runtime and packaging](docs/runtime-and-packaging.md).
- For update checks, announcements, sponsors, or their GitHub workflows, read the [manifest contract](docs/winforms-manifest-design.md).
- Smoke-test the changed Windows behavior (for example login, tray, patching, or input); an unrelated checklist is not required.
