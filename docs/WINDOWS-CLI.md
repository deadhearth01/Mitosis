# Windows read-only CLI (Stage 24)

`mitosis.exe` is a small, read-only console companion for the Windows port. It
shares discovery, the profile store, and the diagnostics report with the GUI but
cannot clone, launch, modify, or delete anything. Engine commands are refused
outright rather than pretending to work.

## 1. Status and scope

| Area | Status |
|---|---|
| `version` | Implemented, runtime-verified |
| `list` | Implemented, runtime-verified |
| `profiles` | Implemented, runtime-verified |
| `doctor` | Implemented, runtime-verified (text and `--json`) |
| `help` / `--help` / `--version` | Implemented |
| Clone, stats, clean, refresh, delete, open, links | Not implemented; not accepted as commands |
| Release-archive packaging | Shipped in `cli/` inside the Windows release zip (Stage 26; see §7) |

The CLI targets `net10.0-windows10.0.19041.0`, is framework-dependent x64, and
does not reference WinUI. It therefore starts without the Windows App SDK UI
runtime, while discovery still uses the same registry and packaged-app sources as
the app.

## 2. Build, run, publish

From `windows/`:

```powershell
dotnet build Mitosis.sln -c Release
dotnet run --project src/Mitosis.Cli -c Release -- doctor
```

The build output is
`src/Mitosis.Cli/bin/Release/net10.0-windows10.0.19041.0/mitosis.exe`. For a
deployable tree:

```powershell
dotnet publish src/Mitosis.Cli/Mitosis.Cli.csproj -c Release -r win-x64 --self-contained false -o artifacts/cli
```

The publish tree is 17 files (the CLI, the shared Mitosis assemblies, the
Windows SDK projection, WinRT, and PDBs). It is gitignored under
`windows/artifacts/`.

## 3. Commands

### `mitosis version`

Prints the informational version (which may carry a `+<git-sha>` suffix), the
.NET runtime, and the OS description. `--json` emits the same fields.

Observed on the development machine:

```
Mitosis 0.2.2+6f9ee6bad83877d8f20712bfce6f048559fcd05b
.NET 10.0.15 on Microsoft Windows 10.0.26300 (X64)
```

### `mitosis list`

Discovers installed applications exactly as the app does: current-user and
machine uninstall registrations (32/64-bit) plus packaged-app metadata. Reads
only; it does not execute, crawl, or write.

Text output is a fixed-width table with `NAME`, `PUBLISHER`, `VERSION`, `SCOPE`,
`SOURCE`, and `SUPPORT`. `SUPPORT` is the same compatibility verdict the UI
shows; with the production strategy catalog empty it is `unknown`, and Windows
system components assess as `unsupported`.

```
NAME                  PUBLISHER             VERSION     SCOPE    SOURCE                 SUPPORT
Slack                 Slack Technologies    4.40.0      user     user uninstall         unknown
...
127 applications.
Read-only: Windows uninstall registrations and packaged-app metadata.
```

Empty discovery prints `No applications discovered.` rather than a fake row.

### `mitosis profiles`

Lists clone-profile **metadata** records from the same JSON store the app uses.
Every record is labelled `planned` until a future engine provisions it; the
command ends with an explicit note that none of these is a working clone. An
empty store prints `No clone profiles yet.` An unreadable or damaged store exits
with code 2 and states that the store was not changed.

### `mitosis doctor`

Prints the Stage 22 diagnostics report plus a `Local state` section:

- profile store readability and record count (or the exception type);
- log file count, total bytes, and newest entry timestamp (or `none yet`);
- discovered-application count, or why discovery was unavailable;
- data-folder writability.

Discovery makes `doctor` slower than the other commands (about 1.5 s for 127
applications on the development machine). It is a diagnostic, not a polling
command.

```
Mitosis diagnostics
...
Windows App SDK: not loaded by the CLI
Data folder: ~\AppData\Local\Mitosis
Data folder writable: yes
Log folder: ~\AppData\Local\Mitosis\Logs

Local state
Profiles: 0 (store readable)
Logs: 1 file, 1.5 KB, latest 2026-11-26T15:09:09Z
Discovered applications: 127

Recorded: app/runtime versions, Mitosis data paths, and local counts only.
Not recorded: application data, credentials, file contents, or network activity.
```

The CLI does not load WinUI, so it cannot read the Windows App SDK version the
GUI runs against; it prints `not loaded by the CLI` instead of guessing. The GUI
report is unchanged and still shows the real App SDK version.

## 4. JSON output

Every command except `help` accepts `--json`. JSON naming is camelCase; enums are
serialized as camelCase strings (for example `currentUser`,
`currentUserUninstall`, `needsReview`). Text-mode labels are human-readable and
differ deliberately (`user`, `user uninstall`, `needs review`).

`list`:

```json
{
  "count": 1,
  "applications": [
    {
      "id": "currentuseruninstall:0123456789abcdef",
      "name": "Slack",
      "publisher": "Slack Technologies",
      "version": "4.40.0",
      "scope": "currentUser",
      "sources": ["currentUserUninstall"],
      "support": "unknown",
      "isSystemComponent": false
    }
  ]
}
```

`profiles` adds `note`; `doctor` emits `generatedAt`, environment fields,
`profiles { readable, count, problem }`, `logs { fileCount, bytes, latestEntryUtc,
problem }`, and `discovery { count, problem }`. Null means "not available", not
zero.

Home paths are redacted to `~` in both text and JSON, because CLI output is
likely to be pasted into an issue. Record contents, application data,
credentials, and network activity are never included.

## 5. Exit codes

| Code | Meaning |
|---|---|
| 0 | Success, including an empty list and a doctor that found problems |
| 1 | Bad usage: unknown command, unexpected argument, duplicate `--json` |
| 2 | A read-only operation failed (discovery, profile store) |

`doctor` returns 0 when it can report a problem honestly. Use the fields, not the
exit code, to detect a damaged store or failed discovery.

## 6. Read-only guarantees

- `version`, `list`, `profiles`, and `help` perform no writes at all.
- `doctor` tests writability by creating and immediately deleting one zero-byte
  probe file in the Mitosis data folder. If the folder does not exist, the probe
  runs in its nearest existing parent and the folder is **not** created, so a
  fresh machine stays fresh. No profile, preference, log, or application-data
  file is written, and no probe file is left behind. A test asserts that a real
  `profiles.json` is not created and that a missing data folder is still missing
  after `doctor`; a runtime check confirmed the existing log file's SHA-256 was
  unchanged after `doctor` and `doctor --json`.
- The CLI never writes to the registry, never launches an application, never
  scans drives, and never opens a network connection.
- Profile reads use the existing repository, so schema, duplicate, and
  atomicity rules are identical to the app's.

## 7. Packaging decision

Stage 26 ships the CLI inside the release archive in a `cli/` subfolder:

- Windows filenames are case-insensitive, so `Mitosis.exe` (the GUI) and
  `mitosis.exe` (the CLI) - and their `.deps.json` files - cannot share one
  folder. `cli/` is therefore a real requirement, not a preference.
- `publish-release.ps1` publishes the CLI into the same archive, checks its
  version metadata against the app's, and smoke-runs `cli\mitosis.exe version`
  before zipping. `ReleaseVerifier` requires `cli/mitosis.exe`,
  `cli/mitosis.deps.json`, and `cli/mitosis.runtimeconfig.json`, audits the CLI
  as x64, and checks its version metadata against the archive name. The CLI is
  not required to carry an icon.
- The `cli/` folder duplicates the shared assemblies (about 26 MB unpacked,
  dominated by `Microsoft.Windows.SDK.NET.dll`) because the two executables
  cannot share one probing path. That cost is accepted for a correct layout.
- No system `PATH` change is made or proposed. Run `cli\mitosis.exe` from the
  unzipped folder or by explicit path. An installer/PATH decision remains
  separate and authorization-gated.

## 8. Verification

Automated (MSTest, release build):

- 31 CLI tests: parsing (all commands, `-v`/`--version`/`-h`, `--json` casing,
  duplicate/unknown/extra arguments), version text and JSON, list text/JSON/empty/
  failure, profiles text/empty/failure, doctor text/JSON/readable and unreadable
  state, the no-store-write guarantee, and the no-data-folder guarantee.
- 2 additional `DoctorReport` tests cover the new local-state rendering and the
  failure text.
- Full suite: 396/396 green, 0 warnings.
- Runtime observation on Windows 11 25H2 (2026-11-26): `version` 0.2.2+`6f9ee6b`
  (the Stage 23 commit), 127 applications listed in about 1.5 s, no profiles,
  `doctor` text and JSON agreed on counts, and `clone Slack` exited 1 with a
  usage error.

Not verified: a clean machine (runtime prerequisites), a real damaged store on
disk (covered by fake-store tests only), alternate console hosts and code pages,
and second-user/session behavior. Non-ASCII display names depend on the console
font and code page; use `--json` for automation.
