# Mitosis for Windows

This folder holds the native Windows port of Mitosis. The macOS implementation in
`Sources/` (Swift/SwiftUI) is untouched and remains the authoritative Mac app.
See [docs/WINDOWS-PORT.md](../docs/WINDOWS-PORT.md) for the full port plan.

## Status

Shell, read-only discovery, metadata profiles, the metadata-only New Clone
wizard, Settings, Help, onboarding, and the release-candidate pipeline are
implemented. `Mitosis.Windows.App` is a real, launchable WinUI 3 application: the
sidebar lists genuinely discovered applications (read-only Windows registrations)
with per-app compatibility verdicts; profiles can be created, renamed, re-badged,
and removed as metadata records. Sample data survives only behind the explicit
`Show preview instead` fallback and is never mixed with real results.

The clone engine is framework only: the production strategy catalog is empty and
every provisioning gate is closed. Nothing here clones, launches, or modifies an
application. Local diagnostics (a bounded, redacted log, a copyable environment
report, and crash hooks), single-instance activation (one window per user
session), a read-only CLI (`version`, `list`, `profiles`, `doctor`) that ships in
`cli/` inside the release zip, and a manual, read-only update-availability check
against GitHub Releases are implemented. Closing the main window quits. The
update banner opens only when a newer release exists and offers its release page;
nothing downloads or installs. The release zip also carries a per-user installer
(`install.ps1`), so there is a supported install and uninstall path. Signing,
self-update, and tray integration remain later stages. See
[docs/WINDOWS-AUTONOMOUS-ROADMAP.md](../docs/WINDOWS-AUTONOMOUS-ROADMAP.md) for
current status and gaps.

## Prerequisites

| Tool | Required | Status on the development machine |
|---|---|---|
| Git | any recent | 2.54.0 installed |
| .NET SDK | 10.0.100 or newer (LTS, `latestFeature` roll-forward per `global.json`) | 10.0.404 (user-local install) |
| Windows App SDK (NuGet `Microsoft.WindowsAppSDK` 2.5.1) | needed from Stage 2 (WinUI 3 UI) | Referenced; runtime 2.5.1 present on this machine (unpackaged app requires a machine runtime, see Run) |
| Visual Studio 2026 / VS Code + C# Dev Kit | recommended for XAML work | Not installed; `dotnet` CLI only |
| Windows 10 SDK / MSVC | only for native Win32 interop (Stage 5+) | Not installed |

Minimum supported Windows version: **Windows 10 version 1809 (build 17763)**,
per `TargetPlatformMinVersion 10.0.17763.0` in the app project (Windows App SDK
floor); libraries target the 19041 API contract. Verified on Windows 11 25H2.

No system-wide installation was performed during Stage 0. Install the .NET 10 SDK
from <https://aka.ms/dotnet/download> (or `winget install Microsoft.DotNet.SDK.10`),
then restore works offline of any template: plain `dotnet build` / `dotnet test`.

## Layout

```text
windows/
  Mitosis.sln                 solution (9 src projects, 8 test projects, verifier)
  global.json                 pins .NET SDK 10.0.100+ and MSTest.Sdk 4.5.1
  Directory.Build.props       shared compiler settings (nullable + implicit usings)
  src/
    Mitosis.Domain/           domain models, contracts, state definitions (net10.0)
    Mitosis.Application/      use cases and orchestration (net10.0)
    Mitosis.Infrastructure/   persistence, logging, filesystem abstractions (net10.0)
    Mitosis.Windows.Platform/ Windows-specific APIs and integration (net10.0-windows)
    Mitosis.Compatibility/    app detection and compatibility decisions (net10.0)
    Mitosis.CloneEngine/      clone creation, launching, refresh, recovery (net10.0)
    Mitosis.Windows.ViewModels/ hand-rolled MVVM state for the shell, preview data, commands (net10.0)
    Mitosis.Windows.App/      WinUI 3 application: main window, sidebar, detail, theme (unpackaged WinExe)
    Mitosis.Cli/              read-only console CLI: version, list, profiles, doctor (net10.0-windows)
  tests/
    Mitosis.Domain.Tests/          domain primitives, profile lifecycle, version comparison
    Mitosis.Application.Tests/     update checker outcomes and product-version source
    Mitosis.Infrastructure.Tests/  JSON stores, persistence, GitHub release feed
    Mitosis.Compatibility.Tests/   compatibility assessment
    Mitosis.CloneEngine.Tests/     planner/gate framework (no execution)
    Mitosis.ViewModels.Tests/      shell view models (discovery, profiles, wizard, settings, updates)
    Mitosis.Windows.Platform.Tests/ Windows-specific discovery and app-host tests
    Mitosis.Cli.Tests/             CLI parsing, text/JSON output, read-only guarantees
  tools/
    ReleaseVerifier/          release-candidate archive verification console tool (Stage 19)
  scripts/
    publish-release.ps1       publish, version-check, smoke-test, zip, SHA-256 sidecar
    install.ps1               per-user install/uninstall shipped in the archive root (Stage 26)
```

The domain, application, infrastructure, compatibility, and Windows platform
layers carry real implementations. `Mitosis.CloneEngine` currently contains the
planning/gating framework only; it has no executor and the production strategy
catalog is deliberately empty.

## Build and test

```powershell
cd windows   # dotnet commands run here so windows/global.json applies
dotnet restore Mitosis.sln
dotnet build Mitosis.sln --no-restore
dotnet test --solution Mitosis.sln --no-build
```

## Run the app

Unpackaged: build first, then run the exe (`Mitosis.exe`, `FileVersion`
metadata from `windows/Directory.Build.props`; needs a Windows App SDK 2.x
runtime (2.5+) and the .NET 10 runtime; when the App SDK runtime is missing
the app shows an error dialog naming the component and the HRESULT — exit
code 101 — instead of a crash dialog with a stack trace):

```powershell
.\src\Mitosis.Windows.App\bin\x64\Debug\net10.0-windows10.0.19041.0\Mitosis.exe
```

For release-readiness (deployment model, prerequisites, publish commands,
release-candidate packaging, checklist, and blockers), see
[docs/WINDOWS-RELEASE.md](../docs/WINDOWS-RELEASE.md).

## Discovery (Stage 3, read-only)

On launch the sidebar lists real installed applications from known Windows
registrations (current-user/machine uninstall views plus packaged apps), grouped
by initial letter with a compatibility verdict per app (`Not yet assessed` until
per-application strategies land; system components are `Not supported`).
Selecting an app shows its publisher, version, scope, sources, and reasons.

- Read-only: registry keys open without write access; nothing is executed,
  downloaded, or recursively scanned; standard user rights suffice.
- Search covers names and publishers; loading/empty/error states include retry.
- On failure, `Show preview instead` loads explicit sample data — never automatic,
  never mixed with real results.
- Details: `docs/WINDOWS-DISCOVERY.md`.

## Profiles (Stage 4, metadata only)

Clone profiles persist as metadata in `%LOCALAPPDATA%\Mitosis\Profiles\profiles.json`
(versioned JSON, atomic saves). Each app shows its profiles (`Planned — metadata
only` until the engine provisions them); profiles whose app is missing are kept
under an `Unavailable` section and rejoin automatically. `Planned` profiles can be
renamed, re-badged, or removed from the app detail and orphan detail panels — a
removal deletes the metadata record only, never application files, logins, or data.
Profiles that have left the planning stage are refused because managing them needs
the clone engine. Details: `docs/WINDOWS-PROFILES.md`.

## New Clone wizard (Stage 5, metadata only)

Ctrl+N (or New Clone) opens the wizard: pick an application, enter details with
live badge preview, review, then save. The app detail's “New Clone…” button opens
the same wizard with that application preselected. Saving writes one `Planned`
profile record — the app is not copied or launched. `Unsupported` apps are blocked
with reasons; review-required apps need explicit acknowledgement. The review step
also states provisioning availability: profile flags are under research, nothing is
validated yet (see `docs/WINDOWS-STRATEGY-RESEARCH.md`). Unsupported visual states
and manual screenshot comparison steps are listed in `docs/UI-PARITY-MATRIX.md`.

## Evidence bundles (Stage 8, framework only)

Isolation evidence records live in `%LOCALAPPDATA%\Mitosis\Evidence` (versioned JSON,
atomic saves, integrity-checked). Recording what must be proven is implemented;
proving it requires separately authorized live testing. App detail shows a
verification summary per application. Details: `docs/WINDOWS-EVIDENCE-BUNDLES.md`.

## Local diagnostics (Stage 22)

Logs live in `%LOCALAPPDATA%\Mitosis\Logs` (`mitosis.log` plus two rotated
archives, 512 KB each). They record app start/stop and unexpected exceptions only;
home paths are redacted to `~`, line breaks are collapsed, and a filesystem failure
disables the logger instead of affecting the app. Settings ▸ General ▸ Diagnostics
reveals the folder and copies a plain environment report (versions, OS,
architecture, data-folder writability, paths). Nothing is uploaded and no
application data or credentials are read. Details:
`docs/WINDOWS-DIAGNOSTICS.md`.

## Single instance (Stage 23)

One Mitosis window per user session. A per-user `Local\` named mutex is decided in
`Program.Main`; a second launch asks the running instance to surface its window
(restoring it if minimized) and exits without creating a duplicate. A crashed
instance leaves only an abandoned mutex, which the next launch recovers. Closing
the main window quits the process even when the welcome, Help, or Settings window
is open, and releases the lock immediately. Details and observed runtime results:
`docs/WINDOWS-SINGLE-INSTANCE.md`.

## Read-only CLI (Stage 24)

`mitosis.exe` is a separate console binary with `version`, `list`, `profiles`,
`doctor`, and `help`; every command except help accepts `--json`. It shares
discovery, the JSON profile store, and diagnostics with the app, and it cannot
clone, launch, modify, or delete anything. Exit codes are 0 (success), 1 (bad
usage), and 2 (a read-only operation failed). Run it from its build/publish tree:

```powershell
dotnet run --project src/Mitosis.Cli -c Release -- doctor
dotnet publish src/Mitosis.Cli/Mitosis.Cli.csproj -c Release -r win-x64 --self-contained false -o artifacts/cli
```

The CLI ships in `cli\` inside the release zip since Stage 26, so one unzipped
folder supports both `Mitosis.exe` and `cli\mitosis.exe`. No PATH change is
made; run it by explicit path. Details and runtime evidence:
`docs/WINDOWS-CLI.md`.

## Install and uninstall (Stage 26)

The release zip's `install.ps1` installs the app per-user — payload under
`%LOCALAPPDATA%\Programs\Mitosis`, a Start-menu shortcut, and an HKCU Apps entry —
detects the .NET 10 and Windows App Runtime prerequisites without downloading
anything, and uninstalls from Settings ▸ Apps or with `-Uninstall`. It removes only
files it recorded at install time and keeps profile data unless `-RemoveUserData`
is passed. `-DryRun`, `-NoShellIntegration`, and `-StrictPrerequisites` help with
testing and portable use. There is no elevation, PATH change, service, or MSI yet.
Details and evidence: `docs/WINDOWS-INSTALL.md`.

```powershell
Expand-Archive Mitosis-windows-<version>-x64-<rev>.zip -DestinationPath Mitosis
powershell -ExecutionPolicy Bypass -File .\Mitosis\install.ps1 -DryRun
powershell -ExecutionPolicy Bypass -File .\Mitosis\install.ps1
```

## Continuous integration (Stage 11)

Pushes to `WindowsVersion` and pull requests run `.github/workflows/windows-ci.yml`
(`windows-latest`): restore, Release build, full test suite with `.trx` artifacts
(14 days). Since Stage 17 a second job publishes the x64 release candidate from
the same recipe (`windows/scripts/publish-release.ps1`); since Stage 19 it then
verifies the actual archive with `windows/tools/ReleaseVerifier` (ZIP
enumeration + extraction, required files, x64 executable, embedded icon,
version coherence, `SHA-256` sidecar) before uploading the
`windows-release-candidate` artifact (the zip plus its `.sha256` sidecar, both
14 days). The recipe also bundles the read-only CLI in `cli\` and smoke-runs it,
copies the parse-checked per-user installer to the archive root, and both the
recipe and verifier require `Mitosis.pri` and `install.ps1` (Stage 26). Stage 20
hardened the verifier against hostile input: documented
resource limits (archive/expanded size, entry count, sidecar size), strict
two-token sidecar binding, traversal/absolute/drive/Windows-ambiguous entry
rejection, metered extraction with guaranteed scratch cleanup, and
bounds-checked PE parsing that fails as a finding rather than a crash — see
`docs/WINDOWS-RELEASE.md`. Reproduce locally with the commands in
`docs/WINDOWS-CI.md`. CI covers compilation, unit tests, and artifact
verification only — not UI launch, pixels, Narrator, isolation, licensing,
install-time behavior, or clean-machine runs.

What you should see: the Mitosis main window (1060×720) with discovered
applications grouped by initial letter, an update banner, and the empty state when
nothing is selected. Selecting an app shows its detail page with the compatibility
verdict and its saved profiles; the empty state appears when discovery returns
nothing. Search filters live; Enter opens the selected app, Ctrl+N starts New
Clone, Ctrl+F focuses search, right-click shows the app/clone menu, and F1 / Ctrl+,
open Help / Settings. Engine-only actions (Open, Refresh, Delete, Clean Caches, …)
still show honest “unavailable” dialogs because no executor exists; they never
pretend to succeed.

Manual visual verification (no automated UI tests in this environment): compare the
running window against `Assets/Screenshots/clone-light.png` / `clone-dark.png` and
`docs/WINDOWS-DESIGN-SPEC.md`. Known provisional items: font-size mapping, tile and
pill glyph choices (compile-safe `Symbol` enum), sidebar width 240, icon tiles are
initial letters (real badged icons arrive with the engine), minimum window size not
enforced, and no tray integration or signing yet. Screenshots, Narrator runs,
contrast themes, keypress observation, the crash dialog, clipboard delivery, and
File Explorer reveal remain pending (`docs/WINDOWS-UI-VERIFICATION.md`); installer
behavior has automated and development-machine evidence but no clean-VM run
(`docs/WINDOWS-INSTALL.md`).
