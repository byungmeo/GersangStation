# Game launch lifecycle

## Ownership

- `MainWindow` owns `GameLaunchCoordinator`, `GameStarter`, and the shared browser session. Sidebar launch state outlives HomePage/BrowserPage transitions.
- `GameLaunchAttempt` captures the server, global slot, account, install path, attempt identity, and cancellation token at request time.
- `WebViewManager` owns login/OTP and launch-payload reception. `GameStarter` owns Run.exe launch and Gersang.exe discovery/tracking.

## State and completion

| Phase | Meaning |
| --- | --- |
| Preparing / Starting | Preconditions, login/OTP, payload reception, or game discovery are in progress. |
| Running | GameStarter attached a new Gersang.exe process to the session. |
| Failed / Canceled | The attempt ended before Running; late completion cannot revive it. |
| Exited | The attached game process ended. |

Process attachment checks the baseline PID set, process start time, and executable path when available. It does not prove that the game window or its runtime dependencies initialized successfully. A dialog found by GameStarter is currently debug output, not a classified startup failure.

Login success, Run.exe creation, and a boolean request return are not Running. The launch socket has a 10-second wait limit. If Run.exe exits and no game is found within the 5-second discovery grace period, the attempt fails. Running process liveness is polled every second.

## Concurrency and cancellation

- Slots and the three-client limit are global across servers; account uniqueness is also global. Changing selection does not transfer ownership of an existing attempt.
- One launch/login preparation owns the shared browser at a time. The active Starting button cancels its attempt; another request cannot overwrite it.
- Async login, socket, launcher, process-discovery, and cleanup callbacks retain attempt/session identity. Terminal transitions invalidate their late results and cannot remove a newer attempt in the same slot.
- Cancellation ends the attempt before cleanup. It cancels owned browser/socket work and process discovery; navigation alone does not cancel JavaScript timers already scheduled in a document.
- An external launcher already started can still create a game after cancellation. Detached sessions track those processes for count/account limits without restoring the cancelled button's Running state.
- The retry cooldown is separate from the attempt's terminal state.

## OTP

OTP dialog waits, resubmission, and countdowns share the attempt's cancellation lifetime. Cancel/direct input end automatic launch; direct input preserves the browser document. Dialog cancellation affects only the dialog belonging to that attempt.

Related contracts: [game file operations](game-policies.md), [shared browser](browser-session.md).
