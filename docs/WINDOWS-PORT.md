# Mitosis for Windows — port plan and foundation record

Stage 0 document. The macOS application is the product reference; this file records
what the Windows port must preserve, the stack it will use, and the staged plan.
It is updated as stages land. UI parity is **not** claimed in Stage 0.

## 1. Product objective

Mitosis for Windows is the Windows sibling of the existing macOS application, not a
redesign and not a separate product. It gives Windows users the same capability:
run separate copies of an app side by side, one login per copy, one taskbar entry
per copy, with logins and data kept apart, guided creation, health verification,
stats, safe refresh after the original updates, and delete-to-Recycle-Bin semantics.

## 2. UI parity requirement

The macOS application is the design and behavioral reference for the Windows
implementation. The Windows app must preserve, wherever practical on Windows:

- Overall layout and navigation (sidebar + detail, windows, hierarchy).
- Sidebar structure and proportions; application cards and clone lists.
- Clone detail pages, status indicators, and usage stats.
- New Clone workflows, dialogs, and validation behavior.
- Settings, menus, contextual actions, help, onboarding, and empty/error states.
- Typography, spacing, colors, visual language, icons, illustrations and branding.
- Animations, transitions, and product terminology.

Windows-specific adaptations are permitted only where the OS or native usability
conventions require them (window chrome, taskbar vs Dock, file dialogs, packaging).
The same design language and workflows are kept everywhere else.

Reference implementation (Stage 0 audit, `Sources/MitosisUI/`):

| Area | macOS source |
|---|---|
| App entry, windows, settings, menu-bar extra, commands | `MitosisRootApp.swift` |
| Main window, split view, toolbar, sheets, onboarding | `Views/MainWindow.swift` |
| Clone list / sidebar | `Views/CloneList.swift`, `CloneLibrary.swift` |
| Clone detail page | `Views/CloneDetail.swift` |
| New Clone flow | `Views/NewClone/` (`AppPickerView`, `NewCloneSheet`, `QuickSheetView`, `WorkingViews`), `NewCloneForm.swift`, `NewCloneModel.swift` |
| Settings, help, menu-bar content, update prompts | `Views/SettingsView.swift`, `Views/HelpView.swift`, `Views/MenuBarContent.swift`, `Views/UpdateActions.swift` |
| Badges, shared components, empty states | `Views/BadgeEditing.swift`, `Views/Components.swift`, `Views/EmptyState.swift` |
| Brand color/type, app model, services, errors | `Brand.swift`, `AppModel.swift`, `AppServices.swift`, `ErrorReport.swift` |
| Artwork (mascots used across states) | `Resources/Mascot/`, `Assets/Brand/`, `Assets/Screenshots/` |

Observed anchor values, to be formalized by the Stage 1 audit (not a spec):
main window default 1060×720 (min 820×520), help window 820×580, sidebar
column 210/240/320, clone display names of the form `App (Label)`.

Stage 1 audits the macOS UI and writes the detailed parity specification.
Stage 2 implements the native Windows shell against that specification.

## 3. Technology stack

| Layer | Choice | Version / reference |
|---|---|---|
| Language / runtime | C# on .NET 10 (LTS, supported to 2028-11-14) | SDK `10.0.100`+, `latestFeature` roll-forward (`windows/global.json`) |
| UI | WinUI 3 + XAML via the Windows App SDK (stable channel) | NuGet `Microsoft.WindowsAppSDK` **2.5.1** (2026-09-16); referenced from Stage 2 |
| App model | Packaged MSIX, single-project packaging (Stage 2) | — |
| Storage (Stages 6+) | SQLite with EF Core | versions pinned when introduced |
| Process / filesystem | Windows APIs + Win32 interop (`Mitosis.Windows.Platform`) | .NET 10 Windows targeting pack via `net10.0-windows10.0.19041.0` |
| Tests | MSTest (`MSTest.Sdk`, Microsoft Testing Platform) | `4.5.1` pinned in `windows/global.json` |

Tooling found on the Stage 0 machine (Windows 11 25H2, build 26200):

| Tool | Status |
|---|---|
| Git | 2.54.0 installed |
| .NET SDK | **Missing** — runtime 9.0.18 only, no SDK, so `dotnet build`/`dotnet test` cannot run yet |
| Visual Studio / VS Code C# Dev Kit | Not installed |
| WinUI 3 templates / Windows App SDK | Not installed (NuGet restore only, from Stage 2) |
| Windows 10/11 SDK, MSVC, NuGet CLI | Not installed (needed only for native interop/packaging work) |
| winget | Available (no installs performed; needs explicit authorization) |

Because the SDK is missing, Stage 0 validates the foundation structurally
(well-formed project files, coherent references, complete diff review) and records
`dotnet build` / `dotnet test` as **blocked** until the .NET 10 SDK is installed.
No system-wide installation was performed.

## 4. Architecture boundaries

All Windows code lives under `windows/`; the Swift tree is never moved or renamed.

| Project | Responsibility | Stage 0 state |
|---|---|---|
| `Mitosis.Windows.App` | Native WinUI 3 application and XAML UI | Not created; lands in Stage 2 against the parity spec |
| `Mitosis.Application` | Use cases and application orchestration | Project + reference graph only |
| `Mitosis.Domain` | Domain models, contracts, state definitions | `CloneName` (naming rules ported from `Sources/MitosisCore/Naming.swift`) + MSTest suite |
| `Mitosis.Infrastructure` | Persistence, logging, filesystem abstractions | Project + reference graph only |
| `Mitosis.Windows.Platform` | Windows-specific APIs and Win32 integration | Project + reference graph only (`net10.0-windows`) |
| `Mitosis.Compatibility` | Application detection and compatibility decisions | Project + reference graph only |
| `Mitosis.CloneEngine` | Clone creation, launching, refresh, recovery | Project + reference graph only |
| `Mitosis.Domain.Tests` | Unit tests for implemented domain behavior | `CloneNameTests` (13 tests) |

Reference graph: `Domain` ← `Application` ← `Infrastructure`;
`Domain` ← `Compatibility`; `Application` + `Compatibility` ← `CloneEngine`;
`Domain` ← `Windows.Platform`. No third-party dependencies yet; MSTest is the only
external package and is test-only.

## 5. Cross-platform preservation

- The Swift implementation (`Sources/`, `Tests/`, `Package.swift`) remains the
  authoritative macOS implementation. It is not rewritten, moved, renamed or broken
  by the Windows port.
- Brand assets (`Assets/Brand/`, mascot artwork, screenshots) stay in place; the
  Windows UI reuses them from their current paths in later stages.
- Mac release configuration is untouched: `scripts/build-app.sh`,
  `scripts/package-release.sh`, `scripts/install.sh`, `.github/workflows/ci.yml`
  (macOS runners, `swift build`/`swift test`), `.github/workflows/release.yml`.
- The root `README.md` keeps its Mac installation instructions; Windows
  documentation lives in `windows/README.md` and this file.
- Stage 0 changes are limited to `windows/`, `docs/WINDOWS-PORT.md`, and
  `.gitignore` additions for .NET build outputs.

## 6. Known engineering risks

- **Application compatibility.** Windows apps vary wildly (installers, services,
  drivers, Store/MSIX packages). The compatibility engine must classify what can
  be cloned, what works with limits, and what is unsupported — mirroring the
  Mac `ModeDecider`/`Profiles` approach with Windows-specific detection.
- **Independent writable profile directories.** Each clone needs isolated
  per-user data (`%APPDATA%`/`%LOCALAPPDATA`/registry hives). Redirection must be
  complete yet invisible to the app.
- **Single-instance enforcement.** Apps using mutexes, named pipes or lock files
  to prevent multiple copies need per-clone identity for those primitives.
- **Registry and shared-state dependencies.** HKCU/HKLM keys, COM registration,
  shell extensions and protocol handlers shared across copies must be separated
  or virtualized without breaking the original.
- **Application update detection.** Originals update via installers, Store, or
  in-app updaters. The port must detect updates and rebuild clones in the
  background, as the Mac `AutoRefreshAgent` does with app bundles.
- **Process lifecycle management.** Launch verification ("a clone is ready only
  after it actually starts"), shutdown ordering, and handling open clones during
  refresh need Windows job objects / process APIs.
- **Safe refresh and rollback.** Failed rebuilds must restore the previous working
  clone automatically; partial copies must never be left in place.
- **Filesystem permissions and path validation.** ACLs, long paths, OneDrive/redirected
  folders, and per-user vs per-machine installs all affect where clones can live.
- **Windows packaging and distribution.** MSIX identity, signing (no paid Apple
  account equivalent problem, but SmartScreen/EV reputation), Store vs sideload,
  and keeping an installer/update story as smooth as `scripts/install.sh`.

## 7. Staged development

- Stage 0: repository audit and Windows foundation (this stage).
- Stage 1: macOS UI audit and feature-parity specification — see
  `docs/MACOS-UI-AUDIT.md`, `docs/UI-PARITY-MATRIX.md`, `docs/WINDOWS-DESIGN-SPEC.md`,
  and `docs/WINDOWS-ARCHITECTURE-MAPPING.md`.
- Stage 2: native Windows shell with visual parity.
- Stages 3–8: compatibility, cloning, process management, verification and recovery.
- Stages 9–11: UI parity completion, CLI, integration and quality hardening.
- Stages 12–15: security, CI/CD, release testing and public release.

## 8. License and redistribution

Mitosis is free and source-available under the **PolyForm Noncommercial License
1.0.0** (`LICENSE`), with an additional permission allowing anyone to run
unmodified releases for any purpose, including at work. It may not be sold, and
its source may not be used in commercial or paid products.

The Mitosis name, app icon, and Mito mascot are **not** covered by the license:
forks must use a different name and icon. The Windows port is developed in this
repository as Mitosis itself, so it keeps the product identity; any future
separate distribution must respect the same trademark restriction. No assumptions
about commercial rights are made here — see `LICENSE` and the maintainer.

## 9. Stage 0 record

- Baseline: `main` at `fee3a77`, clean tree; work branch `WindowsVersion`.
- Added `windows/` solution foundation (6 libraries, 1 MSTest project, 13 tests),
  `docs/WINDOWS-PORT.md` (this file), `.gitignore` entries for .NET outputs.
- macOS Swift sources and release configuration unchanged (verified by diff).
- Build/test blocked on the missing .NET 10 SDK; recorded in `windows/README.md`.
