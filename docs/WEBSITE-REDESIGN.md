# Mitosis website redesign

Date: 2026-11-28
Scope: `website/public` (the static marketing site) and its generator
`website/tools/build_site.py`.
Status: implemented and verified in headless Chromium on Windows. A macOS visual
review and a keyboard/screen-reader pass are still open (see Open items).

## What this is

The site was rebuilt against one new design system instead of a patch on the old
one. Everything is still plain static HTML, CSS, and JavaScript: no framework, no
build step, no external font, no new dependency, and no change to hosting.

The redesign covers every page the site serves:

- the home page,
- the guides index,
- all 22 generated guides,
- the Parall comparison page,
- the 404 page.

One stylesheet serves all of them, so the generated guides inherit the redesign
without editing 22 files by hand.

## Design direction: "The Split"

Mitosis is about one app becoming two. The page says that visually:

- **Paper and ink.** A warm off-white paper (`#FAF8F4`), near-black ink
  (`#14171C`), and hairline rules instead of drop-shadowed cards.
- **A seam.** A single 1px vertical rule runs down the middle of the hero, where
  the two halves of the page meet. It is the only structural flourish.
- **A serif voice.** Headlines use the system serif stack (New York on macOS,
  Palatino or Georgia elsewhere); interface text stays in the system sans; code
  stays in the system mono. Nothing is downloaded.
- **Two accents.** Mitosis blue (`#1450A8`) carries actions and links; a coral
  (`#A8401F` for text, `#E2603C` for decoration) marks the "clone" half: section
  numbers, the answer rule, the counter dots.
- **Light and dark from the same tokens.** Every surface reads from CSS custom
  properties. Dark mode follows the system until the reader picks a side with the
  header toggle, and the choice persists in `localStorage`.

The direction and token relationships were grounded in the OpenDesign
`editorial` design system (`E:\Open Design\...\design-systems\editorial`), which
defines the ink/paper pair, the 8pt rhythm, and the serif-display stance this
build follows.

## Files

| File | Change |
|---|---|
| `website/public/styles.css` | Rewritten. New token set, light and dark palettes, home page, guides index, article/guide contract, 404, and responsive rules at 900 / 760 / 420 / 360 px. |
| `website/public/index.html` | New body markup: hero grid with the seam, numbered section eyebrows, install cards, feature and step grids, editorial guide list, FAQ. Head, SEO tags, JSON-LD markers, copy, anchors, and asset paths are unchanged. |
| `website/public/script.js` | Guarded selectors (the script now also loads on guide pages), clipboard fallback for blocked `navigator.clipboard`, persisted light/dark toggle, and a swap of the app screenshot that matches the chosen theme. |
| `website/public/404.html` | Restyled to the new system, with the same header, footer, and theme control as the rest of the site. |
| `website/tools/build_site.py` | `head()` gains the theme boot script and `/script.js`; `HEADER` gains the theme toggle; theme colours and the manifest background follow the new palette. |

Generated output (`guides/*`, `guides/index.html`, `parall-alternative/index.html`,
`sitemap.xml`, `site.webmanifest`) was rebuilt from the generator, not edited by
hand.

## Preserved contract

- All copy, section order, headings, and links on the home page.
- Anchors: `#top`, `#main`, `#install`, `#how-it-works`, `#faq`.
- Asset paths and dimensions, including the light/dark `<picture>` for the app
  screenshot and the third-party "Featured on TryMacApps" badge.
- `<!-- ld:start -->` / `<!-- ld:end -->` markers, so `build_site.py` still owns
  the structured data.
- The article class contract (`article-page`, `article`, `breadcrumbs`, `lede`,
  `answer`, `howto`, `article-figure`, `tips`, `faq-item`, `install-box`,
  `related`, `guide-grid`, `guide-card`, `guide-category`, `guide-jump`,
  `guides-lede`, `guides-more`, `command-block`, `button primary`), so guides and
  the comparison page need no template change.
- `data-copy`, `data-releases`, `data-repo`, `data-license`,
  `data-repo-readme`, and `#copy-status` hooks used by `script.js`.

## Verification

| Claim | Level | Evidence |
|---|---|---|
| Every page rebuilds | Verified | `python tools/build_site.py` (with `PYTHONUTF8=1`) printed `Built 25 pages (version 0.2.3)`. |
| No horizontal overflow, 320-1920 px | Verified | Headless Chromium reported `scrollWidth === clientWidth` on the home page, guides index, and a guide page at 320, 360, 768, 1024, 1440, and 1920 px. An early 320 px overflow from the header was fixed by collapsing the wordmark below 360 px. |
| Light and dark both render | Verified | Captures at 1440 px in both themes, plus 390 and 768 px light; see `docs/evidence/website/`. |
| Theme toggle works and persists | Verified | A click set `data-theme="dark"`, changed the body background to `rgb(14, 17, 22)`, wrote `mitosis-theme=dark`, updated the accessible label to "Switch to light theme", swapped the screenshot to `screen-dark.webp`, and survived navigation to `/guides/`. |
| Without JavaScript the page still works | Implemented, not observed | The boot script adds a `js` class and the toggle is hidden without it; light/dark still follow the OS. This path was not exercised with scripting disabled. |
| Internal links resolve | Verified | 48 internal links across home, guides index, a guide, and the comparison page returned HTTP 200; responsive images had no broken sources. |
| Copy button works | Verified | Clicking Copy produced "Install command copied." in `#copy-status` and "Copied" on the button; a `document.execCommand` fallback covers browsers that deny clipboard writes. |
| Text contrast meets WCAG AA | Verified (measured) | Every token pair that carries text measures between 5.79:1 and 16.93:1. The lowest are coral on paper (5.79), muted on paper (5.89, light) and muted on paper (8.46, dark); body ink is 16.93 light and 16.63 dark. |
| No duplicate element IDs | Verified | Checked on the home page: none. |
| Accessibility audit (axe/Lighthouse), keyboard and screen-reader pass | **Not verified** | Landmarks, focus rings, heading order, skip link, and 24 px minimum targets were designed in but no automated or manual audit was run. |
| macOS visual review | **Not verified** | Captures are Windows Chromium. There the serif falls back to Times New Roman; on macOS it will be New York, so line breaks and the hero measure should be re-checked once. |

## Open items

1. macOS capture of the home page and one guide.
2. axe or Lighthouse run, plus a keyboard-only and VoiceOver pass.
3. Decide whether the third-party badge should keep its white plate in dark mode
   (it is drawn for light surfaces).
4. Real-device iOS Safari check for the sticky header and the 320 px header.

## OpenDesign note

The request was to use the OpenDesign MCP for this redesign. The MCP server was
reachable after restarting the app, a project (`mitosis-website-redesign`) and a
`BRIEF.md` were created, and three design runs were commissioned. None produced
files:

- `2f58d52c-…` (`qwen`, skills `redesign-existing-projects` +
  `design-taste-frontend`) stalled after reading the brief; the child exited
  without writing.
- `ee0ef6bb-…` (`qwen`) answered with a plan only, then stopped.
- `42b33176-…` (`qwen`, production prompt) stalled while reading task-profile
  files; the model process disappeared with the run still marked running.
- `b5656e43-…` (`opencode` agent) failed immediately: the adapter passes
  `--dir`, which opencode 2.4.2 no longer accepts.

With no working agent runtime on this machine, the markup and CSS were written in
this repository, using the OpenDesign `editorial` design system as the visual
foundation. The OpenDesign project and brief remain on disk for a retry once a
working agent is configured.
