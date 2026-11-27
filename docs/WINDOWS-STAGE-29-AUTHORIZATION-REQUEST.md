# Stage 29 authorization request: first real clone (Brave profile flag)

> **Status: request only.** Nothing in this document is approved. No third-party
> application has been launched, copied, or modified for verification. The
> production strategy catalog is still empty and every execution gate is closed.
> This file exists so the next step can be authorized (or refused) in writing
> instead of inferred.

## 1. The decision being requested

Permission to build one production strategy and run one controlled isolation
test against a single named application, in a disposable environment, using the
vendor-documented profile flag rather than copying the installation. If the
evidence passes an independent review, a separate decision is still required
before the strategy is registered in `ProductionCatalog`.

Three separate approvals are involved and this document asks for all three
explicitly:

1. **Implementation** - write the strategy, the non-copying executor, and the
   tests that can execute provisioning.
2. **Execution** - launch the named application twice in a disposable
   environment and observe it.
3. **Registration** - allow the strategy into `ProductionCatalog` after the
   evidence is reviewed. (Not requested yet; it depends on the result.)

## 2. Candidate and why it is first

| Item | Value |
|---|---|
| Candidate record | `documented-profile-flags` v1, disposition `EligibleForImplementation` (`ResearchCatalog.cs`; `WINDOWS-STRATEGY-RESEARCH.md`) |
| Primary application | Brave `158.1.100.59` (`C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe`) |
| Fallback application | Microsoft Edge `158.0.4370.45` (`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`) |
| Mechanism | Vendor-documented `--user-data-dir=<private directory>`; the installed binary runs unmodified and no application files are copied |
| Vendor documentation | `https://chromium.googlesource.com/chromium/src/+/HEAD/docs/user_data_dir.md`, `https://www.chromium.org/developers/creating-and-using-profiles` (accessed 2026-10-16) |

Reasons this is the smallest honest first test:

- The mechanism is documented by the vendor and needs no file copying, so the
  largest data-loss risk in a naive clone is absent by design.
- The writable state that matters (cookies, history, cache, Local State,
  extensions) lives under the flagged directory, which gives a checkable
  boundary.
- A fresh profile has no account, so no personal data enters the test.
- The existing `CopyProvisionExecutor` does **not** fit this model and must not
  be reused: copying a browser installation is unnecessary and outside the
  candidate's isolation model.

Risks that the test is designed to measure, not assume: single-instance/mutex
behaviour when the normal browser is already running, registry writes per
profile, protocol/file-association handling, update survival, and whether any
process writes outside the flagged directory.

## 3. Code to be written before any execution

All of it is testable without touching the application, and all of it stays
unreachable from the product until the strategy is registered.

1. `ProfileFlagStrategy` implementing `IProvisioningStrategy`:
   `Id = "documented-profile-flags"`, `Version = "1"`, prerequisites from the
   candidate record, `AppliesTo` matched on the exact application identity and
   executable, `SupportsExecution` false until the authorization record exists.
2. `ProfileFlagProvisionExecutor` implementing `IProvisionExecutor`:
   creates only `<instance root>\instances\<profileId>\data`, writes
   `mitosis-instance.json` and `mitosis-launch.json` (arguments include
   `--user-data-dir={dataDirectory}`), refuses an existing destination, creates
   no files outside that tree, and removes only what it created on failure.
   It never copies application files and never writes the registry.
3. Launch through the existing `InstanceLauncher` with the vendor executable,
   the instance working directory, and the substituted data directory.
4. Tests: plan/gate refusals, executor creation and idempotent refusal, launch
   configuration contents, deletion of only the created tree, and a simulated
   double-instance run. No third-party process is started in CI.

## 4. Exact operations proposed for the execution run

Environment first (Phase 2 of `WINDOWS-ISOLATION-VERIFICATION-PLAN.md`): a
disposable Windows 11 VM with a clean snapshot, no personal accounts, no
synced profile, no stored credentials, network policy decided in the
authorization record. Nothing runs on the development machine unless that is
explicitly chosen instead and the additional risk is accepted in writing.

Then, on that machine:

1. Install Brave once from vendor media at the recorded version; never launch
   it before the baseline capture.
2. Capture the baseline: `%LOCALAPPDATA%\BraveSoftware\Brave-Browser` file list
   with sizes and timestamps, `HKCU\Software\Classes` handler entries, the
   `HKCU`/`HKLM` Brave uninstall and policy keys, running processes, and the
   scheduled-task/service list.
3. Create the instance through Mitosis (no copy): private `data` directory,
   manifest, launch configuration.
4. Launch instance A with `--user-data-dir=<A>\data`. Confirm the process is a
   separate instance, that `Local State` and `Cookies` appear only under A, and
   that the application opens a first-run window with no account.
5. Launch instance B from a second instance directory and confirm both run
   concurrently with separate data.
6. While A and B run, launch the normal browser once and observe whether it
   reuses either private instance or opens its own profile (single-instance
   behaviour).
7. Close both instances, compare the post-state to the baseline, then relaunch
   the normal browser to confirm its own profile is untouched.
8. Delete the two instance directories through Mitosis and confirm only those
   trees disappear. Snapshot-restore the VM or dispose it.

Nothing in this list is executed automatically by the test; each step is an
observed action recorded with timestamps and the tool versions used.

## 5. Evidence to collect

Mapped to the ten dimensions in `WINDOWS-ISOLATION-VERIFICATION-PLAN.md`:
D1 profile directories (the core claim), D2 registry writes, D3 mutexes and
single-instance behaviour, D4 protocol handlers and file associations, D5
services/scheduled tasks, D6 update survival, D7 credentials (expectation:
none, since the profile starts empty), D8 prerequisites and launch behaviour,
D9 removal/rollback, D10 licensing and vendor restrictions.

Each observation is classified exactly one way (vendor-documented capability,
static configuration evidence, direct observation, inference, unknown, failed,
or not applicable) per section 5 of that plan. An aborted run is never a pass,
and contradictory evidence keeps the dimension non-verified.

## 6. Safety and abort controls

- Default deny: anything not listed in section 4 is prohibited.
- Immediate abort on any write outside the disposable environment, any contact
  with real credentials or personal data, any unexpected registry/service/
  scheduled-task change, unexpected outbound traffic, cross-instance leakage,
  a step needing elevation without prior approval, or failed cleanup.
- Rollback: delete only the Mitosis-created instance trees; otherwise restore
  the VM snapshot. The installed browser is left as installed.
- The development machine, its real browser profiles, and this repository are
  never part of the test.

## 7. What the run will not do

- It will not copy or modify Brave's or Edge's installed files.
- It will not open, read, or copy any real browser profile, cookie, login, or
  history database.
- It will not sign in, sync, or create an account.
- It will not write the registry (any observed write is a finding, not a step).
- It will not register the strategy, enable the wizard path, or ship anything.
- It will not run on this machine unless that option is chosen explicitly.

## 8. Authorization record (to be completed by you)

| Field | Value |
|---|---|
| Candidate / version | `documented-profile-flags` / `1` |
| Application + version | Brave `158.1.100.59` (or Edge `158.0.4370.45`) |
| Environment | Disposable VM id + clean snapshot id (or: development machine, risk accepted) |
| Permitted operations | Section 4, steps 1-8, and nothing else |
| Network during the run | Offline / vendor-only / allowed (choose one) |
| Operator label | (non-sensitive) |
| Reviewer label | (must not be the operator) |
| Start / end window | (UTC) |
| Expiry | (UTC) |
| Approval date | (UTC) |
| Conditions | Any scope change, abort, new application version, or expiry requires a fresh record |

## 9. After the run

- Evidence bundle reviewed; each dimension classified; the strategy is
  registered only by a separate written decision.
- Still blocked regardless of the result: rollback and refresh for real clones,
  delete/recovery semantics, launch wiring in the UI, live stats, and anything
  that touches a second application category.
- If the evidence fails or is ambiguous, the strategy stays unregistered and the
  catalog stays empty. A failed run is recorded as a failure, not retried into a
  pass.
