# Current Game Policies

- Allow at most 3 game clients at the same time across all servers.
- Enforce account uniqueness globally so the same account cannot run on multiple clients at once, even across different servers.
- Treat client-slot state as server-specific so the same slot number may be used independently on different servers.
- Switch the slot to `Starting` immediately when the user clicks launch.
- Return the slot to `Available` if launch preconditions fail before the game process starts.
- Validate client install paths by checking for `Run.exe`, `\Online\Map`, and `\Online\vsn.dat` instead of the legacy `\char` marker.
- Treat a path without `\Online\vsn.dat` as invalid for launch and multi-client creation, because the current client version cannot be determined.
- For multi-client creation on `v34100+` layouts, hard-copy direct files under `Online` with overwrite while keeping subdirectories under `Online` as symbolic links.
- For multi-client creation on `v34100+` layouts, apply the config overwrite policy to `\Assets\Config` and everything under it instead of symbolic-linking that tree.
- Choose the multi-client layout policy from the current client version only; do not fetch the latest server version just to decide the local layout rule.
