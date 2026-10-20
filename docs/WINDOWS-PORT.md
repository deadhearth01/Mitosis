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
| App model | Unpackaged (framework-dependent; requires a machine Windows App SDK 2.x runtime, bootstrapped in `Program.Main`) | Min Windows 10 1809 per `TargetPlatformMinVersion` |
| Storage (Stages 6+) | SQLite with EF Core | versions pinned when introduced |
| Process / filesystem | Windows APIs + Win32 interop (`Mitosis.Windows.Platform`) | .NET 10 Windows targeting pack via `net10.0-windows10.0.19041.0` |
| Tests | MSTest (`MSTest.Sdk`, Microsoft Testing Platform) | `4.5.1` pinned in `windows/global.json` |

Tooling found on the Stage 0 machine (Windows 11 25H2, build 26200):

| Tool | Status |
|---|---|
| Git | 2.54.0 installed |
| .NET SDK | **Missing** — runtime 9.0.18 only, no SDK, so `dotnet build`/`dotnet test` cannot run yet |
| Visual Studio / VS Code C# Dev Kit | Not installed |
| WinUI 3 templates / Windows App SDK | No VS templates used; hand-authored SDK-style project + NuGet `Microsoft.WindowsAppSDK` 2.5.1 (built with `dotnet` CLI only) |
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
| `Mitosis.Windows.App` | Native WinUI 3 application and XAML UI | **Stage 2: main-window shell implemented** (sidebar, detail, empty state, banner; preview data only) |
| `Mitosis.Application` | Use cases and application orchestration | Project + reference graph only |
| `Mitosis.Domain` | Domain models, contracts, state definitions | `CloneName` (naming rules ported from `Sources/MitosisCore/Naming.swift`) + MSTest suite |
| `Mitosis.Infrastructure` | Persistence, logging, filesystem abstractions | Project + reference graph only |
| `Mitosis.Windows.Platform` | Windows-specific APIs and Win32 integration | Project + reference graph only (`net10.0-windows`) |
| `Mitosis.Windows.ViewModels` | MVVM presentation state for the shell (added Stage 2; hand-rolled, no toolkit — see D2) | View models + preview data + MSTest suite |
| `Mitosis.Compatibility` | Application detection and compatibility decisions | Project + reference graph only |
| `Mitosis.CloneEngine` | Clone creation, launching, refresh, recovery | Project + reference graph only |
| `Mitosis.Domain.Tests` / `Mitosis.ViewModels.Tests` | Unit tests for implemented behavior | `CloneNameTests` (14 tests) / `MainViewModelTests` (17 tests) |

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
- Stage 2: native Windows shell with visual parity — **implemented**: unpackaged
  WinUI 3 app (`Mitosis.Windows.App`, Windows App SDK 2.5.1, min Windows 10 1809)
  with the main-window shell on explicit preview data; 31/31 tests green; app
  launches with a real main window. Engine, wizard, Settings/Help/onboarding/tray
  remain for later stages.
- Stage 3: read-only discovery + compatibility assessment — **implemented**:
  uninstall-registration and packaged-app discovery with deterministic identity,
  merge, and ordering; rule-based assessor (`Unknown` by default, `Unsupported`
  for system components, empty strategy catalog by design); sidebar + app detail
  integration with loading/empty/error/retry and explicit preview fallback;
  71/71 tests green at the time; 180-app non-elevated smoke test clean. See
  `docs/WINDOWS-DISCOVERY.md`.
- Stage 4: independent clone profiles + safe persistence — **implemented**:
  `CloneProfile` metadata with explicit lifecycle (`Planned` default, never shown
  as verified), JSON store with atomic saves and corruption safety, per-app lists
  plus preserved orphans in the shell; 108/108 tests green at the time. See
  `docs/WINDOWS-PROFILES.md`.
- Stage 5: metadata-only New Clone wizard — **implemented**: five-state wizard
  (pick/details/review/saving/done/failed) with compatibility gating, name/badge
  validation, double-submit protection, and honest completion copy; 134/134 tests
  green at the time. No provisioning. See `docs/WINDOWS-PROFILES.md`.
- Stage 6: provisioning strategy framework — **implemented, execution disabled**:
  strategy contracts, deterministic planner with destination safety, execution
  gate, empty production catalog by evidence; 162/162 tests green at the time.
  No copying, launching, or deletion. See `docs/WINDOWS-PROVISIONING.md`.
- Stage 7: strategy research + availability UI — **implemented, catalog still empty**:
  vendor evidence reviewed (profile flags eligible for review; portable copies need
  review; Store/MSIX and generic copies rejected), research records separated from
  strategies by type, prerequisites shown on app detail; 171/171 tests green at the
  time. See `docs/WINDOWS-STRATEGY-RESEARCH.md`.
- Stage 8: evidence bundles + verification framework — **implemented, nothing
  verified live**: bundle schema with 10 isolation dimensions, deterministic
  validation/evaluation, atomic JSON store with integrity checks, app-detail
  verification summary; 205/205 tests green. See `docs/WINDOWS-EVIDENCE-BUNDLES.md`.
- Stage 9A: isolation verification planning — **plan only, no live experiments**:
  seven-phase protocol, ten-dimension matrix, disposable-environment spec,
  authorization/abort controls, report template. See
  `docs/WINDOWS-ISOLATION-VERIFICATION-PLAN.md`.
- Stage 10: security and release-readiness audit — **audit only, gates stay closed**:
  findings with bounded fixes (reserved device names, ISO evidence dates, verifier
  charset, swatch accessible names, deterministic retries), release-readiness
  assessment. See `docs/WINDOWS-SECURITY-RELEASE-AUDIT.md`.
- Stage 11: Windows CI — **automation only**: `windows-ci.yml` restores, Release
  builds, and tests on `windows-latest` with trx artifacts; 210/210 green locally.
  No remote run observed yet. See `docs/WINDOWS-CI.md`.
- Stage 12: UI parity and accessibility audit — **audit only**: evidence-based
  matrix statuses, manual verification protocol, bounded a11y fixes (decorative
  art hidden, swatch names, wizard focus, font cleanup); 210/210 green. No
  screenshots or Narrator runs available here. See
  `docs/WINDOWS-UI-VERIFICATION.md`.
- Stage 13: Settings, Help, and onboarding parity — **implemented, bounded, no
  pixels verified**. Help window (820×580, nine macOS topics with honest
  Windows copy, F1 + sidebar), Settings window (540 wide, General/Advanced;
  Storage reveal + Show Tips Again only), first-launch welcome flow (580×460,
  non-dismissable, 3 pages, Later / Choose an App) with versioned
  `preferences.json` persistence (atomic, corruption-safe, defaults mirroring
  macOS: onboarding once, tips on); 266/266 tests green (210 legacy + 56 new).
  Deliberately absent because no Windows component can honor them yet:
  menu-bar/tray toggle, update checks + Check Now, auto-update clones, the
  command-line installer, and the default-clone-mode picker. Visual runtime
  comparison and Narrator pass pending (S10–S13 in
  `docs/WINDOWS-UI-VERIFICATION.md`). See `docs/UI-PARITY-MATRIX.md` (Stage 13
  notes).
- Stage 14: window lifecycle, navigation, and keyboard parity — **implemented,
  bounded, no pixels verified**. Native minimum-size floors matching the macOS
  reference (main 820×520, Help 700×500), Settings width locked to the macOS
  540, onboarding/wizard windows fixed-size non-maximizable like macOS sheets;
  secondary-window close returns focus to the main window; key routing
  extracted to unit-tested `MainWindowShortcuts` (F1/Ctrl+,/Ctrl+F preserved,
  Ctrl+N deliberately not double-routed). Window position/size **restoration is
  deferred**: the macOS reference contains no restoration code, so persistence
  would be unverified behavior on both sides — revisit only after confirming
  the macOS behavior on a Mac, with work-area validation and debounced writes.
  Tray/menu-bar **deferred** (see below); 278/278 tests green (56 + 12 new
  on top of 210 legacy). See `docs/UI-PARITY-MATRIX.md` (Stage 14 notes).

### Stage 14 tray and menu decision

The macOS menu-bar extra (`MenuBarExtra`, `MenuBarContent.swift`,
`Prefs.menuBarKey`, off by default) carries three behaviors the Windows port
has consciously deferred with W04/W42 still Missing:

1. **Close-to-tray lifetime** — macOS keeps Mitosis running when the last
   window closes because `applicationShouldTerminateAfterLastWindowClosed`
   returns `!menuBarKey` (MitosisRootApp.swift:75-77). The Windows app quits on
   window close, matching the macOS default (menu bar off). A tray lifetime on
   Windows needs application-lifetime rework: `Bootstrap.Shutdown()` on
   `Window.Closed` must become conditional, plus WM messaging for
   reopen/shutdown and a documented single-instance policy.
2. **Win32 tray icon** — WinUI 3 has no first-party tray affordance; a real
   implementation means `Shell_NotifyIcon` interop, a custom context flyout
   (`XamlRoot` hosting across two islands), per-DPI icon variants, and the
   "show in the taskbar area" Rules (taskbar button behavior is coupled to the
   AppWindow, not the tray). A third-party NuGet (e.g. H.NotifyIcon) would add
   a new runtime dependency for a feature that is off by default on macOS.
3. **Menu contents** — the macOS menu opens any clone quickly and shows the
   update action; neither has a Windows effect yet (no running clones, no
   update engine). Shipping tray buttons that call honest no-ops would repeat
   the anti-pattern Stage 13 removed from Settings.

Prerequisites to revisit (not this stage): tray lifetime architecture, a
tray icon design (spec §2 notes a redrawn 18 px monochrome template), the
notification/reopen flow, and a user preference key with the same default-off
parity. Until then: no fake tray commands; quit-on-close stays.
- Stage 15: main-window interaction parity — **implemented, bounded scope; no
  desktop session available here, so interaction checks are pending
  (S19–S21)**. Corrections against the macOS reference: a search filter no
  longer clears the selection/detail page (macOS clears only when the entry
  leaves the model, `AppModel.swift:112` — implemented via OneWay list
  selection, existence-based clearing, and re-announcement on refresh); the
  no-results view is gated on an active query and echoes the typed text
  (macOS `ContentUnavailableView.search` semantics), so an empty library
  never claims "no matching clones"; Esc clears the search query like macOS
  `.searchable`. The discovery empty/loading/error states, context menu
  (10 items, availability rules), compatibility explanations, and preview-vs-
  production separation were audited and stand unchanged. Deferred: auto-select
  first entry (needs the real clone list), All/Running rows (W16, macOS spec
  drift), column-collapse interaction (needs clone counts). 283/283 tests
  green (278 + 5 new + 1 corrected to pin the macOS evidence). See
  `docs/UI-PARITY-MATRIX.md` (Stage 15 notes).
- Stage 16: keyboard navigation, focus management, and accessibility parity —
  **implemented, bounded; no desktop session available, so keypress/screen-
  reader checks are pending (S22–S24)**. Four source-verifiable defects fixed:
  the update banner's close button now actually dismisses the banner (macOS
  `updates.dismissed`, `MainWindow.swift:94-103`; the tested
  `DismissBannerCommand` was wired to the InfoBar); dynamic error/status text
  gained `AutomationProperties.LiveSetting="Polite"` (wizard validation error,
  unsupported-app reason, failed-step message, settings/welcome error bars);
  the wizard's eight color swatches expose their selection state in
  automation (macOS `.isSelected` trait, `BadgeEditing.swift:43`), which was
  previously ring-only; Help, Settings, and the welcome window now set
  initial keyboard focus on open, matching the wizard's Stage 12 pattern
  (AppKit sheets get a first responder; WinUI has none by default). No new
  features, no styling changes, no new dependencies; 283/283 tests green
  (no new unit tests — every change is declarative XAML or view wiring, and
  the banner logic was already tested). See `docs/UI-PARITY-MATRIX.md`
  (Stage 16 notes).
- Stage 17: release packaging and deployment audit — **audited and instrumented;
  the app is not release-ready** (the clone engine remains unimplemented, no
  clean-machine run has been performed). The audited deployment model is
  unpackaged + framework-dependent + x64 (csproj evidence in
  `docs/WINDOWS-RELEASE.md`), published to a reproducible, SHA-anchored zipped
  release candidate (`windows/artifacts/Mitosis-windows-x64-<sha>.zip`,
  ≈39 MB) via the same pinned recipe CI executes: `publish-release-candidate`
  in `windows-ci.yml` runs after tests and uploads the artifact with 14-day
  retention (nothing is published externally or attached to a release page).
  Startup diagnostics for missing prerequisites were made path-free and
  testable: `Bootstrap.Initialize` failure now surfaces as a Win32 MessageBox
  naming the component and HRESULT (exit 101) instead of the .NET
  unhandled-exception dialog whose stack trace leaked local install paths.
  Release checklist, prerequisite table, reproducibility findings (identical
  file sets + stable SHA-256 hashes across back-to-back publishes), and
  outstanding blockers (no icon, no signing, placeholder version, engine
  unimplemented, all Windows versions other than 11 25H2 untested) live in
  `docs/WINDOWS-RELEASE.md`. 287/287 tests green (283 + 4 new
  `StartupDiagnosticsTests`). macOS sources and the macOS `release.yml`
  workflow untouched.
- Stage 18: version metadata and application identity — **implemented,
  verified on published output**. The Windows port now has one documented
  version source: `WindowsPortVersion` in `windows/Directory.Build.props`
  (SemVer MAJOR.MINOR.PATCH, currently mirroring the product's `v0.2.2` tag
  line), flowing to `AssemblyVersion`/`FileVersion` (`0.2.2.0`) and
  `InformationalVersion`/`ProductVersion` (`0.2.2+<git-sha>`); an msbuild
  target rejects malformed `-p:WindowsPortVersion` overrides (verified once
  with a deliberately bad value), a repo copy without `.git` builds with
  `Product '0.2.2'` and no fabricated hash, and the publish script reads the
  version back from the built exe (`FileVersionInfo`) so the archive name —
  now `Mitosis-windows-<version>-x64-<sha>` (nogit fallback) — can never
  silently disagree with the metadata it packages. Application identity: the
  app project's `AssemblyName` is `Mitosis` (distributed `Mitosis.exe`), and
  the exe embeds `app.ico` derived read-only from the product's icon artwork
  (`Assets/Brand/AppIcon.icon/Assets/mito.png`, 16–256 px, all-BMP entries,
  generated by `windows/scripts/make-icon.ps1`); per `WINDOWS-PORT.md`
  section 8 the port is Mitosis itself, so reusing the product artwork is
  established permission, no macOS asset was modified, and no publisher or
  signing identity is invented. 291/291 tests green (+4
  `WindowsReleaseIdentityTests`). Open items: purpose-drawn 16/24 px icon
  sizes, a shipping version stamp decision, code signing. See
  `docs/WINDOWS-RELEASE.md` (Stage 18 sections).
- Stage 19: published-artifact integrity and release verification —
  **implemented, verified end-to-end on the real artifact**. The publish script
  was decoupled from trust: it now deletes previous `Mitosis-windows-*.zip`s
  before packaging (a stale RC cannot be uploaded by mistake), fixes a latent
  denylist bug (`.json.user` never matched because `Extension` reports only the
  last dot; `.user` now matches all editor-state files), and writes a
  `.sha256` sidecar next to the archive. A new, independent verifier —
  `windows/tools/ReleaseVerifier`, a plain .NET console project in the solution
  that is also the CI step's executable — takes a ZIP path and judges the
  artifact itself: it recomputes and prints the SHA-256, compares the sidecar,
  opens and fully enumerates the archive, rejects traversal and duplicate
  entries and any source/project/git-metadata content, extracts into a fresh
  temp directory, re-checks the required runtime files and the 13 pose assets
  inside the *extracted* tree, reads the exe's COFF header (x64 enforced) and
  PE resource directory (application icon enforced), and checks that
  `FileVersion`/`ProductVersion` agree with the archive name including the
  nogit-vs-hash rule. CI runs this same command between packaging and upload in
  the `publish-release-candidate` job, so a failing verification now blocks the
  artifact upload, and the uploaded CI artifact includes the `.sha256`
  sidecar. 16 failure-case fixture tests (`ReleaseVerifierTests`, synthetic
  hand-built PE images and fixture zips — consistent, missing file, missing
  pose, source entry, git metadata, traversal, corrupt zip, sidecar mismatch,
  missing sidecar, bad name, version mismatch, revision mismatch, nogit
  violation, ARM64, icon-free exe, non-exe) took the suite to 307/307 green;
  the real pipeline was run end-to-end
  (`Mitosis-windows-0.2.2-x64-2308fd9.zip`, SHA-256
  `e710f35e41c8e64cf8eb81b1dc8f3ee23634c010d0c77581ed8e1431f7028c86`,
  "verified") plus negative demos (tampered bytes, wrong sidecar, injected
  `Assets/Rogue.cs`) that all exited nonzero with a single precise finding.
  Archive bytes remain non-reproducible (Compress-Archive timestamps); the
  sidecar pins the hash per artifact. Verification remains structural only —
  it provides no clean-machine launch, OS-compatibility, or signing evidence.
  See `docs/WINDOWS-RELEASE.md` (Stage 19 section).
- Stage 20: security and robustness audit of the release verifier and packaging
  pipeline — **implemented, all findings evidence-based, verified end-to-end**.
  The verifier's inputs (ZIP bytes, PE headers, sidecar) are now treated as
  hostile: every PE read is bounds-checked in long arithmetic and malformed
  structures (truncated optional header, unresolvable resource RVA, huge
  section count, bad magic, near-int-max `e_lfanew`) surface as findings
  instead of uncontrolled crashes; explicit resource limits
  (`VerificationLimits`: 512 MB archive/expanded total, 256 MB per entry, 1000
  entries, 1 MB sidecar — sized ~13× the real artifact with a documented
  rejection message) guard hashing, enumeration, and metered extraction
  (`FileMode.CreateNew` targets, no overwrite, no reliance on archive file
  attributes); the `.sha256` sidecar is strictly two-token bound (`<hash>`
  followed by the archive's own file name) with capped, failure-tolerant
  reads; and Windows filesystem equivalence rules are enforced — trailing
  dots/spaces (NTFS name stripping), NTFS alternate-data-stream colons, `.`
  -segment duplicate spellings, absolute/drive-qualified entry names, and
  backslash-style directory entries are all rejected before extraction.
  Scratch extraction cleanup is guaranteed on success, meter-abort, and
  failure paths. Audit findings included a discovered observation that the
  .NET ZIP reader truncates decompression at the declared entry size (now
  pinned by a regression test as the primary bomb guard, with the metered
  copy as backstop). 22 adversarial fixture tests took the verifier class to
  38 and the suite to 329/329 green; the real pipeline re-ran end-to-end plus
  tamper/negative demos. The packaging script and CI workflow needed no
  changes (cleanup already fail-closed; CI already least-privilege with
  verify-before-upload ordering). Limits stay structural: no clean-machine
  launch, OS-compatibility, or signing evidence is claimed by passing
  verification. See `docs/WINDOWS-RELEASE.md` (Stage 20 section).
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
