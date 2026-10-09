# Mitosis — Design Spec

_Date: 2026-10-09 · Status: draft for review · Repo folder: `mac-apps/app-cloner` (public repo name: `mitosis-mac`)_

## 1. Intent

**What:** Mitosis is a free, source-available macOS app that runs several fully separate copies ("clones") of the same Mac app — e.g. *Slack Work* and *Slack Client* side by side, each with its own login, data, Dock icon, and notifications.

**Why (owner's goal):** visibility and personal brand for the developer (GitHub stars, X/Reddit audience), not revenue. Ship 0.1 → 1.0, then market on X and Reddit.

**Success looks like:**
- Clones behave like truly separate apps: open in any order, separate data, notification clicks open the right clone.
- It feels like an Apple-made app (native SwiftUI, standard Mac patterns, no web views for UI).
- Installs without warnings and without a paid Apple Developer account.
- It beats the market leader (Parall, $9.99) on its known gaps: notification routing, launch order, sandboxed/App Store apps, login flow, workspaces.

**Non-goals:**
- Anti-detect / fingerprint spoofing tools (ToS and abuse risk).
- Bypassing licensing, receipts, or DRM. Apps that refuse to run as clones are marked unsupported.
- Cloning Apple system apps (sealed system volume), iPhone/iPad apps on Mac, Windows/Linux.
- Redistributing third-party apps. Clones are created only locally on the user's Mac.

## 2. Roadmap

| Version | Scope |
|---|---|
| **Phase 0 — Spike** | Throwaway feasibility test of identity mode (section 12). Adjusts this spec. |
| **0.1** | Clone engine (identity + fallback), separate data, badge icons, any-order launch and correct notification routing (identity-mode clones), main window (sidebar + grid), app picker with search, quick sheet, guided first run, optional menu bar icon, `mitosis` CLI, manual "Refresh" after original updates, install script + Homebrew tap + build from source, Mitosis self-update |
| 0.2 | Background helper: automatic clone repair on original-app update; login router (OAuth callbacks reach the clone that started sign-in) |
| 0.3 | Workspaces: launch a set of clones/apps/folders together (e.g. "Client A"); "Open at login" and "Keep in Dock" automation |
| 0.4 | Light mode: WebKit-based lightweight clients (Slack, Teams, WhatsApp, Discord web) |
| 0.5 | Per-workspace proxy; profile backup/export/import; remote app-profile updates (signed) |
| 1.0 | Polish, broader compatibility list, website, launch on X + Reddit |

This spec details **0.1** fully and fixes the architecture so later versions slot in without restructuring.

## 3. Platform & constraints

- **Language/UI:** Swift 6, SwiftUI (AppKit only where SwiftUI lacks an API). Xcode 27.
- **Minimum OS:** macOS 15. Liquid Glass styling applies automatically on macOS 26+.
- **Architecture:** Apple silicon + Intel builds (universal) as long as macOS 15 supports Intel; macOS 27 is Apple-silicon-only, so Intel is best-effort.
- **No paid Apple Developer account.** Mitosis is ad-hoc signed; clones are ad-hoc signed locally. Not distributable via Mac App Store or official Homebrew cask.
- **License:** PolyForm Noncommercial 1.0.0 + an additional permission letting anyone run unmodified releases for any purpose (including at work); no selling and no use of the source in commercial or paid products. The Mitosis name, icon, and Mito mascot are reserved (not licensed). Marketed as "free and source-available" (not "open source", which requires allowing commercial use). _Changed 2026-10-09 at the owner's request; replaces MIT._
- **Mitosis bundle ID:** `com.mitosis-mac.Mitosis`.
- **Privacy:** no telemetry/analytics. Network use: Mitosis update check (can be disabled). Nothing else in 0.1.

## 4. Architecture

```
Mitosis.app (SwiftUI)  ─┐
                        ├──> MitosisCore (Swift package: engine, no UI)
mitosis (CLI)          ─┘           │
                                    ├── AppInspector
                                    ├── ModeDecider  <── ProfileStore (bundled profiles.json)
                                    ├── CloneBuilder (Identity / Fallback)
                                    ├── IconRenderer
                                    ├── Signer
                                    ├── CloneRegistry (clones.json + embedded manifests)
                                    └── CloneLauncher / RunningMonitor
Clone bundle  ──> contains LaunchStub (tiny binary) + mitosis.json manifest
MitosisHelper (0.2, login item) ──> reuses MitosisCore for auto-repair + login routing
```

| Unit | Responsibility | Depends on |
|---|---|---|
| **AppInspector** | Reads an `.app`: bundle ID, version, executable, Info.plist, entitlements (via `codesign -d --entitlements`), sandbox flag, Mac App Store receipt presence, framework type (Electron/Chromium/Sparkle/Squirrel/Catalyst/native), location volume. Returns `AppInfo`. Pure read-only. | Foundation, `codesign` |
| **ProfileStore** | Loads bundled `profiles.json`; looks up profile by source bundle ID. | — |
| **ModeDecider** | `AppInfo` + optional profile → `CloneMode` (identity / fallback / unsupported) + `SupportLevel` (full / limited / unsupported) + reasons (human-readable). | ProfileStore |
| **CloneBuilder** | Builds a clone in a temp location, then atomically moves it into place. Two strategies: `IdentityStrategy`, `FallbackStrategy`. Rolls back on any failure. | IconRenderer, Signer, LaunchStub |
| **IconRenderer** | Source icon + badge (text, color) → `.icns`. Also used for live preview. | AppKit, ImageIO |
| **Signer** | Inside-out ad-hoc signing with sanitized entitlements; verification. | `codesign` |
| **CloneRegistry** | CRUD for clones; persists `~/Library/Application Support/Mitosis/clones.json`; can rebuild itself by scanning clone folder for embedded manifests. | — |
| **CloneLauncher / RunningMonitor** | Opens clones, detects running state, detects crash-within-10s. | NSWorkspace |
| **LaunchStub** | Tiny compiled binary placed in clones that need launch arguments or environment (e.g. per-clone data folder); reads `Contents/Resources/mitosis-launch.json` and `execve`s the real executable. | libc |

Rules: MitosisCore has no UI code and is fully unit-testable; the app and CLI are thin layers over it.

## 5. Clone engine

### 5.1 File locations

| What | Where |
|---|---|
| Clones | `~/Applications/Mitosis/<Clone Name>.app` (configurable in Settings) |
| Registry | `~/Library/Application Support/Mitosis/clones.json` |
| Per-clone data (when Mitosis manages it) | `~/Library/Application Support/Mitosis/Data/<clone-uuid>/` |
| Embedded manifest | `<Clone>.app/Contents/Resources/mitosis.json` |
| Launch config (if stub used) | `<Clone>.app/Contents/Resources/mitosis-launch.json` |

### 5.2 Manifest (`mitosis.json`)

```json
{
  "schema": 1,
  "id": "6F1C…-UUID",
  "name": "Slack Work",
  "badge": { "text": "W", "color": "#0A84FF" },
  "mode": "identity",
  "cloneBundleID": "com.tinyspeck.slackmacgap.mitosis.slack-work",
  "source": {
    "path": "/Applications/Slack.app",
    "bundleID": "com.tinyspeck.slackmacgap",
    "version": "4.47.0",
    "cdhash": "…"
  },
  "profile": { "id": "slack", "version": 3 },
  "dataPath": "~/Library/Application Support/Mitosis/Data/6F1C…",
  "createdAt": "2026-10-09T05:40:00Z",
  "refreshedAt": "2026-10-09T05:40:00Z",
  "mitosisVersion": "0.1.0"
}
```

`clones.json` is an array of these. If missing/corrupt it is rebuilt from embedded manifests.

### 5.3 Identity mode (default)

1. **Inspect** source (AppInspector).
2. **Copy** source → temp dir on the same volume using APFS clone (`clonefile`) → zero extra disk. If source and target are on different volumes, fall back to a regular copy after warning with the size.
3. **Rewrite Info.plist:**
   - `CFBundleIdentifier` = `<source ID>.mitosis.<slug>`; slug = lowercase ASCII letters/digits/hyphens from the clone name, de-duplicated with `-2`, `-3`.
   - `CFBundleName` / `CFBundleDisplayName` = clone name.
   - Icon: write `MitosisIcon.icns`, set `CFBundleIconFile = MitosisIcon`, remove `CFBundleIconName` (so an asset-catalog icon doesn't override it).
   - Sparkle apps: `SUEnableAutomaticChecks = false`, `SUAutomaticallyUpdate = false`, remove `SUFeedURL`.
   - Keep `CFBundleURLTypes` unchanged in 0.1 (login routing comes in 0.2).
4. **Launch stub (only if the profile/strategy needs args or env):** move the real executable to `Contents/MacOS/<exe>.mitosis-real`, place LaunchStub as `<exe>`, write `mitosis-launch.json` with args/env (e.g. `--user-data-dir=<dataPath>` for Chromium/Electron, or `HOME` override). The stub preserves `argv[0]` semantics and passes through any extra arguments it receives.
5. **Sanitize entitlements** of the main executable: keep sandbox and resource entitlements (`com.apple.security.app-sandbox`, `…files.*`, `…network.*`, `…device.*`, `…personal-information.*`); remove team-bound/restricted ones (`com.apple.developer.*`, `keychain-access-groups`, `com.apple.application-identifier`, `aps-environment`, team-prefixed `com.apple.security.application-groups`).
6. **Sign** inside-out with ad-hoc identity (`codesign --force --sign -`), no hardened runtime: nested frameworks, dylibs, XPC services, helper apps, plugins, then the main bundle with sanitized entitlements. Verify with `codesign --verify --deep --strict`.
7. **Write manifest**, move temp bundle into `~/Applications/Mitosis/`, register with LaunchServices (`lsregister -f`), add to registry.

Result: macOS sees a distinct app → its own preferences, sandbox container, notification identity, Dock identity; can run alongside the original in any order.

### 5.4 Fallback mode

Creates a small **shortcut app** (Mitosis-built, ad-hoc signed) with its own bundle ID, name, and badge icon. Its LaunchStub opens the original app as a new instance with per-clone args/env (data folder or `HOME` override), Parall-style. The original app is untouched and keeps its own updates. Known limitations (shown to the user): shared notification identity, launch-order dependency, sandboxed apps can't get separate data.

### 5.5 Mode decision

| Condition | Result |
|---|---|
| Path under `/System/` (Apple system app) | Unsupported |
| Profile exists | Use profile's mode/support level |
| Has restricted entitlements that the app needs to function (iCloud, keychain groups, push) | Fallback, support = limited |
| Mac App Store app (has `_MASReceipt`) | Identity, support = limited ("may ask you to sign in again or refuse to start"); crash detection offers fallback |
| Otherwise | Identity, support = full |

Each decision carries reasons shown in the UI and in `mitosis doctor`.

### 5.6 App profiles (`profiles.json`, bundled in 0.1)

```json
{ "schema": 1, "profiles": [
  { "id": "slack", "bundleIDs": ["com.tinyspeck.slackmacgap"], "version": 3,
    "mode": "identity", "support": "full",
    "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} },
    "notes": "Electron. Data separated via user-data-dir." }
]}
```
_(Illustrative entry; real args/env per app are set from Phase 0 results.)_

Profiles can only use an allowlist of operations: mode, support level, args matching an allowlisted set (e.g. `--user-data-dir={dataPath}`), env keys from an allowlist (`HOME`, app-specific data-dir variables), and named Info.plist tweaks. This keeps future remotely-updated profiles (0.5) from injecting dangerous flags (e.g. remote debugging).

Initial profiles (verified during Phase 0 and 0.1 testing): Slack, Discord, Claude, Codex, Cursor, VS Code, Google Chrome, Signal, Telegram, WhatsApp, Notion.

### 5.7 Refresh (automatic since 0.1, revised 2026-10-09)

_Revised:_ clones refresh automatically (setting on by default): the app rebuilds outdated clones that aren't running (no launch, atomic install), retries a running clone after it quits, and a per-user LaunchAgent (`com.mitosis-mac.autorefresh`, launchd `WatchPaths` on the originals plus a 6-hour fallback) runs `mitosis refresh --outdated --quiet` while Mitosis is closed. Originals that are mid-update (signature doesn't verify) are skipped until the next run.


Registry compares the source app's current version/cdhash with the manifest. If different, the clone shows "Update available → Refresh". Refresh rebuilds the clone from the current source using the same name, badge, bundle ID, and data path, then atomically replaces it. Data and logins are kept because they live outside the bundle (preferences/containers keyed by the unchanged clone bundle ID, or the Mitosis data folder). Refuses to refresh while the clone is running (asks to quit it).

### 5.8 Deleting a clone

Asks: "Keep its data" (default) or "Delete data too". Deleted bundles and data are moved to the Trash, never removed permanently.

## 6. User interface (0.1)

> _Revised 2026-10-09 after the owner tried the first build:_ the main window is a **sidebar list of clones (grouped by original app, with status) + a full page per clone** (header with actions, stat tiles for extra disk/data/memory/CPU, details, maintenance) instead of an icon grid with a hidden inspector. Clones also **update automatically** when their original app updates (pulled forward from 0.2; see §5.7). The grid/inspector text below is kept for history.

**Main window — sidebar + icon grid (NavigationSplitView):**
- Sidebar: *All Clones*, *Running*, *By App* (one entry per source app), *Workspaces* (hidden until 0.3).
- Content: grid of clone tiles: icon with badge, name, status (Running / Not running / Update available / Original missing). Double-click or Return opens the clone.
- Context menu: Open, Refresh, Edit…, Show in Finder, Show Data Folder, Delete…
- Inspector (toggle): source app, version, mode, support level and reasons, data path, created/refreshed dates.
- Toolbar: `+ New Clone` (⌘N), search (⌘F).
- Empty state: large illustration + "Clone your first app" button + "or drag an app here".
- Drag-and-drop an `.app` onto the window or Mitosis Dock icon → opens New Clone at step 2.

**New Clone — two steps in a sheet:**
1. **App picker:** lists all apps found via Spotlight (`kMDItemContentType == com.apple.application-bundle`) in `/Applications`, `~/Applications`, `/Applications/Setapp`; search field (focused by default); filters *All / Supported / Recently used*; support badge per app (Full / Works with limits / Not supported, with reason tooltip); Mitosis' own clones excluded; drop zone.
2. **Quick sheet:** Name (prefilled "<App> 2"), badge text (1–2 chars, prefilled from name), color swatches (8 system colors), "Open after creating" (on by default — user can then choose Keep in Dock); live icon preview; support summary line; `Advanced…` disclosure: mode override (Automatic/Identity/Fallback), data folder location, extra launch arguments (advanced users).
- **Guided mode:** on first run the same two steps show short tips; after the first clone the quick flow is default. Re-enable in Settings.

**Settings window:** General — show menu bar icon (off by default), check for updates automatically (on), clone location. Advanced — default mode, reset guided tips.

**Menu bar extra (optional):** list of clones with running dots; click to open; "New Clone…", "Open Mitosis".

**Native feel checklist:** system fonts/colors/materials only; SF Symbols; standard keyboard shortcuts (⌘N, ⌘F, ⌘,, Delete, Return, ⌘I for inspector); VoiceOver labels on all controls and tiles; honors Reduce Motion and Increase Contrast; light/dark; windows restore size/position; no custom title bars.

Mockups from the design session: `.superpowers/brainstorm/` (main-window-layout A, new-clone-flow-v2).

## 7. CLI (`mitosis`)

```
mitosis list
mitosis doctor <app>                     # inspection + mode decision + reasons
mitosis clone <app> --name <name> [--badge W] [--color blue] [--mode auto|identity|fallback]
mitosis refresh <clone>|--all
mitosis delete <clone> [--delete-data]
mitosis open <clone>
```
`<app>` accepts an app name ("Slack"), a bundle ID, or a path. The CLI is shipped inside `Mitosis.app` and symlinked to `/usr/local/bin/mitosis` (or `~/.local/bin`) by the installer / a Settings button.

## 8. Errors and recovery

| Situation | Behavior |
|---|---|
| Any clone step fails | Delete temp bundle; nothing half-made remains; plain-language error + "Copy details" + "Report on GitHub" (prefilled issue with app name/version, mode, macOS version, step and error — no personal data). |
| Clone exits within 10 s of first launch | Prompt: "Slack Work closed unexpectedly. Try safe mode (fallback)?" → rebuild in fallback mode keeping data path. |
| Source app updated | Tile shows "Update available"; Refresh (section 5.7). |
| Source app moved/deleted | Tile shows "Original missing"; "Find app…" relinks by bundle ID. |
| Cross-volume copy | Warn with exact size before copying. |
| Target name exists | Inline validation in the sheet. |
| Registry corrupt/missing | Rebuild from embedded manifests; log a warning. |

## 9. Distribution & updates (no paid developer account)

- **GitHub Releases:** `Mitosis-<version>.zip` (universal, ad-hoc signed) + `SHA256SUMS`. Built by GitHub Actions on tag push from the public source.
- **Install script:** `curl -fsSL https://raw.githubusercontent.com/<owner>/mitosis-mac/main/install.sh | bash` — downloads latest release via `curl` (no quarantine attribute), verifies SHA-256, installs to `/Applications` (or `~/Applications` without write access), re-signs ad-hoc, removes any quarantine attribute, optionally links the CLI, opens the app. `<owner>` = the developer's GitHub account, set when the repo is created.
- **Homebrew:** personal tap `<owner>/homebrew-tap`, cask `mitosis` with a `postflight` that removes the quarantine attribute and re-signs. (Official homebrew-cask requires notarization since Homebrew 5.0, so it's out of scope until a paid account exists.) Re-verify tap rules at release time; if casks become impractical, ship a source-building formula instead.
- **Build from source:** `git clone … && make install`.
- **Self-update:** Sparkle 2 with EdDSA-signed updates (appcast on GitHub Releases/Pages); download → verify EdDSA → replace → ad-hoc re-sign → relaunch. Phase 0 confirms Sparkle works for an ad-hoc-signed app; if not, a small custom updater with the same verification steps replaces it.

## 10. Testing

1. **Unit tests (MitosisCore):** Info.plist rewriting, slug generation, entitlement sanitization, mode decisions, manifest/registry round-trips, profile allowlist validation, icon rendering sizes. Uses fixture apps built by the test target: plain native app, sandboxed app, Electron-like bundle (nested helpers + frameworks), app with restricted entitlements, Sparkle-style app.
2. **Integration tests:** clone fixture apps end-to-end; verify signature, unique bundle ID, launch, separate preferences/container, refresh keeps data, delete moves to Trash, rollback on injected failure.
3. **Real-app compatibility checklist** (manual, scripted where possible) on Signal, Claude, Codex, Cursor, VS Code, Chrome, Telegram, WhatsApp, Notion: opens; separate login/data; badge icon; notification click opens correct clone; any-order launch; refresh after source update. Results go to `COMPATIBILITY.md` and feed `profiles.json`.
4. **CI:** GitHub Actions on the latest macOS runner: build, unit + integration tests (fixtures only), release artifacts + checksums on tags.
5. **UI:** SwiftUI previews for all states (empty, grid, sheet steps, errors) and a small set of UI tests for the New Clone flow.

## 11. Repository layout

_Revised 2026-10-09 (follows the conventions of large open-source Mac apps such as AeroSpace, Rectangle, and CodeEdit; one root Swift package instead of an Xcode project)._

```
mitosis-mac/
├─ Package.swift, Package.resolved   # one SwiftPM package: engine, stub, CLI, (app targets in Plan 3)
├─ Sources/
│  ├─ MitosisCore/      # engine library (no UI) + Resources/profiles.json
│  ├─ LaunchStub/       # tiny launcher copied into clones
│  └─ mitosis/          # `mitosis` CLI
├─ Tests/MitosisCoreTests/
├─ Assets/Brand/        # icon + Mito mascot masters (reserved, not licensed)
├─ docs/                # specs/, plans/, brand/
├─ website/             # one-page site
├─ scripts/             # build-app.sh, install.sh (Plan 4)
├─ .github/             # issue forms, PR template, (workflows in Plan 4)
└─ README.md, LICENSE, CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md, CHANGELOG.md, .editorconfig
```

## 12. Phase 0 — Spike (throwaway, before 0.1)

All work happens in a scratch folder; original apps are never modified; all spike artifacts are deleted afterwards.

| # | Question | Test app(s) | Pass criteria |
|---|---|---|---|
| S1 | Does an identity clone run alongside the original in any order? | Signal | Both open in either order; distinct Dock icons |
| S2 | Does an Electron clone keep separate data, and which method works (args vs `HOME`)? | Signal, Claude | Separate login; original's data untouched |
| S3 | Do Electron helper processes (renderer/GPU) work after the bundle ID change and ad-hoc re-sign? | Signal, Claude, VS Code | No crashes; windows render |
| S4 | Does clicking a notification open the clone that sent it? | Signal or Telegram | Correct instance comes to front |
| S5 | Does a sandboxed Mac App Store app run with a new ID and get its own container? | WhatsApp | Runs; new container created; record receipt behavior |
| S6 | Does the badge icon show on macOS 27 after removing `CFBundleIconName`? | any | Dock + Finder show badge icon |
| S7 | What does the clone's own updater do (Sparkle/Squirrel)? | Signal/Claude | Fails safely or is disabled; no corruption |
| S8 | Can Sparkle 2 update an ad-hoc-signed app? | minimal test app | Update installs and relaunches without warnings |

Outcome: a short "Spike results" section appended to this spec with decisions (stub methods per framework, profile defaults, updater choice). If S1–S4 fail broadly, re-scope 0.1 to fallback-first and revisit identity mode.

## 13. Risks

| Risk | Mitigation |
|---|---|
| Some apps break when re-signed (restricted entitlements, integrity checks) | Hybrid mode + crash detection → fallback; per-app profiles; honest support badges |
| Mac App Store apps validate receipts tied to bundle ID | Mark "limited"; never bypass licensing; fallback offered |
| Apple tightens ad-hoc signing/Gatekeeper rules | Curl install + build-from-source keep working; buy Developer ID when possible |
| Homebrew tightens third-party tap rules | Source-building formula as backup; curl installer primary |
| Parall or others copy the identity approach | Speed of shipping, source-available community, CLI + workspaces roadmap |
| App ToS concerns about multiple accounts | Mitosis only separates local data; README notes users are responsible for each app's terms |

## 14. Spike results (2026-10-09)

| # | Result | Evidence |
|---|---|---|
| S1 any-order launch | **PASS** | NotifyProbe clone + original run side by side, distinct bundle IDs/pids, both launch orders |
| S2 Electron data separation | **args `--user-data-dir={dataPath}`** | VS Code, Signal, Claude: all files opened in clone data dir, 0 in original's (lsof) |
| S3 Electron helpers | **PASS with helper rename** | Changing CFBundleName alone → Electron FATAL "Unable to find helper app"; keeping CFBundleName → helpers OK but clone asks for original's "<App> Safe Storage" keychain item; renaming helpers to "<Clone> Helper*.app" → no prompt, all helpers run |
| S4 notification routing | pending (see appendix when done) | ad-hoc apps get notifications only when run from an Applications folder (from /tmp: instant UNError 1) |
| S5 sandboxed app with iCloud/app-groups (WhatsApp) | **FAIL in identity mode** | own container created, process aborts (exit 134) after restricted entitlements stripped |
| S6 badge icon | **PASS** | badged icon visible in Dock/Stage Manager after removing CFBundleIconName |
| S7 clone's built-in updater | harmless but wasteful | Signal clone downloaded a 146 MB update into its data dir; clone bundle not replaced |
| Disk | **~1.4 MB per clone** | only when unmodified nested code keeps vendor signatures; re-signing everything rewrote big binaries (~839 MB per Claude clone) |
| Launch speed | **no clone overhead** | VS Code original 42.6 s / 39.7 s vs clone 41.8 s / 44.6 s on an overloaded Mac (disk 1.1 GB free) |

**Decisions (binding for the core-engine plan)**
1. **Signing:** never re-sign unmodified nested code. Sign only: renamed helper apps, the real executable behind the stub (with sanitized entitlements + identifier), and the main bundle. Library validation is off for the ad-hoc main executable (no hardened runtime), so vendor-signed frameworks load fine.
2. **Electron naming:** set `CFBundleName` and `CFBundleDisplayName` to the clone name **and** rename every `Contents/Frameworks/<Old> Helper*.app` to `<Clone> Helper*.app`, renaming its executable and updating its `CFBundleExecutable`/`CFBundleName`. Non-Electron apps: set both names directly.
3. **Data root:** `~/Library/Mitosis/Data/<8-hex id>` (Unix socket paths inside user-data-dir must stay < 104 chars; the spec's UUID path was 120 chars and broke VS Code).
4. **Clone location** must be an Applications folder (`~/Applications/Mitosis/`), otherwise macOS refuses notifications.
5. **Default Electron/Chromium launch settings:** `--user-data-dir={dataPath}` (confirmed).
6. **Sandboxed apps that use iCloud, app groups or push** (restricted entitlements + sandbox) → **unsupported** in 0.1 with a clear reason; Light mode (0.4) is the answer. WhatsApp profile → unsupported.
7. **Privacy permissions are per clone** (Files & Folders, other apps' data, camera, mic…). The in-app guide must explain this and deep-link to System Settings → Privacy & Security.
8. **Health check:** after creating/refreshing a clone, launch-verify it (still running after 10 s) before reporting success; on failure roll back (or offer fallback) with a plain-language reason.
9. **Per-clone stats (new, user request):** real extra disk (unshared bytes), data folder size, live RAM/CPU while running.
10. **Clone updater caches:** show data-folder size and offer "Clean caches" (deletes `update-cache`, `Cache`, `Code Cache`, `GPUCache` inside the clone's data folder).

**Naming (user request 2026-10-09):** clone display names follow "<App> (<Label>)", e.g. "Slack (Work)"; the UI asks for the label only and shows the composed name.

**Brand (decided 2026-10-09):** app icon + mascot = "mascot-2" — a friendly blue rounded-tile creature with a split line down its middle and a wink (about to divide). Master: `Assets/Brand/mitosis-mascot-1024.png`. Use the same character in onboarding, help guide, website and social posts.
