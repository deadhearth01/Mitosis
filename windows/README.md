# Mitosis for Windows — project foundation (Stage 0)

This folder holds the native Windows port of Mitosis. The macOS implementation in
`Sources/` (Swift/SwiftUI) is untouched and remains the authoritative Mac app.
See [docs/WINDOWS-PORT.md](../docs/WINDOWS-PORT.md) for the full port plan.

## Status

Stage 0 foundation only. The WinUI 3 application shell (`Mitosis.Windows.App`)
arrives in Stage 2; the clone engine, compatibility checks, process management
and persistence arrive in Stages 3–8. Nothing here clones, launches, modifies
or deletes any application.

## Prerequisites

| Tool | Required | Status on the Stage 0 machine |
|---|---|---|
| Git | any recent | 2.54.0 installed |
| .NET SDK | 10.0.100 or newer (LTS, `latestFeature` roll-forward per `global.json`) | **Missing** — runtime 9.0.18 only, no SDK. Install before building. |
| Windows App SDK (NuGet `Microsoft.WindowsAppSDK` 2.5.1) | needed from Stage 2 (WinUI 3 UI) | Not referenced yet |
| Visual Studio 2026 / VS Code + C# Dev Kit | recommended for XAML work | Not installed |
| Windows 10 SDK / MSVC | only for native Win32 interop (Stage 5+) | Not installed |

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
  tests/
    Mitosis.Domain.Tests/     MSTest suite for the domain layer
```

Only `Mitosis.Domain` carries code so far: `CloneName`, a direct port of the
macOS naming rules (`Sources/MitosisCore/Naming.swift`), covered by
`Mitosis.Domain.Tests`. The remaining libraries are structural placeholders that
reserve the assembly and namespace boundaries for later stages.

## Build and test (once the SDK is installed)

```powershell
cd windows   # dotnet commands run here so windows/global.json applies
dotnet build Mitosis.sln
dotnet test --solution Mitosis.sln
```
