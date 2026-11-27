# Windows provisioning strategy framework (Stage 6; execution spine added 2026-11-27)

The framework plans an instance and, since the Stage 28 execution spine, can carry out
the mechanical part: copy an install directory into a private instance directory, write
the instance manifest and launch configuration, create the private data directory, and
launch that copy. It still has **no production strategy**:
`ProductionCatalog.Strategies` is empty, so the planner refuses execution for every real
application and the UI can only save planned profiles. A self-contained probe — the
system command processor, not an installed app — exercises the spine end to end.

## 1. Strategy architecture

- `IProvisioningStrategy` (`Mitosis.CloneEngine/Provisioning/Strategy.cs`): stable
  `Id` + `Version`, `SupportsExecution` (false means planning only), `IsEnabled`,
  `RequiredPrerequisites`, deterministic `AppliesTo(app)` over discovery metadata.
  No I/O, no launching.
- `CompatibilityAssessment` (Stage 3) decides *whether* an app may ever work;
  a provisioning strategy answers *how* one instance would be built. The planner
  requires `Supported` status plus a matching strategy id.
- `ProductionCatalog.Strategies` is **empty by design**: no Windows application
  category has validated isolation evidence yet (see §7).
- Progress and outcomes use `ProvisionProgress` / `ProvisionResult` records;
  `IProvisionExecutor` takes the immutable plan plus a
  `IProgress<ProvisionProgress>` sink (`CopyProvisionExecutor` is the first
  implementation, §9).

## 2. Evidence requirements (`IsolationEvidence`)

Eight flags: install files readable, copying legally/technically permitted,
launch from a private location, writable data isolated, registry/config isolated,
updates preserve the instance, runtimes available, licensing permits. Unknown stays
unknown — a directory copy proves nothing. `Missing()` lists unproven items as plan
warnings. Stage 6 gathers no evidence automatically; capabilities arrive as planner
inputs so every claim stays explicit and testable.

## 3. Capability and isolation model

Capabilities = caller-supplied facts (satisfied prerequisites, evidence, existing
destinations). The planner validates them lexically and logically but never probes
the machine: no filesystem, registry, network, or process access. Reparse-point and
existence checks belong to execution time, which must revalidate everything.

## 4. Operation plan format (`OperationPlan`)

Immutable record: plan/provisioning identity (`PlanId`, `StrategyId/Version`,
`ProfileId`, `AppId`), `InstanceRoot` + `InstanceRelativePath`
(`instances/{profile-guid-N}` — never embeds profile names), required vs satisfied
prerequisites, `Operations` (`CreateInstanceDirectory`, `CopyApplicationFiles`,
`WriteInstanceManifest`, `WriteLaunchConfig`), `ExpectedOutputs`, `Warnings`,
`Failures`, `CanExecute`, timestamp. Refused plans carry failures and empty
operations/outputs.

## 5. Destination safety rules (`DestinationSafety`)

- Destinations are relative paths under one absolute, normalized, application-owned
  root. Absolute paths, empty segments, `.`/`..`, trailing dots/spaces, invalid
  filename characters, and >200-char paths are rejected.
- Profile display names never enter destinations (GUID-hex only), so hostile names
  (`..\..\evil`, `..`, trailing spaces) cannot escape by construction.
- Existing destinations collide safely: planning refuses instead of overwriting.
- Registry-reported paths are never destinations; the install location is recorded
  as a future copy source to be revalidated at execution.

## 6. Execution boundary (`ProvisioningGate`)

`ExecuteAsync` admits a request only when: the plan `CanExecute` with zero failures,
an executor is installed, plan and strategy ids/versions still match, the strategy
is enabled and supports execution, prerequisites are still satisfied, and evidence
is still complete. Anything else returns `Refused` without touching the system.
Profile records are never mutated by planning or gating — `Ready` can only be set
by a future provisioner after genuine verified work. No executor shipped in Stage 6;
the Stage 28 spine adds one (§9), but the gate still refuses every real application
because the production catalog is empty.

## 7. Supported and unsupported categories

- Supported: none. Store/MSIX packages are protected installations; registry-listed
  applications lack per-app evidence (user-data locations, single-instance
  behavior, update behavior all unknown); system components are `Unsupported` at
  assessment time. No category clears the evidence bar, so no strategy was
  validated — the empty catalog is the honest outcome, not a gap in the framework.
- The 2026-11-27 execution spine changes the mechanism, not the evidence: the
  probe proves that copy, manifest, launch configuration, private data directory,
  and launch work for a controlled target. It says nothing about any installed
  application's data, registry, mutex, update, or licensing behavior, so the
  catalog stays empty.
- Stage 7 research (`docs/WINDOWS-STRATEGY-RESEARCH.md`) advanced the question:
  vendor-documented profile flags are eligible for engineering review, portable
  copies need review with a threat model, and Store/MSIX plus generic copies are
  rejected. Research records are data, not strategies: the production catalog
  remains empty and nothing executes.
- The New Clone wizard surfaces this: the review step states that no validated
  provisioning strategy exists and only a planned profile is saved.

## 8. Remaining research and verification needs

Per-application data-isolation research (Electron `--user-data-dir`-style switches on
Windows, Chromium profile dirs, Squirrel/installer layouts, single-instance mutexes,
protocol-handler separation, update-in-place behavior); reparse-point and ACL handling
at execution (the copy executor refuses reparse points rather than traversing them);
schema for strategy evidence bundles; UI for strategy selection once multiple
strategies exist. The file-copy mechanism that replaces APFS clonefile now exists (§9);
everything per application remains Stage 7+ research.
Evidence recording for these dimensions is defined in
`docs/WINDOWS-EVIDENCE-BUNDLES.md` (Stage 8); verification itself stays unauthorized
until a dedicated live-testing stage conducted under
`docs/WINDOWS-ISOLATION-VERIFICATION-PLAN.md` (Stage 9A, plan only).

## 9. Execution spine (Stage 28, 2026-11-27)

Two engine components now exist; neither is reachable for a real application because
no strategy is registered.

`CopyProvisionExecutor` (`Mitosis.CloneEngine/Provisioning/CopyProvisionExecutor.cs`)
revalidates the plan at execution time, then copies the install directory, writes the
manifest and launch configuration, and creates the private data directory. Its rules:

- Refuses an unadmitted plan, a non-normalized or non-absolute instance root, a drive
  or share root, a source that is missing or contains/overlaps the instance root, a
  destination that exists, a destination reached through a reparse point, and a launch
  executable that is not part of the source tree.
- Walks the source without following reparse points (refuses any it finds), caps the
  tree at 64 levels, 100,000 files, and 8 GiB, and rejects names that fail
  `DestinationSafety` (so a hostile install directory cannot escape the instance).
- Copies with `overwrite: false`, clears read-only attributes on the copies, preserves
  empty directories, and on any failure deletes the instance directory it created —
  never the source, never anything outside the destination.
- Does not launch, touch the registry, change ACLs, or mutate the profile record.

`InstanceLauncher` re-reads `mitosis-launch.json` and requires the executable, working
directory, and data directory to resolve inside the instance, so a tampered
configuration cannot start an arbitrary program. It substitutes `{instance}` and
`{dataDirectory}` in arguments, keeps at most 64 KiB of output while draining the rest
(so an instance that logs heavily cannot block on a full pipe), refuses with a named
error when the data directory cannot be created, and kills the process tree after the
timeout (30 s by default).

Skeletons written into every instance:

```json
// mitosis-instance.json
{ "SchemaVersion": 1, "PlanId": "…", "StrategyId": "…", "StrategyVersion": "…",
  "ProfileId": "…", "AppId": "…", "SourceRoot": "C:\\…",
  "InstancePath": "C:\\…\\instances\\<profile-guid>", "CreatedAt": "…" }

// mitosis-launch.json
{ "SchemaVersion": 1, "Executable": "app.exe", "Arguments": [],
  "DataDirectory": "data", "WorkingDirectory": "." }
```

**Probe evidence.** Twelve tests cover the executor (copy/manifest/config/writable
copies, existing destination, missing source, reparse point, unadmitted plan), the
launcher (external executable refused, missing configuration refused, placeholder
substitution, 200 KiB of output drained without stalling, an occupied data-directory
name refused), and an end-to-end run: the same source directory is provisioned for two
profiles, both copies launch from their own instance directory, and each writes its
marker only into its own tree. The probe executable is the system command processor, so
no installed application was copied, launched, or modified. Suite 509/509, 0 warnings.

**Not proven by the probe.** Any real application's data, registry, mutex, protocol,
update, or licensing behavior; rollback after a successful provisioning; refresh;
deletion/relink; strategy selection in the UI (the catalog is still empty, so the
wizard still saves planned profiles only). Those remain Stage 28's authorization-gated
remainder and Stages 29–31.
