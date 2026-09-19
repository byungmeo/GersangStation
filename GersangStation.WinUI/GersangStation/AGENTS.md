# WinUI app

Applies only to `GersangStation.WinUI/GersangStation/`, not `Core/` or the shared library.

- App launch for validation requires a user request; report runtime checks left unperformed.
- Keep MSIX identity and self-contained delivery; keep release trimming disabled because it has caused runtime failures.
- For exception handling, dialogs, app navigation, startup registration, or packaged links, read only the matching section of [app policies](../docs/app-policies.md).
