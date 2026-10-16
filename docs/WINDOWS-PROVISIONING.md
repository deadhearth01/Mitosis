# Windows provisioning strategy framework (Stage 6)

Planning-only architecture for turning a discovered application into an independent
instance. Stage 6 plans but never executes: the production catalog is empty, the
planner refuses everything, and no executor exists.

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
  a future `IProvisionExecutor` takes the immutable plan plus an
  `IProgress<ProvisionProgress>` sink.

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
by a future provisioner after genuine verified work. No executor ships in Stage 6,
so every request is refused by the missing-executor rule at the latest.

## 7. Supported and unsupported categories

- Supported: none. Store/MSIX packages are protected installations; registry-listed
  applications lack per-app evidence (user-data locations, single-instance
  behavior, update behavior all unknown); system components are `Unsupported` at
  assessment time. No category clears the evidence bar, so no strategy was
  validated — the empty catalog is the honest outcome, not a gap in the framework.
- The New Clone wizard surfaces this: the review step states that no validated
  provisioning strategy exists and only a planned profile is saved.

## 8. Remaining research and verification needs

Per-application data-isolation research (Electron `--user-data-dir`-style switches on
Windows, Chromium profile dirs, Squirrel/installer layouts, single-instance mutexes,
protocol-handler separation, update-in-place behavior); a lawful copy mechanism
replacing APFS clonefile; signature/integrity story replacing ad-hoc codesign;
reparse-point and ACL handling at execution; schema for strategy evidence bundles;
UI for strategy selection once multiple strategies exist. All belong to Stage 7+.
