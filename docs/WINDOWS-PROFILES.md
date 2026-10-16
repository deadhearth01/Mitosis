# Windows clone profiles (Stage 4)

Persistent metadata records for intended independent instances. A record is data
only: it never proves an application was cloned, launched, or verified.
Stage 4 creates no copies, launches nothing, and deletes nothing.

## 1. Identity and schema

`CloneProfile` (`Mitosis.Domain/CloneProfile.cs`):

| Field | Rule |
|---|---|
| `Id` | Immutable `Guid`, generated at creation; duplicates rejected |
| `AppId` | Stable Stage 3 application identity (`{source}:{hash}`); never an executable path |
| `Name` | Validated by the shared `CloneName` rules (60 chars, no `/ :` newline NUL, no leading dot); duplicates rejected case-insensitively (`You already have {name}.`) |
| `BadgeText` / `BadgeColor` | Optional; 1–2 chars uppercased; color is a macOS preset name or `#RRGGBB` |
| `State` | Lifecycle state (see §2) |
| `CreatedAt` / `UpdatedAt` | UTC (`DateTimeOffset`, zero offset); `UpdatedAt` bumps on every mutation |
| Schema | `profiles.json`: `{ "schemaVersion": 1, "profiles": [...] }`, indented, name-sorted |

Profiles reference discovery records by `AppId`; discovery metadata is never copied
into a profile.

## 2. Lifecycle states and transitions

`Planned` → `Provisioning` → `Ready` | `Failed`; `Failed` → `Provisioning`;
`Ready` ↔ `Provisioning`/`Unavailable`; `Unavailable` → `Provisioning`/`Ready`.
Every other transition is rejected (`CloneProfileTransitions.IsValid`, exhaustively
tested). Stage 4 only ever creates `Planned`: a new record is metadata, never a
verified clone, and the UI labels it `Planned — metadata only, not a working clone.`

## 3. Storage path strategy and format

`WindowsProfilePaths.ProfileDirectory()` →
`%LOCALAPPDATA%\Mitosis\Profiles\profiles.json`. The directory is created lazily on
first save, never on read; tests always use isolated temp directories and never touch
the real store. Records serialize deterministically (name-sorted); unknown JSON
fields are tolerated for forward compatibility.

## 4. Atomic-save behavior

Each mutation is read-modify-write under an instance `SemaphoreSlim`: validate
everything first, serialize to a temp file in the same directory, then atomic
`File.Move(overwrite: true)`. On any failure the temp file is removed and the
existing store is untouched (verified by locked-file tests). Concurrent callers are
serialized; 20 parallel adds land intact. Cross-process locking is out of scope
(single desktop app instance owns the store; documented limitation).

## 5. Application association and missing-app behavior

Loading joins profiles to current discovery by `AppId`. Matches appear under their
app; non-matches are preserved as **orphans** — listed in an `Unavailable` sidebar
section with a dedicated detail panel (`The application for this profile was not
found. The record is kept…`), never auto-deleted, rejoining automatically if the app
returns. Profiles for a selected app render name, honest state subtitle, and created
date, with loading / empty (`No profiles yet.`) / error + retry states.

## 6. Privacy guarantees

- Stored per profile: IDs, names, badge text/color, state, timestamps. No credentials,
  tokens, command lines, environment, file contents, or browser data.
- Stored paths: none (profiles hold no paths at all).
- Errors name exception types only; home-folder prefixes are redacted to `~`.
- Local-only: no network calls in any persistence path; nothing uploaded.

## 7. Corruption and unsupported-version handling

- Missing file or directory → empty repository (nothing created on read).
- Malformed JSON, invalid records, duplicate IDs/names, unknown states →
  `ProfileStoreException`; the file is left byte-identical.
- `schemaVersion != 1` → `UnsupportedProfileSchemaException`; file untouched.
- Read/write/permission failures → `ProfileStoreException` naming the exception type;
  failed saves preserve the previous file and remove temp files.

## 8. Explicitly deferred

Provisioning and verification (engine sets `Ready`), standalone badge editor,
refresh/relink/delete of clones, `Ready`/`Failed` transitions in the UI,
cross-process file locking, migration between schema versions, and encrypted storage
(nothing stored warrants it).

## 9. New Clone wizard (Stage 5, metadata only)

`NewCloneWizardViewModel` drives Pick → Details → Review → Saving → Done / Failed
in `NewCloneWindow` (single instance from Ctrl+N / New Clone). Details prefills the
first unused `App (Label)` name, app-initial badge, and first unused color.
Compatibility gates (Stage 3 assessments): `Unsupported` blocks with reasons,
`NeedsReview` needs an acknowledgement checkbox, `Unknown` records with a notice,
`Supported` flows through. Saving writes exactly one `Planned` record (reentrant
saves ignored; duplicates and store failures land on Failed with retry; cancel
leaves nothing). Done reads `{name} planned.` plus the metadata-only notice and
never transitions to `Ready`; the main list then refreshes and selects the app.
