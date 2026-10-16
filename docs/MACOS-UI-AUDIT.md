# macOS UI audit (Stage 1)

Evidence-based inventory of the macOS Mitosis implementation as of `main` #`fee3a77`
(observed from the `WindowsVersion` branch; no macOS file was modified).
Every claim cites a source path. Values are quoted only when explicitly defined in
code or assets; otherwise the item is marked `needs runtime verification`.

Conventions: `MitosisUI` = `Sources/MitosisUI`, `Core` = `Sources/MitosisCore`.

---

## 1. App entry and windows

### 1.1 Entry point — `MitosisUI/MitosisRootApp.swift`, `Sources/MitosisApp/main.swift`
- `main.swift` is 2 lines: `import MitosisUI` + `MitosisRootApp.main()`.
- `MitosisRootApp` scenes (`MitosisRootApp.swift:14-43`):
  - `Window("Mitosis", id: "main")` → `MainWindow`, `.frame(minWidth: 820, minHeight: 520)`,
    `.defaultSize(width: 1060, height: 720)`, `.tint(Brand.accent)`. Drop handler for app URLs.
  - `Window("Mitosis Help", id: "help")` → `HelpView`, `.defaultSize(width: 820, height: 580)`.
  - `Settings` → `SettingsView`.
  - `MenuBarExtra` (optional, `Prefs.showMenuBarExtra`) → `MenuBarContent`, label = `MenuBarIcon.image`.
- Commands (`MitosisRootApp.swift:46-64`): Help group replaced with `Mitosis Help` (⌘?), divider,
  `Report a Problem…`, `Mitosis on GitHub`; New-Item group replaced with `New Clone…` (⌘N).
- `AppDelegate` (`MitosisRootApp.swift:67-78`): Dock-drop routing to `model.handleDrop`;
  quit-on-last-window-closed unless the menu-bar extra is on.
- Load failure (`MitosisRootApp.swift:80-90`): if `AppServices.live()` throws, model shows
  `"Mitosis is missing some of its files. Reinstall it, then try again. (\(error))"`.

### 1.2 Main window — `MitosisUI/Views/MainWindow.swift`
- `NavigationSplitView`, title `"Mitosis"`, subtitle `"N clones"` / `"1 clone"` / `""` (`:113-116`).
- Sidebar column width `min: 210, ideal: 240, max: 320` (`:15`).
- Toolbar `primaryAction`: `New Clone` (`plus` symbol), tooltip `Create a clone (⌘N)` (`:22-31`).
- Update banner pinned top (`safeAreaInset`, `:18, :87-111`): accent `arrow.down.circle.fill`,
  `"Mitosis {version} is available."`, `UpdateActions`, dismiss `xmark` (`Hide until next launch`,
  `Hide update message`); font `.callout`; padding h16/v8; `.bar` background + bottom divider.
- Detail routing (`:71-85`): load error → `ContentUnavailableView("Couldn't load your clones", …)`;
  no entries → `EmptyStateView`; selection → `CloneDetailView` (`.id(e.id)`); empty search →
  `ContentUnavailableView.search`; else `ContentUnavailableView("Select a clone", systemImage:
  "square.on.square", description: "Choose a clone in the sidebar to see its details and stats.")`.
- Columns collapse to detail-only when entries are empty (`:62-65`); reload on app-activate (`:66-68`).
- Sheets: `NewCloneSheet` (item `model.newClone`), `RestyleSheet` (item `model.restyling`),
  `FailureView` (item `model.failure`), `OnboardingView` (non-dismissable, shown once when
  `!onboarded && services != nil`).
- Alerts from `AppPrompt` (`:124-178`, actions in `AppModel`): exact strings in §5.

### 1.3 Settings — `MitosisUI/Views/SettingsView.swift`
- Window width 540, two tabs: General (`gearshape`), Advanced (`slider.horizontal.3`).
- General (height 470, grouped form): `Show Mitosis in the menu bar`; `Check for updates
  automatically` + status row + `Check Now` (disabled while checking); `Clones` section —
  `Update clones automatically` + caption `"When an app updates, Mitosis rebuilds its clones in
  the background, even while Mitosis is closed. Logins and data stay. Clones that are open update
  after you quit them."`; `Location` row (path + `Show in Finder`) + caption `"Clones stay in an
  Applications folder because macOS only sends notifications to apps there."`; `Command line`
  section — `"Use Mitosis from Terminal with the \`mitosis\` command."` + `Install Command` +
  result label (success with `checkmark.circle.fill`, failure with `exclamationmark.triangle.fill`).
- Update status strings (`:85-101`): `"Mitosis {v}"`, `"Checking…"`, `"Mitosis {v} is up to date."`,
  `"Mitosis {version} is available."` + `UpdateActions`, failure message verbatim.
- CLI install messages (`:109-126`): `"This copy of Mitosis doesn't include the command. Reinstall
  Mitosis, then try again."`; `"Installed at …"` / `"Already installed at …"` + optional
  `" If Terminal can't find it, add this line to ~/.zshrc: export PATH="$HOME/.local/bin:$PATH"`.
- Advanced (height 260): `Default mode` picker (`Automatic (recommended)` / `Full clone` /
  `Compatibility mode`) + caption `"Automatic picks the best mode for each app. Full clones get
  their own Dock icon and notifications; compatibility mode works with more apps."`;
  `Tips and the welcome screen` + `Show Tips Again` → `"Tips Are On"` (then disabled).

### 1.4 Menu bar — `MitosisUI/Views/MenuBarContent.swift`, `MenuBarIcon.swift`
- Items: clone list (16 px icons, `"{name}  ·  Running"` suffix when running; `"No clones yet"`
  when empty), divider, conditional `Update Mitosis to {version}`, `New Clone…`, `Open Mitosis`,
  divider, `Quit Mitosis` (Q).
- Icon: hand-drawn 18×18 template Mito glyph (body, seam, wink, feet; `isTemplate`, label "Mitosis").

### 1.5 Help — `MitosisUI/Views/HelpView.swift`
- Split view, sidebar 200/220/280, min window 700×500. 9 topics with SF Symbol + mascot pose:

  | Topic | Symbol | Pose |
  |---|---|---|
  | Getting started | sparkles | guide |
  | Which apps work | checkmark.seal | search |
  | Permissions | lock.shield | key |
  | Sign-in links | link | point |
  | App updates | arrow.triangle.2.circlepath | refresh |
  | Stats and caches | chart.bar | stats |
  | Deleting clones | trash | goodbye |
  | Troubleshooting | wrench.and.screwdriver | oops |
  | Command line | terminal | thinking |

- Pages: 72 px mascot + `.largeTitle.semibold` title, `.body` paragraphs (`lineSpacing: 3`,
  selectable, maxWidth 640); extra buttons: Permissions → `Open Privacy & Security`
  (`x-apple.systempreferences:` URL in `Onboarding.swift:3-8`); Troubleshooting → `Report a
  Problem…` (GitHub `/issues/new/choose`). Full paragraph texts are in code (`:68-125`).
- NOTE (audit finding): the Stats page references an "inspector (⌘I)" that has no implementation
  (see §7.3). The copy is stale relative to the revised design (spec §6 revision note).

### 1.6 Onboarding — `MitosisUI/Views/Onboarding.swift`
- Non-dismissable sheet 580×460. 3 pages (160 px mascot, `.title.semibold` title, `.title3`
  secondary text maxWidth 420): wave/`Welcome to Mitosis`/`Run separate copies of your Mac apps.
  Each copy has its own login, data, and Dock icon.`; key/`Each clone asks for its own
  permissions`/… (+ link-style `Open Privacy & Security` on page 2); split/`Make your first
  clone`/`Pick an app, add a label like Work, and Mitosis checks that the copy starts.`
- 7 px page dots (accent vs secondary 35%, VoiceOver `Page X of 3`); Back / Next (Enter);
  final: Later (Esc) / Choose an App (Enter). Page change animates 0.25s easeInOut + opacity
  transition unless Reduce Motion.

---

## 2. Clone list (sidebar) — `MitosisUI/Views/CloneList.swift`, `MitosisUI/CloneLibrary.swift`

- `.sidebar` list, selection bound to model; `.searchable(placement: .sidebar, prompt: "Search")`.
- Groups by source app (`CloneLibrary.groups`, sorted `localizedStandardCompare`): header =
  14 px app icon + app name; rows filtered per group.
- `CloneRow`: 30 px badged icon (55% opacity if original missing); label part of `App (Label)`
  (falls back to full name); `StatusLabel` caption; vertical padding 2; tooltip + VoiceOver =
  full clone name, value = busy text or status text.
- Bottom `New Clone` button (`plus.circle.fill`, `.callout.medium`, full-width leading,
  padding h16/v12, tooltip `Create a clone (⌘N)`).
- Keys: ⌫ (`onDeleteCommand`) → delete confirm; Return → open.
- `CloneMenu` (context menu + detail More menu): `Open`; `Refresh` (disabled if original
  missing); `Edit Badge…` (disabled if missing); `Find App…` (only if missing); divider;
  `Show in Finder`; `Show Original in Finder` (disabled if missing); `Show Data Folder`;
  `Clean Caches…`; divider; `Delete…` (destructive).
- `SidebarItem` (`.all/.running/.app`) and `CloneLibrary.filter` (search matches name or app,
  case/diacritic-insensitive, sorted) exist; the sidebar renders **only By-App groups** — the
  spec's All-Clones/Running rows are not rendered (see §7.3).

---

## 3. Clone detail — `MitosisUI/Views/CloneDetail.swift`

- Scrollable, sections spaced 28, content maxWidth 780, padding h32/v28.
- Header: 104 px icon (shadow black 12%, radius 8, y 4; 55% if missing); `.largeTitle.bold`
  name (2 lines, selectable); `StatusPill` + secondary `Clone of {app} {version}`; actions
  (`.controlSize(.large)`): `Open`/`Show` (`arrow.up.forward.app`, borderedProminent, h-padding 4,
  disabled when busy/missing), `Edit Badge…` (disabled when busy/missing), `…` menu
  (`More actions`, no indicator).
- `notice`: update-available → orange `NoticeBox` with 3 variants (running + auto-update →
  `Quit and Update`; idle + auto-update → no button, `Mitosis is updating this clone`;
  else `Refresh`): all end `Logins and data stay.`; original-missing → red box + `Find App…`;
  up-to-date → nothing.
- Usage: `SectionTitle`, adaptive grid (min 160). Tiles — Extra disk (`internaldrive`, blue),
  Data (`folder`, teal, `Logins, settings and caches`), Memory (`memorychip`, purple),
  CPU (`cpu`, orange). Values: byte counts; `—` + `Not running` when idle; `1 process`/`N
  processes` or `Measuring…`; `<1%` under 1%; `Right now`. Disk captions: fallback → `A small
  shortcut that opens {app}`; cross-drive → `A full copy: {app} is on another drive`; else
  `The rest is shared with {app}`. Loading value → small spinner. Usage polls every 2 s while
  running (`:245-253`).
- Details card: Mode (`Full clone`/`Compatibility mode` + note `Own login, data, notifications
  and Dock icon` / `Own login and data; uses {app}'s Dock icon`); Original (path + small
  `Show`, disabled if missing); Data folder (path + small `Show`); Created (abbreviated +
  shortened); Last updated (relative, named).
- Sign-in links card (identity mode + non-empty schemes only): `Send sign-in links to the right
  copy` (body medium) + callout explanation + switch (labels hidden). Toggle failure →
  `Couldn't turn on/off sign-in links` sheet.
- Maintenance card: `Clean caches` (`Delete temporary files… Logins and settings stay.`,
  `Clean…`); `Rebuild` (`Rebuild this clone from the current {app}…`, `Refresh`, disabled if
  busy/missing); `Delete clone` (`Move it to the Trash. You choose whether its data goes too.`,
  `Delete…` destructive). Post-clean message: `Caches cleaned` / `Freed {bytes} from {name}.
  Logins and settings stay.` / `{name} had no caches to clean.`
- Mode/support display names (`ErrorReport.swift:41-58`): `Full clone`, `Compatibility mode`;
  `Works great`, `Works with limits`, `Not supported`.

---

## 4. New Clone flow — `Views/NewClone/*`, `NewCloneForm.swift`, `NewCloneModel.swift`

Sheet sizes: pick 700×680; details/working/done/failed 640×440. Step changes animate
easeInOut 0.2 (static if Reduce Motion); dismiss blocked while working.

### 4.1 Pick (`AppPickerView.swift`)
- `New Clone` (title2 semibold) + `Choose the app you want a separate copy of.` (secondary).
- Autofocused `Search apps` rounded field + segmented `Show` picker: All / Works great /
  Recently used (30-day `lastUsed`, newest first).
- Bordered list, hidden separators. `AppRow`: 26 px icon, name (1 line), caption version,
  right `SupportCapsule` (tooltip = reasons joined); unsupported rows 50% opacity, not
  selectable; undecided → small spinner (VoiceOver `Checking`).
- States: `Finding your apps…` (loading); `ContentUnavailableView.search` (no match);
  small spinner while inspecting; red callout drop error; else `TipRow` (search pose,
  `Apps marked Works great get their own login, data, notifications, and Dock icon.`) when tips on.
- Footer: `{shown} apps. ` or `{shown} of {apps} apps. ` + `You can also drop an app here.`
  (callout secondary); Cancel (Esc, role cancel); Continue (Enter, disabled unless a choosable
  app is selected and not inspecting). Double-click/Return on a row continues.
- Drag-drop anywhere (accent 2 px dashed `[6,4]` rounded overlay inset 8 while targeted);
  non-app drops → error string from `NewCloneModel.rejection` (`That's not an app.` / Apple
  message / nil→inspect); unreadable app → `That app is missing its details, so Mitosis can't
  read it.`; unsupported → first reason or `This app can't be cloned yet.`

### 4.2 Details (`QuickSheetView.swift`)
- `New Clone of {app}` (title2 semibold) + 28 px app icon. Left 180 px column: 128 px live
  badged preview (falls back to app icon, hidden from VoiceOver) + headline composed name
  (3 lines, middle-truncated, VoiceOver `Clone name: …`).
- Label field (prompt `Work`, autofocus): inline red `△` message or `Shown in the name, like
  {App} (Work).` caption. Badge field (120 px, prompt = app initial, `One or two letters or
  digits`): max 2 chars, uppercased, whitespace-trimmed; empty → app initial. Color swatches
  (see §6.4). `Open after creating` toggle (default on). `Advanced` disclosure → Mode picker:
  `Automatic (recommended)` / `Full clone` / `Compatibility mode` (defaults from Settings).
- `supportLine`: capsule + first reason (callout secondary). `TipRow` (split pose,
  `The badge helps you spot this copy in the Dock. Mitosis opens it once to check that it
  starts.`) when tips on.
- Back / Cancel (Esc) / Create Clone (Enter, disabled while invalid). Cross-drive → `Make a
  full copy?` alert (`{app} is on a different drive, so this clone will be a full copy and use
  {bytes}.` / `Create Copy` / Cancel).
- Form rules (`NewCloneForm.swift`): suggestions Work → Personal → 2…99 (first unused);
  badge follows label until hand-edited; first unused color per app (else blue);
  `Add a label, like Work.` / CloneName errors verbatim / `You already have {name}.`
  (case-insensitive).

### 4.3 Working / Done / Failed (`WorkingViews.swift`, `NewCloneSheet.swift:16-26`)
- Working: building mascot 140; `Creating {name}…` (title3 semibold); live step label
  (secondary, opacity transition); 280 px linear progress; tertiary caption — launch-check
  step → `Mitosis opens the clone once to make sure it starts.`, else `This usually takes a
  few seconds.` Step labels derive from engine steps (`Copying the app…`, …, `Checking that
  it starts…`, `Launch check…`).
- Done: success mascot 150 (spring 0.35, scale 0.92→1, opacity 0.6→1; static if Reduce
  Motion); `{name} is ready.` (title2 semibold); opened → `It's open now with its own login
  and data. Sign in inside the clone to keep it separate.` else `It has its own login and
  data. Open it from the Mitosis window or your Applications folder.` (maxWidth 420);
  `Show in Finder` / Done (Enter).
- Failed: `FailureView` titled `Couldn't create {name}`; retry `Try Compatibility Mode`
  only when an identity clone failed at launch check (`NewCloneModel.swift:200-206`).

### 4.4 Edit Badge (`BadgeEditing.swift:49-113`)
- Sheet 460 wide: `Edit Badge` (title2 semibold); 96 px live preview; Badge field (56 px,
  prompt `A`, max 2 chars uppercase, `One or two letters or digits`); Color swatches;
  `The name stays {name}, so the clone keeps its logins.` (callout secondary);
  Cancel (Esc) / Save (Enter, disabled if unchanged); after close +300 ms, routes through
  the quit-first flow when running.

---

## 5. Prompts, failures, banners — `MainWindow.swift:118-179`, `Components.swift:128-186`

- `Quit {name} first?`: `{name} is open. Mitosis will ask it to quit, then {rebuild it from
  the current {app}. Your logins and data stay. | update its badge. | move it to the Trash. |
  clean its caches.}` / `Quit and {Refresh|Update Badge|Delete|Clean Caches}` / Cancel.
  Quit waits up to 10 s (`Quitting…` busy); still open → `{name} is still open` / `It may be
  asking to save something. Quit it yourself, then try again.`
- `Delete {name}?`: `The clone goes to the Trash. Its logins and settings stay unless you also
  delete its data.` / `Move to Trash` / `Move to Trash with Data` (destructive) / Cancel.
- `Clean caches for {name}?`: `This deletes temporary files the app can download again. Logins
  and settings stay.` / `Clean Caches` / Cancel.
- Message prompts: custom title + text / OK. Instances: `{app} is missing` (`The original app
  isn't where it was. Use Find App……`), `No data yet` (`{name} hasn't saved anything yet. Open
  it once to create its data folder.`), `That's not an app` (`Drop an app from your Applications
  folder to clone it.`), auto-update failure (`Couldn't update {name} automatically`).
- `FailureView`: oops mascot 104; title2 semibold title; selectable secondary message; `Details`
  disclosure (monospace caption scroll, max height 110); `Copy Details`→`Copied` + `Report on
  GitHub` (link style, callout); Close (Esc, or Enter when no retry) + optional retry (Enter).
  `ErrorReport` (`ErrorReport.swift`): message sanitized (`~` for home), details block
  (`Mitosis: / macOS: / App: / Mode: / Error:`), prefilled `bug_report.yml` issue URL.
- Update banner + `UpdateActions` strings: see §1.2 and `UpdateActions.swift` (`What's New`,
  `Update Now`, `Copy Update Command`→`Copied` (`Paste in Terminal to update`), `Downloading…`,
  `Installing… Mitosis will reopen.`, failure + `Try Again`).

---

## 6. Reusable components and tokens

### 6.1 Layout tokens (`Brand.swift:15-25`)
`Space xs 4 / s 8 / m 16 / l 24 / xl 32` (8 pt rhythm); `cardRadius 12`.

### 6.2 Building blocks (`CloneDetail.swift:258-439`, `Components.swift`)
- `SectionTitle`: title3 semibold + header trait. `Card`: control-background fill, continuous
  r12, separator 60% 0.5 pt border. `StatTile`: 30 px tinted symbol (12% fill, r8), value in
  rounded-title2 semibold with monospaced digits (fixed height 30), callout-medium title,
  2-line caption; padding 16; r14 card; combined VoiceOver element.
- `StatusLabel` (caption, secondary, 1 line): busy → mini spinner + text; Original missing →
  red `exclamationmark.triangle.fill`; Update available → orange `arrow.triangle.2.circlepath`;
  Running → 7 px green filled circle; Not running → 7 px secondary stroked circle.
- `StatusPill` (caption semibold, h9/v4, 13% capsule; busy → accent): same symbols except idle
  uses `moon.zzz.fill` + secondary.
- `SupportCapsule` (caption medium, h7/v2, 12% capsule): full → green `checkmark.circle.fill`;
  limited → orange `exclamationmark.circle.fill`; unsupported → secondary `nosign`.
- `DetailRow` (label secondary / trailing value, h16/v11); `ActionRow` (body-medium title,
  callout secondary text, trailing button red when destructive, h16/v12); `PathText`
  (callout, 1 line middle-truncated, selectable, full-path tooltip, `~` abbreviated);
  `NoticeBox` (title3 tinted symbol + wrapping text + optional button, padding 14, tint 10%
  fill, r12); `TipRow` (44 px mascot + callout, primaryBlue 8% fill, r10, h12/v8).
- `MascotView` (default 120, high interpolation, aspect-fit, VoiceOver-hidden); 17 poses in
  `Mascot` enum; `notyet/clean/sleeping` have resources but no UI reference located (reserved).
- `IconCache`/`AppIconView`/`CloneIconView` (default 32/72), `IconPreview` (256 px render).

### 6.3 Color tokens (exact, from `Brand.swift`, `BRAND.md`, `NewCloneForm.swift`)
- Accent: light `#1A7FE0`, dark `#3B9EF0` (contrast-safe derivative of primary `#41AEF8`;
  applied via `.tint`, never full-window).
- Primary blue `#41AEF8`: illustrations, completed-step glint, TipRow fill (8%) only.
- Badge presets: Blue `#0A84FF`, Green `#30D158`, Orange `#FF9F0A`, Red `#FF453A`,
  Purple `#BF5AF2`, Pink `#FF375F`, Yellow `#FFD60A`, Gray `#8E8E93`.
- Status colors are **system semantic** (red/orange/green/secondary) — no hex in source.
- Surfaces/text: system (`controlBackgroundColor`, `separatorColor`, `.bar`,
  `windowBackground`, primary/secondary/tertiary) — no hex in source.
- BRAND.md web tokens (`#5CC7FD, #1450A8, #9CD1FB, #F5F5F7, #EAF9FB, #021944` + dark variants)
  are web/illustration tokens, not app UI tokens.

### 6.4 Typography (system styles only; SF Pro, Dynamic Type)
largeTitle.bold (detail name) · largeTitle.semibold (help title) · title.semibold (onboarding) ·
title2.semibold · title3.semibold/medium · headline · body/body.medium · callout/callout.medium ·
caption/caption.medium/caption2.bold · monospace caption (failure details) · rounded-title2
semibold + monospaced digits (stat values).

### 6.5 SF Symbols used (22, Windows mapping required — see design spec)
`plus`, `plus.circle.fill`, `exclamationmark.triangle.fill`, `arrow.triangle.2.circlepath`,
`checkmark.circle.fill`, `exclamationmark.circle.fill`, `nosign`, `arrow.up.forward.app`,
`ellipsis`, `xmark`, `moon.zzz.fill`, `circle.fill`, `arrow.down.circle.fill`, `gearshape`,
`slider.horizontal.3`, `square.on.square`, `internaldrive`, `folder`, `memorychip`, `cpu`,
`sparkles`, `checkmark.seal`, `lock.shield`, `link`, `chart.bar`, `trash`,
`wrench.and.screwdriver`, `terminal`.

### 6.6 Motion and states
- Durations: sheet-step 0.2 easeInOut; onboarding page 0.25 easeInOut + opacity; done-view
  spring 0.35 (scale 0.92→1, opacity 0.6→1); working label opacity transition. All static
  under Reduce Motion (`NewCloneSheet.swift:29`, `Onboarding.swift:38,83`, `WorkingViews.swift:62`).
- Loading: spinners (mini/small/linear 280 px), `Finding your apps…`, `Checking` label,
  `Measuring…`, stat-value spinners. Busy texts double as status (`Refreshing…`, `Updating…`,
  `Updating badge…`, `Moving to Trash…`, `Cleaning…`, `Linking…`, `Quitting…`).
- Focus/hover: SwiftUI defaults; no custom states in source → needs runtime verification,
  and Windows equivalents must be specified in Stage 2.

---

## 7. Deltas: spec vs implementation (audit findings)

### 7.1 Implemented as specified (verified in source)
Split-view main window with By-App sidebar; full clone page (header/actions/stat tiles/details/
maintenance); 5-step New Clone flow with validation; badge editor with live preview; General +
Advanced settings; optional menu-bar extra; onboarding; 9-topic help; failure reports with GitHub
prefill; quit-first/delete/clean confirmations; auto-refresh + launchd agent sync; link-routing
toggle; CLI parity for all engine actions; drag-drop onto window and Dock icon.

### 7.2 Partially implemented
- Sidebar sections: `SidebarItem.all/.running` exist and filtering works, but no All-Clones /
  Running rows are rendered (`CloneList.swift` renders groups only).
- Quick-sheet Advanced: mode override only; spec's data-folder location and extra launch
  arguments are absent.
- Search focus shortcut: no explicit ⌘F binding (may be provided by SwiftUI sidebar search —
  unverified at runtime).
- Window state restoration: `.defaultSize` set; position/size restoration not observed in code.

### 7.3 Documented but implementation not located
- `⌘I` inspector (Help ▸ Stats page copy): no inspector view, command, or shortcut exists.
- Workspaces (spec §6: hidden until 0.3): no code.
- Light mode for sandboxed apps (README roadmap): no code.
- Spec's `8 system colors` for badges: implementation uses 8 custom hex presets.

### 7.4 Gaps requiring runtime or visual verification (no Mac available in this environment)
Rendered pixel values beyond code constants; ⌘F focus behavior; window restore behavior;
VoiceOver order/announcements; hover/focus/pressed visuals; Increase Contrast behavior;
menu-bar glyph legibility at 16 px; badge geometry on real icons; pose usage for
`notyet/clean/sleeping`; `ContentUnavailableView` exact visuals; sheet presentation sizes on
smaller displays.

---

## 8. Feature inventory (engine + CLI, from source)

User-visible engine features, all verified in `MitosisCore` + `mitosis/MitosisCLI.swift`
(symbols and literal strings inventoried during this audit): discovery scan
(`/Applications`, `/Applications/Utilities`, `~/Applications`, `/Applications/Setapp`, top +
one-deep, clones/self excluded); `doctor` (mode/support/reasons/frameworks/sandbox/receipt/
entitlements report); verified create with launch check + engine-step progress; identity vs
compatibility modes with override; full-copy cross-drive gate (`--allow-full-copy` / alert);
refresh (single/`--all`/`--outdated --quiet`), auto-refresh on original change (6 h agent +
in-app), skip-while-running, defer-mid-update, failed-refresh restore; badge restyle
(name-preserving); delete to Trash with optional data; relink via Find App…; stats (extra
disk/data/RAM/CPU) with 2 s live polling; cache cleaning (known regenerable folders only);
self-update with checksum + signature verification (in-place or copy-command fallback);
link routing (per-app on/off/status, auto-target when unambiguous, chooser UI, previous-handler
restore); `open`; `list` (`ok` / `update available (v)` / `original missing`); `links`;
`stats`; `clean`. Name rules: `App (Label)`, 60 chars, no `/ :` newline NUL, no leading dot
(already ported to `Mitosis.Domain.CloneName` in Stage 0).

---

## 9. Sources consulted

`MitosisUI/`: `MitosisRootApp.swift`, `AppModel.swift`, `AppServices.swift`, `Background.swift`,
`Brand.swift`, `CloneLibrary.swift`, `CommandLineTool.swift`, `ErrorReport.swift`,
`MenuBarIcon.swift`, `NewCloneForm.swift`, `NewCloneModel.swift`, `RunningTracker.swift`,
`Snapshots.swift`, `UpdateChecker.swift`, `Versioning.swift`,
`Views/{MainWindow, CloneList, CloneDetail, Components, EmptyState, HelpView, MenuBarContent,
Onboarding, SettingsView, UpdateActions, BadgeEditing}.swift`,
`Views/NewClone/{AppPickerView, NewCloneSheet, QuickSheetView, WorkingViews}.swift`,
`Resources/Mascot/` (17 files). `MitosisCore/`: all 25 files (via targeted inventory:
`Models`, `Mitosis`, `MitosisEnvironment`, `Naming`, `Profiles`, `AppInspector`,
`AppCatalog`, `ModeDecider`, `CloneBuilder`, `CloneWorkflow`, `CloneRegistry`,
`CloneMaintenance`, `FileCloner`, `Signer`, `Entitlements`, `InfoPlistEditor`,
`IconRenderer`, `HelperRenamer`, `LaunchConfig`, `Runtime`, `Shell`, `StatsCollector`,
`LinkRouter`, `LinkRouterSetup`, `AutoRefreshAgent`, `SelfUpdater`). Entry points:
`MitosisApp/main.swift`, `mitosis/MitosisCLI.swift`, `LaunchStub/main.swift`,
`MitosisRouter/main.swift`, `MitosisSnapshot` (Snapshots.swift). Tests: 27
`MitosisCoreTests` + 10 `MitosisUITests` files (per-file coverage inventoried).
Assets: `Assets/Brand` (icon master 1024², social 1280×640, Icon Composer source, 20 poses
1024² + hero 1600×1000 + NOTES.md legend), `Assets/Screenshots` (5 PNGs, all inspected
visually). Config/docs: `Package.swift`, `README.md`, `CONTRIBUTING.md`, `LICENSE`,
`docs/specs/2026-10-09-mitosis-design.md` (§5–§8, §15), `docs/brand/BRAND.md`,
`docs/plans/*.md`, `scripts/*.sh`, `.github/workflows/*.yml`, `website/` (structure only).
