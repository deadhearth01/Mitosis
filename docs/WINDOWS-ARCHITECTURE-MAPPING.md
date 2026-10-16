# Windows architecture mapping (Stage 1)

How macOS responsibilities map to the `windows/` projects created in Stage 0.
No architecture changes are made in this stage; proposals are recorded as decisions
(D1–D7) for approval. Current `windows/` state: `Mitosis.sln` + `global.json` (.NET 10,
MSTest.Sdk 4.5.1) + 6 libraries + `Mitosis.Domain.Tests` (14 passing tests).
The only ported contract is `Mitosis.Domain.CloneName` (from `MitosisCore/Naming.swift`).

Reference graph (Stage 0): `Domain` ← `Application` ← `Infrastructure`;
`Domain` ← `Compatibility`; `Application` + `Compatibility` ← `CloneEngine`;
`Domain` ← `Windows.Platform` (`net10.0-windows10.0.19041.0`; rest `net10.0`).

Stage 3 additions (documented, minimal): `Windows.Platform` → `Application`
(discovery implementation behind the application port), `Compatibility` →
`Application` (assessor behind its port), `Windows.ViewModels` → `Application`
+ `Domain` (view models consume ports and domain models), `Windows.App` →
`Application`/`Compatibility`/`Domain`/`Windows.Platform` (manual composition
root only). No new third-party dependencies.

## 1. `Mitosis.Domain` — models, contracts, state

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisCore/Models.swift` (`CloneMode`, `SupportLevel`, `ModeDecision`, `Badge`, `CloneManifest`, `RegistryEntry`) | Port as records/enums; `Badge.normalizedColor` + presets move with it |
| `MitosisCore/Naming.swift` (`CloneName`, `Slug`) | `CloneName` done; `Slug.make` ports here (Stage 3+) |
| `MitosisCore/CloneMaintenance.swift` (`CloneStatus`, `AutoRefreshReport`) | Port state enums here |
| `MitosisCore/LaunchConfig.swift` (`Kind`, `LaunchConfig`, `{dataPath}` expansion) | Port contract here; execution stays platform-side |
| `MitosisCore/LinkRouter.swift` (`URLSchemes`, `LinkRouterState`, pure `targets`) | Port pure selection here or `Compatibility` (see D1) |
| `MitosisCore/StatsCollector.swift` (`CloneStats`, `CloneUsage`) | Port DTOs here |
| `MitosisCore/Profiles.swift` (`AppProfile`, `LaunchSettings`, allowlists) | Port schema here; bundled `profiles.json` ships as content |
| UI display names (`Full clone`, `Compatibility mode`, `Works great`…) | Reuse verbatim as constants/resources |
| Engine step labels + busy strings (`copying the app…`, `Refreshing…`…) | Reuse verbatim; owned by engine, surfaced by UI |

## 2. `Mitosis.Application` — use cases and orchestration

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisUI/AppModel.swift` (reload/status/auto-refresh/prompts/actions) | Orchestration use cases: reload library, refresh statuses, auto-refresh idle clones, quit-then-perform, delete/clean/refresh/restyle/relink flows |
| `MitosisUI/NewCloneModel.swift` + `NewCloneForm.swift` (wizard state, validation, suggestions) | Wizard state machine + validation rules (label suggestions, badge-follows-label, first-unused-color) |
| `MitosisUI/CloneLibrary.swift` (group/filter/sort/label-parse) | Port pure logic as-is (already covered by `CloneLibraryTests` semantics) |
| `MitosisUI/RunningTracker.swift` (launch/quit observation) | Running-state subscription; event source is platform-side |
| `MitosisUI/UpdateChecker.swift` + `Versioning.swift` (check/pick/install-orchestration) | Port check + semver pick; install execution is platform-side |
| `MitosisUI/AppServices.swift` (composition root) | Composition root for the Windows app (wires engine + profiles + environment) |
| `MitosisUI/Background.swift` (`offMain`) | Task-runner abstraction (CPU-bound engine work off the UI thread) |
| `MitosisUI/ErrorReport.swift` (sanitize + prefill) | Port with `%USERPROFILE%` redaction + same issue template |

## 3. `Mitosis.Infrastructure` — persistence, logging, filesystem abstractions

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisCore/CloneRegistry.swift` (JSON registry, self-heal, Trash helper) | Registry store (SQLite/EF Core from Stage 6; JSON shape preserved for migration); self-heal-from-manifest rule |
| `MitosisCore/MitosisEnvironment.swift` (locations) | Windows locations: clone root, per-clone data root, registry file (see D6) |
| `MitosisCore/FileCloner.swift` (APFS clone-or-copy) | Abstraction; Windows copy strategy behind it (see D5) |
| Logging | New: file/event logging for engine operations (no macOS equivalent beyond agent log) |

## 4. `Mitosis.Windows.Platform` — Windows APIs and integration

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisCore/Runtime.swift` (liveness, open/check/terminate, app resolution) | Process launch/liveness/terminate via Windows APIs; app resolution via Start Menu/registry |
| `LaunchStub/main.swift` (per-clone launcher: exec/open/spawn, pid files, dirs) | Windows launcher executable design (see D3) |
| `MitosisCore/Shell.swift` (safe process runner) | Safe process runner on `System.Diagnostics.Process` |
| `MitosisCore/Signer.swift` + `Entitlements.swift` (sign/verify) | Windows integrity story (see D5) |
| `MitosisRouter/main.swift` + `LinkRouterSetup.swift` (protocol routing, chooser host) | Protocol-handler registration/restore + chooser host window |
| `MitosisCore/AutoRefreshAgent.swift` (launchd agent) | Task Scheduler entry management (same 6 h + watch semantics) |
| `SelfUpdater.install` + `UpdateChecker.relaunch` | Replace/relaunch mechanics per packaging decision (see D7) |
| `MitosisUI/MenuBarIcon.swift`, `CommandLineTool.swift` (tray glyph, `~/.local/bin` link) | Tray icon asset + PATH registration (`%LOCALAPPDATA%\Microsoft\WindowsApps` or user bin) |

## 5. `Mitosis.Compatibility` — detection and decisions

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisCore/AppInspector.swift` (identity/frameworks/signature/sandbox) | Installed-app inspection: PE metadata, Store/MSIX detection, framework heuristics (Electron/Chromium/Squirrel/…) |
| `MitosisCore/AppCatalog.swift` (scan locations, exclusions, recency) | App discovery scan (Start Menu, registry Uninstall keys, well-known dirs) |
| `MitosisCore/ModeDecider.swift` (identity/fallback/unsupported + reasons) | Decision engine with user-facing reason strings (reuse macOS reasons where OS-neutral; add Windows reasons) |
| `MitosisCore/Profiles.swift` (profile lookup + allowlists) | Profile matching + launch-arg/env allowlists (same file format) |

## 6. `Mitosis.CloneEngine` — creation, launching, refresh, recovery

| macOS source | Responsibility → mapping |
|---|---|
| `MitosisCore/CloneBuilder.swift` (preflight/plan/identity+fallback build/install) | Build plans + verified create pipeline (Windows clone layout TBD, stages 4–5) |
| `MitosisCore/CloneWorkflow.swift` (verified create/refresh, rollback/restore) | Same guarantees: launch-check gate, failed-create removal, failed-refresh restore |
| `MitosisCore/CloneMaintenance.swift` (status/refresh/restyle/delete/relink) | Same operations against Windows primitives |
| `MitosisCore/HelperRenamer.swift`, `InfoPlistEditor.swift`, `IconRenderer.swift` | Windows equivalents: identity separation, manifest/config editing, badged-icon rendering (Stages 4–5) |
| `MitosisCore/StatsCollector.swift` | Disk/data/CPU/RAM accounting + cache cleaning via Windows APIs |

## 7. Later-stage hosts (not created yet)

| macOS source | Mapping |
|---|---|
| `MitosisUI` views + `MitosisApp/main.swift` | `Mitosis.Windows.App` (Stage 2 shell; WinUI 3 + XAML; MVVM per D2) |
| `mitosis/MitosisCLI.swift` (9 subcommands) | `Mitosis.Cli` console project (Stage 10; same surface) |
| `MitosisRouter` accessory app | Protocol-handler host inside the Windows app package (see D1) |
| `LaunchStub` binary | Per-clone launcher (see D3) |
| `MitosisSnapshot` dev tool | `Mitosis.Snapshot`-style review harness (Stage 2+, same shot list) |

## 8. Unresolved decisions (approval required; nothing implemented)

- **D1** — Link-router home: pure target selection → `Domain`/`Compatibility`; protocol
  registration + chooser host → `Windows.Platform`. Confirm split before Stage 6.
- **D2** — UI pattern for `Mitosis.Windows.App`: CommunityToolkit.Mvvm vs hand-rolled
  `INotifyPropertyChanged`. No dependency may be added until approved (Stage 2).
  **Resolved in Stage 2: hand-rolled** (`ObservableObject` + `RelayCommand` in the new
  `Mitosis.Windows.ViewModels` library, `net10.0`, no WinUI references so the MSTest
  suite can cover selection/filtering/states/placeholders). Revisit only with cause.
- **D3** — Per-clone launcher: dedicated stub `.exe` (mirrors `LaunchStub`) vs
  shortcut/symlink strategy. Affects clone layout (Stage 5).
- **D4** — CLI project shape: single `Mitosis.Cli` sharing `Application`/`CloneEngine`
  (Stage 10). No action now.
- **D5** — Copy + integrity strategy replacing APFS clonefile and ad-hoc codesign
  (block-level copy? VSS? MSIX modification packages? signature approach?). Spikes in
  Stages 4–5; matrix rows W47–W53 depend on it.
- **D6** — Windows data locations (clone root, per-clone `%APPDATA%` redirection,
  registry hive handling, machine vs user installs). Stage 4.
- **D7** — Distribution + self-update vehicle (MSIX sideload with auto-update, Store,
  or installer + in-app updater mirroring `SelfUpdater`). Stage 12+; blocks W53 detail.
- **D8** — Parity-matrix row W16 (All-Clones/Running sidebar rows) and stale `⌘I` help
  copy: drop, implement, or fix upstream on `main`? Recommend fixing the Help copy
  upstream and dropping both rows in v1; needs owner sign-off.
- **D9 (decided in Stage 3)** — Discovery/assessor placement: ports
  (`IAppDiscoveryService`, `ICompatibilityAssessor`) in `Mitosis.Application`,
  implementations in `Mitosis.Windows.Platform` / `Mitosis.Compatibility`, manual
  composition in `App.xaml.cs`. No container, no new dependencies.

## 9. Reusable contracts (no changes made)

`CloneName` (+ messages) · badge presets + `Badge` normalization · `Slug` rules ·
`CloneMode`/`SupportLevel` display names · engine step labels + busy strings · validation
and prompt copy catalogued in the audit · `profiles.json` schema · `mitosis.json` manifest
shape · `mitosis-launch.json` contract · Prefs keys (map to Windows app settings) ·
`Versioning` semver rules (port with `Mitosis.Application` update check).
