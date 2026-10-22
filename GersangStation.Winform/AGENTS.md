# WinForms subtree

Applies to `GersangStation.Winform/`. Use the root solution and build instructions.

- Keep `SelfContained=false`, the `GersangStation` assembly/config names, and the independent GitHub release channel.
- Form `.cs`, `.Designer.cs`, and `.resx` files share event/resource bindings and DPI/layout assumptions.
- Settings changes involve both `ConfigManager.Validation()` and `Properties/App.config`. Legacy settings use semicolon-delimited values; account storage uses per-user DPAPI.
- For client cloning/patching, mouse hotkeys, updater changes, or ZIP packaging, read only the relevant section of [runtime and packaging](docs/runtime-and-packaging.md).
- For update checks, announcements, sponsors, or their GitHub workflows, read the [manifest contract](docs/winforms-manifest-design.md).
