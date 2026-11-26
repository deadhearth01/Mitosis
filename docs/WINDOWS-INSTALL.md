# Windows install and uninstall (per-user, unsigned)

Status: implemented in Stage 26 and verified on the development machine
(Windows 11 25H2). Not yet verified on a clean Windows 10/11 VM, and **not**
code-signed. This is the install story that ships in the unsigned release
archive; MSI/Store packaging, signing, and public distribution are separate,
authorization-gated stages (`docs/WINDOWS-AUTONOMOUS-ROADMAP.md`, Stage 32).

## 1. What the archive is

`Mitosis-windows-<version>-x64-<rev>.zip` contains the unpackaged, framework-
dependent x64 app, the read-only CLI under `cli\`, the brand assets, and
`install.ps1`. Nothing is pre-installed: the zip is the whole package. The
`install.ps1` in the zip root is both the installer and the uninstaller; there is
no separate setup executable, service, or scheduled task.

The archive is **unsigned**, so SmartScreen and some security products will
warn. Verification is by the `.sha256` sidecar next to the zip and by
`ReleaseVerifier` (see `docs/WINDOWS-RELEASE.md`).

## 2. Prerequisites

| Requirement | Why | Detected by |
|---|---|---|
| 64-bit Windows 10 version 1809 (build 17763) or newer, or Windows 11 | The build is x64-only and targets Windows App SDK 2.x | `Is64BitOperatingSystem`; build check |
| Microsoft .NET 10 x64 runtime (`Microsoft.NETCore.App` 10.0) | The app is framework-dependent; the apphost will not start without it | Machine-wide install recorded by the runtime installer, `%ProgramFiles%\dotnet`, or `DOTNET_ROOT` |
| Windows App Runtime 2.5 or newer (per-user framework package) | `Bootstrap.Initialize` initializes the Windows App SDK dynamic dependency | The archive's own `Microsoft.WindowsAppRuntime.Bootstrap.dll`, using the same `MddBootstrapInitialize` call the app makes; per-user `Get-AppxPackage` as a fallback |

The installer **detects and reports** prerequisites; it never downloads or
installs them, and it never elevates. A missing prerequisite is a warning by
default (the install still succeeds, and the app shows its own diagnostic
message at launch). `-StrictPrerequisites` turns any warning into a preflight
failure before a single file is written.

A user-local .NET install (for example the `dotnet-install` script's
`%LOCALAPPDATA%\Microsoft\dotnet`) is reported separately, because the apphost
only uses it when `DOTNET_ROOT` points at it. The check therefore says
"found only in a user-local install" rather than claiming the machine is ready.

## 3. Install

```powershell
Expand-Archive Mitosis-windows-0.2.2-x64-<rev>.zip -DestinationPath Mitosis
cd Mitosis
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Use `-DryRun` to print the plan (prerequisite findings, target, file count,
shortcut and uninstall entry) without changing anything.

| Parameter | Default | Effect |
|---|---|---|
| `-Source` | the script's folder | Folder containing `Mitosis.exe` |
| `-InstallRoot` | `%LOCALAPPDATA%\Programs\Mitosis` | Per-user install location; must be at least two folders below a drive root and must not be a protected system/profile folder |
| `-UserDataRoot` | `%LOCALAPPDATA%\Mitosis` | Profile/log data owned by the app; only used to report or remove data |
| `-DryRun` | off | Print the plan; write nothing |
| `-Force` | off | Replace an existing Mitosis install, or overwrite an otherwise-unowned non-empty folder |
| `-NoShellIntegration` | off | Skip the Start-menu shortcut and the HKCU uninstall entry (portable/test use) |
| `-SkipPrerequisiteCheck` | off | Do not inspect the host at all |
| `-StrictPrerequisites` | off | Treat prerequisite warnings as a blocking failure |
| `-Quiet` | off | Suppress informational output (warnings still print) |

Exactly what a default install writes:

| Location | Content |
|---|---|
| `%LOCALAPPDATA%\Programs\Mitosis\` | The archive payload (94 files, including `install.ps1`), plus `.mitosis-install.json` (product, version, install time, recorded file list) |
| `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Mitosis.lnk` | Shortcut to the installed `Mitosis.exe` (icon from the exe, working directory = install folder) |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mitosis` | Apps-list entry: display name/version/icon, install location, `UninstallString`, `QuietUninstallString`, `NoModify`, `NoRepair` |

Nothing is written outside the current user's profile and HKCU. There is no
`PATH` change, no machine-wide registration, no service, and no firewall rule.

## 4. Uninstall

Use **Settings → Apps → Mitosis → Uninstall**, or run the installed copy:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\Mitosis\install.ps1" -Uninstall
```

- The uninstaller refuses a folder without the `.mitosis-install.json` marker,
  and refuses when the marker's recorded install path does not match the target.
- It removes exactly the files recorded at install time, then the marker, the
  shortcut, and the HKCU entry. Files added to the folder later (for example a
  user note) are kept, and so is the folder if anything remains.
- Profile data in `%LOCALAPPDATA%\Mitosis` is **kept** by default. Passing
  `-RemoveUserData` deletes it explicitly; `-DryRun` shows which root would be
  deleted first.
- Uninstalling while Mitosis is running from the install folder is refused, so
  locked files cannot produce a half-removed install.

Exit codes: `0` success, `1` usage/preflight failure, `2` refused (unsafe path,
already installed, app running, missing or mismatched marker).

## 5. Safety properties

- Per-user only; no elevation, no machine-wide state, no `PATH` edit.
- No network access, no downloads, no telemetry.
- Deletion is scoped: a marker plus a recorded file list, never a recursive
  delete of a folder the installer does not own; profile data needs an explicit
  `-RemoveUserData`.
- The install root must be at least two folders below a drive root and must not
  be a drive root, the user profile, `%LOCALAPPDATA%`, `%APPDATA%`,
  `%ProgramData%`, `Program Files`, or `System32`.
- A reinstall requires `-Force`; an unrelated non-empty folder is never
  overwritten silently.

## 6. Verification

Automated (`windows/tests/Mitosis.Windows.Platform.Tests/InstallerScriptTests.cs`,
11 tests, each running the real script in a child PowerShell against a fixture
payload under a temporary directory with shell integration disabled):

- dry run writes nothing; a source without `Mitosis.exe` fails before writing;
- payload and marker are written, and every payload file is recorded;
- reinstall is refused without `-Force` and accepted with it; an unowned non-empty
  folder is refused and its file untouched; a drive-root target is refused;
- uninstall removes recorded files, keeps unrelated files and profile data, and
  refuses a folder without a marker;
- `-RemoveUserData` removes both roots; uninstall dry run changes nothing;
- `-RemoveUserData` without `-Uninstall` is rejected.

Packaging: `publish-release.ps1` copies `scripts/install.ps1` into the archive
root after parse-checking it, and `ReleaseVerifier` requires `install.ps1` in the
archive (one fixture test covers the missing case).

Observed manually on 2026-11-26/27 (development machine, not a clean VM):

- installed from the published tree and from a freshly unzipped archive; the
  installed app showed the main and welcome windows and exited when the main
  window closed; uninstall removed both the install folder and (with
  `-RemoveUserData`) the data folder;
- prerequisite detection reported the real .NET 10 runtime (user-local, with
  `DOTNET_ROOT` set) and initialized the real Windows App Runtime 2.5+
  bootstrapper; without `DOTNET_ROOT` it reported the user-local install
  honestly. `-StrictPrerequisites` failed before writing in that case and passed
  with `DOTNET_ROOT` set;
- a full shell-integration install (shortcut + HKCU entry) was inspected (target,
  working directory, icon, uninstall strings) and then uninstalled, which
  removed both; the Start menu and HKCU were clean afterwards;
- the release verifier passed the final archive (94 files, 45,762,106 bytes,
  SHA-256 `9db1ef6bc9b74fd40afa042536cf03500348ff0823e73934f4687d8a53b8d499`).

Not verified here: clean Windows 10/11 VM behaviour, standard (non-admin) user,
another user profile, Windows on ARM, PowerShell 7 only, SmartScreen/AV
behaviour, reboot/upgrade semantics, and anything involving a signed build.

## 7. Deliberately out of scope

- MSI (WiX), Inno Setup, NSIS, MSIX/Store, winget, and an `PATH` installation are
  not built or chosen. The zip plus this script is the current unsigned model;
  the installer technology decision belongs with signing and public distribution
  (Stage 32) and needs explicit authorization.
- No automatic update, no background service, no tray mode.
- The read-only CLI is used by path (`cli\mitosis.exe`); it is not added to
  `PATH`.
