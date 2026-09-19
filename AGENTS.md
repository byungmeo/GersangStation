# Repository guidance

Applies to this repository. Subdirectory `AGENTS.md` files add guidance only for their subtree; linked references apply only to the task described by their link.

- Follow explicit user instructions; clarify unresolved product choices rather than treating these documents as immutable policy.
- Preserve existing encodings, user settings, unrelated changes, and the independent WinForms/WinUI release channels.
- Keep common guidance here, subtree rules in the nearest `AGENTS.md`, and task-specific details in linked references. Update the existing owner of a rule instead of duplicating it.

## Desktop work only

- Use `GersangStation.slnx`. Desktop projects target .NET 10, Windows 10 1809+, and x64; shared build settings and NuGet versions live in `Directory.Build.props` and `Directory.Packages.props`.
- For desktop code changes, build the full solution with Visual Studio MSBuild: `MSBuild.exe GersangStation.slnx /restore /t:Build /p:Configuration=Debug /p:Platform=x64`, from the repository root in a Visual Studio developer shell. `dotnet build` cannot resolve the WinForms COM reference (`MSB4803`).
- Validate changed runtime behavior separately from compilation; report any unperformed checks. Documentation-only changes need link/content checks, not an app build.
- Do not commit temporary verification harnesses.

## Task-specific references

- Shared mouse engine or either host's mouse adapter: [shared contract](GersangStation.Shared/README.md).
- WinUI release workflow, Store package, or draft edits: [Store release procedure](docs/winui-store-release.md).
- WinForms release/announcement/sponsor metadata or its workflows: [manifest contract](GersangStation.Winform/docs/winforms-manifest-design.md).
- Website work: [site development](Website/README.md).
