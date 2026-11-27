# Mitosis for Windows — autonomous roadmap

Living plan for the remaining Windows port. This document is updated after every
stage. It is deliberately evidence-first: a subsystem is only "complete" when code,
automated tests, and the stated verification level agree.

- **Branch:** `WindowsVersion`
- **Baseline at creation:** `6f8e3de` (merge of `origin/main` `v0.2.3` into
  `WindowsVersion`; the merge changed no Windows source)
- **Automated suite at creation:** 329/329 green (Release, .NET SDK 10.0.404)
- **Current suite:** 497/497 green (Release, .NET SDK 10.0.404; 0 warnings)
- **Release-candidate pipeline:** present and verified structurally (Stages 17–20)
- **Clone engine:** framework only; production strategy catalog empty; execution
  gates closed
- **Last updated:** 2026-11-27

> **Product truth.** The UI shell is largely built. The product is not: nothing on
> Windows can create, launch, refresh, repair, or delete a real clone. No amount of
> UI polish changes that. The clone engine and its isolation evidence remain the
> largest blocker and require explicit authorization before implementation or
> execution.

## 0. Status vocabulary

| Status | Meaning |
|---|---|
| **Complete and verified** | Implemented; automated tests and the stated runtime/artifact checks pass. |
| **Implemented, verification pending** | Built and launchable/tested at the logic level; pixels, Narrator, or desktop interaction not yet observed. |
| **Partially implemented** | A real, honest subset works; the missing part is named. |
| **Planned** | Designed here; no implementation yet. |
| **Blocked** | Cannot proceed without a dependency, environment, or authorization. |
| **Requires explicit authorization** | Touches third-party apps/data, system state, signing, or public distribution. |

"Verified" never means "a filename suggests it works." Each claim below cites the
source, test, or artifact that establishes it.

## 1. Current project status

### 1.1 Foundation and data layer

| Subsystem | Status | Evidence |
|---|---|---|
| Solution/project boundaries (.NET 10, 9 src projects, 8 test projects, verifier tool) | Complete and verified | `windows/Mitosis.sln`; Release build (0 warnings); 514 tests |
| Clone-name rules | Complete and verified | `Mitosis.Domain/CloneName.cs`; `CloneNameTests` (10) |
| Clone profile model and lifecycle transitions | Complete and verified | `Mitosis.Domain/CloneProfile.cs`; `CloneProfileTests` (9) |
| Profile JSON store (atomic, corruption-safe, duplicate rejection) | Complete and verified | `JsonProfileRepository.cs`; `JsonProfileRepositoryTests` (19) |
| Preferences JSON store | Complete and verified | `JsonAppPreferencesStore.cs`; 17 tests |
| Evidence bundle model/store/evaluation | Complete and verified (framework only) | `Evidence.cs`, `EvidenceEvaluation.cs`, `JsonEvidenceStore.cs`; 40 tests |
| Read-only app discovery (registry + packaged apps) | Complete and verified | `Windows.Platform/Discovery`; 14 ViewModel tests + platform tests; 180-app smoke test recorded in Stage 3; duplicate registrations of one app merge on the publisher's common name (Edge appeared twice until 2026-11-27 — one row now, `df998f7`) |
| Compatibility assessor | Complete and verified (rule-based, conservative) | `Mitosis.Compatibility`; 8 tests |
| Provisioning planner/gate framework | Complete and verified (planning only) | `Planner.cs`, `Gate.cs`; 27 planning tests |
| Strategy research catalog | Complete and verified (research records only) | `ResearchCatalog.cs`; 8 tests |
| Production provisioning catalog | **Empty by design** | `Provisioning/Gate.cs:65`; no strategy registered |

### 1.2 Application shell and UI

| Subsystem | Status | Evidence / missing part |
|---|---|---|
| Main window, sidebar, search, grouping, selection | Verified for observed states; Narrator pending | Real discovery; Stage 15 interaction tests; Stage 27 desktop evidence plus the 2026-11-27 modern-chrome captures: unified title bar, sidebar and content surfaces, search match/no-match/Esc, selection preserved, 820x520-1600x900 |
| App detail (metadata, compatibility, evidence, profiles) | Verified for observed states; Narrator pending | `AppDetailView.xaml`; header, Details, Compatibility, and Profiles cards with the application's own icon; empty/with-profile/removal states observed and screenshotted; layout clipping fixed (Stage 27); blank reason/evidence bullets fixed 2026-11-27 |
| Clone detail page | Partially implemented | Only reachable through explicit preview data; all engine actions are honest placeholders |
| New Clone wizard | Partially implemented | Metadata-only save works; app-detail entry opens it preselected (Stage 21); macOS picker segments/mode picker/launch check deferred |
| Badge editing | Partially implemented | Wizard inline editor plus `Planned`-profile re-badging (Stage 21); provisioned profiles still read-only |
| Profile lifecycle management | Implemented, verification pending (metadata only) | Rename, re-badge, and remove for `Planned` records on app and orphan detail; non-`Planned` refused; dialog/Narrator checks pending |
| Settings | Partially implemented | General/Advanced subset; manual GitHub release check added (Stage 25); tray, clone auto-update, default mode, CLI installer deliberately absent |
| Help | Implemented, verification pending | Nine topics, honest Windows copy |
| Onboarding | Verified for observed states | Three pages, Next/Back/Later, fixed 580×460, focus on Next (Stage 27 screenshots); column defect fixed; Narrator pending |
| Window lifecycle and keyboard routing | Verified for observed scenarios | Fixed wizard/Settings sizes, Help 700×500 floor, first focus in all secondary windows, F1/Ctrl+,/Ctrl+F/Ctrl+N routing, focus return (Stage 27); Narrator/contrast pending |
| Tray / menu-bar equivalent | Deferred by decision | `docs/WINDOWS-PORT.md` § Stage 14 |
| Window position/size restoration | Deferred by decision | No macOS restoration evidence to mirror |
| Accessibility | Partially implemented; names/roles inspected | Sidebar row names, swatch states, and control names checked with UI Automation (Stage 27); 150% and 200% text scaling reflow without clipping (2026-11-27); high contrast keeps the system selection colour and system text but the app surfaces stayed on the light theme values (recorded defect); Narrator speech not run |
| Visual parity | Verified for captured states in both themes; macOS pixel diff pending | 57 screenshots in `docs/evidence/windows-ui/`; modern chrome, cards, and real application icons captured 2026-11-27; no side-by-side macOS comparison yet |

### 1.3 Clone engine and Windows integration

| Subsystem | Status | Evidence / missing part |
|---|---|---|
| Real cloning/provisioning | **Blocked / requires authorization** | No executor; catalog empty; gates closed |
| Application data separation | **Blocked / requires authorization** | Research only; no live evidence |
| Registry/filesystem isolation | **Blocked / requires authorization** | Research only; no live evidence |
| Process launch and liveness verification | **Blocked / requires authorization** | No implementation |
| Refresh / rollback / restore | **Blocked** | Depends on engine |
| Delete / clean caches / relink | **Blocked** | Clone deletion needs the engine; metadata-record removal is implemented for `Planned` profiles only |
| Live stats (disk/data/memory/CPU) | **Missing** | Static placeholder tiles in preview; no collector |
| Link routing / protocol handler | Partially implemented (UI only) | Toggle holds state; no interception |
| Background refresh agent | **Missing** | Depends on engine |
| CLI (`mitosis.exe`) | Implemented and runtime-verified | Separate read-only console project: `version`, `list`, `profiles`, `doctor` (+`--json`); no clone/launch/delete commands | Combined release-archive packaging and PATH install |
| Single-instance activation | Implemented and runtime-verified | Per-user named mutex + focus/ack events; second launch redirected and exits; crash recovery and shutdown hand-over observed | Visual foreground on a desktop session; minimized restore |
| Structured logging / diagnostics | Implemented, verification pending | `FileDiagnosticLog` + `DoctorReport` + crash hooks; Settings reveal/copy; cross-process log lock; CLI doctor adds store/log/discovery state; manual dialog checks pending |
| Update availability check | Implemented and runtime-verified | Manual GitHub Releases read with 15 s timeout/cancellation; banner only when strictly newer; no download, install, or auto-check (Stage 25); click-through and a published Windows artifact remain unverified |
| Installer / uninstaller | Complete for the unsigned per-user model; clean-VM run pending | `install.ps1` in the archive: install/uninstall, prerequisites, shortcut, HKCU entry; 11 tests |
| Code signing | **Missing / requires authorization** | Unsigned; SmartScreen expected |
| Clean-machine validation | **Blocked by environment** | Only Windows 11 25H2 dev machine used |
| ARM64 / MSIX / Store | Deferred | `Platforms=x64`; unpackaged model documented |

### 1.4 Quality and release engineering

| Subsystem | Status | Evidence |
|---|---|---|
| Unit/integration tests | Complete for implemented logic | 514/514; covers domain, stores, discovery (including the packaged/registered merge key), view models, shortcuts, verifier, CLI, installer scripts, update checks, view-model thread affinity, and the provisioning execution spine |
| Windows CI | Complete; remote run observed | `.github/workflows/windows-ci.yml`; restore/build/test/RC verify; run `45907355528` succeeded on `6e3417d` |
| Release packaging | Complete and launch-verified on the development machine | `publish-release.ps1`; SHA sidecar; 46 MB x64 zip; CLI in `cli/`; `install.ps1`; `Mitosis.pri` fix |
| Artifact verifier | Complete and verified (45 fixture tests) | `tools/ReleaseVerifier`; hostile-input hardening; CLI + PRI + installer rules in Stage 26 |
| Version metadata | Complete; release stamp decision pending | `Directory.Build.props` (`0.2.2`; product tag is `v0.2.3`) |
| Application icon | Complete for ≥32 px; small-size glyph open | `app.ico`; `make-icon.ps1` |
| Documentation | Current through Stage 26; manual UI evidence still missing | Stage docs, `windows/README.md`, release/CLI/update/install docs |

## 2. Remaining engineering gaps

The table records only gaps that are real today. "Disposition" says whether the gap
is eligible now, deferred, or authorization-gated.

| # | Area | Gap | Evidence | Impact | Disposition |
|---|---|---|---|---|---|
| G1 | Core product | Executor exists; no production strategy | `CopyProvisionExecutor` + `InstanceLauncher`, probe-verified (`WINDOWS-PROVISIONING.md` §9); `ProductionCatalog.Strategies` still empty | The mechanism can build and launch an instance, but no real app is eligible, so the product still cannot clone | Production strategies authorization-gated |
| G2 | Isolation | No live isolation evidence for any Windows app | `WINDOWS-ISOLATION-VERIFICATION-PLAN.md`; the probe proves only its own private tree | Cannot claim separation for a real app | Authorization-gated |
| G3 | Lifecycle | Launch verified for the probe only; no refresh, rollback, or recovery | `InstanceLauncher` + probe test; engine has no rollback/refresh | Clones could not be trusted | Probe launch done; remainder authorization-gated |
| G4 | Data safety | No delete/clean/relink semantics for real clones | Engine absent | Cannot safely remove data | Authorization-gated |
| G5 | Profiles | Saved profiles had no rename, re-badge, or removal path | `Planned`-profile Edit/Remove added in Stage 21; provisioned profiles remain read-only | Future provisioned records must be managed by the engine, not metadata code | Complete for `Planned` (Stage 21) |
| G6 | Wizard entry | App-detail “New Clone…” showed a placeholder | `CreateCloneFromAppCommand` now raises `NewCloneRequested`; preview keeps the placeholder | A primary path works | Complete (Stage 21) |
| G7 | Diagnostics | No structured logs, crash report, or doctor output | `FileDiagnosticLog`, `DoctorReport`, crash hooks, Settings diagnostics added in Stage 22; CLI `doctor` adds store/log/discovery state in Stage 24 | Support and engine debugging would be blind | Complete (Stages 22, 24), UI verification pending |
| G8 | Process integrity | Multiple app instances can run concurrently | Per-user named mutex + activation events added in Stage 23; second launch is redirected and exits | Duplicate windows, concurrent store access, confusing onboarding | Complete (Stage 23), runtime-verified |
| G9 | CLI | No `mitosis.exe`; no list/doctor/version commands | Read-only console project added in Stage 24 (`version`, `list`, `profiles`, `doctor`, `--json`) | Automation and support path missing | Complete (Stage 24), runtime-verified |
| G10 | Updates | No update check; banner was static preview data | Manual GitHub Releases check added in Stage 25 (`UpdateChecker`, `GitHubReleaseFeed`, Settings action, live banner) | Users cannot learn about releases | Complete (Stage 25), runtime-verified; install/self-update still deferred |
| G11 | Install | Per-user script installer added (Stage 26); no MSI/signing | `WINDOWS-INSTALL.md` | Manual prerequisites replaced by detected ones; Start-menu/uninstall entry provided; clean-VM run and system-wide install pending | Complete for the unsigned per-user model |
| G12 | Trust | Unsigned binaries | Release checklist | SmartScreen blocks/warns | Requires authorization |
| G13 | Compatibility | Only Windows 11 25H2 ever exercised | Release checklist | Unknown behavior on 10/24H2/23H2 | Blocked by environment |
| G14 | Verification | No screenshots, Narrator, contrast, or keypress observations | Stage 27 added 33 screenshots plus UIA/keyboard observations (`WINDOWS-UI-VERIFICATION.md` §2/§5) and fixed 11 UI defects; the 2026-11-27 follow-up added eight screenshots for the profile dialogs, Settings diagnostics/tips, and wizard validation (defect 12); the unlocked Help pass added two more and verified both action buttons and the Help tab cycle, and a light-theme pass added three more (§5.5) | Visual/a11y parity unproven | Narrator, contrast, 150/200% scaling, clean-VM runs remain |
| G15 | Stats | Detail tiles are static preview values | `PreviewData` | Misleading only if shown as live; currently preview-gated | Depends on engine |
| G16 | Link routing | Toggle is UI-only | `ShowLinksCard` / `LinkRoutingOn` | Users may expect routing | Depends on engine/protocol handler |
| G17 | Background | No refresh agent or scheduled task | No code | Clones would age silently | Depends on engine |
| G18 | Docs | `windows/README.md` described Stage 3 | File header | Misleads contributors | Addressed (Stage 21) |
| G19 | Version | Windows stamp `0.2.2` vs product `v0.2.3` | `Directory.Build.props` | Artifact naming/version claims drift | Release decision |
| G20 | Performance | Discovery cost/scale unmeasured beyond one smoke test | Stage 3 record | Startup may be slow on large machines | Eligible later |
| G21 | Licensing | PolyForm Noncommercial + trademark constraints | `LICENSE`, `WINDOWS-PORT.md` §8 | Distribution must respect them | Policy, not code |

## 3. Stage plan

Stages are ordered by dependency, safety, and product value. Numbers are not a
target; stages may be split, merged, or reordered when evidence changes. A stage is
only started when its dependencies and authorization state allow it.

### Stage 21 — Profile lifecycle and app-detail handoff (completed)

- **Objective:** Make saved metadata profiles manageable and make the app-detail
  “New Clone…” action open the wizard preselected to that app.
- **Current problem:** Profiles can only be created. A typo in a name or badge is
  permanent, and the app-detail button shows a placeholder even though the wizard
  exists.
- **Evidence:** `AppDetailView.xaml` profile list has no actions;
  `MainViewModel.CreateCloneFromAppCommand` raises a placeholder;
  `CloneProfile.Rename/ChangeBadge` and `ICloneProfileRepository.UpdateAsync/
  RemoveAsync` already exist and are tested.
- **Implementation:** View-model edit/remove operations restricted to `Planned`
  records; repository-backed reload; edit dialog (name, 1–2 char badge, preset
  color); remove confirmation that states metadata-only impact; same actions for
  orphan profiles; fix the app-detail command to raise `NewCloneRequested`;
  correct the test that pins the placeholder.
- **Dependencies:** None beyond the existing profile store.
- **Risks:** Accidental deletion of a future provisioned record — guarded by the
  `Planned`-only rule and an explicit dialog; duplicate names — repository and
  view-model reject with the existing message.
- **Automated tests:** Rename/re-badge persistence; duplicate/invalid name
  rejection; non-`Planned` refusal; removal; orphan refresh; app-detail wizard
  event and preselection; `OrphanProfiles` change notification.
- **Manual verification:** Dialog layout, focus, Enter/Esc, Narrator names
  (pending; add scenarios to `WINDOWS-UI-VERIFICATION.md`).
- **Definition of done:** All tests green; no app data touched; docs updated;
  one conventional commit pushed.
- **Authorization:** Not required (Mitosis-owned metadata only).
- **Result:** Implemented and pushed; 339/339 tests green. Planned records can be
  edited and removed on app and orphan detail; non-`Planned` records are refused
  with an engine-dependent message; the app-detail “New Clone…” action opens the
  wizard preselected. Manual dialog/Narrator checks remain pending.

### Stage 22 — Diagnostics, logging, and crash reporting (completed)

- **Objective:** Add a safe, local diagnostic trail and a user-facing failure path.
- **Current problem:** No logs, no crash handler, no doctor-style report; engine
  work and support would be blind.
- **Evidence:** No `ILogger`, `UnhandledException`, or log directory in `windows/src`.
- **Implementation:** Small structured file logger under
  `%LOCALAPPDATA%\Mitosis\Logs` with size/age rotation; no secrets, paths redacted
  to `~`; UI-thread and task exception hooks that show a plain dialog and exit
  nonzero; `doctor`-style report assembled from app version, OS, runtime, App SDK
  presence, data-directory writability, profile/evidence store health, and
  discovery counts. No network, no elevation.
- **Dependencies:** None.
- **Risks:** Logging sensitive paths or app names; unbounded growth; hiding
  exceptions. Mitigate with redaction, caps, and rethrow/exit semantics.
- **Automated tests:** Redaction, rotation caps, report fields, failure paths.
- **Manual verification:** Crash dialog and log inspection on a desktop.
- **Definition of done:** Tests green; logs capped; docs describe what is and is
  not recorded.
- **Authorization:** Not required (local diagnostics only).
- **Result:** Implemented and pushed; 353/353 tests green. Ships a bounded,
  home-redacted file log (`%LOCALAPPDATA%\Mitosis\Logs`, 512 KB × 3), startup/stop
  and crash hooks with a plain Win32 failure dialog, a copyable environment report,
  and Settings reveal/copy actions. Store health and discovery counts were not
  included in this report; they move to the read-only CLI doctor (Stage 24), where
  the values can be queried honestly. Crash-dialog, clipboard, Explorer reveal, and
  visual checks remain manual. See `docs/WINDOWS-DIAGNOSTICS.md`.

### Stage 23 — Single-instance activation and store hardening (completed)

- **Objective:** One Mitosis process per user session; a second launch focuses the
  existing window instead of creating a duplicate.
- **Current problem:** Multiple instances can start, duplicating onboarding and
  racing the JSON stores.
- **Evidence:** No mutex/`AppInstance` code; stores serialize only within a process.
- **Implementation:** Per-user named mutex (`Local\`, SID-derived scope) acquired in
  `Program.Main` before the UI; named focus/acknowledgement events; a dedicated
  listener in the primary that restores/minimizes/shows and activates the window
  through the dispatcher queue; `AllowSetForegroundWindow` grant from the secondary;
  bounded acknowledgement wait; abandoned-mutex recovery; explicit release on main
  window close; cross-process log-write mutex (Stage 22 deferral).
- **Dependencies:** None.
- **Risks:** Stale mutex/handle on crash; redirected activation when the primary is
  unresponsive. Mitigated with abandoned-mutex recovery and a bounded wait; both
  were observed at runtime.
- **Automated tests:** Decision/state logic, abandoned owner, listener delivery and
  timeout, shutdown hand-over, scope validation, concurrent log writers.
- **Manual verification:** Launch twice, observe focus; kill primary and relaunch.
- **Definition of done:** Tests green; duplicate launches prevented; docs updated.
- **Authorization:** Not required (current-user process behavior).
- **Result:** Implemented, pushed, and runtime-verified. 10 new tests (363/363
  green). Observed on the development machine: a second launch exits with code 0
  in 122–215 ms and leaves exactly one process; a force-killed primary is recovered
  by the next launch; closing the main window exits the process with code 0 and
  releases the lock. Closing the main window now quits even when the welcome window
  is open — the runtime check exposed that the old shutdown path left an orphaned
  process holding the instance. Visual foreground focus and minimized restore remain
  manual. See `docs/WINDOWS-SINGLE-INSTANCE.md`.
- **Follow-up (2026-11-26, found during Stage 25 verification):** closing the main
  window while Help or Settings was open could still leave that window and the
  process alive, because the welcome window vetoes closes and
  `Application.Exit()` sometimes skipped a remaining window. Shutdown now marks the
  process as quitting, closes every secondary window explicitly, and only then
  calls `Exit()`; the welcome guard ignores closes during shutdown. Six repeated
  trials (Help only, Settings only, both orders, repeated) all exited; one combined
  trial checked for updates, found 0.2.3, then closed the main window with Settings
  still open and exited cleanly.

### Stage 24 — Read-only CLI and doctor report (completed)

- **Objective:** Ship `mitosis.exe` with read-only commands: `version`, `list`,
  `profiles`, `doctor`. No clone, launch, delete, or mutation commands.
- **Current problem:** No CLI; macOS has one and Windows support/automation has no
  path.
- **Evidence:** No console project; `WINDOWS-PORT.md` W46/W55 marked missing.
- **Implementation:** New `Mitosis.Cli` console project (`mitosis.exe`, separate
  assembly name because Windows filenames are case-insensitive) sharing discovery,
  the JSON profile store, and diagnostics with the app. Pure argument parser;
  `--json` on every command; fixed-width text tables; exit codes 0/1/2. `doctor`
  extends `DoctorReport` with store readability/count, log files/bytes/latest,
  discovered-app count, and writability, and redacts home paths. Read-only by
  construction; only the writability probe creates and deletes one zero-byte file,
  and when the data folder is absent the probe runs in the nearest existing parent
  rather than creating it.
- **Dependencies:** Stage 22 diagnostics for `doctor`.
- **Risks:** Shipping a second binary; PATH installation. Mitigated by keeping the
  CLI out of the GUI zip for now (archive layout and verifier contract unchanged)
  and making no PATH change; both are documented in `docs/WINDOWS-CLI.md`.
- **Automated tests:** 31 CLI tests (parsing, text/JSON output, empty and failure
  paths, no-store-write and no-data-folder guarantees) plus 2 `DoctorReport`
  local-state tests.
- **Manual verification:** Run from the publish tree.
- **Definition of done:** Tests green; commands honest; docs updated.
- **Authorization:** Not required for read-only local commands; PATH/system
  installation remains out of scope.
- **Result:** Implemented, pushed, and runtime-verified. 33 new tests (396/396
  green, 0 warnings). Observed: `version` reported `0.2.2+6f9ee6b`; `list` found
  127 applications in about 1.5 s; `profiles` reported none; `doctor` text and JSON
  agreed on profile/log/discovery counts; `clone Slack` exited 1 as an unknown
  command; the existing log file's SHA-256 was unchanged, no profile store was
  created, and no probe file was left behind. Combined release-archive packaging
  and PATH install are deferred to the installer/distribution stage. See
  `docs/WINDOWS-CLI.md`.

### Stage 25 — Update availability check (completed)

- **Objective:** Tell the user when a newer GitHub release exists; no automatic
  download or install.
- **Current problem:** The update banner uses a hardcoded preview version.
- **Implementation:** Opt-in/manual check against the repository’s public releases;
  version comparison; timeout/cancellation; no telemetry; honest offline/error
  states; banner only when a newer version exists.
- **Dependencies:** Stage 22 for diagnostics.
- **Risks:** Network privacy, rate limits, malformed responses. Keep the request
  minimal and documented.
- **Automated tests:** Version comparison, response parsing, failure states.
- **Manual verification:** Check with network on/off.
- **Definition of done:** Tests green; no auto-update claim; docs updated.
- **Authorization:** Not required (read-only public network request).
- **Result:** Implemented, pushed (`58353a0`), and runtime-verified. 76 new tests (472/472
  green; one pre-existing CS0105 warning remained, cleaned up with the other
  pre-existing test warning in Stage 26). The only network read is a GET
  of `https://api.github.com/repos/deadhearth01/Mitosis/releases?per_page=10`
  with no token, query identity, or telemetry; the response is bounded to 512 KB
  and must be a JSON array. Selection skips drafts, skips prereleases for a
  stable build, requires an HTTP(S) release page and a strictly newer parseable
  tag, and mirrors the macOS `Versioning.swift`/`UpdateChecker.pick` rules.
  Settings ▸ General ▸ Updates owns the manual button, version line, live status,
  and View Release link; the main InfoBar appears only after a real newer release
  is found, opens the release page through the OS, and stays dismissed for the
  session. Preview data keeps its sample banner and honest placeholder. Observed
  on the development machine: Version `0.2.2` checked against the real GitHub
  feed and reported 0.2.3 with the correct release URL; re-checking as `0.2.3`
  reported up to date; a blocked proxy produced “Couldn't reach GitHub. Check
  your internet connection.” with no banner or link; the banner updated live,
  dismissed, and the app exited cleanly with no error log entries. The current
  v0.2.3 release ships only the macOS zip, so the Windows banner names the
  product release line and links the release page without implying a Windows
  download; no Windows artifact is published yet. Self-update/install remains
  deferred. The same verification pass found and fixed the Stage 23 shutdown gap
  described above. See `docs/WINDOWS-UPDATES.md`.

### Stage 26 — Installer and unsigned distribution package (implemented for the per-user model; clean-VM run pending)

- **Objective:** Decide and implement the install story (per-user installer vs
  zip-only), including uninstall and prerequisite checks.
- **Current problem:** Zip + manual .NET/App SDK prerequisites; no Start-menu or
  uninstall path.
- **Evidence:** `WINDOWS-RELEASE.md` deployment model.
- **Implementation:** Per-user installer script (e.g. Inno Setup or WiX) built
  locally; prerequisite detection; uninstall removes app files only, never user
  profile data without explicit choice; no system-wide install by default. Decide
  whether the read-only CLI ships beside the app (for example a `cli/` subfolder)
  and extend the verifier and docs if it does; no PATH change without explicit
  authorization.
- **Dependencies:** Stages 21–25; signing decision.
- **Risks:** Installer writing outside per-user scope; leftover files; false
  prerequisite claims. Test in a disposable VM.
- **Automated tests:** Script/manifest validation; path and uninstall rules.
- **Manual verification:** Install/uninstall in a clean VM.
- **Definition of done:** Installer builds and passes structural checks; clean-VM
  run recorded; signing/public distribution still separate.
- **Authorization:** Building is allowed; installing system-wide, signing, and
  public distribution require explicit authorization.
- **Result, packaging half (2026-11-26, pushed `18dd7d6`):** The distribution
  package now bundles the read-only CLI in `cli/` and launch-testing the artifact
  exposed a real
  startup defect that structural verification had missed:
  `dotnet publish` omitted the XAML-compiled `Mitosis.pri` for unpackaged apps
  (microsoft/WindowsAppSDK#6720), so the published exe threw
  `XamlParseException` while the build output worked. The app project now wires
  the PRI into `ResolvedFileToPublish`; the publish recipe requires the PRI,
  publishes the CLI, compares CLI/app version metadata, and smoke-runs
  `cli\mitosis.exe version`; the verifier requires the CLI runtime files and the
  PRI, audits both executables as x64, and checks both versions. 6 new verifier
  tests and two warning cleanups; 478/478 green, 0 build warnings.
- **Result, installer half (2026-11-27, pushed `3ac2d86`):** No Inno Setup, WiX, or NSIS is
  installed, and adding one is a tooling decision, so the install story is a
  dependency-free per-user script instead: `windows/scripts/install.ps1` ships as
  `install.ps1` in the archive root and is both installer and uninstaller. It
  installs the payload under `%LOCALAPPDATA%\Programs\Mitosis`, records every
  copied file in `.mitosis-install.json`, adds a Start-menu shortcut and an HKCU
  Apps entry, and detects the .NET 10 and Windows App Runtime 2.5 prerequisites
  without downloading or elevating. Uninstall removes exactly the recorded files
  plus the marker, shortcut, and registry entry; it keeps files the user added
  and keeps profile data unless `-RemoveUserData` is passed; it refuses unsafe
  roots, unowned folders, a missing/mismatched marker, and a running app. Exit
  codes 0/1/2 mean success/usage/refused, and `-DryRun`,
  `-NoShellIntegration`, `-SkipPrerequisiteCheck`, and `-StrictPrerequisites`
  exist for testing and portable use. The recipe now parse-checks and copies the
  script into the archive; the verifier requires it (one fixture test for the
  missing case). 11 behavioral tests run the real script against a temporary
  fixture with a redirected install root, data root, and shell integration
  disabled; the suite is 490/490 green with 0 build warnings.
- **Startup hardening:** the async first-activation continuation and
  `ShowOnboarding` no longer open the welcome window once shutdown has begun
  (defense in depth around `Application.Exit()` being asynchronous). The earlier
  "orphan process" suspicion was an automation artifact: closing before the main
  window handle exists is not a close request. Closing the handle 500 ms after it
  appears exited the process in every trial.
- **Observed on this machine (2026-11-26/27, not a clean VM):** the publish tree
  and a freshly unzipped archive start, show the main and welcome windows, and
  exit cleanly when the main window closes; installing from the unzipped archive
  and launching the installed copy behaves the same; a full shell-integration
  install was inspected (shortcut target/workdir/icon, uninstall strings) and
  uninstalled, leaving the Start menu and HKCU clean; uninstall removed both the
  install folder and, with `-RemoveUserData`, the data folder; prerequisite
  detection reported the user-local .NET 10 runtime honestly (it is not on the
  apphost search path without `DOTNET_ROOT`) and initialized the real Windows App
  Runtime bootstrapper; `-StrictPrerequisites` failed before writing in that case
  and passed with `DOTNET_ROOT` set; the verifier passes the final archive (94
  files, 45,762,106 bytes, SHA-256
  `9db1ef6bc9b74fd40afa042536cf03500348ff0823e73934f4687d8a53b8d499`).
  Still open: clean-VM install/launch runs, MSI/Inno/WiX/Store packaging, signing,
  `PATH` installation, and public distribution — all authorization-gated. See
  `docs/WINDOWS-INSTALL.md`.

### Stage 27 — Visual, accessibility, and clean-machine verification (partly blocked; desktop pass complete)

- **Objective:** Execute the manual protocol and close W65/W66 plus the pending
  interaction scenarios.
- **Current problem:** No screenshots, Narrator runs, contrast checks, or
  clean-machine launches exist.
- **Evidence:** `WINDOWS-UI-VERIFICATION.md`; release checklist.
- **Implementation:** Run S1–S24, capture screenshots, record Narrator order,
  contrast themes, DPI, keypresses; fix only evidence-backed defects; run the
  published zip on a clean Windows 11 and Windows 10 1809+ VM with and without
  prerequisites.
- **Dependencies:** A real desktop session and clean VMs.
- **Risks:** Environment cannot provide clean VMs; do not fabricate results.
- **Automated tests:** None for pixels; regression tests for any code fix.
- **Manual verification:** The point of the stage.
- **Definition of done:** Evidence files/screenshots attached; remaining
  differences classified cosmetic/minor/major.
- **Authorization:** Not required for local UI checks; VM provisioning may need
  environment access.
- **Result (2026-11-27):** Desktop pass executed on the development machine
  (Windows 11 25H2, 100% scale, dark theme): 33 screenshots in
  `docs/evidence/windows-ui/` cover 1060×720/820×520/1600×900, search
  match/no-match/Esc, app detail with and without profiles, profile removal,
  the wizard steps, Help, Settings/update check, the update banner and its
  dismissal, the three onboarding pages, and selection after a wizard close.
  Eleven defects were reproduced and fixed: detail-column clipping (the
  ScrollViewer centred its direct content by desired width), selection jumping
  when `ItemsSource` is replaced, Esc swallowed by two `AutoSuggestBox`es,
  missing first focus in four secondary windows, an `RPC_E_WRONG_THREAD`
  startup crash from `ConfigureAwait(false)`, silent profiles empty/error
  states, the publisher separator, sidebar accessible names, the onboarding
  column, and packages with an empty display name. Logic changes carry
  regression tests (including `ViewModelThreadAffinityTests`). Live checks
  confirmed one Help/Settings window per open, fixed wizard/Settings sizes,
  the Help size floor, F1/Ctrl+,/Ctrl+F/Ctrl+N routing, selection surviving
  search and Esc, banner dismissal, single-instance raise with minimized
  restore, and clean shutdown.
  Narrator speech, high-contrast themes, 150/200% text scaling, clean-VM
  installs, and a second Windows user were not run; this stage therefore
  stays **partly blocked**. Suite 497/497, 0 warnings. The republished archive
  `Mitosis-windows-0.2.2-x64-47dd1f0.zip` (94 files, 45,762,467 bytes, SHA-256
  `e67f68623c0b6963b2b41415f14dc7eb52b06227c5dd23ad1a67299ddb6038d3`) passes the
  verifier.
  **Follow-up (2026-11-27):** the profile edit/remove dialogs, the Settings
  diagnostics clipboard and folder reveal, Advanced ▸ Show Tips Again, and the
  wizard's validation/review/save path were exercised and recorded in
  `WINDOWS-UI-VERIFICATION.md` §5.3. One more defect was fixed: a failed profile
  save left focus on Save and never announced the error, so the dialog now
  focuses the first invalid field and raises an automation notification. Suite
  497/497, 0 warnings. GitHub Actions run `45906908476` on `59a0f9d` completed
  successfully (restore/build/test and the x64 release-candidate publish). The
  republished archive `Mitosis-windows-0.2.2-x64-59a0f9d.zip` (94 files,
  45,762,634 bytes, SHA-256
  `eeec0264cd12c7c95d4aa91cf4780a1b863a87e88f313677b0f2092e123711e2`) passes the
  verifier.

  **Follow-up (2026-11-27, modern UI):** the shell was brought up to the macOS
  reference and the evidence gap for text scaling was closed. `MainWindow` now
  uses a Mica (BaseAlt) backdrop, a custom unified title bar that keeps the
  sidebar tint behind the caption area, theme-aware caption glyphs, and themed
  window/content surfaces; controls use a 6 px corner radius in both themes.
  Application rows load their real icon from the registry `DisplayIcon` value
  (read-only shell thumbnail, UI-thread bitmap, letter badge as fallback, 8
  parser tests), and the app detail page became a header plus Details,
  Compatibility, and Profiles cards with label-left/value-right rows. A
  binding bug that left the reason/evidence bullets blank is fixed, and the
  empty state no longer advertises drag-and-drop, which is not implemented.
  Captures at 1180x960: `modern-main-{dark,light}.png`,
  `modern-appdetail-{dark,light}.png`, `textscale-150-*`, `textscale-200-*`.
  Text scaling reflows without clipping (the 240 px sidebar ellipsizes row
  names and keeps the full name in the row tooltip); `TextScaleFactor` was
  restored and re-read. High contrast was enabled for `hc-main.png` and
  `hc-appdetail.png`: system selection and system text kept their values and
  every label stayed legible, but the app surfaces stayed on the light theme
  values under the high-contrast scheme, so the `HighContrast` theme
  dictionary is not taking effect. That is recorded as a visual defect, not
  marked complete. Suite 522/522, 0 warnings (commit `396e445`).

### Stage 28 — Clone engine authorization and strategy implementation (execution spine implemented; production strategies blocked)

- **Objective:** Implement the first production provisioning strategy and executor
  behind the existing gate.
- **Current problem:** The catalog is empty, so the product cannot clone; the
  executor and launcher exist but are unreachable for a real application.
- **Evidence:** `Gate.cs`, `Planner.cs`, `WINDOWS-PROVISIONING.md`.
- **Implementation:** Only after written authorization: choose a researched
  candidate (e.g. documented per-profile flag), implement a strategy + executor,
  create one private data directory per instance, verify launch, and register the
  strategy only when evidence passes. No generic copying.
- **Dependencies:** Stages 22–23; explicit authorization; disposable test app.
- **Risks:** Data loss, cross-account leakage, breaking the original install.
  Require a disposable environment, backups, and abort controls.
- **Automated tests:** Planner/gate unit tests; executor dry-run tests; no live
  third-party app tests in CI.
- **Manual verification:** The isolation protocol with a disposable app.
- **Definition of done:** One strategy registered with complete evidence; a clone
  launches and is verifiably separate; rollback works.
- **Authorization:** **Required before any code that can execute provisioning.**
- **Progress (2026-11-27):** the execution spine is implemented and probe-verified.
  `CopyProvisionExecutor` revalidates the plan at execution time (root, source,
  destination, reparse points, launch executable), copies with reparse-point refusal
  and file/size/depth bounds, writes `mitosis-instance.json` and
  `mitosis-launch.json`, creates the private `data` directory, clears read-only
  attributes on copies, and removes only what it created on failure.
  `InstanceLauncher` requires the executable, working directory, and data directory
  to resolve inside the instance, substitutes `{instance}`/`{dataDirectory}`, drains
  output while keeping at most 64 KiB, and enforces a timeout. `OperationPlan` now
  carries `SourceRoot` so execution can revalidate the copy source. Twelve tests
  cover refusals, a real copy, launcher substitution and output drain, and a
  two-profile run in which both copies launch from their own directory and write
  only into their own tree; the probe target is the system command processor, so no
  installed application was copied or launched. At `c2cbea6` (ten tests) the suite
  ran 507/507 with 0 warnings; GitHub Actions run `45938961584` on that commit
  completed successfully (restore, build, 507 tests, and the x64
  release-candidate publish with verification). `Mitosis-windows-0.2.2-x64-c2cbea6.zip`
  (94 files, 45,775,840 bytes) passed the verifier; the current artifact is
  `Mitosis-windows-0.2.2-x64-ddc1711.zip` (94 files, 45,775,876 bytes, SHA-256
  `2149c138b0a05f27a16376fbffa1ea8f7cbff9b459025595dda365daa605196d`), rebuilt from the
  tree with both follow-up fixes and verified the same way. It was started once: the
  process stayed alive and created its “Mitosis” main window, which is all a locked
  desktop allows; no interactive pass was possible at that moment. Later passes ran
  interactively on the same package once the desktop was unlocked (§5.4/§5.5 of
  `WINDOWS-UI-VERIFICATION.md`). (An intermediate `aadbb52` archive
  was built and verified between the two fixes.)
  A follow-up fixed a launch defect found by review: the bounded output reader stopped
  reading at the 64 KiB cap, so an instance that wrote more stalled on a full pipe and
  could only be stopped by the timeout. It now drains the stream to the end and still
  caps what it keeps; the regression test fails without the fix (20 s timeout), and the
  suite is 509/509. A second reviewed failure path was closed the same way: a launch
  whose data-directory name is held by a file now returns a named refusal instead of
  throwing out of the call. The current artifact is
  `Mitosis-windows-0.2.2-x64-df998f7.zip` (94 files, 45,777,177 bytes, SHA-256
  `89b0bb376487b33649c89d4336310302037c2c9673fc8d46d5b87aed7b5f7317`), verified by
  `ReleaseVerifier` and started once, with a clean `app.stop | main window closed` on
  close. It also carries the discovery merge fix recorded below.
  **Still blocked:** a production strategy for a real application (requires its own
  isolation evidence and your authorization), UI wiring, rollback, refresh, and
  deletion/relink. The catalog stays empty, so the wizard and detail views still
  offer planned profiles only. The Help action buttons, which the locked desktop had
  blocked, were verified in a later unlocked pass (§5.4 of
  `WINDOWS-UI-VERIFICATION.md`).

### Stage 29 — Isolation verification execution (blocked)

- **Objective:** Run the seven-phase protocol and produce evidence bundles for the
  first candidate.
- **Current problem:** Framework only; nothing verified live.
- **Evidence:** `WINDOWS-ISOLATION-VERIFICATION-PLAN.md`.
- **Implementation:** Execute phases 1–7 in a disposable environment; record all
  ten dimensions; owner approval; no production catalog change without a pass.
- **Dependencies:** Stage 28; named operator/reviewer; written authorization.
- **Risks:** Residual shared state (registry, IPC, credentials, updates, file
  associations). A partial pass is a fail.
- **Automated tests:** Evidence validation/evaluation (already present).
- **Manual verification:** The protocol itself.
- **Definition of done:** Evidence bundle with all dimensions verified and
  approved; otherwise the candidate remains ineligible.
- **Authorization:** **Required.**

### Stage 30 — Clone lifecycle: launch, refresh, rollback, delete/recovery (blocked)

- **Objective:** Make a provisioned clone maintainable: open, verify running,
  refresh after the original updates, restore on failure, delete to Recycle Bin
  with an explicit data choice, and relink.
- **Current problem:** No lifecycle code.
- **Evidence:** macOS `CloneMaintenance.swift`, `CloneBuilder.swift`,
  `AutoRefreshAgent.swift`; Windows engine absent.
- **Implementation:** Process launch/liveness; snapshot/rollback; refresh
  orchestration; Recycle Bin semantics; no permanent delete; explicit data
  handling.
- **Dependencies:** Stages 28–29.
- **Risks:** Data loss during refresh/delete; open-process conflicts. Require
  backups, dry runs, and abort paths.
- **Automated tests:** State machine, path safety, rollback decision logic.
- **Manual verification:** Disposable-app lifecycle runs.
- **Definition of done:** A clone can be created, opened, refreshed, rolled back,
  and deleted without touching the original or other clones.
- **Authorization:** **Required.**

### Stage 31 — Live stats, link routing, and background refresh (blocked)

- **Objective:** Replace placeholder tiles with real measurements; route links to
  the right clone; refresh in the background.
- **Current problem:** Static tiles, UI-only link toggle, no scheduler.
- **Evidence:** macOS `StatsCollector.swift`, `LinkRouterSetup.swift`,
  `AutoRefreshAgent.swift`.
- **Implementation:** Per-clone disk/data/memory/CPU collection; protocol handler
  registration; per-clone routing and restore; scheduled refresh with skip-running
  and defer-mid-update.
- **Dependencies:** Stage 30.
- **Risks:** Protocol-handler hijacking, performance overhead, background task
  lifetime.
- **Automated tests:** Parsing/formatting, routing decisions, schedule logic.
- **Manual verification:** Real clone runs and link opens.
- **Definition of done:** Live values match the OS; links land in the chosen clone;
  background refresh is observable and cancellable.
- **Authorization:** **Required** (third-party launches, protocol registration).

### Stage 32 — Signing, public distribution, and support (blocked)

- **Objective:** Signed installer, release notes, SmartScreen/reputation plan,
  support and rollback process.
- **Current problem:** Unsigned; no public release.
- **Evidence:** Release checklist.
- **Implementation:** Authenticode certificate decision, signed builds, release
  channel, checksums, support docs, privacy statement.
- **Dependencies:** Stages 26–31.
- **Risks:** Identity/legal claims, certificate handling, distribution policy.
- **Automated tests:** Signature/checksum verification in CI.
- **Manual verification:** Install and update on clean VMs.
- **Definition of done:** Signed artifact verified; public release approved.
- **Authorization:** **Required.**

### Deferred (not stages yet)

- Tray/menu-bar equivalent (needs lifetime architecture and a real reason).
- Window position/size restoration (no macOS evidence to mirror).
- MSIX/Store distribution, ARM64, self-update, workspaces, sandboxed light mode.
- macOS UI screenshot parity for states that do not exist on Windows yet.

## 4. Completion criteria

These are separate milestones. Passing one does not imply the next.

1. **Feature complete** — Every parity row is implemented or explicitly deferred
   with a documented reason. The engine, lifecycle, stats, links, CLI, settings,
   and help all have real behavior; no dead buttons presented as complete.
2. **Functionally verified** — Unit/integration tests pass; the manual UI protocol
   is executed; a clean-machine install and launch are recorded; supported OS
   versions are exercised.
3. **Security reviewed** — Threat model, isolation evidence for every registered
   strategy, path/permission review, dependency review, and a release audit with
   no open high-severity findings.
4. **Installable** — A per-user installer (or documented zip) with prerequisite
   detection, uninstall, and no hidden system changes; user data survives
   uninstall unless explicitly removed. (`install.ps1` satisfies this on the
   development machine; clean-VM repetition is part of milestone 2.)
5. **Maintainable** — Structured diagnostics, CI, reproducible builds, current
   docs, and no machine-specific assumptions.
6. **Ready for a controlled release** — Signed artifact, small-cohort rollout,
   rollback plan, known-issues list, and support contact.
7. **Ready for broad public distribution** — SmartScreen/reputation strategy,
   update channel, privacy statement, license/trademark compliance, and capacity
   to support users.

**Current position:** Stages 21-27 closed the metadata-profile dead end, the
support-tooling gap, duplicate-process risk, the missing automation path, the
static update banner, the missing install story, and most of the manual UI
evidence gap. Stage 28 added the provisioning execution spine (copy, manifest,
launch configuration, private data directory, bounded launch) and proved it with
a two-profile probe, but no production strategy is registered, so the product
still cannot clone a real application. Narrator speech, high-contrast themes, text
scaling, and clean-VM runs remain unverified. The two Help action buttons were
verified in a later unlocked pass, including the Settings and issue-template targets
and the Help tab cycle (§5.4 of `WINDOWS-UI-VERIFICATION.md`). The port is
installable per-user and ships in a launch-tested but unsigned archive. The Light
theme was captured for the main window, Help, and Settings (§5.5), so both themes
now have evidence. Next eligible engineering work is the tray/background decision;
the remaining verification items (Narrator, high contrast, text scaling, clean VM)
need OS accessibility settings or another machine. Clean-VM runs, signing,
system-wide install, public distribution, and any real strategy stay
authorization-gated.

## 5. Authorization boundaries

The following always require explicit user authorization before implementation or
execution, regardless of roadmap placement:

- Implementing or enabling real application cloning/provisioning.
- Executing a clone, launching third-party apps through clone infrastructure, or
  modifying third-party app data.
- Live isolation testing against real applications.
- Deleting/recovering real application data.
- System-wide registry/filesystem changes, services, drivers, or admin-required
  operations.
- Signing with a real identity/certificate.
- Public distribution of binaries.
- Licensing or legal claims.

Planning, documentation, tests for non-executing logic, and Mitosis-owned metadata
operations are allowed without additional authorization.

## 6. Verification ledger

| Claim | Level | Evidence | Still needed |
|---|---|---|---|
| Code compiles (Release) | Verified | `dotnet build` (0 warnings) on the Stage 27 tree and on the execution-spine tree; GitHub Actions run `45906908476` succeeded on `59a0f9d` | None for this tree |
| 522 tests pass | Verified | `dotnet test` on the modern-UI tree (2026-11-27), eight test projects | Clean-machine test run |
| Manual UI protocol | Verified for observed scenarios (desktop) | 57 screenshots plus UI-Automation focus and keyboard observations in `WINDOWS-UI-VERIFICATION.md` sections 2/5: both Help action buttons, the Help three-stop tab cycle, light-theme captures, modern chrome with real app icons in dark and light, and 150/200% text scaling; 12 defects fixed with regression tests | S24 Narrator, a verified high-contrast fix, clean VM, second user |
| Read-only CLI | Verified (runtime) | `version`, `list` (127 apps, ~1.5 s), `profiles` (empty), `doctor` text+JSON, `clone` usage error; log SHA unchanged; no probe files left; bundled `cli\mitosis.exe` launched from the unzipped archive and reported the build version | Clean machine; damaged real store; alternate consoles |
| Single-instance behavior | Verified (runtime) | Two launches, force-kill/relaunch, graceful close observed with process counts, exit codes, and log lines; 2026-11-26 follow-up: six trials closing the main window with Help/Settings open all exited | Second user/session, elevated launch; focus/restore observed 2026-11-27 |
| Update availability check | Verified (runtime + logic) | Real GitHub feed: 0.2.2 → 0.2.3 available with URL, 0.2.3 → up to date; blocked proxy → honest failure, no banner/link; banner updated and dismissed in the live app; 76 tests cover parsing, selection, timeout, cancellation, bounded reads, and states | Clicking View Release; Narrator; pixels; a published Windows artifact; proxy/offline on another network |
| Planned-profile edit/remove | Verified (logic + runtime) | 10 ViewModel tests; store/domain tests; live dialogs: focus in `Name`, inline errors, duplicate rejection, persisted rename/color, Esc/Enter cancel, metadata-only removal, error announced via automation notification | Narrator speech; standard user |
| Local diagnostics log/report | Verified (logic + runtime) | 14 tests: redaction, rotation, report, failure, commands; live clipboard report and Explorer reveal of the real log folder | Crash dialog; Narrator; second network |
| Discovery reads real apps | Verified (logic + one smoke run) | Stage 3 record; tests | Large-machine performance, OS matrix |
| Application icons | Verified (live) | Registry `DisplayIcon` is parsed and read read-only as a shell thumbnail; Android Studio, AntiGravity, Brave, and Bun rendered real icons; a letter badge remains when the value is missing or unreadable; 8 parser tests; bitmaps are created on the UI-thread dispatcher, which WinUI requires | Applications whose icon lives in an `.ico` or `.dll` source on another machine |
| High-contrast surfaces | **Defect recorded** | High-contrast run: the system selection colour and system-drawn text kept system values and everything stayed legible, but the app surfaces (sidebar, content) stayed on the light theme values, so the `HighContrast` theme dictionary is not taking effect | A verified fix and re-run |
| Profiles persist atomically | Verified | Store tests + implementation | Cross-process contention test |
| Release zip is structurally sound | Verified | Stage 19/20 verifier + 45 fixture tests; Stage 26 additionally bundled the CLI, requires the PRI, and requires `install.ps1` | Clean-machine launch |
| Provisioning execution spine | Verified (runtime, probe target only) | 12 tests: copy + manifest + launch config + private data directory; refusals for an existing destination, missing source, reparse point, unadmitted plan, an external executable, and an occupied data-directory name; launcher placeholder substitution and a 200 KiB output drain; two-profile probe launched both copies with separate writable trees; suite 509/509 | A real application; rollback, refresh, delete/relink; UI wiring |
| Published artifact launches | Verified (development machine only) | Stage 26: the publish tree and a freshly unzipped archive start, show the main/welcome windows, and exit cleanly; this exposed and fixed the missing `Mitosis.pri` (microsoft/WindowsAppSDK#6720) | Clean Windows 10/11 VM, missing-prerequisite path, another desktop session |
| UI matches macOS pixels | **Not verified** | The 2026-11-27 captures follow the macOS reference (unified title bar, sidebar tint, cards, status pill, 92 px icon) but there is no macOS side-by-side diff or measurement | macOS pixel diff; Narrator |
| Any clone works | **Not verified** (probe only) | The execution spine builds and launches an instance for a controlled probe; the production catalog is empty | Authorization + a real strategy + isolation evidence |
| Installer works | Verified (development machine, logic + runtime) | `install.ps1`: dry run, install/reinstall/`-Force`, unsafe-path and unowned-folder refusal, recorded-file-only uninstall, data preservation and `-RemoveUserData`, missing-marker refusal, real shortcut + HKCU entry inspected and removed; 11 behavioral tests | Clean Windows 10/11 VM, standard user, PowerShell 7, SmartScreen/AV |
| Binary is trusted | **Not verified** | Unsigned | Signing decision |

## 7. Roadmap maintenance

After every stage:

1. Update the status table and gap table with what actually changed.
2. Move completed stages to a short "completed" log with commit hash and test count.
3. Re-read the next stage's dependencies; reorder if new evidence changes priority.
4. Record new gaps rather than hiding them in a stage report.
5. Never mark a manual check complete without a recorded observation.
6. Keep the authorization boundary explicit; a roadmap entry is not permission.

### Completed stage log

| Stage | Title | Commit | Tests | Notes |
|---|---|---|---|---|
| 13 | Settings, Help, onboarding | `1b92b2d` | 266 | Real preference persistence |
| 14 | Window lifecycle and keyboard | `4bf257c` | 278 | Tray/restoration deferred |
| 15 | Main-window interaction parity | `08a1a6d` | 283 | Selection survives search |
| 16 | Keyboard/focus/accessibility | `0aa36bc` | 283 | XAML/wiring fixes |
| 17 | Release packaging audit | `1cfacc4` | 287 | RC pipeline |
| 18 | Version metadata and identity | `2308fd9` | 291 | `Mitosis.exe` + icon |
| 19 | Artifact integrity verification | `67ceeb6` | 307 | SHA sidecar + verifier |
| 20 | Verifier/security hardening | `2ef60d0` | 329 | Hostile-input limits |
| — | Merge `origin/main` `v0.2.3` | `6f8e3de` | 329 | No Windows source changes |
| 21 | Profile lifecycle + wizard handoff | `0e2b523` | 339 | Metadata-only edit/remove; app-detail entry |
| 22 | Local diagnostics and crash safety | `e36fdec` | 353 | Bounded redacted log; report; Settings actions |
| 23 | Single-instance activation and shutdown | `29d6812` | 363 | Mutex + focus/ack events; log write lock; quit on main close |
| 24 | Read-only CLI and doctor report | `6dbb0c5` | 396 | `version`/`list`/`profiles`/`doctor`; `--json`; no writes |
| 25 | Update availability check | `58353a0` | 472 | Manual GitHub read; live banner; shutdown child-window fix |
| 26 | CLI in release zip, PRI fix, per-user installer | `18dd7d6`, `3ac2d86` | 490 | `cli/` layout; launch-tested artifact; `install.ps1`; clean-VM run pending |
| 27 | Manual UI/accessibility pass and fixes | `6e0b802` | 497 | 33 screenshots; 11 defects fixed; Narrator/contrast/clean VM pending |
| 27 (follow-up) | Dialogs, support actions, wizard paths | `59a0f9d` | 497 | save-error focus + announcement (defect 12); 8 screenshots; edit/remove, diagnostics/reveal/tips, wizard validation verified |
| 28 (execution spine) | Provisioning executor + instance launcher, probe-verified | `c2cbea6` | 507 | no production strategy (catalog still empty); two-profile probe launched both copies with separate writable trees; live Help-button check blocked by a locked session |
| 28 (follow-up) | Bounded-output drain fix | `15ac563` | 508 | chatty child no longer stalls on a full pipe; regression test times out without the fix |
| 28 (follow-up 2) | Occupied data-directory refusal | `ddc1711` | 509 | launcher returns a named refusal instead of throwing |
| 28 (evidence) | Help action buttons and light-theme captures | `b837199`, `2f72818`, `a19fcb4` | 509 | both Help buttons live-verified (Settings privacy page, issue chooser); Help tab cycle recorded; three light-theme screenshots; app theme toggled per-user and restored |
| — | Duplicate app registrations merged by publisher common name | `df998f7` | 514 | Edge was listed twice (X.500 vs friendly publisher); 126 → 125 applications, one row, detail shows both sources; 5 new tests |
| 27 (follow-up 3) | macOS chrome, app icons, card detail layout | `396e445` | 522 | Mica + unified title bar; real app icons with letter fallback; dark/light and 150/200% text-scale captures; high-contrast surface defect recorded |

Stages 21+ are appended as they complete.
