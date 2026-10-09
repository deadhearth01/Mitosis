# Changelog

All notable changes to Mitosis are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.1] - 2026-10-09

### Changed
- The menu bar icon is now a small Mito instead of a generic symbol.

### Fixed
- A clone put back from the Trash shows up in Mitosis again, and a clone you trash in Finder leaves the list.

## [0.1.0] - 2026-10-09

The Mac app.

### Added
- **Mitosis.app**, a native SwiftUI app:
  - clones listed by original app, each with its own page: status, actions, usage stats (extra disk, data, memory, CPU), details, and maintenance;
  - a guided New Clone flow: search your apps, see which work, add a label and badge with a live icon preview, and watch progress while the clone is built and checked;
  - a welcome screen, a built-in help guide, an optional menu bar menu, and a daily update check (can be turned off);
  - drag an app onto the window or the Dock icon to clone it.
- **Automatic updates for clones.** When an original app updates, Mitosis rebuilds its clones in the background (logins and data stay). Open clones update after they quit. A LaunchAgent does this while Mitosis is closed.
- `mitosis refresh --outdated` for scripted updates.
- Installer now installs Mitosis.app to `/Applications`, verifies the signature as well as the checksum, and links the `mitosis` command.
- Homebrew tap: `brew install --cask deadhearth01/tap/mitosis`.

### Changed
- Clone badges sit inside the icon's corner, so macOS 26 and later show clone icons full size instead of shrinking them onto a gray plate. Mitosis's own icon uses the new icon format for the same reason.
- RAM and CPU stats now work for compatibility-mode clones too.

### Fixed
- `--color` rejects values like `#GGGGGG` instead of accepting them.
- A compatibility-mode clone that fails to start no longer suggests compatibility mode.

## [0.1.0-alpha.1] - 2026-10-09

First preview: the clone engine and the `mitosis` command-line tool.

### Added
- One-line installer (`scripts/install.sh`) that verifies the download's SHA-256 checksum and installs for the current user.
- Clone engine: separate copies of Mac apps with their own bundle ID, data folder, Dock icon, and badge.
- Identity mode for most apps (Electron and Chromium apps get their own data folder) and a compatibility mode for apps that can't take a new identity.
- Launch check: a clone is only kept after it actually starts; a failed refresh restores the previous version.
- Per-clone stats: extra disk, data size, RAM, and CPU; cache cleaning that keeps logins and settings.
- Refresh after the original app updates; delete to the Trash (never permanently).
- `mitosis` command-line tool: `doctor`, `clone`, `list`, `stats`, `clean`, `refresh`, `delete`, `open`.

[Unreleased]: https://github.com/deadhearth01/Mitosis/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/deadhearth01/Mitosis/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/deadhearth01/Mitosis/compare/v0.1.0-alpha.1...v0.1.0
[0.1.0-alpha.1]: https://github.com/deadhearth01/Mitosis/releases/tag/v0.1.0-alpha.1
