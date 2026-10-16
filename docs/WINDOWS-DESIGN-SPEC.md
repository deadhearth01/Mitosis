# Windows design spec — visual fidelity contract (Stage 1)

Reference for the Stage 2 Windows shell. Every value below is either quoted from macOS
source (marked **[S]**) or explicitly marked **[R]** (requires runtime/visual verification)
or **[TBD-2]** (Stage 2 decision, do not guess now). Screenshots inspected:
`Assets/Screenshots/clone-light.png`, `clone-dark.png`, `new-clone-pick.png`,
`new-clone-details.png`, `new-clone-done.png` — all consistent with the code.

## 1. Design principles (from `docs/brand/BRAND.md` + code)

1. Accent sparingly: brand blue tints key controls only; windows, text, and surfaces stay
   system (`Brand.swift:4`, BRAND.md §Color/SwiftUI mapping). Never a blue Vista.
2. Status is always symbol + color + text, never color alone (`Components.swift:54`).
3. 8 pt rhythm; native control spacing wins on conflict (BRAND.md §Native app UI).
4. One mascot moment per view; decorative images hidden from assistive tech (`Brand.swift:46-58`).
5. Short plain copy: what happened, then one next action. Show exact clone names. No jokes in
   errors (BRAND.md §Voice and tone).
6. Destructive actions are explicit, offer the data choice, and use the Recycle Bin —
   never permanent delete. Success is verified (launch check), never assumed.
7. Keyboard-first: every action reachable and shortcut-consistent; screen-reader names on all
   controls; Reduce Motion honored everywhere.

## 2. Window structure **[S]**

- Main: sidebar (210 / 240 ideal / 320, **[S]** `MainWindow.swift:15`) + detail; title `Mitosis`;
  subtitle `N clones`; toolbar primary action `New Clone`; update banner pinned above detail;
  default 1060×720, min 820×520 **[S]**.
- Detail content: sections spaced 28, max width 780, padding 32 h / 28 v **[S]**
  (`CloneDetail.swift:19-30`). Order: header → notice → Usage → Details → Sign-in links →
  Maintenance.
- Help: split 200 / 220 / 280, min 700×500, default 820×580 **[S]**.
- Settings: width 540; General height 470; Advanced height 260 **[S]**.
- Sheets: pick 700×680; details/working/done/failed 640×440; badge editor 460 wide **[S]**.
- Onboarding sheet 580×460, non-dismissable **[S]**.
- Failure sheet width 480; details scroll max height 110 **[S]**.

## 3. Typography **[S]** (names) / **[TBD-2]** (WinUI mapping)

macOS uses SF system styles only — no custom fonts, Dynamic Type respected (`BRAND.md`
§Typography, `Brand.swift` has no font tokens):

| Style | Used for **[S]** |
|---|---|
| largeTitle.bold | Clone detail name (2-line max, selectable) |
| largeTitle.semibold | Help page title |
| title.semibold | Onboarding title |
| title2.semibold | Sheet titles, empty state, done title |
| title3.semibold / .medium | Section-adjacent titles, working title, notice symbols |
| headline | Details-preview clone name |
| body / body.medium | Paragraphs, action-row titles |
| callout / callout.medium | Captions-actions, tiles, banner, tips |
| caption / caption.medium / caption2.bold | Status, versions, field hints, pill text |
| Monospace caption | Failure details |
| Rounded-title2 semibold + monospaced digits | Stat values (fixed height 30) |

**[TBD-2]**: map each row to a WinUI `TextBlock` style/size in Stage 2 from running-app
measurement. Do not assume pt equivalence with Segoe UI Variable.

## 4. Color tokens

Exact **[S]**:

| Token | Light | Dark | Use **[S]** |
|---|---|---|---|
| Accent | `#1A7FE0` | `#3B9EF0` | Control tint (`.tint`), links, dots, busy, drop overlay |
| Primary blue | `#41AEF8` | (`#5CC7FD` illustration token, BRAND.md) | Illustrations, completed-step glint, TipRow fill 8% |
| Badge Blue / Green / Orange / Red / Purple / Pink / Yellow / Gray | `#0A84FF` `#30D158` `#FF9F0A` `#FF453A` `#BF5AF2` `#FF375F` `#FFD60A` `#8E8E93` | same | Badge presets + swatches |

Semantic (no hex in source — use Windows system/status equivalents) **[S]**:
status red/orange/green/secondary; text primary/secondary/tertiary; surfaces
control-background, separator (60% × 0.5 pt borders on cards), `.bar` banner fill,
window background. Fills: tile icon 12%, capsule 12–13%, notice 10%, all in tint color.

## 5. Spacing and sizing **[S]**

Base rhythm 4 / 8 / 16 / 24 / 32. Spot values: card radius 12; stat tile radius 14;
tip radius 10; icon wells: 30 px tile icon on 12% r8 fill; swatches 20 px (white 2 px
selected ring + outer primary 55% 1 px ring); status dot 7 px; onboarding dots 7 px;
pill padding h9/v4; capsule padding h7/v2; caption rows h16/v11–12; card padding 14–16;
mascot sizes 44 (tips) / 72 (help) / 96 (badge preview) / 104 (header + failure) / 120
(default) / 128 (details preview) / 140 (working) / 150 (done) / 160–168 (onboarding/empty);
app icons 14 (group) / 16 (tray) / 26–30 (rows) / 28 (details title) / 96–128 (previews).

## 6. Borders, shadows, materials **[S]** / **[R]**

- Cards/tiles: control-background fill + separator-color 60% at 0.5 pt, continuous corners **[S]**.
- Header icon shadow: black 12%, radius 8, y-offset 4 **[S]** (`CloneDetail.swift:41`).
- Banner: `.bar` material + bottom divider **[S]**.
- Exact blur/radius values of `.bar`, translucency, dark-mode surface ramp, hover/pressed
  overlays: **[R]** — specify from Windows running-app visuals in Stage 2 (use WinUI
  defaults: `CardBackground`, `Layer`, `Acrylic` only where macOS shows material).

## 7. Icons and assets **[S]**

- Reuse in place: `mitosis-mascot-1024.png` (app/tile art), 17 in-app mascot PNGs at 480 px
  (`Sources/MitosisUI/Resources/Mascot/`), badge rendering algorithm (ports in Stage 5+).
- Tray glyph: redraw the 18 px Mito template concept for Windows (monochrome, light/dark
  aware) — new asset, same character **[TBD-2]**.
- 28 SF Symbols inventoried (audit §6.5): map each to Segoe Fluent Icons in Stage 2 **[TBD-2]**.
  Do not embed Apple glyphs; do not invent codepoints here.
- Badge geometry was tuned for the macOS rounded-square icon silhouette
  (`IconRenderer.swift` per engine inventory): re-tune measurements for Windows icon
  presentation in Stage 5+, keep colors/typography rules **[TBD-2]**.

## 8. Motion **[S]** values / **[S]** principles

- 0.2 s easeInOut (sheet step), 0.25 s easeInOut + opacity (onboarding pages), 0.35 s spring
  scale 0.92→1 + opacity 0.6→1 (done mascot), opacity transition (working label) **[S]**.
- Never delay work for animation; freeze all motion under Reduce Motion **[S]**.
- Windows equivalents (matching durations/easings via the compositor) in Stage 2 **[TBD-2]**.

## 9. Responsive behavior **[S]**

- Detail column collapses to detail-only when the library is empty; sidebar collapses per
  split-view behavior **[S]**.
- Stat tiles: adaptive grid, minimum 160 **[S]**.
- Content max-widths (780 detail, 640 help, 420 done/onboarding text) center with infinite
  max width **[S]**.
- Minimum window/sheet sizes are hard floors (see §2); behavior below them is OS-defined **[R]**.

## 10. Interaction, focus, hover, accessibility **[S]** rules / **[TBD-2]** + **[R]** visuals

- Rules **[S]**: Esc cancels, Enter confirms; Del deletes selection; Return opens; shortcuts in
  tooltips; VoiceOver labels on rows (`name` + status), switches, swatches (name + selected),
  dots (`Page X of 3`); section titles marked as headers; focus starts in search/label fields.
- Windows visuals **[TBD-2]**: focus rectangles, pointer-over/pressed states, default-button
  emphasis, text-selection colors — follow WinUI defaults keyed to the accent token.
- Narrator order, Contrast Themes rendering, touch targets: **[R]** — verify against macOS
  behavior with a running Mac build before sign-off.

## 11. Acceptable Windows adaptations (identity preserved)

Native chrome · taskbar (badges stay on clone icons) · File Explorer reveal · Recycle Bin ·
system tray + flyout · `Ctrl` shortcuts (`Ctrl+N`, `F1`, `Delete`, `Enter`, `Esc`) ·
`ContentDialog` for alerts/sheets · `NavigationView` sidebar · `CommandBar` toolbar ·
`Segmented` filter · `ToggleSwitch` · `Expander` for Advanced · `InfoBar` for notices/banner ·
Settings page instead of separate window (if idiomatic, same tabs/content) · Segoe UI Variable
mapped per §3 in Stage 2 · Windows date/byte formatting via OS APIs (same strings:
abbreviated + shortened timestamps, named relatives, file-style byte counts).

Non-negotiable: layout order, copy (modulo OS nouns), status system, badge rules, mascot use,
accent discipline, confirmation flows, and terminology (`Full clone`, `Compatibility mode`,
`Works great`, `Works with limits`, `Not supported`, `App (Label)`).

## 12. Visual references still required **[R]**

Existing screenshots cover: main window light/dark, pick, details, done. Before any
pixel-level matching claim, capture on macOS: General + Advanced settings, tray menu, all 3
onboarding pages, 2+ help pages, empty state, each alert variant, badge editor, working,
failed + compatibility retry, update banner + Update Now states, search-no-match, load-error,
context menu. The `.build/snapshots` renderer (`swift run MitosisSnapshot`) produces these
deterministically — see `Snapshots.swift:30-98` shot list.
