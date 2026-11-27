# Windows local diagnostics (Stage 22, extended in Stage 23)

Local, always-on, append-only diagnostics for the Windows port. Nothing here
leaves the machine: there is no telemetry, no network call, and no upload path.
Logs exist so a user can find out why Mitosis failed and so the later read-only
CLI (`mitosis doctor`) has a report to print.

## 1. What is recorded

| Event | Level | When | Contains |
|---|---|---|---|
| `app.instance` | Info/Error | Startup, before the UI | `primary`, `primary (recovered an abandoned lock)`, or `secondary launch; asking the running instance to surface`; Error only when the guard itself failed |
| `app.start` | Info | `App.OnLaunched` | App version, OS description, .NET runtime |
| `app.secondary-exit` | Info/Warning | Secondary launch, before exit | Whether the running instance acknowledged; Warning when it did not |
| `app.stop` | Info | Main window closed | Fixed text |
| `app.unhandled` | Error | WinUI UI-thread exception | Exception type and message |
| `app.domain-unhandled` | Error | `AppDomain.UnhandledException` | Exception type and message |
| `app.unobserved-task` | Error | `TaskScheduler.UnobservedTaskException` | Exception type and message |

Not recorded: application names, profile names, application data, credentials,
tokens, environment variables, file contents, request/response bodies, or network
activity. Feature-level logging will be added only when there is a feature that
needs it; no provisioning strategy exists today.

## 2. Location, format, and rotation

- Folder: `%LOCALAPPDATA%\Mitosis\Logs` (`WindowsProfilePaths.LogDirectory()`).
- Current file: `mitosis.log`; archives: `mitosis.1.log` (newest) and
  `mitosis.2.log`.
- One line per call:
  `2026-11-26T10:00:00.000Z INFO app.start | <message>`
- The timestamp is UTC with milliseconds; the level is `INFO`, `WARN`, or `ERROR`;
  the event name is limited to 64 characters and non-alphanumerics become `_`.
- The message has line breaks and tabs collapsed, runs of spaces collapsed, and is
  capped at 2,000 characters, so one call is always one line.
- The current file is capped at 512 KB. When the next entry would exceed it, files
  shift (`mitosis.log` → `mitosis.1.log` → `mitosis.2.log`) and the oldest archive
  is removed. The current file can exceed the cap by at most one entry.
- Total disk use is bounded at roughly 1.5 MB.
- Writers from different processes serialize on a named mutex derived from the log
  directory (Stage 23: the primary and a short-lived secondary can write at the
  same moment). Waiting is capped at 1 second; a missed window skips one entry and
  never disables the logger.

## 3. Privacy and redaction

- The current user's home directory is replaced with `~` in every message and in
  the diagnostics report, case-insensitively and in both slash styles.
- Exception text can still contain other local paths (for example a drive-rooted
  build path); that is documented rather than hidden. No source code or user
  document content is read to produce a log entry.
- The crash dialog shows the log-folder path (the user's own machine) and never a
  stack trace.
- Diagnostics report fields: app version, .NET runtime, OS description,
  architecture, Windows App SDK version, data-folder path and writability, and log
  folder. Application data is never enumerated for the report.

## 4. Failure behavior

`FileDiagnosticLog` never throws into application code. Directory creation or an
append can fail (permissions, disk full, a file in place of the directory); the
logger then disables itself for the rest of the process and records the exception
type in `DisabledReason`. Repeated writes become no-ops. Failing to acquire the
cross-process write lock within its 1-second bound only skips that entry; it does
not disable the logger. Logging is best effort: it must never change what the
application does.

## 5. Crash handling

- The WinUI `UnhandledException` handler writes `app.unhandled`, marks the event
  handled, releases the single-instance lock, shows a Win32 message box naming the
  log folder, and exits the process. The app does not continue after an unknown
  failure.
- `AppDomain.UnhandledException` writes `app.domain-unhandled`; the process is
  already terminating, so no dialog is attempted.
- `TaskScheduler.UnobservedTaskException` writes `app.unobserved-task` and marks
  the exception observed, matching normal .NET guidance.
- Closing the main window writes `app.stop` and exits the process (Stage 23:
  closing that window quits Mitosis, even if the welcome/Help/Settings window is
  open, so no orphaned process holds the log open).
- Exit code for the unhandled-UI path is the normal `Exit()` path; the existing
  startup-prerequisite path still returns `101`; a redirected second launch
  returns `0`.

## 6. Settings surface

Settings ▸ General ▸ Diagnostics shows the log-folder path, a
**Show in File Explorer** action, and **Copy diagnostics**, which puts the plain
report on the clipboard. Both actions are honest no-ops on failure; the path stays
selectable. The section says explicitly that logs stay on this PC and never include
application data, credentials, or file contents.

## 7. Verification

- Unit tests cover home redaction (both slash styles, case-insensitive), line
  hygiene and length caps, report fields and writability, rotation bounds and
  newest-entry retention, disabled-on-failure behavior, constructor limits,
  concurrent writers from two logger instances (cross-process lock), and the
  Settings commands/events.
- Runtime observation (Stage 23): a primary launch, a redirected second launch
  (exit 0 in 122–215 ms), a force-kill/relaunch, and a graceful close (exit 0)
  each produced the expected log lines. See `docs/WINDOWS-SINGLE-INSTANCE.md`.
- Not verified in this environment: the actual crash dialog, clipboard delivery,
  Explorer reveal, and visual layout. Those remain manual checks (see
  `docs/WINDOWS-UI-VERIFICATION.md`); no result is claimed without an observation.

## 8. Explicitly deferred

- Store-health and discovery counts in the **GUI copy report**. The read-only CLI
  `doctor` prints them (Stage 24, `docs/WINDOWS-CLI.md`); the Settings clipboard
  report stays environment-only.
- Per-feature log calls for the clone engine (no production strategy exists).
- Log export/zip, retention-by-age, and any remote reporting.
- Forwarding launch arguments to the running instance (no activation kinds exist).
