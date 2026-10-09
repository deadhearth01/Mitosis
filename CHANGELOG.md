# Changelog

All notable changes to Mitosis are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Planned for 0.1
- The native Mac app: guided onboarding, app picker, per-clone stats panel, and a built-in help guide.

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

[Unreleased]: https://github.com/deadhearth01/Mitosis/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/deadhearth01/Mitosis/releases/tag/v0.1.0-alpha.1
