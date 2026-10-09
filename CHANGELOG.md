# Changelog

All notable changes to Mitosis are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Clone engine: separate copies of Mac apps with their own bundle ID, data folder, Dock icon, and badge.
- Identity mode for most apps (Electron and Chromium apps get their own data folder) and a compatibility mode for apps that can't take a new identity.
- Launch check: a clone is only kept after it actually starts; a failed refresh restores the previous version.
- Per-clone stats: extra disk, data size, RAM, and CPU; cache cleaning that keeps logins and settings.
- Refresh after the original app updates; delete to the Trash (never permanently).
- `mitosis` command-line tool: `doctor`, `clone`, `list`, `stats`, `clean`, `refresh`, `delete`, `open`.
