# Windows single-instance behavior (Stage 23)

Mitosis is a single-main-window desktop app. Starting it again must surface the
running window, not open a second copy of the app, onboarding, and stores. This
document records how that works, what is guaranteed, and what was observed.

## 1. Why a named mutex, not `AppInstance`

The app is unpackaged (no MSIX identity, `EnableMsixTooling=false`). The Windows
App SDK's `Microsoft.Windows.AppLifecycle.AppInstance` redirection is built around
package identity, and Microsoft's app-lifecycle documentation describes a single
named mutex as the normal mechanism for unpackaged apps. Stage 23 therefore uses
one mutex plus two named events, with no dependency beyond Win32/.NET primitives.

## 2. Objects and scope

| Object | Kind | Name |
|---|---|---|
| Instance lock | `Mutex` | `Local\Mitosis.Windows.<scope>.lock` |
| Activation request | auto-reset `EventWaitHandle` | `Local\Mitosis.Windows.<scope>.focus` |
| Acknowledgement | auto-reset `EventWaitHandle` | `Local\Mitosis.Windows.<scope>.ack` |

- `scope` is the first 8 bytes (16 hex characters) of the SHA-256 of the current
  user's SID. If the SID cannot be read, a sanitized account name is used. The
  raw account name and SID never appear in an object name.
- The `Local\` namespace makes the objects per logon session. Two Windows
  sessions (for example two users, or RDP and console) get independent instances;
  that is intentional.
- No `Global\` namespace, no elevation, no service, no machine-wide state.

## 3. Launch flow

### Primary (first launch)

1. `Program.Main` bootstraps the Windows App SDK, then creates the diagnostic log
   and asks `SingleInstanceCoordinator.TryBecomePrimary`.
2. `WaitOne(0)` succeeds and the process owns the lock. It creates the focus and
   acknowledgement events and logs `app.instance | primary`.
3. `App.OnLaunched` builds the main window, then starts a background activation
   listener and activates the window.
4. An `AbandonedMutexException` means the previous owner died without releasing
   (a crash or a force-kill). The process takes ownership, logs
   `primary (recovered an abandoned lock)`, and continues normally.

### Secondary (later launch)

1. The mutex wait fails. `Program.Main` logs
   `app.instance | secondary launch; asking the running instance to surface`.
2. It calls `AllowSetForegroundWindow(ASFW_ANY)`. Windows otherwise blocks a
   background process from taking the foreground; the process the user just
   launched is permitted to grant that for one activation. Best effort.
3. It opens the focus and acknowledgement events (retrying briefly while the
   primary may still be starting), sets the focus event, and waits up to 5
   seconds for an acknowledgement.
4. It logs `app.secondary-exit` and returns exit code 0. It never creates a
   window, a store writer, or a second onboarding flow.

The acknowledgement means "the running instance accepted an activation request
and queued it on its UI thread". It is not proof that Windows actually raised the
window above every other window.

### Activation delivery in the primary

The listener is a dedicated background thread (not a thread-pool work item), so
it cannot be starved by UI work. On a request it invokes a callback that marshals
through the main window's `DispatcherQueue`; the callback:

1. restores the window if the presenter is minimized (`OverlappedPresenter.Restore`),
2. shows it if `AppWindow.IsVisible` is false,
3. calls `Activate()`.

A window that closes between queueing and dispatch is ignored rather than
treated as a crash.

## 4. Shutdown behavior

- Closing the main window writes `app.stop`, stops the listener, releases the
  instance lock, shuts the Windows App SDK bootstrap down, marks the process as
  shutting down, closes every secondary window explicitly, and calls
  `Application.Exit()`. Mitosis is a single-main-window app: closing that window
  quits even if the welcome, Help, or Settings window is still open.
- The explicit child close is deliberate. `Application.Exit()` alone was observed
  leaving a remaining child window (and the process) alive, depending on which
  windows were open and in what order. The welcome flow also refuses an early
  close unless the flow finished; that guard now yields to application shutdown.
- Releasing the lock before process exit lets a launch during teardown become the
  new primary immediately instead of waiting for the old process to disappear.
- A hard crash or force-kill leaves the mutex abandoned; the next launch recovers
  it as described above. No stale lock file or manual cleanup exists.

The explicit `Application.Exit()` was added after runtime verification found that
closing the main window while the welcome window was open logged `app.stop` but
left the process and the orphaned welcome window alive. Stage 25 verification then
found a second case: closing the main window with Help or Settings open could still
leave that window and the process alive. Shutdown now closes the children itself
instead of trusting `Exit()` to enumerate them. Six repeated trials (Help only,
Settings only, both orders, repeated) all exited; the welcome-close guard is
bypassed only while the app is quitting, never for a user-initiated close.

## 5. Failure behavior

- If the guard itself cannot decide (an unexpected OS/ACL failure), the app logs
  `app.instance` at error level with the exception type and continues as primary.
  A broken guard must never prevent Mitosis from starting; the trade-off (a
  possible duplicate window) is visible in the log.
- If no acknowledgement arrives within the bound, the secondary exits 0 and logs
  a warning. It does not force a duplicate window or hang indefinitely.
- If the primary is hung, the secondary still waits only the bounded time. It
  cannot force the hung UI to repaint.

## 6. Verification

Automated tests (in `Mitosis.Windows.Platform.Tests`):

- first acquisition becomes primary and names its objects;
- a second acquisition from another thread is secondary (a holder thread models
  the separate process, because mutex ownership is thread-affine);
- an abandoned owner is recovered as primary;
- an activation request reaches the listener and is acknowledged;
- a missing listener times out; a missing primary fails;
- shutdown stops the listener and hands the lock to a new launch from another
  thread;
- scope validation and the user-scoped name;
- the logger serializes two writers to the same directory (Stage 22 deferral
  closed here, because Stage 23 introduces concurrent log writers).

Runtime observation on the development machine (Windows 11 25H2, Release build,
2026-11-26; the binary embeds the Stage 22 commit because Stage 23 was not yet
committed when it was built):

| Check | Observation |
|---|---|
| First launch | one process, window title `Mitosis`, log `app.instance \| primary` then `app.start` |
| Second launch | exited in 122–215 ms with code 0; still exactly one process; log shows the secondary request and `existing window activation requested` |
| Force-kill then relaunch | no processes remained; the relaunch became primary and opened a window |
| Close the main window | process exited with code 0; zero processes remained; `app.stop` logged |

Not verified: whether Windows visually raised the window on screen (the agent
session cannot observe focus), restore-from-minimized behavior, a second Windows
user/session, and elevated/secondary-integrity launches. These remain manual
checks in `docs/WINDOWS-UI-VERIFICATION.md` (S25).

## 7. Deliberately not implemented

- Forwarding launch arguments to the running instance. The app currently accepts
  no file, protocol, or command-line activation of its own, so there is nothing
  to forward. When that surface exists, the focus event should carry the request.
- Background/tray mode: closing the window exits; there is no hidden process.
- Cross-session or machine-wide instance blocking: the scope is per user session.
- Elevation flows: Mitosis never requests elevation; a manually elevated launch
  would be a separate instance and is unsupported.
