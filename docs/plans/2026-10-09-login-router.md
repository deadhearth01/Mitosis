# Login Router (0.2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Sign-in links (custom URL schemes like `slack://`) reach the copy of the app that started the sign-in.

**Architecture:** The engine gains `URLSchemes`, `LinkRouterState`, `LinkRouter` (targets, auto-pick, enable/disable/sync, router bundle build) and an injectable `URLHandlerRegistry` (NSWorkspace-backed in production, fake in tests). A new `MitosisRouter` executable (AppKit) receives links, routes them, and shows a chooser when needed. The app adds an opt-in toggle per original app and keeps the router in sync.

**Spec:** `docs/specs/2026-10-09-mitosis-design.md` §15.

## Global Constraints
Same as `docs/plans/2026-10-09-mac-app.md`. Additionally:
- **Opt-in only.** Turning it off, or deleting the last clone of an app, restores the previous default handler.
- **Never take over web or system schemes:** http, https, file, mailto, ftp, sftp, ssh, tel, sms, facetime, data, about, javascript.
- **Only full (identity) clones are link targets.**

## Review Focus
- **The original app is deleted or moved while routing is on.** The original drops out of the candidates; links still reach clones; nothing crashes.
- **Two clones running plus the original.** The chooser appears, with the most recent app preselected.
- **Mitosis is updated or moved.** The router is rebuilt from the bundled executable on the next sync.
- **The user deletes the router folder by hand.** The next sync rebuilds it, or routing turns off cleanly.
- **The scheme's previous handler is gone.** Disabling still succeeds; the handler falls back to the original app's bundle ID.

### Task 18: Engine: schemes, state, targets
- [ ] **Failing tests:**
  - `URLSchemes.declared` on a fixture with `CFBundleURLTypes` [{schemes: ["slack", "HTTPS"]}, {schemes: ["slack-beta"]}] returns ["slack", "slack-beta"].
  - `LinkRouterState` round-trips through JSON.
  - `LinkRouter.targets(for: slack://x)` returns the original + identity clones (fallback clones excluded, unknown scheme → []).
  - `autoTarget` picks the only running target, or the only target, else nil.
- [ ] Implement; tests pass; commit.

### Task 19: Engine: router bundle + enable/disable/sync (fake handler registry)
- [ ] **Failing tests**, using a temp environment and a fake registry:
  - `enable` builds `.router/Mitosis Link Router.app` whose Info.plist declares the schemes, records the previous handler per scheme, and sets the router as handler.
  - Enabling a second app unions the schemes.
  - `disable` restores the previous handlers and removes the bundle when no apps are left.
  - `sync` rebuilds a missing bundle and drops apps that have no identity clones (restoring their handlers).
- [ ] Implement; tests pass; commit.

### Task 20: MitosisRouter executable + live end-to-end check
- [ ] Add `Sources/MitosisRouter/main.swift` (AppKit + SwiftUI chooser).
- [ ] `build-app.sh` copies it into `Contents/Helpers/MitosisRouter`.
- [ ] Live check: a fixture app declaring `mitosistest-<random>` and its identity clone, with routing enabled through the real NSWorkspace registry. `open mitosistest-…://hello` reaches the clone, as recorded in its log. Then disable and clean up.
- [ ] Commit.

### Task 21: App UI + CLI + help
- [ ] Clone page: a "Sign-in links" card with a toggle and explanation. It shows only for identity clones whose original declares custom schemes.
- [ ] AppModel: `setLinkRouting(_:for:)`; sync on reload and after delete.
- [ ] CLI: `mitosis links status|on <app>|off <app>`.
- [ ] Help topic "Sign-in links".
- [ ] Snapshots; full suite; commit.
