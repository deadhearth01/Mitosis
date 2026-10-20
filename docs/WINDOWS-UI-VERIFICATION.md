# Windows UI verification protocol (Stage 12, extended in Stages 13–14)

Repeatable manual verification for the WinUI 3 shell. Automated tests cover
ViewModel logic only — nothing here substitutes for eyes and keyboard.
No screenshots have been captured in this environment; every scenario below is
**pending** until performed.

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
- S3: sidebar search (matches, no-match state), selection highlight, group
  headers, bottom button, right-click menu with disabled items on a
  missing-original entry.
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
- S10: Help window (W02/W38): opens at 820×580 via F1 and the sidebar Help
  button; starts on "Getting started"; each of the 9 topics changes title,
  paragraphs, mascot pose, and button area; only Permissions and Troubleshooting
  show an action button; "Open Privacy & Security" launches the Windows privacy
  page (`ms-settings:privacy`); "Report a Problem…" opens the repository issue
  chooser in a browser; topic list, scroll, Esc, and title-bar close work;
  compare against `help-*.png` references (pixel parity pending — W66).
- S11: Settings window (W03/W36/W37): opens at 540 wide via Ctrl+, and the
  sidebar Settings button; General tab shows Storage (data folder path shown as
  text; "Show in File Explorer" opens the folder, creating it if needed);
  Advanced tab shows "Show Tips Again" enabled; clicking it persists, flips the
  label to "Tips Are On", and disables for the window; reopening the window
  re-enables it (macOS window-local state); after clicking, the next launch
  shows the welcome flow again; no other toggles exist by design (update/tray/
  CLI/mode rows deferred); compare against `settings-*.png` (pending — W66).
- S12: Onboarding (W39): first launch after deleting `preferences.json` shows
  the 580×460 welcome; pages advance via Next/dots, Back works, Back is
  unavailable on page 1; title-bar close, Esc, and Alt+F4 do nothing while
  unfinished; "Later" closes and is not re-shown on next launch; "Choose an
  App" closes, then opens the existing New Clone wizard; completing with a
  locked/unwritable preference store keeps the window open with the inline
  store error and allows retry without losing the choice; removing
  `preferences.json` again re-shows the flow (deterministic).
- S13: keyboard reach over the new windows (W44): F1 opens Help from the main
  window; Ctrl+, opens Settings; both sidebar buttons carry accessible names
  and tooltips; Tab order inside Help (topics → page → button) and Settings
  (tabs → rows) is sensible with visible focus.
- S14: main-window minimum size (W01): launch at 1060×720; try to drag the
  edge below 820×520 — the window stops at the floor; snap layouts and
  Aero Snap respect it; 820×520 shows usable sidebar + detail proportions
  (reference: sidebar 210/240); large sizes (1600×900+) keep the layout
  composed; maximize on a single display ok.
- S15: repeated Settings/Help opening (W02/W03/W05/W44): press F1, then F1
  again while Help is focused — no second Help window appears and the existing
  one stays put; press Ctrl+, while Settings is already open — same single
  window; close Help via the title bar and press F1 again — a fresh Help
  window opens with the "Getting started" selection; close Settings and reopen
  — buttons reset per window state (no stale "Tips Are On"); closing Help or
  Settings returns keyboard focus to the main window; Help and Settings open
  simultaneously without conflict.
- S16: fixed-size windows (W27/W39 adaptation): the welcome window cannot be
  resized or maximized (580×460 fixed); the New Clone wizard stays 700×680
  through every step with maximize disabled; Esc/Enter still navigate within
  the wizard; the Settings window keeps a fixed width (540) while its height
  follows the General/Advanced tab (tab switch re-sizes height).
- S17: keyboard shortcuts and collisions (W05/W44): F1 and Ctrl+, work from
  the main window with focus anywhere in it; Ctrl+N opens the wizard exactly
  once per press (no double window from accelerator + router); Ctrl+F focuses
  the search box; typing F/comma alone never triggers anything; Esc on the
  main window does nothing unexpected; F1 while Settings/Help is the active
  window has no route (documented: shortcuts are main-window scoped; revisit
  in a later stage when a global command surface exists).
- S18: restoration deferral record (W10): no persistence is expected — resize
  and move the main window, close and relaunch: the window opens at the
  1060×720 default again. This is the accepted, documented behavior until a
  later stage implements restoration with Mac-verified semantics.

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
