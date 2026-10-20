# UI / feature parity matrix (Stage 1)

macOS source → Windows implementation status per item. Statuses:

- **V** — Implemented and verified in macOS source (cited).
- **P** — Partially implemented (what is missing is stated).
- **D** — Documented but implementation not located.
- **R** — Requires runtime or visual verification (on a Mac).
- **W** — Windows-specific adaptation required (deliberate decision recorded).

Nothing here is marked verified from a name or README alone.

## 1. App shell and windows

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W01 | Main window 1060×720, min 820×520, title + clone count | `MitosisRootApp.swift:14-25` | V | Same metrics; WinUI `AppWindow` sizing + title bar (W: native chrome) |
| W02 | Help window 820×580 (min 700×500) | `MitosisRootApp.swift:27-32`, `HelpView.swift:18` | V | Same metrics; WinUI secondary window |
| W03 | Settings window, 540 wide, 2 tabs | `SettingsView.swift:9-19` | V | Same metrics/tabs; WinUI window or dialog |
| W04 | Optional tray menu (menu-bar equivalent) | `MenuBarContent.swift`, `Prefs.menuBarKey` | V | **W**: system-tray icon + flyout instead of menu-bar extra; same items |
| W05 | Menu commands: New Clone ⌘N/Ctrl+N; Help ⌘?/F1-area; Report a Problem; repo link | `MitosisRootApp.swift:46-64` | V | Same commands, Ctrl-based shortcuts (W) |
| W06 | Dock-icon app drop → New Clone | `MitosisRootApp.swift:67-72` | V | **W**: taskbar drop unsupported — provide window drop + file-association entry instead |
| W07 | Quit-on-last-window-close unless tray mode | `MitosisRootApp.swift:75-77` | V | Same lifecycle rule |
| W08 | Update banner (top, dismissible) | `MainWindow.swift:87-111` | V | Same placement/content; WinUI `InfoBar` equivalent |
| W09 | Load-failure state `"Mitosis is missing some of its files…"` | `MitosisRootApp.swift:80-90` | V | Same state and copy |
| W10 | Window position/size restoration | `.defaultSize` only; no restore code located | P+R | **W**: implement via `AppWindow` persistence; verify on Mac whether restore happens |

## 2. Sidebar and navigation

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W11 | By-App groups (14 px icon + name, sorted) | `CloneList.swift:9-24`, `CloneLibrary.swift:33-40` | V | `NavigationView`/list with same grouping |
| W12 | Rows: 30 px badged icon + label + status caption | `CloneList.swift:52-74` | V | Same row composition |
| W13 | Sidebar search (`Search`, case/diacritic-insensitive) | `CloneList.swift:26`, `CloneLibrary.swift:42-58` | V | Same behavior; shortcut binding TBD (see W15) |
| W14 | Bottom New Clone button + tooltip | `CloneList.swift:35-48` | V | Same placement/label |
| W15 | `⌘F` focuses search | No binding in source | P+R | Verify on Mac; provide Ctrl+F equivalent (W) |
| W16 | All-Clones / Running sidebar rows | Enum exists; not rendered (`CloneList.swift`) | P | Decision needed: render rows or drop (spec drift) |
| W17 | ⌫ deletes selected; Return opens | `CloneList.swift:27-34` | V | Same (Del/Enter); document in Windows UI |
| W18 | Row context menu (10 items, §audit-§2) | `CloneList.swift:77-99` | V | Same items as `MenuFlyout`; same enablement rules |

## 3. Clone detail page

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W19 | Header: 104 px icon + largeTitle name + StatusPill + `Clone of {app} {ver}` + Open/Show, Edit Badge…, … | `CloneDetail.swift:38-79` | V | Same hierarchy; WinUI type scale mapping in Stage 2 |
| W20 | Update/missing notice boxes (3 + 1 variants, exact copy) | `CloneDetail.swift:81-105` | V | Same copy; `InfoBar`-style control |
| W21 | Usage tiles (Extra disk/Data/Memory/CPU, captions, loading + live polling) | `CloneDetail.swift:109-134,234-253` | V | Same 4 tiles; 2 s polling while running |
| W22 | Details card (Mode/Original/Data folder/Created/Last updated) | `CloneDetail.swift:138-171` | V | Same rows; Explorer reveal (W); date/relative formats via OS APIs |
| W23 | Sign-in links card + toggle (identity only) | `CloneDetail.swift:182-206`, `AppModel.swift:143-180` | V | Same card; Windows protocol-handler backend (W) |
| W24 | Maintenance card (Clean/Rebuild/Delete with exact copy) | `CloneDetail.swift:210-230` | V | Same rows; Recycle Bin semantics (W) |
| W25 | Empty / no-selection / no-match / load-error states | `MainWindow.swift:71-85`, `EmptyState.swift` | V | Same states and copy |
| W26 | Status never color-alone (symbol + text) | `Components.swift:54-96,320-358` | V | Binding design rule for Windows controls |

## 4. New Clone flow

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W27 | Pick step (700×680): title, autofocus search, segmented All/Works great/Recently used, bordered list, capsules + tooltips, counts, Cancel/Continue, drop zone + dashed overlay | `AppPickerView.swift`, `NewCloneSheet.swift:28` | V | Same layout/steps; WinUI dialog + `Segmented` |
| W28 | Details step (640×440): 128 px live preview, Label/Badge/Color/Open-after, Advanced mode picker, support line, tips, Back/Cancel/Create | `QuickSheetView.swift` | V | Same; `ContentDialog`-style sheet |
| W29 | Validation (label/badge/duplicate messages verbatim) | `NewCloneForm.swift:56-62`, `Naming.swift` | V | Same strings (already in `Mitosis.Domain.CloneName`) |
| W30 | Full-copy cross-drive alert with size | `QuickSheetView.swift:60-65` | V | Same; Windows drive/volume check (W) |
| W31 | Working step (live engine step labels + progress + hints) | `WorkingViews.swift:5-30`, `NewCloneModel.swift:185-192` | V | Same; step labels reused from engine |
| W32 | Done step (mascot, ready copy, Show in Finder/Done) | `WorkingViews.swift:32-64` | V | Same; Explorer reveal (W) |
| W33 | Failed step + compatibility-mode retry (identity launch-check only) | `NewCloneSheet.swift:20-26`, `NewCloneModel.swift:200-211` | V | Same rule |
| W34 | Advanced: data-folder location + extra launch args (per spec §6) | Not in source | D | Decision: out of scope or Stage 8+ |
| W35 | Badge editor sheet (96 px preview, 2-char badge, swatches, Save gating, quit-first routing) | `BadgeEditing.swift:49-113` | V | Same; `ColorSwatches` equivalent |

## 5. Settings, help, onboarding, menus

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W36 | General tab (tray toggle, update check + status + Check Now, auto-update + caption, location + reveal, CLI install) | `SettingsView.swift:21-135` | V | Same; tray/Task Scheduler/`%PATH%` equivalents (W) |
| W37 | Advanced tab (default mode, Show Tips Again) | `SettingsView.swift:137-173` | V | Same |
| W38 | Help: 9 topics, symbols, poses, exact pages + 2 action buttons | `HelpView.swift` | V | Same content; Windows paths in Permissions page (W) |
| W39 | Onboarding: 3 pages, dots, Later/Choose an App, non-dismissable | `Onboarding.swift` | V | Same |
| W40 | Failure sheet (oops, message, Details, Copy, GitHub prefill) | `Components.swift:128-186`, `ErrorReport.swift` | V | Same; `~`-style path redaction for `%USERPROFILE%` (W) |
| W41 | Alerts: quit-first / delete / clean / message (exact copy) | `MainWindow.swift:124-178` | V | Same copy; `ContentDialog` (W) |
| W42 | Tray menu items incl. per-clone Running suffix + update item | `MenuBarContent.swift` | V | Same items in tray flyout (W) |
| W43 | `⌘I` inspector | Help copy only; no implementation | D | Decision: remove stale copy; no inspector in v1 |
| W44 | Shortcuts ⌘N, ⌘?, Esc/Enter, ⌫, Return, Q | Grep-verified (see audit §6.6) | V | Ctrl mapping (W); F1 for help |

## 6. Engine and CLI features

| # | Item | macOS evidence | Status | Windows requirement / adaptation |
|---|---|---|---|---|
| W45 | Discovery scan + support capsules | `AppCatalog.swift`, `ModeDecider.swift` | V | **W**: Start Menu/registry/Uninstall-key scan; same capsule UI |
| W46 | `doctor` report | `MitosisCLI.swift` (Doctor) | V | Same report shape; Windows checks |
| W47 | Verified create + launch check + rollback | `CloneWorkflow.swift`, `CloneBuilder.swift` | V | Same semantics; Windows launch/liveness APIs (W) |
| W48 | Identity vs compatibility modes + override | `ModeDecider.swift`, `CloneRequest.modeOverride` | V | Same model; Windows mechanism TBD (stages 3–8) |
| W49 | Refresh (manual/auto/agent), skip-running, defer-mid-update, restore-on-failure | `CloneMaintenance.swift`, `AutoRefreshAgent.swift`, `AppModel.swift:186-218` | V | Same; Task Scheduler instead of launchd (W) |
| W50 | Badge restyle (name-preserving) | `CloneMaintenance.restyle` | V | Same |
| W51 | Delete to Trash (+optional data), relink | `CloneMaintenance.delete/relink` | V | Recycle Bin semantics (W) |
| W52 | Stats + live usage + cache cleaning | `StatsCollector.swift` | V | Same tiles; ETW/PDH/process APIs (W) |
| W53 | Self-update (checksum + signature, in-place or command fallback) | `SelfUpdater.swift`, `UpdateChecker.swift`, `UpdateActions.swift` | V | **W**: MSIX/Store/sideload strategy decision (Stage 12+) |
| W54 | Link routing (toggle/status/chooser/restore) | `LinkRouter*.swift`, `MitosisRouter/main.swift` | V | Same UX; protocol-handler registration (W) |
| W55 | CLI: list/doctor/clone/stats/clean/refresh/delete/open/links | `MitosisCLI.swift` | V | Same surface (Stage 10); `mitosis.exe` + PATH |
| W56 | Workspaces (spec: hidden until 0.3) | Not in source | D | Out of scope for v1 |
| W57 | Light mode for sandboxed apps (README roadmap) | Not in source | D | Out of scope for v1 |

## 7. Cross-cutting rules (apply to every Windows screen)

| # | Item | Status | Note |
|---|---|---|---|
| W58 | Exact user-facing strings reused verbatim (unless OS noun differs: Dock→taskbar, Finder→File Explorer, Trash→Recycle Bin, macOS→Windows) | V | String catalog to be extracted in Stage 2 |
| W59 | Status = symbol + color + text, never color alone | V | `Components.swift:54` comment is the rule |
| W60 | One mascot moment per view; decorative images hidden from AT | V | Same; reuse 480 px resources |
| W61 | Destructive actions: explicit choice, data option, Recycle Bin never permanent delete; failed creates removed (no user data yet) | V | Engine safeguard parity (stages 5–8) |
| W62 | Verify-before-success (launch check) + automatic restore | V | Same |
| W63 | Reduce Motion honored on every animation | V | Same (`prefers-reduced-motion` equivalent) |
| W64 | Keyboard operability + screen-reader names on all controls | V+P | Names verified; tab order/focus visuals need Stage 2 spec |
| W65 | Rendered pixel values, hover/pressed/focus visuals, Contrast Themes, Narrator order | R | Must be specified in Stage 2 with running-app screenshots |
| W66 | Screenshots still needed before pixel-matching: settings tabs, tray menu, onboarding, help pages, empty state, alerts, badge editor, working/failed, update banner | R | Existing: main light/dark, pick, details, done |

## Coverage check (Task 6)
Top-level screens/states in source and their matrix rows: main window (W01,W25) · sidebar
(W11–W18) · detail (W19–W26) · pick/details/working/done/failed (W27–W33) · badge editor
(W35) · settings general/advanced (W36–W37) · help 9 topics (W38) · onboarding (W39) ·
failure/prompts/banner (W40–W41,W08) · tray (W04,W42) · engine/CLI (W45–W55). All covered.

## Stage 2 shell implementation notes

Only rows genuinely built in the Stage 2 shell are listed. Everything else keeps its
Stage 1 status. All shell data is explicit preview data; engine actions show honest
"not available in this preview" dialogs and change nothing.

- W01: window 1060×720, title `Mitosis`. Subtitle count shown in sidebar header instead
  of the title bar (provisional); minimum size not enforced (gap).
- W08: update `InfoBar` with Update Now (placeholder) + dismiss. Implemented.
- W11–W14: groups, rows (initial-letter tiles stand in for badged icons), search,
  New Clone button with tooltip. Implemented on preview data.
- W15: Ctrl+F focuses search. Implemented.
- W17: Enter opens (placeholder), Delete requests delete (placeholder). Implemented.
- W18: row context menu with the 10 macOS items; right-click selects the row first.
  Enablement mirrors macOS (Refresh/EditBadge/Show-Original disabled when the original
  is missing; Find App only then). Implemented.
- W19–W24: header, notices, 4 usage tiles, details card, links card (toggle holds local
  state only), maintenance card. Static preview values; no live polling. Implemented.
- W25: empty state (mascot art reused) + search-no-match state. Implemented.
- W26: symbol + text statuses throughout. Implemented.
- W41: engine actions route to explanatory dialogs (full alert flows need the engine).
  Partial.
- W44: Ctrl+N, Ctrl+F, Enter, Delete, Esc/Enter in dialogs. No menu bar yet. Partial.
- Gaps remaining in shell scope: W02, W03, W04, W05 (menu bar), W06, W07, W09, W10, W16,
  W27–W29 UI, W30–W35, W36–W40, W42, W43, W45–W57 (engine/CLI).
- Provisional visuals pending runtime comparison: font-size mapping, tile/pill glyph
  choices, sidebar width 240, initial-letter tiles, card/border/material rendering,
  focus/hover/pressed treatments, Contrast Themes, Narrator order.

## Stage 3 discovery implementation notes

Genuinely implemented and tested in Stage 3 (read-only; no cloning):

- W45 (discovery half): uninstall-registration views (HKCU/HKLM-64/HKLM-32) +
  packaged-app enumeration; deterministic identity, merge, ordering; per-record
  warnings. Support capsules and the picker UI remain pending (no wizard yet).
- W13: sidebar search now filters real records (name + publisher), still
  case/diacritic-insensitive.
- W25: loading (`Finding your apps…`), empty + retry, error + retry states added
  for discovery; explicit `Show preview instead` fallback, never mixed.
- W11: discovered apps group by initial letter; clone By-App grouping unchanged
  (no clones exist before the engine).
- New app detail page: publisher, version, scope, sources, compatibility status
  with reasons, and an honest not-yet-available notice.
- Not implemented: W46–W57 engine rows, wizard rows W27–W35, capsules UI,
  doctor/CLI, tray, Settings/Help/onboarding.

## Stage 4 profile implementation notes

Genuinely implemented and tested in Stage 4 (metadata only; no provisioning):

- Profile records (`Id`, `AppId`, name, badge, `Planned` default, UTC timestamps,
  schema 1) with shared naming validation and explicit state transitions.
- `ICloneProfileRepository` + versioned JSON store: atomic saves, corruption-safe
  loads, duplicate rejection, deterministic ordering, serialized writes.
- App detail profiles section (loading / list / `No profiles yet.` / error + retry);
  `Unavailable` sidebar section with dedicated detail panel; orphans preserved,
  never auto-deleted.
- `Planned` is always labeled `Planned — metadata only, not a working clone.`
- Not implemented: provisioning, `Ready`/`Failed` UI transitions, wizard, badge
  editor, deletion workflows, schema migration, encrypted storage.

## Stage 5 wizard implementation notes

Genuinely implemented and tested in Stage 5 (metadata only; single `Planned` save):

- Five states (Pick → Details → Review → Saving → Done / Failed) with back
  navigation, state preservation, cancellation without records, and single-instance
  window hosting from the main window (Ctrl+N / New Clone button).
- Compatibility gating: `Unsupported` blocks with reasons; `NeedsReview` requires an
  explicit acknowledgement checkbox; `Unknown` records with an explanatory notice;
  `Supported` flows directly (reachable only with a validated strategy).
- Name/badge/color validation mirrors macOS copy and domain rules; duplicates
  rejected per repository rules; double submission guarded plus repository backstop.
- Completion is honest: `{name} planned.` plus `The profile was saved. The
  application has not been copied, launched, or modified — provisioning arrives in
  a later stage.` The record stays `Planned`; failures keep the wizard open with
  retry. After save, profiles refresh and the app is selected.
- macOS elements deliberately deferred: filter segments, Open-after toggle, mode
  picker, full-copy alert, launch check, compatibility-mode retry, drag-drop onto
  the wizard, standalone badge editor.
- Visual runtime comparison still needed: wizard window against
  `new-clone-pick/details/done.png`, focus order, swatch rings, dialog spacing.

## Stage 6 provisioning framework notes

Genuinely implemented and tested in Stage 6 (planning only; execution disabled):

- Strategy contract (`Id`/`Version`/`SupportsExecution`/`IsEnabled`/prerequisites/
  `AppliesTo`), isolation evidence model (8 flags, unknown stays unknown),
  deterministic planner (immutable plans, refusal with reasons), execution gate
  (revalidation, missing-executor refusal, no profile mutation), empty production
  catalog by evidence.
- Destination safety: GUID-hex destinations under one absolute normalized root;
  traversal/absolute/overlong/invalid rejected; collisions refused; no directory
  creation during planning (verified); registry paths never destinations.
- Wizard review step states provisioning availability honestly (empty catalog →
  planned-profile-only notice).

## Stage 7 research and availability notes

Genuinely done in Stage 7 (research + explanation; execution still disabled):

- Five candidates researched against vendor docs (2026-10-16): profile flags
  eligible for review; portable copies need review; Store/MSIX, generic copies,
  and vendor channels rejected with reasons.
- `StrategyCandidate` records live apart from strategies by type; the planner
  cannot select them and the production catalog stays empty.
- App detail now lists missing prerequisites (`Still needed`); review keeps the
  planned-profile-only notice.
- Not implemented: evidence gathering, any executing strategy, strategy
  selection UI beyond honest status display, per-app verification.

## Stage 8 evidence framework notes

Genuinely done in Stage 8 (framework only; nothing verified live):

- Evidence bundle schema (10 isolation dimensions, 5 statuses, sources with
  dates, method/steps, approval kept separate, integrity hash) with deterministic
  validation and evaluation; atomic JSON store apart from profiles.
- App detail Compatibility card shows a counts-only verification summary
  (`X of 10 dimensions recorded · Y verified · provisioning unavailable`),
  defaulting to `No verification recorded for this application.`
- Not implemented: live verification runs, evidence gathering automation,
  counsel review, owner approvals, strategy registration.

## Stage 12 parity and accessibility audit (evidence-based statuses)

`Implemented and verified` = code + automated tests and/or launch; behavior
confirmed without pixels. `Implemented, verification pending` = built and
launchable, needs the protocol in `docs/WINDOWS-UI-VERIFICATION.md`. No
pixel-level parity is claimed anywhere.

- Shell/windows: W01 Implemented, verification pending (size/title built;
  subtitle adapted to sidebar). W02, W03, W04 Missing. W05 Partially (commands
  without menu bar). W06 Missing (no dock; window drop also absent). W07
  Partially (close-quit only; no tray mode). W08 Implemented, verification
  pending. W09 Not applicable (in-memory composition has no load-failure mode).
  W10 Missing.
- Sidebar: W11, W12, W14 Implemented, verification pending. W13 Implemented and
  verified (filter unit tests). W15 Implemented, verification pending (Ctrl+F
  wired, keypress unobserved). W16 Partially (All/Running not rendered; D8 open).
  W17 Implemented, verification pending (handlers code-reviewed). W18
  Implemented, verification pending (10-item menu with macOS enablement).
- Detail: W19–W24 Implemented, verification pending (static preview values).
  W25 Implemented and verified (states + ViewModel tests). W26 Implemented and
  verified (symbol+text rule in code review).
- Wizard: W27, W28 Implemented, verification pending. W29 Implemented and
  verified (validation unit tests). W30 Not applicable (no copying in
  metadata stage). W31–W33 Partially (honest saving step; adapted done copy;
  retry without compat-mode path). W34 Missing by design. W35 Partially
  (inline badge editing; no standalone sheet).
- Settings/help/menus: W36–W39 Missing. W40 Partially (wizard failed state;
  no standalone report sheet). W41 Partially (honest placeholder dialogs).
  W42 Missing. W43 Not applicable (macOS lacks it too; correctly omitted).
  W44 Partially (Ctrl+N/Ctrl+F/Enter/Delete/Esc; no menu bar or F1).
  W58 Partially (copy reused modulo OS nouns; no string catalog yet).
  W60 Implemented, verification pending (one mascot per view; decorative art
  hidden from the tree). W61 Partially (Recycle Bin copy; engine safeguards
  not applicable). W62 Partially (no launch check; honesty rule kept via
  planned-profile copy). W63 Implemented and verified (no custom animations
  exist, so Reduce Motion holds vacuously). W64 Partially (names, tab order,
  keyboard paths present; Narrator pass pending). W65, W66 Missing (no
  screenshots or screen-reader runs in this environment).
- Engine/CLI: W45 Implemented and verified (discovery + smoke counts). W46–W51
  Missing. W52 Partially (static tiles). W53 Missing. W54 Partially (toggle UI
  only). W55 Missing. W56, W57 Not applicable (future roadmap items).

Stage 12 fixes: decorative art hidden from the accessibility tree (macOS parity),
swatch accessible names, wizard initial focus, font-family cleanup. No safety
model changes; catalog empty; gates closed.
- Not implemented: any executing strategy, copying, launching, deletion, recovery,
  reparse-point handling, evidence gathering, strategy selection UI.

## Stage 13 settings/help/onboarding implementation notes

Implemented in Stage 13 with real persistence (`Application/Preferences` +
`Infrastructure/Preferences`). Every flag has a real, testable effect; the
welcome flow shows once. Nothing here is pixel-verified yet — visual runtime
comparison stays pending (W66, S10–S12 in `docs/WINDOWS-UI-VERIFICATION.md`).

- W02 (Help window): Implemented, verification pending. Secondary `Window` at
  820×580 sized in code before activation; nine topics with macOS titles, poses,
  and symbols; content is Windows-honest (planned-only copy, engine gaps named
  plainly). Opened via F1 and the sidebar Help button. Provisional glyph mapping
  for SF Symbols (same provisional standing as the Stage 2 mapping).
- W03 (Settings window): Implemented, verification pending. 540-wide window with
  Pivot General/Advanced tabs matching macOS metrics. Deliberately bounded:
  General ▸ Storage shows the application's own data folder (plain text + Show
  in File Explorer via `Launcher`); Advanced ▸ Show Tips Again matches macOS
  semantics (persists `ShowTips=true`, `Onboarded=false`; reappears next launch;
  window-local disabled state like `SettingsView.swift:141`).
- W36 (Settings General): Partially by design. The macOS rows without any
  Windows effect remain absent: menu-bar/tray toggle, update check + Check Now +
  status, auto-update clones, clones location, and the command-line installer
  (no Windows CLI exists). Toggles are not shown before they can do something.
- W37 (Settings Advanced): Partially by design. Show Tips Again implemented
  against the real preference store; the default-clone-mode picker is deferred
  because no Windows default mode is honored by any code path yet.
- W38 (Help content): Implemented, verification pending. Nine topics in macOS
  order (Start…Cli) with pose assets re-linked from the original brand files;
  two action buttons map to `ms-settings:privacy` and the repository issue
  chooser. Copy marks everything not-actually-implemented explicitly.
- W39 (Onboarding): Implemented, verification pending. 580×460 non-dismissable
  window (closing cancelled until `Finished`), 3 pages with dots, Back/Next,
  Later / Choose an App; both completion actions persist only the onboarded
  flag (merged, never clobbering saved flags) and raise a handoff that may open
  the existing New Clone wizard — no profiles are created, nothing is launched.
  Persistence failure stays honest: the window does not release, shows the
  store's message inline, and allows retry.
- W44 (keyboard): Partially. F1 → Help and Ctrl+, → Settings added alongside
  Ctrl+N/Ctrl+F/Enter/Delete/Esc. Still no menu bar.
- W58/W64/W66: unchanged standing (no string catalog; Narrator pass and
  screenshots still pending for the three new windows).

Persistence guarantee (tested): versioned `preferences.json`
(`{schemaVersion, onboarded, showTips}`), atomic same-directory replace,
serialized writes; a missing file yields macOS-mirroring defaults
(`Onboarded=false, ShowTips=true`, MitosisRootApp.swift:11) without creating
anything; damaged or wrong-schema stores raise actionable exceptions and are
never silently overwritten; unreadable stores in the main window are treated
as onboarded so a damaged file cannot re-show the welcome on every launch.
266/266 tests green in this environment (210 legacy + 56 new).
Deferred beyond this stage: menu-bar/tray toggle, update-check controls,
auto-update clones, CLI installer, default-clone-mode picker, onboarding
animations/pixels.

## Stage 14 window lifecycle, navigation, and keyboard notes

Bounded set taken after auditing the macOS window code again: minimum sizes and
fixed sheet geometry, secondary-window focus handoff, keyboard routing, and a
tray/menu decision. Window position/size RESTORATION was evaluated and is
deferred (rationale below). No engine, provisioning, or persistence-semantics
changes; the preference store is untouched.

- W01 (main window sizing): Implemented, verification pending. Launch size
  1060×720 preserved (MitosisRootApp.swift:24) and the macOS floor 820×520
  (MitosisRootApp.swift:17) is now enforced natively by the overlapped presenter
  (`PreferredMinimumWidth/Height`), so the sidebar and detail panel cannot be
  squeezed below the reference proportions. Values live in
  `Mitosis.Windows.Platform.Windowing.WindowSizes` with unit-tested validation
  (`Mitosis.Windows.Platform.Tests.WindowSizeTests`).
- W02 (help window sizing): Implemented, verification pending. Same macOS
  metrics kept (820×580 default) plus the macOS floor 700×500
  (HelpView.swift:18) enforced by the presenter.
- W03 (settings window sizing): Implemented, verification pending. Adaptation
  recorded: macOS fixes the window via `.frame(width: 540)` (SettingsView.swift:16);
  Windows locks `PreferredMinimumWidth = PreferredMaximumWidth = 540`, leaving
  height to the per-tab `ResizeClient`, which behaves like the macOS
  per-content height (470/260 regions). Maximize stays available because
  height is adjustable; subject to the pending visual pass (W66).
- W39/W27 (sheet geometry): Implemented, verification pending. The onboarding
  flow and the New Clone wizard are fixed-size (580×460 and 700×680) with
  maximize disabled, mirroring macOS sheet behavior (spec §2) — previously
  they were freely resizable windows, which macOS never had.
- W10 (window position/size restoration): **Deferred by decision.** The macOS
  reference contains no restoration code — `.defaultSize` only, no
  `frameAutosaveName` or `setFrame` anywhere (matrix even marked the behavior
  itself as "verify on Mac"). Any Windows persistence would therefore be
  unvalidated platform behavior on both sides. A later stage may add
  restoration built on the existing preference store only if the macOS
  behavior is first confirmed (restore on/off, work-area validation rules,
  debounce policy); until then the app keeps safe defaults.
- W05/W44 (keyboard): Implemented and verified where testable. The main-window
  key routing now goes through `Mitosis.Windows.Platform.Input.MainWindowShortcuts`
  (unit-tested in `MainWindowShortcutTests`): F1 → Help (Windows adaptation of
  macOS ⌘? — rationale: F1 is the standard Windows help key; ⌘? has no direct
  equivalent), Ctrl+, → Settings (same key shape as macOS ⌘,), Ctrl+F → search
  focus (macOS audit found no ⌘F binding; kept deliberate so search stays one
  keystroke away). Ctrl+N intentionally stays on the sidebar
  `KeyboardAccelerator` and is not routed — a second path would double-invoke
  the command on one keypress (test pins this). Esc/Enter remain wizard-scoped;
  the main window routes nothing on Escape (dialog/OS semantics untouched).
  Actual keypress observation on a live desktop remains pending (S17).
- W07 (lifecycle): Unchanged — quit on last window close (matches the macOS
  default where the menu-bar extra is off, `menuBarKey = false`).
- Secondary-window lifecycle (Help/Settings/wizard/onboarding): Implemented,
  verification pending. Single-instance reuse with focus restore already
  existed; closing any secondary window now re-activates the main window when
  it is visible (AppKit's close-returns-focus default), stale references are
  nulled in every `Closed` handler before reuse, and ViewModels are
  constructed fresh per opening (no cross-opening state carryover).
- W04/W42 (menu bar / tray): **Deferred by decision** — evidence and
  constraints recorded in `docs/WINDOWS-PORT.md` (§ Tray and menu decision).
  No tray icon or menu commands were added; the macOS behaviors that depend on
  the menu bar (close-to-tray lifetime, quick-open clone list) are called out
  as follow-up requirements, not faked.

Tests added this stage: 12 (6 keyboard routing, 6 size validation) on top of
Stage 13's 56; total 278/278 green. Desktop checks (resize below floors,
repeat-open focus, sheet refuse-resize, restore behavior) are pending and
listed as scenarios S14–S18 in `docs/WINDOWS-UI-VERIFICATION.md`.

## Stage 15 main-window interaction notes

Scope chosen after re-auditing the macOS sidebar/detail interaction model
(`CloneList.swift`, `AppModel.swift`, `MainWindow.swift:71-85`) against the
Windows shell. Every change below is main-window interaction only — no engine,
provisioning, or persistence-semantics changes.

- W13/W25 (search filtering and no-match state): Implemented and verified
  (unit tests). Three corrections against the macOS reference:
  1. Selection now survives search filtering. macOS clears a selection only
     when the entry leaves the model (`AppModel.swift:112` — the guard is
     "entry(selection) == nil", not "hidden by search"); the Windows view
     model previously cleared the selection (and the whole detail page with
     it) as soon as a query filtered the selected row, which in production
     meant selecting an app and then searching always destroyed the detail
     page. The list views now keep selection state out of the view model
     (OneWay `SelectedItem` + explicit selection-changed forwarding), an
     existence-based guard clears the selection only on genuine removals,
     and the view model re-announces selections after every refresh so a
     highlighted row reappears when the query clears. A former test pinning
     the wrong behavior was corrected, not weakened — its replacement pins
     the macOS evidence.
  2. The no-results view is gated on an active query. macOS's
     `ContentUnavailableView.search` (`MainWindow.swift:80`) only exists for
     typed text; the Windows sidebar previously showed "No clones match your
     search." even on a fresh empty library — overlapping the honest
     discovery empty state. `HasNoResults` now requires
     `HasActiveQuery`, and the discovery empty state yields while searching.
     The copy mirrors macOS's system strings honestly: "No results for
     "{query}"" plus a spelling hint.
  3. Esc clears the search query, mirroring macOS `.searchable`
     (CloneList.swift:26) via `MainViewModel.ClearSearch()`; an empty box's
     Esc passes through untouched. No other keys are touched.
- Selection preservation across discovery refresh (keptAppId / keptOrphanId)
  was already implemented and tested in earlier stages and is unchanged.
- W18 (context menu): unchanged — 10 items matching `CloneMenu`
  availability semantics; one minor documented adaptation: macOS *hides*
  "Find App…" for clones whose original exists while Windows *disables* it
  (same information, consistent menu shape). Left as-is intentionally.
- W16 (All-Clones/Running sidebar rows): still open (spec drift on the macOS
  side too — the enum exists but macOS does not render it either); unchanged.

Deferred from the candidate set, with reasons:
- Auto-selecting the first visible entry on load (macOS AppModel.swift:85,113):
  macOS auto-selects the first *clone*; Windows production has no clone list
  yet (sidebar clones are explicit preview data), and auto-selecting a
  discovered app would open a detail page macOS never opens on launch.
  Revisit when the real clone list lands.
- macOS column-collapse behavior (`entries.isEmpty → detail-only`, 
  MainWindow.swift:62-65): a layout interaction coupled to real clone counts;
  deferred with W16.
- Search focus/text selection, arrow-key list navigation beyond WinUI list
  defaults: no macOS source behavior found for these (audit §6 marks them
  P+R/unverifiable); WinUI's native defaults stand.

Tests: 5 new + 1 corrected (kept as strong as before) — total 283/283 green.
Manual checks (selection persistence while typing, Esc, no-results copy,
highlight restore) are pending and listed as S19–S21 in
`docs/WINDOWS-UI-VERIFICATION.md`.

## Stage 16 keyboard/focus/accessibility notes

Scope chosen from a control-inventory audit of every window against the
macOS source. Four concrete defects fixed; no new features, no styling
changes, no new dependencies.

- W05 (update banner commands): Implemented and verified where testable.
  The Windows InfoBar renders a close button, but nothing handled it — the
  banner only listened to pushes from the view model, so closing it did
  nothing persistent and any later PropertyChanged reopened it. macOS's
  banner dismisses for the session (`updates.dismissed = true`,
  MainWindow.swift:94-103, accessibility label "Hide update message"), and
  `DismissBannerCommand` + the `DismissBanner_HidesIt` test already
  implemented exactly that logic. The close button is now wired to it. One
  limitation recorded: the InfoBar close button keeps its platform "Close"
  accessible name; the macOS wording ("Hide update message") would need a
  retemplate, deferred as not worth the fragility.
- W29/W60/W64 (announcements): Implemented, verification pending. Dynamic
  status/error text now carries `AutomationProperties.LiveSetting="Polite"`:
  the wizard's name-validation error, the picker's unsupported-app reason,
  and the failed-step message; the settings and welcome windows' error
  InfoBars carry it too. Declarative XAML only; actual Narrator behavior is
  a pending manual check (S24).
- Screen-reader-selected colors (spec §6.4 selection ring; macOS
  `.isSelected` trait, BadgeEditing.swift:43): Implemented, verification
  pending. The eight swatch buttons expose `AutomationProperties.ItemStatus`
  "Selected"/"Not selected", updated with the ring in code (no visual
  change). The ring alone was invisible to assistive tech.
- W44 (focus on open): Implemented, verification pending. The wizard had
  first-activation focus since Stage 12; Help (topic list), Settings
  (General tab item), and the welcome flow (Next / closing actions) now
  receive initial keyboard focus too, using the same `OnFirstActivated`
  pattern. AppKit gives a presented sheet a first responder; WinUI does
  not, so the focus target is explicit.
- Documented findings needing no code change: keyboard context-menu access
  on a focused sidebar row is the standard WinUI ContextFlyout contract
  (Shift+F10 / Apps key; pending manual check, S24); Ctrl+N remains
  single-path (Stage 14 test pins it); search Esc-clear landed in Stage 15;
  decorative art and badge tiles are already excluded from the
  accessibility tree (Stage 12); ListView selection/status/group states
  come from the platform; no redundant labels were added.
- Deferred: session "Hide until next launch" wording for the InfoBar close
  button (retemplate risk); a keyboard-accessible full drop-zone alternative
  to "drag an app here" (macOS also lacks a button for drag-only entry — the
  picker button above it is the keyboard path); Narrator/visual passes for
  everything above (S22–S24, W66 standing).

Tests: none added this stage — every change is declarative XAML or
view-layer wiring; the one behavior change with testable logic (banner
dismissal) was already covered by `DismissBanner_HidesIt`. Existing suite
283/283 green; Release build clean (the pre-existing CS0105 duplicate-using
warning in `NewCloneWizardTests.cs` remains, untouched).
