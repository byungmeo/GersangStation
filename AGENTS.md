# Repository guidance

- Treat AGENTS and skills as reference guidance. Follow explicit user instructions and ask about ambiguous choices before implementing them.
- Use the root `GersangStation.slnx`; all desktop projects target .NET 10, Windows 10 1809 or later, and x64.
- Keep NuGet versions in `Directory.Packages.props`. Keep WinForms `SelfContained=false` and its existing assembly/config names; WinUI retains its MSIX identity and self-contained delivery.
- `GersangStation.Shared` must not depend on either UI project. The mouse engine follows WinUI behavior, with compatibility options used only by WinForms.
- Shared modules report structured diagnostics and preserve exception context without selecting UI or calling an app's global exception handler. Hosts choose presentation and logging.
- Build the full solution with Visual Studio MSBuild because WinForms still uses a COM reference. Do not infer desktop runtime behavior from build success alone.
- Preserve existing encodings, user settings, unrelated changes, and independent release channels. Do not commit temporary verification harnesses.
