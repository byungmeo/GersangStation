# Architecture Rules

- Keep `WebViewManager` focused on WebView2 and browser orchestration.
- Keep launcher execution and game-process tracking logic in `GameStarter`.
- Keep app single-instance redirection in the startup entry point, and keep window reactivation behavior in `App`.
- Keep first-run prompt sequencing in `MainWindow`, and keep desktop shortcut file-system operations in a dedicated service.
- Keep UI policy in `StationPage` and process or runtime policy in services when possible.
- Avoid unnecessary XAML structure changes when the requirement can be satisfied from code-behind or bindings.
- When app UI opens web URLs, prefer navigating to the internal WebView page instead of launching the external browser unless OS-level browser handoff is explicitly required.
