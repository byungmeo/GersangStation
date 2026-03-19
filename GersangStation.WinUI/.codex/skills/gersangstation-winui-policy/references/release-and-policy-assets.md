# Release And Policy Assets

- Keep the user-facing privacy policy in a version-controlled text file under `GersangStation\Assets\Policies\`, and update the in-app link when the policy changes.
- Keep WinUI help, policy, and external license URLs in the repository-root `metadata\winui-links-manifest.json`, package that manifest into the app, and use the packaged manifest as the default source. When GitHub manifest loading succeeds, treat the remote value as highest priority; if a key is missing remotely, fall back to the packaged manifest before showing the in-app HTML error page.
- Keep package display strings resource-backed in `GersangStation\Strings\` so Windows search and shell-visible app names can be revised without hard-coding manifest text.
- Keep `PublishTrimmed` disabled for release builds because trimming has caused release-only WinUI runtime failures in this app.
- `PublishReadyToRun` may remain enabled for release builds.
