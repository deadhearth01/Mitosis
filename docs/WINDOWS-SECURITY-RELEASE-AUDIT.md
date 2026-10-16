# Windows security and release-readiness audit (Stage 10)

Static audit of the Windows port on `WindowsVersion` (HEAD `04ebf92` at audit time).
Methods: source review with file:line citations, targeted greps, and the pinned
`dotnet restore/build/test` suite (205/205 green before fixes). No live application
experiments, no binaries executed, no system modifications, no fuzzing.

## 1. Findings

### F-M1 — DestinationSafety accepts DOS reserved device names (Medium, confirmed, fixed)
`windows/src/Mitosis.Domain/Provisioning.cs:92-138` rejects traversal, absolute
paths, trailing dots/spaces, invalid chars, and overlong paths, but not reserved
device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, with or
without extensions). A future executor creating `instances\<guid>\CON` would hit
device-namespace behavior instead of a directory. Impact today: none executable
(planning only, GUID-hex destinations), but the validator misrepresents its
guarantee. Fixed: reserved names (and `name.ext` forms) rejected; regression test
`DestinationSafety_RejectsReservedDeviceNames`. Stage: in scope.

### F-M2 — Evidence access dates parse culture-dependently (Medium, confirmed, fixed)
`EvidenceEvaluation.cs:122` used `DateOnly.TryParse`, so `10/11/2026` means
different dates under different cultures — an integrity defect in a ledger whose
dates establish evidence provenance. Fixed: strict ISO `yyyy-MM-dd`
(`TryParseExact`, invariant culture); regression tests for ambiguous and malformed
dates. Stage: in scope.

### F-M3 — Verifier label allows ':' and control characters (Low, confirmed, fixed)
`EvidenceEvaluation.cs:70-71` rejected `@`, `\`, `/` (emails/paths) but not `:`
(`C:foo`, `NT:authority`-style strings) or control characters. Fixed: both rejected;
regression tests added. Stage: in scope.

### F-L1 — Color swatch buttons lack accessible names (Low, confirmed, fixed attempt)
`Views/NewCloneWindow.xaml:158+` swatches expose only tooltips; macOS names every
swatch (`BadgeEditing.swift:41-42`). Fixed by binding `AutomationProperties.Name`
to the color name; if the compiled binding proves invalid it reverts to tooltip
(see verification below). Regression: manual Narrator pass still required
(checklist §3). Stage: in scope.

### F-L2 — Retry test uses a fixed 500 ms delay (Low, limitation, fixed)
`ProfileViewModelTests.ProfilesError_RetryRecovers` waited a fixed delay for an
async-void command. Harmless today (in-memory fakes) but timing-coupled. Fixed by
polling up to 5 s for the expected state. Stage: in scope.

### I-1 — Cross-process concurrent writes serialize per-instance only (Informational)
`JsonProfileRepository`/`JsonEvidenceStore` guard overlapping writes with an
instance `SemaphoreSlim`; two app processes could interleave. Atomic temp+replace
means last-writer-wins without torn files, and single-instance ownership is the
documented assumption. No corruption path; a cross-process lock awaits a real
multi-instance requirement. Not a defect.

### I-2 — No Windows CI workflow (Informational)
`.github/workflows/` runs macOS Swift builds only (preserved untouched, verified:
no `.github` changes on the branch). Windows validation is local `dotnet` commands
per `windows/README.md`. Recommend a `windows-ci.yml` (windows runner, restore,
build, test) as a follow-up; not added here because it cannot be exercised from
this environment.

### I-3 — Future executor exception contract undefined (Informational)
`ProvisioningGate.ExecuteAsync` propagates executor exceptions; no executor ships,
so this is unreachable today. A future executor must define error mapping before
registration. Not a defect.

### I-4 — Planner safety is lexical by design (Informational)
`DestinationSafety` cannot detect reparse points, ACL attacks, or TOCTOU races;
those belong to execution time with revalidation (documented in
`WINDOWS-PROVISIONING.md` §3/§5). No executor exists, so no reachable path.
Not a defect.

### I-5 — Integrity hash limits already documented (Informational)
SHA-256 over stored bytes detects accidental alteration only; not tamper-proofing,
not confidentiality (`WINDOWS-EVIDENCE-BUNDLES.md` §5). Verified as stated.

### Verified good (no finding)
Read-only registry (`RegistryReader.cs:24,38`, `writable: false`); scoped views
and current-user packaged enumeration; empty `ProductionCatalog` (`Gate.cs:62-65`);
gate refuses missing/disabled/mismatched/unsupported plans and null executors;
double-submit guard plus repository backstop; atomic saves with preservation tests;
type-name-only errors with `~` redaction; single external package
(`Microsoft.WindowsAppSDK` 2.5.1) plus pinned test SDKs; macOS tree untouched
(no `Sources/`, `Assets/`, `scripts/`, `.github/`, `website/`, `Tests/` deltas);
zero process-launch, network, or registry-write APIs in `windows/src`.

## 2. Test-quality notes
- Negative and boundary coverage is strong (refusals, malformed JSON, locked
  files, cancellation, concurrency, determinism). Fixed gaps: reserved names,
  ISO dates, verifier charset, null approval/dimensions cases added here.
- `LoadAsync_FailureShowsErrorAndRetryWorks` shares the fixed-delay pattern;
  same polling treatment applied.
- No machine-dependent tests: all platform tests use fakes; the temp-console
  smoke test from Stage 3 was deleted after use per policy.
- Test names match what they prove; no test claims isolation of real apps.

## 3. Release-readiness checklist and statuses

| Area | Status | Evidence |
|---|---|---|
| Windows project builds and tests | Ready | 14/14, 0 warnings/errors; suite green (see final counts) |
| Windows UI functional completeness | Partially Ready | Shell, discovery, profiles, wizard metadata flow work; Settings/Help/onboarding/tray/wizard polish pending |
| macOS UI and feature parity | Partially Ready | Matrix tracks per-row status; pixel parity explicitly unverified |
| Real application-isolation evidence | Blocked | By design: zero dimensions verified, catalog empty |
| Provisioning implementation and safety | Blocked | By design: framework only, execution disabled |
| Windows packaging and runtime installation | Partially Ready | Unpackaged + machine runtime documented; no MSIX/Store story yet |
| Security review | Partially Ready | This audit; M1–M3/L1–L2 fixed; I-items open |
| Accessibility and manual UI verification | Partially Ready | Names/keyboard/focus in place; Narrator + screenshot pass outstanding |
| Licensing and distribution readiness | Blocked | No counsel review; PolyForm terms + trademark note recorded |

Not production-ready: pixel parity unverified, strategy catalog intentionally
empty, isolation unverified, licensing review and owner approval outstanding,
and unpackaged runtime requirements must shape any distribution story.

## 4. Remediation log (this stage)
F-M1 (reserved names + tests), F-M2 (ISO dates + tests), F-M3 (verifier charset +
tests), F-L1 (swatch names; compile-verified, Narrator pass still required),
F-L2 (polling retries + null approval/dimensions/go-back gating tests). No safety
boundary weakened; no provisioning enabled; no macOS files touched; no new
runtime dependencies.
