# Mitosis for Windows — project foundation (Stage 0)

This folder holds the native Windows port of Mitosis. The macOS implementation in
`Sources/` (Swift/SwiftUI) is untouched and remains the authoritative Mac app.
See [docs/WINDOWS-PORT.md](../docs/WINDOWS-PORT.md) for the full port plan.

## Status

Stage 3 shell + read-only discovery. `Mitosis.Windows.App` is a real, launchable
WinUI 3 application: the sidebar lists genuinely discovered applications
(read-only Windows registrations) with per-app compatibility verdicts, plus the
clone detail, empty state, and update banner from Stage 2. Sample data survives
only behind the explicit `Show preview instead` fallback and is never mixed with
real results. The New Clone wizard, Settings, Help, onboarding, tray menu, and
the clone engine arrive in later stages. Nothing here clones, launches, modifies,
or deletes any application.

## Prerequisites

| Tool | Required | Status on the Stage 0 machine |
|---|---|---|
| Git | any recent | 2.54.0 installed |
| .NET SDK | 10.0.100 or newer (LTS, `latestFeature` roll-forward per `global.json`) | 10.0.402 installed |
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
  Mitosis.sln                 solution (src + tests; the WinUI app joins in Stage 2)
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
  tests/
    Mitosis.Domain.Tests/     MSTest suite for the domain layer
    Mitosis.ViewModels.Tests/ MSTest suite for shell view models (selection, filtering, states, placeholders)
```

Only `Mitosis.Domain` carries code so far: `CloneName`, a direct port of the
macOS naming rules (`Sources/MitosisCore/Naming.swift`), covered by
`Mitosis.Domain.Tests`. The remaining libraries are structural placeholders that
reserve the assembly and namespace boundaries for later stages.

## Build and test

```powershell
cd windows   # dotnet commands run here so windows/global.json applies
dotnet restore Mitosis.sln
dotnet build Mitosis.sln --no-restore
dotnet test --solution Mitosis.sln --no-build
```

## Run the app

Unpackaged: build first, then run the exe (needs a Windows App SDK 2.x runtime;
the app fails fast with a clear message when it is missing):

```powershell
.\src\Mitosis.Windows.App\bin\x64\Debug\net10.0-windows10.0.19041.0\Mitosis.Windows.App.exe
```

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
under an `Unavailable` section and rejoin automatically. Nothing is provisioned,
launched, or deleted. Details: `docs/WINDOWS-PROFILES.md`.

## New Clone wizard (Stage 5, metadata only)

Ctrl+N (or New Clone) opens the wizard: pick an application, enter details with
live badge preview, review, then save. Saving writes one `Planned` profile record
— the app is not copied or launched. `Unsupported` apps are blocked with reasons;
review-required apps need explicit acknowledgement. The review step also states
provisioning availability: profile flags are under research, nothing is validated
yet (see `docs/WINDOWS-STRATEGY-RESEARCH.md`). Unsupported visual states and
manual screenshot comparison steps are listed in `docs/UI-PARITY-MATRIX.md`.

## Evidence bundles (Stage 8, framework only)

Isolation evidence records live in `%LOCALAPPDATA%\Mitosis\Evidence` (versioned JSON,
atomic saves, integrity-checked). Recording what must be proven is implemented;
proving it requires separately authorized live testing. App detail shows a
verification summary per application. Details: `docs/WINDOWS-EVIDENCE-BUNDLES.md`.

## Continuous integration (Stage 11)

Pushes to `WindowsVersion` and pull requests run `.github/workflows/windows-ci.yml`
(`windows-latest`): restore, Release build, full test suite with `.trx` artifacts
(14 days). Reproduce locally with the commands in `docs/WINDOWS-CI.md`. CI covers
compilation and unit tests only — not UI launch, pixels, Narrator, isolation,
licensing, or packaging.

What you should see: the Mitosis main window (1060×720) with discovered
applications grouped by initial letter, an update banner, and the empty state when
nothing is selected. Selecting an app shows its detail page with the compatibility
verdict; selecting nothing (or a failed discovery without preview) shows the empty
state. Every engine action (New Clone, Open, Refresh, Delete, …) shows a dialog stating it
is unavailable in the preview. Search filters live; Enter opens (placeholder),
Delete requests delete (placeholder), Ctrl+N starts New Clone (placeholder),
Ctrl+F focuses search, right-click shows the clone menu.

Manual visual verification (no automated UI tests in this environment): compare the
running window against `Assets/Screenshots/clone-light.png` / `clone-dark.png` and
`docs/WINDOWS-DESIGN-SPEC.md`. Known provisional items: font-size mapping, tile and
pill glyph choices (compile-safe `Symbol` enum), sidebar width 240, icon tiles are
initial letters (real badged icons arrive with the engine), no Settings/Help/
onboarding/tray yet, minimum window size not enforced.
