# Contributing to Mitosis

Thanks for helping make Mitosis better. This guide keeps contributions smooth for everyone.

## Ways to help

- **Report a bug** — use the *Bug report* issue form. Include your macOS version, the app you cloned, and the output of `mitosis doctor <App>`.
- **Report app compatibility** — tell us an app that works, works with limits, or fails, using the *App compatibility* form. This directly improves Mitosis for everyone.
- **Suggest a feature** — use the *Feature request* form. Explain the problem first, then your idea.
- **Send a pull request** — for anything bigger than a small fix, open an issue first so we can agree on the approach.

## Development setup

Requirements: macOS 15 or later on Apple silicon, Xcode 16 or later (Swift 6).

```bash
swift build          # builds the engine, the launch stub, and the CLI
swift test           # runs the test suite (builds small fixture apps; takes ~30 s)
```

Project layout:

| Path | What it is |
|---|---|
| `Sources/MitosisCore/` | The clone engine (no UI): inspect apps, decide the clone mode, build, sign, register, refresh, delete, stats |
| `Sources/LaunchStub/` | Tiny launcher copied into each clone; starts the real app with the clone's own data folder |
| `Sources/mitosis/` | The `mitosis` command-line tool |
| `Tests/MitosisCoreTests/` | Swift Testing suite with real fixture apps |
| `Assets/Brand/` | App icon and Mito mascot artwork (not covered by the license; see below) |
| `docs/` | Design spec, implementation plans, and brand guide |
| `website/` | The one-page website |

## Rules that keep Mitosis safe

- **Never modify the original app.** Every change happens on the clone.
- **Never delete user data permanently.** Bundles and data go to the Trash.
- **Verify before reporting success.** A clone is only "ready" after it has actually started.
- **No telemetry**, and no network access in the engine.
- **Write a failing test first**, then the fix. `swift test` must pass before you open a pull request.
- Keep user-facing text short and plain. Clone names always look like `App (Label)`, for example `Slack (Work)`.

## Pull requests

1. Fork, create a branch from `main`, and keep the change focused.
2. Add or update tests; run `swift test`.
3. Describe what changed and how you tested it (the PR template helps).

## License of contributions

Mitosis is free and source-available under the PolyForm Noncommercial License 1.0.0 with an additional permission for use at work (see [LICENSE](LICENSE)). By submitting a contribution, you agree that it may be distributed under the project's current license and any future license the maintainer chooses for Mitosis, and you confirm that you have the right to submit it.

The Mitosis name, app icon, and Mito mascot are not licensed for use in forks.
