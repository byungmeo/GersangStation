# Current Game Policies

## Launch State And Limits

- Allow at most 3 game clients across all servers, enforce account uniqueness globally, and treat launch buttons as global slot numbers.
- Set a slot to `Starting` immediately on launch click, then return it to `Available` if preconditions fail before the game process starts.

## Install, Patch, And Permissions

- Accept only fully qualified client install paths, then validate them with `Run.exe`, `\Online\Map`, and `\Online\vsn.dat`; paths missing `\Online\vsn.dat` are invalid because the current client version cannot be determined.
- Before install, patch, or multi-client creation, probe write access on the current or planned target path; permission failures use the shared permission-warning or permission-guidance dialog flow.

## Multi-Client Layout

- Choose the multi-client layout from the current local client version, and create station-managed clone folders with the `_CreatedByStation` suffix.
- For `v34100+`, hard-copy direct files under `Online`, symbolic-link `Online` subdirectories, and copy `\Assets\Config` according to the config overwrite policy instead of linking it.
- Skip top-level `PatchTemp`, `GersangDown`, and per-client `ScreenShots`; preserve real clone `ScreenShots` folders and remove old symbolic links for that folder.
- When symbolic support is unavailable, treat existing paths as non-symbolic; when a real directory occupies a path that must be symbolic, report a conflict instead of deleting it.
- When overwriting a destination file during multi-client creation, delete the destination first if it is symbolic and then copy the source file.

## Game Window Control

- Keep mouse confinement disabled unless GersangStation runs as administrator, target only the foreground top-level `Gersang` window, and suspend correction while `Alt` is held or temporary window browsing is active.
- Do not compete with the game's global `ClipCursor` ownership; use an app-owned `WH_MOUSE_LL` hook for client-area edge corrections and keep the 5ms poll as a fallback. Activate confinement only after client entry, release it on `Alt`, and bypass correction throughout drags started outside the client. Rearm only after the drag ends and the cursor re-enters the client; retry failed `SetCursorPos` calls on the next poll.
- Revalidate the foreground HWND, its process ID, and current service state immediately before cursor correction. Invalidate pending observations on release or stop. A low-level hook must pass input through when service state is busy, and must not hold the service lock while invoking the next hook.
- Enable the first window-switch mode only as administrator; implement game-window control by polling at a fixed 5ms interval.
- Window switching uses the fixed `Alt` + `` ` `` chord, cycles running launch slots, and uses only short z-order raises without persistent `TopMost`.
- During window browsing, reserve `Alt` + `` ` `` for additional cycling and end browsing on the first left-click; normalize tracked windows to stable top-level root-owner handles.
