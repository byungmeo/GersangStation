# Current Game Policies

- Allow at most 3 game clients at the same time across all servers.
- Enforce account uniqueness globally so the same account cannot run on multiple clients at once, even across different servers.
- Treat launch buttons as global by slot number across all servers, so once button 1, 2, or 3 starts a client, that same-number button stays locked until its process exits.
- Switch the slot to `Starting` immediately when the user clicks launch.
- Return the slot to `Available` if launch preconditions fail before the game process starts.
- Validate client install paths by checking for `Run.exe`, `\Online\Map`, and `\Online\vsn.dat` instead of the legacy `\char` marker.
- Treat a path without `\Online\vsn.dat` as invalid for launch and multi-client creation, because the current client version cannot be determined.
- For multi-client creation on `v34100+` layouts, hard-copy direct files under `Online` with overwrite while keeping subdirectories under `Online` as symbolic links.
- For multi-client creation on `v34100+` layouts, apply the config overwrite policy to `\Assets\Config` and everything under it instead of symbolic-linking that tree.
- Skip top-level `PatchTemp` and `GersangDown` directories entirely during multi-client creation; do not copy or symbolic-link them into clones.
- Choose the multi-client layout policy from the current client version only; do not fetch the latest server version just to decide the local layout rule.
- Before starting install, patch, or multi-client creation, probe write access on the current or planned target path; if the probe fails due to permission, show the shared permission-warning dialog with the wiki help link before letting the user continue anyway.
- If install, patch, or multi-client creation still fails with a permission-related exception after preflight, show the shared permission guidance dialog instead of only surfacing a generic failure message.
- When probing whether an existing path is symbolic, check `CanUseSymbol` first; if the drive does not support symbolic links, treat the path as definitively non-symbolic and skip reparse-point probing.
- When overwriting a destination file during multi-client creation, delete the destination first if it is a symbolic file and then copy the source file.
- Constrain the cursor only while the foreground window belongs to a process named exactly `Gersang` and the cursor is already inside that window's client area; recompute bounds for whichever `Gersang` window is active, suspend the confinement while the selected escape modifier is held (default `Alt`), keep it released while the cursor remains outside the client area, and resume it only after the cursor re-enters.
- Keep the clip-mouse feature fully disabled unless GersangStation itself is running with administrator privileges, even if the saved toggle is on.
