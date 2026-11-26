# Windows update availability check (Stage 25)

Mitosis for Windows can tell you when a newer product release exists. The check
is manual, read-only, and deliberately small: it never downloads, installs, or
relaunches anything, and it never runs on a timer.

## Where it lives

| Piece | Location | Role |
|---|---|---|
| `ReleaseVersion` | `Mitosis.Domain/Updates` | Ports macOS `Versioning.swift` parsing and comparison. |
| `ReleaseSelection` | `Mitosis.Domain/Updates` | Applies the macOS selection rules (draft/prerelease/URL/newer). |
| `IReleaseFeed` / `UpdateChecker` | `Mitosis.Application/Updates` | Timeout, cancellation, and display-ready outcomes. |
| `GitHubReleaseFeed` | `Mitosis.Infrastructure/Updates` | The single bounded GitHub API read. |
| `UpdatesViewModel` | `Mitosis.Windows.ViewModels` | Shared status and banner state. |
| Settings ▸ General ▸ Updates | `SettingsWindow` | Manual button, version line, status, View Release link. |
| Main `InfoBar` | `MainWindow` | Banner, only after a real newer release is found. |

## Request and privacy

- Endpoint (fixed):
  `https://api.github.com/repos/deadhearth01/Mitosis/releases?per_page=10`.
- One `GET` with `Accept: application/vnd.github+json` and
  `User-Agent: Mitosis/<version>`. No authorization header, cookie, account,
  query identity, or telemetry.
- GitHub necessarily sees the caller's IP address and user agent, exactly as any
  HTTPS request reveals them. Nothing is sent to Mitosis or any other service.
- The response body is capped at 512 KB and parsed as JSON with
  `JsonDocument`. The cap is enforced while reading as well as from
  `Content-Length`, so a chunked response cannot exceed it. It must be an array;
  unexpected shapes are refused.
- Timeout: 15 seconds. Closing Settings cancels the check. The check never
  retries automatically.
- No application data is read or written. The check changes only in-memory UI
  state.

## Selection rules

Mirrors macOS `UpdateChecker.pick` and `Versioning.swift`:

1. Skip drafts.
2. Skip prereleases unless the running build is itself a prerelease
   (`0.3.0-beta.1+sha` contains `-`).
3. Require an absolute `http://` or `https://` release page; otherwise the
   release cannot be shown as a usable banner.
4. Require a strictly newer version; equal or older is up to date.
5. Take the newest qualifying release.

Version parsing accepts an optional leading `v`/`V`, one to four numeric core
components, and an optional dotted prerelease. Build metadata after `+` is
ignored, which matters because Windows informational versions always end in
`+<git-sha>`. Alphanumeric prerelease identifiers sort above numeric ones, and a
shorter prerelease sorts below a longer one with the same prefix — the same
quirks as the macOS port. Unparseable tags never produce an update.

Displayed versions drop the `v` prefix and build metadata; the Settings version
line therefore shows `0.2.2`, not `0.2.2+<sha>`.

## States

| State | UI |
|---|---|
| Idle | “Check GitHub Releases for a newer version of Mitosis.” |
| Checking | Button disabled and labelled “Checking…”. |
| Available | “Mitosis X is available. The banner has a link to the release page.”, View Release link, main banner open. |
| Up to date | “Mitosis <current> is up to date.”, no link, no banner. |
| Failed | A fixed readable sentence; no invented version, no link, no banner. |

Covered failure messages: timeout, unreachable network, non-200 response,
malformed JSON/array/item, oversized response, and an unparseable running
version. Exception types and response bodies are never shown to the user.

The banner is dismissible; a dismissal lasts for the session, matching macOS. A
later check does not force a dismissed banner back open. The banner's action
opens the release page in the default browser through the OS after validating
that the URL is absolute HTTP(S); the view model never launches anything.

Preview data (`Show preview instead`) still shows its sample `0.3.0` banner with
“Update Now”, which raises the honest “preview data only” placeholder. Preview
and real update state are never mixed.

## What v0.2.3 means for Windows today

The Windows port is stamped `0.2.2`, while the repository's newest product
release is `v0.2.3`. The check will therefore report `0.2.3` as available even
though that release currently publishes only the macOS zip. The banner says
“Mitosis 0.2.3 is available.” and opens the release page; it does not claim that
a Windows download exists and it never starts one.

When Windows artifacts are published, the same check applies without change. If
the Windows channel must diverge (for example, only releases carrying a Windows
asset should count), add an asset-name filter to `ReleaseSelection` with tests;
do not hardcode a second feed or a second version source.

## Deliberately not implemented

- Automatic or launch-time checks, background timers, or scheduled tasks.
- Downloading, verifying, installing, or relaunching an update.
- A “Check Now” menu-bar/tray item (no tray yet).
- Windows asset filtering or a separate Windows release channel.
- Telemetry or usage reporting of any kind.

Self-update remains part of W53 and the installer/distribution stage.

## Verification

Automated (Release, .NET SDK 10.0.404):

- 76 new tests: version parsing/comparison, selection rules, feed parsing and
  failures, timeout/cancellation, view-model states, banner integration, and
  Settings forwarding/disposal. Suite: 472/472 green.
- One pre-existing warning remains (`CS0105` duplicate using in
  `NewCloneWizardTests.cs`); this stage added none.

Runtime (development machine, Windows 11 25H2, real network):

- A 0.2.2 build reported `0.2.3` with
  `https://github.com/deadhearth01/Mitosis/releases/tag/v0.2.3`.
- The same feed reported up to date for a 0.2.3 build.
- An unreachable endpoint and a blocked process proxy both produced
  “Couldn't reach GitHub. Check your internet connection.” with no banner or
  link.
- In the live app, Settings showed version 0.2.2, the button disabled itself while
  checking, the check updated the status, the main banner appeared with View
  Release, the close button dismissed it, and closing the main window with
  Settings still open exited the process cleanly with no error log entries.

Still manual: clicking View Release end-to-end, Narrator/pixel checks, a second
network, and a published Windows artifact.
