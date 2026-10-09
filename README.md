<p align="center">
  <img src="Assets/Brand/mitosis-mascot-1024.png" width="128" alt="Mito, the Mitosis mascot">
</p>

<h1 align="center">Mitosis</h1>

<p align="center"><b>Run separate copies of your Mac apps, side by side.</b><br>
Free and source-available · Native macOS · No tracking</p>

---

Mitosis makes separate copies ("clones") of a Mac app — like **Slack (Work)** next to **Slack (Personal)** — and each one gets:

- **Its own login.** Sign in to a different account in every clone.
- **Its own data.** Settings, chats, and caches never mix with the original.
- **Its own Dock icon.** A small badge tells the copies apart.

The original app is never changed. A clone shares the original's files on disk (APFS cloning), so it usually costs only a couple of megabytes.

> **Status:** Mitosis 0.1 is in development. The clone engine and the `mitosis` command-line tool work today; the Mac app is being built. Follow along in [CHANGELOG.md](CHANGELOG.md).

## Which apps work?

| | Examples | Notes |
|---|---|---|
| **Works great** | Slack, Discord, Signal, VS Code, Claude, most Electron and Chromium apps | Full separation: login, data, Dock icon |
| **Works with limits** | Some apps with system extensions or shared keychains | Mitosis tells you what won't work before you clone |
| **Not yet** | Sandboxed apps that rely on iCloud or shared app data (for example WhatsApp) | A "light mode" for these is planned |
| **Not supported** | Apple's own apps | Built into macOS and protected by it |

Run `mitosis doctor <App>` to see how a specific app will be cloned.

## Install

Installers (Homebrew tap and a one-line install script) arrive with the 0.1 release. Until then you can build from source.

### Build from source

Requirements: macOS 15 or later on Apple silicon, Xcode 16 or later (Swift 6).

```bash
# in a clone of this repository
swift build -c release
.build/release/mitosis --help
```

### Use the command-line tool

```bash
mitosis doctor Slack                 # can Slack be cloned, and how?
mitosis clone Slack --label Work     # creates "Slack (Work)" and checks that it starts
mitosis list                         # your clones and whether they need a refresh
mitosis stats "Slack (Work)"         # extra disk, data, RAM and CPU used by a clone
mitosis clean "Slack (Work)"         # delete caches (logins and settings are kept)
mitosis refresh --all                # rebuild clones after the original app updates
mitosis delete "Slack (Work)"        # move the clone to the Trash (data kept unless --delete-data)
```

## Good to know

- **Where things live:** clones go in `~/Applications/Mitosis/`, their data in `~/Library/Mitosis/Data/`.
- **Permissions:** macOS treats each clone as a new app, so it asks again for things like notifications, files, or the camera. That's expected.
- **Updates:** when the original app updates, refresh the clone. Your logins and data stay.
- **Disk space:** clones share space only when the original app is on the same drive as your home folder. Mitosis warns you before making a full copy.
- **Removing:** delete a clone and it goes to the Trash. Nothing is ever deleted permanently without you seeing it.
- **Privacy:** Mitosis has no analytics and the clone engine makes no network connections.

## Contributing

Bug reports, app compatibility reports, and pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) first, and please follow the [Code of Conduct](CODE_OF_CONDUCT.md). Found a security problem? See [SECURITY.md](SECURITY.md).

## License

Mitosis is **free and source-available** under the [PolyForm Noncommercial License 1.0.0](LICENSE), with one extra permission: **anyone may use Mitosis itself for any purpose, including at work.**

You may not sell Mitosis or use its source code in commercial or paid products. The Mitosis name, icon, and the Mito mascot are not covered by the license, so forks need their own name and icon.

---

<p align="center">A <a href="https://theavni.studio/labs">The Avni Studio Labs</a> project.</p>
