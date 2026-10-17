# Windows UI verification protocol (Stage 12, extended in Stage 13)

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
