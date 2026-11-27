# Windows UI verification protocol (Stage 12, extended in Stages 13–27)

Repeatable manual verification for the WinUI 3 shell. Automated tests cover
ViewModel logic only — nothing here substitutes for eyes and keyboard.
Screenshots and desktop observations were first captured in Stage 27
(`docs/evidence/windows-ui/`); a scenario is **pending** unless §2 records
an observed result, and the Stage 27 pass record in §5 names what remains
unverified.

## 1. Environment record (fill per session)

Windows version/build, display scale (100/150/200%), app commit, window size,
theme (Light/Dark), contrast mode (None/High-Contrast Black-White + themes),
screenshot filename, scenario ID, macOS reference screenshot, differences with
severity (cosmetic/minor/major), and whether each difference is an intentional
native adaptation or a parity defect.

## 2. Scenarios

- S1: main window at 1060×720 (reference size) vs `clone-light.png` /
  `clone-dark.png`: sidebar width/proportions, header hierarchy, tile grid,
  details card alignment, maintenance rows.
- S2: main window at 820×520 minimum and at 1600×900 wide: wrapping, overflow,
  truncation, empty-area behavior.
  - **Observed 2026-11-27 (dark theme, 100% scale):** 820×520 and 1600×900 were
    captured (`main-820x520-dark.png`, `main-1600x900-dark.png`). At 820 the
    app-detail column was wider than the viewport and shifted right; the fix wraps
    each detail page in a full-width `Grid` with
    `HorizontalContentAlignment="Stretch"` and disables horizontal scrolling. After
    the fix the 780-wide panel sits at x=12 in a 1060-wide window and fills a
    narrower viewport without clipping (`app-detail-fixed-dark.png`,
    `app-detail-820x520-dark.png`).
- S3: sidebar search (matches, no-match state), selection highlight, group
  headers, bottom button, right-click menu with disabled items on a
  missing-original entry.
  - **Observed 2026-11-27:** a matching query filters the list and keeps the
    detail page (`search-match-dark.png`); a query that matches nothing shows
    `No results for "zzz"` with the spelling hint while the selected app stays on
    the right (`search-no-match-dark.png`); clearing the box restores the full
    grouped list with the same row highlighted (`search-esc-cleared-dark.png`).
    The right-click menu and group headers were not re-checked in this pass.
- S4: clone detail (preview mode): header, status pill, notice variants, tiles,
  details rows, links card toggle, maintenance rows.
- S5: app detail: metadata rows, compatibility reasons, `Still needed` list,
  profiles section states, orphan panel via an orphaned record.
- S6: wizard Pick (list, search, unsupported dimming + reason), Details
  (prefill, validation messages, swatches, ack checkbox), Review, Saving,
  Done, Failed + Try Again; Esc at each step; Enter advancement; Back preservation.
- S7: loading (cold start), empty library, discovery error + retry, profiles
  error + retry, update banner with Update Now + dismiss.
- S8: keyboard-only pass: Tab order main window → sidebar → detail; arrows +
  Enter/Delete in lists; Ctrl+N, Ctrl+F, Esc; focus visibility at every stop;
  focus restoration after dialogs close.
- S9: themes and scaling: Light, Dark, each High-Contrast theme; 100/150/200%
  text scaling watching for clipping (tile values, pills, captions, buttons);
  125/150/200% display scale.
  - **Observed 2026-11-27 (100% scale):** the Light theme was captured for the
    main window at 1060×720, the Help window on Permissions, and Settings ▸
    General (`light-main-1060x720.png`, `light-help-permissions.png`,
    `light-settings-general.png`). Text, accents, selection fill, and the focus
    ring stayed legible against the light surfaces; no hardcoded-colour defect
    appeared. Dark was already covered by the Stage 27 set. High-contrast themes
    and 100/150/200% text or display scaling remain unverified because they need
    OS accessibility and display changes.
- S10: Help window (W02/W38): opens at 820×580 via F1 and the sidebar Help
  button; starts on "Getting started"; each of the 9 topics changes title,
  paragraphs, mascot pose, and button area; only Permissions and Troubleshooting
  show an action button; "Open Privacy & Security" launches the Windows privacy
  page (`ms-settings:privacy`); "Report a Problem…" opens the repository issue
  chooser in a browser; topic list, scroll, Esc, and title-bar close work;
  compare against `help-*.png` references (pixel parity pending — W66).
  - **Observed 2026-11-27:** F1 and the sidebar Help button each opened one
    Help window focused on the topic list; Down moved the selection from
    "Getting started" to "Which apps work" and the page followed
    (`help-dark.png`). The two action buttons were clicked in the later unlocked
    pass recorded in §5.4: Settings opened at Privacy & security and the issue
    chooser opened in the default browser.
- S11: Settings window (W03/W36/W37): opens at 540 wide via Ctrl+, and the
  sidebar Settings button; General tab shows Storage (data folder path shown as
  text; "Show in File Explorer" opens the folder, creating it if needed);
  Advanced tab shows "Show Tips Again" enabled; clicking it persists, flips the
  label to "Tips Are On", and disables for the window; reopening the window
  re-enables it (macOS window-local state); after clicking, the next launch
  shows the welcome flow again; no other toggles exist by design (update/tray/
  CLI/mode rows deferred); compare against `settings-*.png` (pending — W66).
  - **Observed 2026-11-27:** Ctrl+, opened one 540-wide Settings window
    (`settings-dark.png`); switching tabs changed the height while the width
    stayed 540 (a resize request to 760 wide was refused); keyboard focus starts
    on the General tab and Tab reaches "Show in File Explorer". A real update
    check reported 0.2.3 (`settings-update-check-dark.png`). The store reveal and
    Show Tips Again actions were not re-run.
- S12: Onboarding (W39): first launch after deleting `preferences.json` shows
  the 580×460 welcome; pages advance via Next/dots, Back works, Back is
  unavailable on page 1; title-bar close, Esc, and Alt+F4 do nothing while
  unfinished; "Later" closes and is not re-shown on next launch; "Choose an
  App" closes, then opens the existing New Clone wizard; completing with a
  locked/unwritable preference store keeps the window open with the inline
  store error and allows retry without losing the choice; removing
  `preferences.json` again re-shows the flow (deterministic).
  - **Observed 2026-11-27:** with `Onboarded` reset, the 580×460 welcome flow drew all
    three pages with Next/Back/Later and focus on Next; page 1 had previously
    been placed in the wrong grid column and now composes correctly
    (`onboarding-page1..3-dark.png`). "Choose an App" and the locked-store retry
    path were not re-run.
- S13: keyboard reach over the new windows (W44): F1 opens Help from the main
  window; Ctrl+, opens Settings; both sidebar buttons carry accessible names
  and tooltips; Tab order inside Help (topics → page → button) and Settings
  (tabs → rows) is sensible with visible focus.
  - **Observed 2026-11-27:** the Help cycle is three stops — topic list item →
    page pane → action button → back to the topic list item — read from
    `FocusedElement` after each Tab (§5.4).
- S14: main-window minimum size (W01): launch at 1060×720; try to drag the
  edge below 820×520 — the window stops at the floor; snap layouts and
  Aero Snap respect it; 820×520 shows usable sidebar + detail proportions
  (reference: sidebar 210/240); large sizes (1600×900+) keep the layout
  composed; maximize on a single display ok.
  - **Observed 2026-11-27:** 1060×720, 820×520 (the code floor), and 1600×900 were
    captured; the layout composes at all three and the detail column no longer
    clips. An explicit drag below the floor was not repeated this pass.
- S15: repeated Settings/Help opening (W02/W03/W05/W44): press F1, then F1
  again while Help is focused — no second Help window appears and the existing
  one stays put; press Ctrl+, while Settings is already open — same single
  window; close Help via the title bar and press F1 again — a fresh Help
  window opens with the "Getting started" selection; close Settings and reopen
  — buttons reset per window state (no stale "Tips Are On"); closing Help or
  Settings returns keyboard focus to the main window; Help and Settings open
  simultaneously without conflict.
  - **Observed 2026-11-27:** F1 twice and Ctrl+, twice produced exactly one
    window each; F1 while Help had focus did nothing; closing Help or Settings
    returned keyboard focus to the selected sidebar row. Help and Settings were
    also open together without conflict.
- S16: fixed-size windows (W27/W39 adaptation): the welcome window cannot be
  resized or maximized (580×460 fixed); the New Clone wizard stays 700×680
  through every step with maximize disabled; Esc/Enter still navigate within
  the wizard; the Settings window keeps a fixed width (540) while its height
  follows the General/Advanced tab (tab switch re-sizes height).
  - **Observed 2026-11-27:** the wizard refused a resize away from 700×680 and is
    non-maximizable; Settings kept width 540 while height followed the tab; Help
    refused 600×400 and honored 900×700 (700×500 floor). The welcome window is
    fixed by code and was not resized.
- S17: keyboard shortcuts and collisions (W05/W44): F1 and Ctrl+, work from
  the main window with focus anywhere in it; Ctrl+N opens the wizard exactly
  once per press (no double window from accelerator + router); Ctrl+F focuses
  the search box; typing F/comma alone never triggers anything; Esc on the
  main window does nothing unexpected; F1 while Settings/Help is the active
  window has no route (documented: shortcuts are main-window scoped; revisit
  in a later stage when a global command surface exists).
  - **Observed 2026-11-27:** F1, Ctrl+,, Ctrl+F, Ctrl+N, and Esc (wizard and
    search) all worked with the list focused; Ctrl+F and Ctrl+, also worked while
    the search box had focus. F1 did nothing while Settings was active, matching
    the documented main-window scope. Ctrl+N produced one window per press.
- S18: restoration deferral record (W10): no persistence is expected — resize
  and move the main window, close and relaunch: the window opens at the
  1060×720 default again. This is the accepted, documented behavior until a
  later stage implements restoration with Mac-verified semantics.
- S19: selection survives search (W13/W25): select an app (or a preview
  clone), then type a query that filters its row out — the detail page keeps
  showing the selection, no empty-state flash; clear the query (or press Esc
  in the search box) — the hidden row comes back already highlighted.
  - **Observed 2026-11-27:** selecting Android Studio, typing a query that
    filtered it out, and pressing Esc kept the same detail page throughout, with
    the row highlighted again after the query cleared (`search-match-dark.png`,
    `search-esc-cleared-dark.png`); no empty-state flash. Closing the wizard no
    longer resets the detail page to the first app
    (`selection-after-wizard-cancel-dark.png`).
- S20: no-results honesty (W25): with no query and an empty discovery
  result, the sidebar shows the applications empty state only (never the
  "no results" phrase); type a query matching nothing — the sidebar shows
  'No results for "{query}"' with the spelling hint, and the empty state
  yields; clear — empty state returns.
- S21: Esc in search (W13): typing text then pressing Esc clears the query
  (focus treatment to be recorded during the manual pass: macOS keeps focus
  in the field); pressing Esc in an empty search box does nothing; Enter and
  list keys behave as before; Ctrl+F focuses the field and another Ctrl+F on
  a selected row leaves selection intact.
  - **Observed 2026-11-27:** Esc now clears a non-empty query while focus stays
    in the box (`search-esc-cleared-dark.png`). The earlier failure was
    `AutoSuggestBox` consuming Escape in `KeyDown`, so the handler moved to
    `PreviewKeyDown`.
- S22: update banner dismissal (W05/P+R): click (or Tab to) the InfoBar close
  button and press Space — the banner hides and does not reappear during the
  session (macOS "Hide until next launch", MainWindow.swift:94-103); relaunch
  brings a new banner back; the banner's close button is reachable by Tab.
  - **Observed 2026-11-27:** a real check produced the 0.2.3 banner
    (`update-banner-dark.png`); its Close button hid it and it stayed hidden for
    the rest of the session (`update-banner-dismissed-dark.png`).
- S23: initial focus of secondary windows: opening Help puts keyboard focus
  on the topic list; Settings focuses the General tab item; the welcome
  window focuses Next; the wizard focuses its search (since Stage 12). Tab
  from each starting point traverses all controls in a predictable order.
  - **Observed 2026-11-27 (UI Automation focus):** Help → topic-list item
    "Getting started"; Settings → tab "General"; wizard → "Search apps" edit;
    welcome → "Next" button. The first attempt failed because `Focus()` ran
    inside `Activated` before loading; all four windows now defer focus to a
    low-priority dispatcher callback.
- S24: screen-reader announcements (W64): with Narrator on, a failed
  validation in the wizard name field speaks the error; selecting an
  unsupported app in the picker speaks its reason; a failed profile save
  speaks the failure; the settings and welcome error bars speak their
  messages; the selected swatch is announced as "Selected" after its color
  name (macOS isButton/isSelected traits, BadgeEditing.swift:43) and the
  others as "Not selected"; keyboard context-menu access on a focused
  sidebar row (Shift+F10 / the Apps key) opens the 10-item clone menu.
- S25: single instance (Stage 23): launch Mitosis twice from Start/taskbar —
  the second launch exits and the existing window is raised (check foreground
  focus and that no second taskbar entry appears); minimize the primary, launch
  again — the minimized window is restored and focused; close the main window
  while the welcome window is open — the whole process exits (no orphaned
  welcome window); force-kill the process, then launch again — a fresh primary
  starts; after each step confirm the log contains `app.instance`,
  `app.secondary-exit` (second launch), and `app.stop` (graceful close). Repeat
  with a different Windows user or session if available.
  - **Observed 2026-11-26 (four scenarios, process/log inspection):** closing the
    main window with Help open, Settings open, both open in either order, and
    with no secondary window all ended the process within 6 s and logged
    `app.stop`; no process or orphaned window remained. This exposed and closed
    the gap where `Application.Exit()` sometimes skipped a child window and left
    the process alive. Visual focus and minimized restore remain pending.
  - **Observed 2026-11-27:** a second launch exited with code 0 and left one
    process; with the primary minimized, the second launch restored and
    foregrounded it (checked with `IsIconic`/`GetForegroundWindow`). Closing the
    main window with no secondary window open exited cleanly and logged
    `app.stop`. A second Windows user was not available.
- S26: update availability (Stage 25): open Settings ▸ General ▸ Updates — the
  version line shows the running version without `+<sha>`; the status starts with
  “Check GitHub Releases…”; press Check for Updates with network available — the
  status becomes either “up to date” or “Mitosis X is available”, and only in the
  latter case does a View Release link appear and the main-window banner open.
  While the check is running the button label changes to “Checking…” and the
  button is disabled. Close the banner — it stays hidden for the session. Retry
  with the process network blocked or a broken proxy — the status is
  “Couldn't reach GitHub. Check your internet connection.”, with no link and no
  banner. Confirm no profile store or log entry is created by the check itself
  and that the app exits cleanly.
  - **Observed 2026-11-26 (partial, UIAutomation + process/log inspection, no
    screenshot/Narrator):** version showed `0.2.2`; the check button disabled
    itself while the check ran (its visible label switches to “Checking…”; the
    accessible name stays fixed); a real check reported `0.2.3` and revealed
    View Release; the main banner showed “Mitosis 0.2.3 is available.”; its close
    button dismissed it; a process-level blocked proxy produced the honest
    connection failure with no banner or link; closing the main window with
    Settings still open exited cleanly with no error log entries. Clicking View
    Release, Narrator order, pixels/contrast, and a second network remain pending.

## 3. Accessibility checks

Names on all icon-only controls (swatches verified in code; re-check with
Inspect); roles/values on lists, toggles, progress indicators; no duplicate
announcements from decorative tiles/mascots (hidden via `AccessibilityView=Raw`
— confirm silence, including the Help page mascot and every onboarding pose);
page dots announced with `Page N of M` labels; disabled menu items announced as
disabled; validation errors exposed as text (confirm reading order); focus
never lost after dialog close.

## 4. What this protocol does not cover

Engine behavior, real isolation, Narrator speech quality (needs a screen-reader
pass), touch, IME, right-to-left layout, and packaging/Store behavior.

## 5. Stage 27 pass record (2026-11-27)

Environment: Windows 11 build 10.0.26300, 100% display scale, dark theme,
`Mitosis 0.2.2+2faa989`, main window 1060×720 (also 820×520 and 1600×900),
Release build with 0 warnings and 497/497 tests green. Screenshots live in
`docs/evidence/windows-ui/`. Focus and selection observations come from UI
Automation (control type and accessible name of the focused element), not from
screenshots. Keyboard input used SendKeys to the foreground window; the app was
launched from the publish tree with `DOTNET_ROOT` set.

### 5.1 Defects found and fixed in this pass

| # | Observed defect | Fix | Evidence |
|---|---|---|---|
| 1 | App-detail/clone-detail/orphan panel shifted right and clipped at 820 wide | Full-width wrapper `Grid`, `HorizontalContentAlignment=Stretch`, horizontal scroll disabled | `app-detail-fixed-dark.png`, `app-detail-820x520-dark.png` |
| 2 | Detail page jumped to a framework-chosen app when a query replaced the list; closing the wizard reset it to the first app | Forward `SelectionChanged` only while the list holds focus; keep the selected app across reloads | `search-match-dark.png`, `selection-after-wizard-cancel-dark.png` |
| 3 | Esc did not clear the search query | `PreviewKeyDown` instead of `KeyDown` | `search-esc-cleared-dark.png` |
| 4 | Wizard ignored Esc/Enter while the pick search had focus | Same `PreviewKeyDown` change | live: Esc closed, Enter advanced |
| 5 | Settings/Help/wizard/welcome opened with no keyboard focus | Set focus after first layout at low dispatcher priority | focus: General tab, Getting started, Search apps, Next |
| 6 | Startup could fail with `RPC_E_WRONG_THREAD` (`0x8001010E`) from a view-model continuation on a pool thread | Removed `ConfigureAwait(false)` from the Windows view models; added thread-affinity tests | `ViewModelThreadAffinityTests` |
| 7 | Profiles empty/error states were not announced when the loading or error flag changed | Raise `ShowProfilesEmpty`/`ShowProfilesError` notifications | `app-detail-profiles-empty-dark.png`; ViewModel test |
| 8 | Publisher line lost the space before the version separator | `" · "` separator | test `PublisherLine_SeparatesPublisherAndVersion` |
| 9 | Sidebar rows announced the type or type-plus-status instead of the row label | `ToString()` overrides on the row models | live UIA names, e.g. `Android Studio, Not yet assessed` |
| 10 | Onboarding page 1 drew its content in the wrong grid column | `Grid.Column` correction | `onboarding-page1..3-dark.png` |
| 11 | Packaged-app registrations with an empty display name appeared in the list | Skip empty names in the Appx source | `WindowsDiscoveryTests` |

The profiles empty-state screenshot and the removal flow were re-checked live;
no app was copied, launched, or modified to produce any of this evidence.

### 5.2 What is still not verified

- Narrator speech (no screen-reader audio capture on this machine): S24 stays
  pending even though names, roles, and states were inspected with UI
  Automation.
- High-contrast themes and 150/200% text scaling; the capture passes used the
  default dark theme and one light-theme pass at 100% scale
  (`light-*.png`).
- Clean Windows 10/11 VMs, a standard (non-admin) account, and a second
  Windows user/session. S4 clone detail was not opened (preview data only).
- Touch, IME, right-to-left layout, and drag-below-floor for the main window.
- A published Windows artifact remains macOS-only upstream, so "View Release"
  click-through was not exercised.

### 5.3 Follow-up pass: dialogs, support actions, and wizard paths (2026-11-27)

Same machine, theme, and scale as §5. This pass ran against the working tree's
publish output (`artifacts\live-check`, Release, 0 warnings, 497/497) so the
fixed build could be checked before packaging. Eight screenshots were added.

- Profile edit (app detail ▸ Edit profile): the dialog opens with keyboard focus
  in `Name`. Saving an empty name keeps the dialog open with "Give the clone a
  name."; a name that matches another profile reports "You already have Android
  Studio Work."; a valid rename plus a different badge color persisted to
  `profiles.json` and re-rendered in the profile list; Esc closed without
  changing the store. UI Automation exposed the error as a `StatusBar` whose
  accessible name is the message (`profile-edit-duplicate-dark.png`).
- **Defect 12:** after a failed save the caret stayed on Save, so a keyboard user
  had to find the invalid field, and the error bar was drawn but never spoken.
  The save path now focuses the first invalid field and selects its text, and
  raises an automation notification carrying the message (`InfoBar` draws its
  message but has no live-region event). Re-checked live: focus returns to
  `Name`, the dialog stays open, Esc still cancels
  (`profile-edit-error-focused-dark.png`).
- Remove (app detail ▸ Remove profile): the default button is Cancel — Enter and
  Esc both closed the dialog with the record intact; Remove deleted only the
  metadata record, the profile list refreshed, and `profiles.json` lost exactly
  that entry (`profile-remove-confirm-dark.png`).
- Settings ▸ General ▸ Diagnostics: Copy diagnostics placed the documented
  report on the clipboard — version, runtime, OS, architecture, Windows App SDK,
  data and log folders, writability, and the explicit "not recorded" note — and
  showed the confirmation text (`settings-diagnostics-copied-dark.png`). Show log
  folder in File Explorer opened the real `%LOCALAPPDATA%\Mitosis\Logs` folder.
- Settings ▸ Advanced: Show Tips Again became a disabled "Tips Are On" and wrote
  `Onboarded: false` to `preferences.json`; preferences were restored from a
  backup afterwards (`settings-tips-reset-dark.png`).
- Settings height follows the tab content by design (General 740 client px,
  Advanced 300), so the window changed from 540×779 to 540×339 including the
  title bar when switching tabs. Recorded as intended macOS parity, not a
  defect.
- Wizard (Ctrl+N): Continue stays disabled until an app is picked; the name field
  validates inline and disabled Continue for empty, leading-dot, and duplicate
  names; the review step restates application, name, badge, and compatibility
  with "This saves a planned profile record only."; Save Profile reached the done
  state with Show Profile/Close; Show Profile opened the app detail with the new
  record listed as *Planned*; removing that temporary profile left the two
  pre-existing records untouched (`wizard-validation-empty-dark.png`,
  `wizard-validation-leading-dot-dark.png`, `wizard-review-dark.png`; the done
  state reuses the Stage 27 `wizard-done-dark.png`).

Not exercised: the acknowledgement checkbox, which appears only for apps
assessed *Needs review* — on this machine every app is *Not yet assessed* or
*Not supported* — and Narrator speech for the new notification. The automation
tree exposes the message text and the focus move, but no audio was captured.

### 5.4 Post-packaging pass: Help action buttons (2026-11-27)

The first attempt ran while the desktop was locked, so `SendKeys` and foreground
focus were denied; the check was repeated after the session was unlocked, against the
packaged build (`Mitosis-windows-0.2.2-x64-ddc1711.zip`, started from the publish
tree).

- Permissions ▸ **Open Privacy & Security** opened Windows Settings at **Privacy &
  security** (window titled "Settings", with Location, Camera, Microphone, Speech,
  and Diagnostics entries). The Settings window was closed afterwards.
- Troubleshooting ▸ **Report a Problem…** started the default browser (Brave was not
  running) at `github.com/deadhearth01/Mitosis/issues/new/choose`; the window title
  "Choose an Issue Template" and the address-bar value both confirmed the target. The
  browser instance was closed afterwards.
- Tab order inside Help is a three-stop cycle: topic list item ▸ page pane ▸ action
  button ▸ back to the topic list item. The mascot zoom/scroll buttons are not in the
  cycle (UI Automation `FocusedElement` after each Tab).
- The action button's accessible name ends with U+2026 ("Report a Problem…"). An
  exact match on a period does not find it, which matters when scripting UI
  Automation.

Screenshots: `help-permissions-dark.png`, `help-troubleshooting-dark.png`.

### 5.5 Light-theme pass (2026-11-27)

The WinUI shell follows the system app theme, so this pass set
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
to `1` for the duration, launched the packaged build
(`Mitosis-windows-0.2.2-x64-ddc1711.zip`), and restored the previous value
(`0`) afterwards; the value was re-read to confirm the restore. Only the
app-theme value was touched — the taskbar/system value was left alone — and the
app was closed before the restore.

Windows and Settings were opened with UI Automation `Invoke` on the sidebar
buttons (`Help`, `Settings`) rather than `SendKeys`, because the desktop was in
active use and a synthetic keystroke had to be avoided.

Observed:

- Main window at 1060×720: light background, dark text, blue accent on the
  selected nav item and the primary button; rows, group headers, and the
  empty-state mascot all read correctly.
- Help on Permissions: selected topic row uses a light fill with a visible focus
  ring; body text, the "Open Privacy & Security" button, and the mascot pose have
  adequate contrast.
- Settings ▸ General at 540×779: section titles, captions, the two "Show in File
  Explorer" buttons, "Copy diagnostics", and the update row are legible; the
  selected tab underline uses the light-theme accent (`#1A7FE0`).

Screenshots: `light-main-1060x720.png`, `light-help-permissions.png`,
`light-settings-general.png`.

Not exercised: high-contrast themes and 150/200% text scaling (both need
OS accessibility settings), and non-100% display scaling.
### 5.6 Modern chrome, app icons, and rescaled-text pass (2026-11-27)

Build: `Release|x64` from `Mitosis.sln`, suite 522/522, 0 warnings. Captures were
taken at 1180x960 on the development machine (Windows 11 25H2, 100% display
scale). The window was resized through UI Automation and the pointer was parked
away from the window before each capture so hover states are not recorded.

**Visual state observed**

- `modern-main-dark.png`, `modern-main-light.png`: unified title bar with the
  sidebar tint running up behind the caption area (`MicaBackdrop` `BaseAlt`
  where the compositor supports it), element-theme caption glyphs, no vertical
  seam between sidebar and content, grouped application rows with real
  application icons (Android Studio, AntiGravity, Brave, Bun), letter-badge
  fallback where `DisplayIcon` is missing or unreadable, status dots
  (`Not yet assessed` grey, `Not supported` red), a filled rounded selection
  row, and the macOS-style accent-text `New Clone` action at the bottom.
- `modern-appdetail-dark.png`, `modern-appdetail-light.png`: 92 px header tile
  with the application's own icon, 32 px bold name, status pill with wash plus
  supporting publisher/version line, accent `New Clone` action, then three
  cards (Details, Compatibility, Profiles). Details rows are label-left /
  value-right with hairline separators. The Compatibility card shows the
  assessment line, the reason, and the still-needed bullet list. Profiles
  renders the two `Planned` records with `Edit`/`Remove` and the honest
  "metadata only, not a working clone" subtitle.
- Light theme: sidebar tint is now clearly distinct from the content surface
  (`#E8E8EE` at 0.92 over Mica vs `#FBFBFD` content); dark theme keeps the
  sidebar lighter than the content, matching the macOS reference.
- Control corners use a 6 px `ControlCornerRadius` override in both non-high-
  contrast themes; high contrast keeps the system radius.

**Application icons**

Registry `DisplayIcon` values are parsed by `AppIconReference` (quoted paths,
trailing `,index`, `.exe`/`.ico`/`.dll` only, no relative or UNC paths) and read
read-only as a `StorageItemThumbnail` by `AppIconLoader`. The bitmap is created
on the UI-thread dispatcher; creating it on the pool thread had raised
`RPC_E_WRONG_THREAD`. Eight `AppIconReference` unit tests cover the real Brave
registry value, quoting, index stripping, a comma inside a directory name, the
allow-list, and relative/rooted/traversal/blank rejection. Live check: every
row whose registry value resolves to an existing file rendered a thumbnail, and
every row without one kept its letter badge.

**Text scaling (150% and 200%)**

`textscale-150-main.png`, `textscale-150-appdetail.png`,
`textscale-200-main.png`, `textscale-200-appdetail.png`. At both steps the
sidebar rows, detail header, status pill, Details rows, and card paragraphs
reflow without clipping or overlap; at 200% the fixed 240 px sidebar truncates
row names to an ellipsis (full name available in the row tooltip) and the
Compatibility copy wraps onto more lines. `TextScaleFactor` was restored to 100
and re-read after the pass.

**High contrast (pass, High Contrast Black)**

`hc-main.png`, `hc-appdetail.png` were captured with Windows High Contrast
enabled (`SystemParametersInfo(SPI_GETHIGHCONTRAST)` reported the on flag set
and the "High Contrast Black" scheme name). Pixel check: the page and sidebar
surfaces read `#202020`, which is exactly the scheme's `Window=32 32 32`, so the
`HighContrast` theme dictionary does take effect and no Mica translucency
remains. Text renders in the scheme `WindowText` (white), the selected row uses
the scheme highlight (aqua with black text), card borders stay visible, the
status dots keep enough contrast against the dark fill, and the caption glyphs
are white over the dark title bar. The gold frame around the window is the
scheme's active-window border, not app chrome.

Correction recorded: the first pass read this same capture as "the app kept its
light surfaces" and logged a high-contrast defect. Sampling the pixels showed
that reading was wrong - the window was dark all along and the tokens did
switch. High Contrast White and the other schemes were not run.

Operational note: high contrast is a system-wide setting. The pass enabled it,
captured, and restored it; one earlier attempt failed mid-script and left the
setting on for several minutes before being restored and verified (flags back
to 126, on-bit clear, dark theme and 100% text scale intact). No further
system-wide accessibility toggles were run in this session.

**Not exercised in this pass:** Narrator speech, a second Windows user, a clean
VM, non-100% display scaling, and the high-contrast fix itself.
### 5.7 System-component filter, smoke harness, and plan dry run (2026-11-28)

Build: `Release|x64` from `Mitosis.sln`, suite 585/585, 0 warnings. The desktop
pass ran at 1180x960 on the development machine (Windows 11 25H2, 100% scale,
dark theme). Actions were driven through named UI Automation elements only
(`Invoke`, `SelectionItem`, `Toggle`); no synthetic clicks or keystrokes were
sent while the machine was in use.

**Application list filter**

- The sidebar hides entries that are not applications a person opens. Windows
  already flags some of them (`SystemComponent=1`, dropped during discovery),
  and a documented name rule set (`SystemComponentClassifier`) catches
  installer-authored entries that carry no flag: runtimes, redistributables,
  media extensions, versioned driver packages, service installers, Local
  Experience Packs, and Microsoft 365 side-by-side names.
- Observed: 59 rows and the footer hint "34 system components hidden".
  Settings > Advanced > Show system components switched on listed
  `App Installer`, `AV1 Video Extension`, `AVC Encoder Video Extension`,
  `English (United Kingdom) Local Experience Pack`,
  `Microsoft .NET Runtime - 8.0.33 (x64)`, `Microsoft .NET Runtime - 9.0.22
  (x64)`, and four `Microsoft Visual C++ Redistributable` rows. Switching it off
  restored the application-only list, and `%LOCALAPPDATA%\Mitosis\preferences.json`
  read back `"ShowSystemComponents": false`.
- Captures: `smoke-main.png`, `smoke-system-components-shown.png`,
  `smoke-system-components-hidden.png`.
- Scope: the filter is a display rule. Every entry stays discovered and keeps
  its identity; selection, the detail page, and profile lookup are unchanged,
  and the toggle restores the full list without a restart.
- Untested: a machine whose installed entries differ from this one, and the
  "only system components found" empty state (unreachable here). Both are
  covered by ViewModel tests.

**Repeatable smoke harness**

- `windows/tools/ui-smoke.ps1` replaces the throwaway `%TEMP%` scripts. It
  accepts `-Scenario main-window|system-filter|plan-preview|all`, resizes the
  window, acts only through named elements, parks the pointer and focus before
  each capture, writes a JSON summary to `%TEMP%\mitosis-ui-smoke\`, and exits
  non-zero if any check fails.
- Live results: `main-window` 5/5 (window size, rows present, no component names
  in the default list, footer hint, capture); `system-filter` 6/6 including the
  restored preference; `plan-preview` 7/7.
- Limitation observed and handled: with the workstation locked, DWM stops
  composing, so `PrintWindow` returns a blank frame and `CopyFromScreen` fails
  with "The handle is invalid". The harness now reports the locked desktop as a
  failed capture check with that explanation while the behavioural checks still
  run. The three committed filter captures come from the unlocked pass.

**Provisioning plan dry run**

- A profile row now offers "Plan". `PlanPreviewViewModel` runs the real
  `OperationPlanner` over the selected application, the profile, the
  assessment, and `ProductionCatalog.Strategies`, then formats the result.
  Planning is a pure function and the preview never calls the gate, executor,
  or launcher.
- Live: the dialog opens with the banner "Preview only - nothing has been
  executed.", states "This plan is refused. No part of it can run in this
  build.", lists `No provisioning strategy is available` as the blocking
  issue, and shows "No steps can be planned...". It closes cleanly and the
  profile record is unchanged; no `instances` directory is created under
  `%LOCALAPPDATA%\Mitosis`.
- Unit tests cover what the live app cannot reach yet: a planning-only strategy
  (never lists steps), an executable strategy with complete evidence (four
  steps, two expected outputs, correct target path), unproven evidence, a
  profile that belongs to another application, and the guarantee that no
  directory is created.
- Not captured: a screenshot of the dialog. The behavioural check ran while
  the desktop was locked, and screenshots need an unlocked session; this is
  recorded as a gap rather than claimed.

**Still not exercised:** Narrator speech, High Contrast White and the remaining
schemes, a second Windows user, a clean VM, non-100% display scaling, and any
real clone.
