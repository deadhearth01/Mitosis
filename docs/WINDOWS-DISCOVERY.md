# Windows application discovery and compatibility (Stage 3)

Read-only inventory of installed Windows applications plus a deterministic,
evidence-based compatibility verdict for future independent-instance support.
Stage 3 creates, modifies, launches, and deletes nothing.

## 1. Discovery sources (read-only)

| Source | Registry / API | Scope | Notes |
|---|---|---|---|
| Current-user uninstall registrations | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` (direct subkeys only) | Current user | No elevation needed |
| Machine 64-bit uninstall registrations | `HKLM\...\Uninstall` with `RegistryView.Registry64` | Machine | Readable by standard users |
| Machine 32-bit uninstall registrations | `HKLM\...\Uninstall` with `RegistryView.Registry32` | Machine | Readable by standard users |
| Packaged apps | `PackageManager.FindPackagesForUser("")` (current user, metadata only) | Current user | Framework and resource packages skipped |

Rules enforced in code (`Mitosis.Windows.Platform/Discovery/`):

- One registry level only: the Uninstall key plus its direct subkeys. Never recursive.
- Keys open read-only (`writable: false`); missing or inaccessible keys yield empty results.
- No value is executed, no path is enumerated or followed for further scanning.
- No icons, metadata, or content downloaded; `DisplayIcon` is recorded as a string only.
- Entries without `DisplayName` are skipped; `SystemComponent = 1` and child entries
  (`ParentKeyName`) are skipped; malformed versions are kept verbatim with a warning.
- Per-entry and per-source failures are tolerated; cancellation is honored throughout.
- Standard user account suffices (verified non-elevated, Medium integrity).

Unreliable-source policy (documented, not worked around): entries that name no
launchable executable are still listed (registration ≠ launchable); executable paths
are left empty rather than guessed from names, publishers, or install folders.

## 2. Data model and identity (`Mitosis.Domain/Discovery.cs`)

`DiscoveredApp`: stable `Id`, `DisplayName`, optional `Publisher`/`Version`/
`InstallLocation`/`ExecutablePath`/`IconReference`, `PrimarySource` + full `Sources`
list, `Scope` (CurrentUser/Machine/Unknown), `IsSystemComponent`, per-record `Warnings`.
Missing metadata stays missing — never fabricated.

Identity (`AppIdentity`): `{source}:{sha256(source␀name␀publisher)[0..8, hex]}` over
trimmed, lowercased name + publisher. Not path-based: reinstalls keep identity while
renames do not collide. Case and surrounding whitespace are insignificant.

Deduplication (`WindowsAppDiscoveryService.Merge`): registrations with equal normalized
keys merge into one entry; sources union; Machine scope wins; first source in
composition order (user before machine) wins display fields; final order is
name (`OrdinalIgnoreCase`) then id — fully deterministic.

## 3. Compatibility statuses (`Mitosis.Domain/Compatibility.cs`)

| Status | Meaning | When assigned (Stage 3) |
|---|---|---|
| `Unknown` | No validated strategy, no concrete blocker | Every ordinary application (catalog is empty by design) |
| `NeedsReview` | Candidate strategy exists but needs validation | Injected strategy matches with an unmet prerequisite |
| `Unsupported` | Concrete limitation prevents independent copies | System-signed components |
| `Supported` | Documented strategy, all prerequisites met | Only with a validated strategy in the catalog |

`CompatibilityAssessor` (in `Mitosis.Compatibility`, pure, no I/O) matches strategies by
name substring plus an optional scope requirement; unknown scope with a scoped strategy
reports `Confirm installation scope` rather than assuming. Assessments carry reasons,
strategy id, missing prerequisites, and a caller-supplied timestamp (deterministic tests).

No generic Win32, MSIX, Store, or uninstall entry is ever `Supported` without a validated
strategy. macOS launch profiles are not Windows evidence and are not imported.

## 4. UI integration

`IAppDiscoveryService` / `ICompatibilityAssessor` live in `Mitosis.Application.Discovery`;
implemented in `Mitosis.Windows.Platform` / `Mitosis.Compatibility`; composed manually in
`App.xaml.cs` (no container, no new dependencies). `MainViewModel` loads asynchronously
on window activation with loading / empty + retry / error + retry states; search covers
name and publisher; apps group by initial letter; selection survives refresh when the
entry is still present. Engine actions stay honest placeholders.

Preview isolation: production never contains preview rows. On discovery failure the error
state offers an explicit `Show preview instead` action (`ShowPreviewCommand`) — the only
path to sample data, never automatic, never mixed.

## 5. Privacy and read-only guarantees

- Collected per app: display name, publisher, version string, install location string,
  icon reference string, registration source, scope, warnings. No binaries, no file
  contents, no command lines, no environment, no credentials, no browser profiles.
- Discovery errors are reported as exception type names only — never paths or messages
  that could contain account names.
- Nothing is uploaded or transmitted; results stay in memory for the session.
- Registry is only read; application registrations are never altered.

## 6. Failure and retry behavior

- One bad entry or one dead source is skipped; the pass continues.
- Total failure → error state with the exception type and a Retry action (re-queries once).
- Cancellation (e.g. window close mid-load) is silent, not an error.

## 7. Known unsupported categories (Stage 3)

- System-signed packaged components (`SignatureKind.System`) → `Unsupported`.
- Framework and resource packages → filtered before assessment (not applications).
- Everything else → `Unknown` until per-application strategies are validated (Stages 4+).

## 8. Manual verification steps

1. `cd windows; dotnet build Mitosis.sln --no-restore` — 0 warnings, 0 errors.
2. `dotnet test --solution Mitosis.sln --no-build` — all suites green.
3. Launch `Mitosis.exe` non-elevated: sidebar fills with installed apps
   grouped by initial within ~1 s; selecting one shows publisher, version, scope,
   sources, and the `Not yet assessed` verdict with its reason.
4. Search filters by name and publisher; clearing restores the full list.
5. The update banner, empty clone list, and New Clone placeholder remain as in Stage 2.
6. Registry check after a run: no writes (read-only opens only); no new processes
   spawned by discovery (verify with Task Manager if desired).
