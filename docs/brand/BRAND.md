# Mitosis brand guide

For the native macOS app and its one-page site. The approved app icon is also the mascot; use the supplied asset as the source of truth.

## Essence

**One Mac app, room for every part of your life.** Mitosis makes separate copies of an app, each with its own login, data, and Dock icon.

**Personality:** friendly, capable, calm, playful.

**Tagline options:**

- One app. Separate worlds.
- Make room for another login.
- Your apps, in their own lanes.

Use “clone” for the product action. Name a clone exactly `<App> (<Label>)`, such as `Slack (Work)`; never lead with technical terms like bundle ID or container.

## Color

The light colors below are sampled from the approved 1024 px PNG. Dark variants are adjusted for use on dark surfaces; they are UI tokens, not a recoloring instruction for the mascot. Keep text and backgrounds on macOS system colors and materials in the app. Check contrast in the final context, especially for small text.

| Token | Light HEX | Dark HEX | Use |
| --- | --- | --- | --- |
| Primary blue | `#41AEF8` | `#5CC7FD` | Main brand accent, primary action emphasis, selected illustration details |
| Deep blue | `#1450A8` | `#48BAFC` | Hover/pressed emphasis, outlines, dark details |
| Soft blue | `#9CD1FB` | `#1E86DD` | Quiet fills, chips, feature illustrations |
| Split highlight | `#5CC7FD` | `#9CD1FB` | Tiny split glint, completed-step accent, success illustration; pair with a clear success label |
| Shell | `#F5F5F7` | `#142337` | Web section surface, card background when a surface is needed |
| Frost | `#EAF9FB` | `#20384C` | Subtle highlight, separators in artwork |
| Ink | `#021944` | `#F5F5F7` | Web heading/text color; use system label colors in the app |

Sample references from the PNG include body blue `#41AEF8`, shadow `#1450A8`, soft edge `#9CD1FB`, cyan highlight `#5CC7FD`, shell `#F5F5F7`, frost `#EAF9FB`, and mouth shadow `#021944`. Small pixel differences from antialiasing are expected.

**SwiftUI mapping:** define semantic asset colors with light/dark appearances. Set `AccentColor` to Primary blue, use `.tint(.accentColor)` for key controls, and use the split highlight sparingly in illustrations and completion badges. Let `Color.primary`, `Color.secondary`, `Color(nsColor: .windowBackgroundColor)`, system selection, and native materials own labels, windows, sidebars, and sheets. Never tint the whole window blue. For status, combine color with an SF Symbol and text; use system semantic status colors where meaning matters.

## Typography

**App:** SF Pro via SwiftUI system text styles (`.largeTitle`, `.title`, `.headline`, `.body`, `.caption`). Prefer Dynamic Type and native weights; use semibold for short headings and buttons, regular for body copy. Avoid custom font loading.

**Web:** `font-family: -apple-system, BlinkMacSystemFont, 'SF Pro', Inter, sans-serif;` Use a 16 px body with 1.5 line height. H1: 48–56 px, semibold, 1.05–1.1 line height; H2: 32–36 px, semibold, 1.15; H3: 20–24 px, semibold, 1.25. At narrow widths, H1 drops to 36–40 px and H2 to 28–30 px. Keep paragraphs under about 65 characters per line.

## Mascot

**Name candidates:** Mito, Pip, Dot. **Choose Mito.** Mito is a small, cheerful guide: curious, helpful, and quietly confident. The center seam and wink tell the story of a clone about to divide.

**Do:** use the approved icon unchanged for the app icon; keep the blue body, seam, wink, white tile, proportions, and soft 3D lighting; give it clear space of at least 10% of its width; use it at a size where the face reads. For new poses, match the same form and lighting and keep the seam visible.

**Don't:** stretch, flatten, recolor, rotate, add a second face, turn it into a warning symbol, place text over it, or use a busy background. Do not swap the approved icon for a pose in Finder, Dock, or app listings.

| Pose | Expression/action | App use | Site use |
| --- | --- | --- | --- |
| Wave | Small hello | Onboarding welcome | Hero, if it reads better than the approved icon |
| Split | Two halves just separating | First clone walkthrough | How it works: create |
| Empty | Looking around | No clones yet | Optional feature illustration |
| Success | Happy, seam glint | Clone created | How it works: launch |
| Oops | Concerned, still calm | Recoverable error | FAQ help illustration only |
| Thinking | Focused | Checking app support | How it works: choose |
| Sleeping | Resting | Paused or no activity, only if useful | Avoid unless a status section needs it |
| Hero | Approved wink, full tile | About/onboarding and app icon | Main hero image |

Use one mascot moment per view or section. Do not animate the face continuously.

## Voice and tone

Write short, plain sentences. Say what happened, then give one useful next action. Use “you” sparingly. Be warm without jokes during errors. Show the exact clone name when it helps. Avoid “magic,” “seamless,” “revolutionary,” and cell-division puns in UI copy.

| Moment | Good | Avoid |
| --- | --- | --- |
| Empty state | “No clones yet. Choose an app to make your first one.” Button: “Create clone” | “Your workspace is looking lonely! Let's make some magic.” |
| Clone created | “Slack (Work) is ready.” Button: “Open clone” | “Success! Your app has undergone mitosis!” |
| Error | “Couldn't create Slack (Work). Check that Slack is installed, then try again.” | “An unexpected error occurred. Code 42.” |
| Unsupported app | “This app can't be cloned yet.” Link: “See supported apps” | “Invalid app selected.” |
| Permission needed | “Mitosis needs access to Applications to create this clone.” Button: “Choose Applications folder” | “Permission denied. Enable access in Settings.” |

Error copy must reflect the actual cause and available recovery path; the examples are patterns, not universal strings.

## Native app UI

- Use a standard macOS window with a sidebar for All Clones, app groups, and Settings; show clones in a calm grid or list with the source icon, exact clone name, and concise status. Use system toolbar, search, menus, sheets, buttons, and SF Symbols.
- Put brand blue on the primary create action, active filters or badges, and onboarding illustrations. Keep secondary actions neutral. Use a small label badge to distinguish clones, while preserving recognizable source-app icons.
- Use an 8 pt spacing rhythm: 8 between icon and label, 16 within cards and form groups, 24 between sections, 32 for major view padding where space permits. Prefer native control spacing when it differs.
- Use 10–12 pt card corners and 12–16 pt onboarding panels; let native controls keep their own radii. Use very light borders or material before adding shadow.
- Animate creation with a brief seam separation or glint (about 200–350 ms), then settle. Respect Reduce Motion with a static state change. Never delay the actual task to play an animation.
- Show progress and errors as native inline states or sheets with a clear next action. Keyboard focus, VoiceOver labels, and color-independent status are required.

## One-page website

Use a centered max content width of **1120 px**, with 24 px side padding on desktop and 20 px on mobile. Give sections 80–96 px vertical space on desktop and 56–64 px on mobile. Use the shell or page white, few borders, and one primary CTA per section; avoid gradients, floating decorations, carousels, and long paragraphs.

1. **Hero:** Mito in the approved pose; one-line value proposition, such as “Run separate copies of your Mac apps, side by side.” Add a clear **Install Mitosis** button and two copyable commands labeled **curl** and **Homebrew**. Populate them from the verified release instructions and current repository owner; never publish placeholder commands. State “Free and open source” nearby.
2. **Three feature cards:** “Separate logins,” “Separate data,” “Separate Dock icons.” One sentence and one simple visual per card.
3. **How it works:** “Choose an app” (Thinking), “Name your clone” (Split; show `Slack (Work)`), “Open both” (Success). Keep each step to one sentence.
4. **FAQ:** Answer which apps work, where clone data lives, how updates affect clones, whether the original changes, and how to uninstall. Link to current docs for details.
5. **Footer:** link to [The Avni Studio Labs](https://theavni.studio/labs) and the project's GitHub repository, plus license and privacy links if published.

Use real app screenshots when available. Keep Mito as a guide, with the product and install path easy to scan.
