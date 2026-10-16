# Windows UI verification protocol (Stage 12)

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

## 3. Accessibility checks

Names on all icon-only controls (swatches verified in code; re-check with
Inspect); roles/values on lists, toggles, progress indicators; no duplicate
announcements from decorative tiles/mascots (hidden via `AccessibilityView=Raw`
— confirm silence); disabled menu items announced as disabled; validation errors
exposed as text (confirm reading order); focus never lost after dialog close.

## 4. What this protocol does not cover

Engine behavior, real isolation, Narrator speech quality (needs a screen-reader
pass), touch, IME, right-to-left layout, and packaging/Store behavior.
