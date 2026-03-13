# Release And Policy Assets

- Keep the user-facing privacy policy in a version-controlled text file under `GersangStation\Assets\Policies\`, and update the in-app link when the policy changes.
- Keep `PublishTrimmed` disabled for release builds because trimming has caused release-only WinUI runtime failures in this app.
- `PublishReadyToRun` may remain enabled for release builds.
