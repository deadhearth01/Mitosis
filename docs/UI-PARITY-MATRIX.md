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
