# Windows isolation verification plan (Stage 9A)

> **Scope and safety statement.** This document is a test plan for a future,
> separately authorized verification stage. It is not permission to run tests.
> Stage 9A performs **no live experiments**: no application launches for
> verification, no copying, no modification, no profile creation in third-party
> apps, no provisioning, no strategy activation, no VM creation, and no system
> configuration changes. A candidate with `EligibleForImplementation` is eligible
> for review only — nothing here authorizes execution.

## 1. Verification phases

### Phase 1 — Authorization and scope definition
- Purpose: establish written permission and exact boundaries before anything else.
- Prerequisites: candidate record, target application/version, named operator and reviewer.
- Permitted: writing the authorization record (§4); nothing that touches software.
- Prohibited: any test activity, environment preparation, or data collection.
- Evidence: the signed authorization record itself.
- Pass/fail: proceeds only with a complete, unexpired authorization record.
- Abort: any missing or expired authorization.
- Reviewer: owner plus an independent reviewer (neither may be the operator).
- Cleanup: none (nothing created).

### Phase 2 — Disposable test-environment preparation
- Purpose: build an isolated, reproducible, discardable Windows environment.
- Prerequisites: Phase 1 approval; environment spec (§3).
- Permitted: provisioning the disposable VM from a clean image; snapshotting;
  installing only the candidate application from vendor media.
- Prohibited: personal accounts, synced profiles, production credentials, private
  keys, personal documents, real app databases; any change to the operator's
  workstation or the project repository.
- Evidence: image provenance, snapshot IDs, tool versions, account setup log.
- Pass/fail: environment matches the spec exactly, else rebuild.
- Abort: any personal or production data entering the environment.
- Reviewer: operator documents; reviewer verifies before Phase 3.
- Cleanup: snapshot restore points retained; nothing persists outside the VM.

### Phase 3 — Baseline capture
- Purpose: record the untouched state everything later is compared against.
- Prerequisites: clean snapshot; candidate installed exactly once, never launched
  for testing beyond installation.
- Permitted: read-only inventory (file listings with hashes, registry exports of
  relevant hives, service/task lists, association tables).
- Prohibited: launching the app except as a Phase 4 test step; modifying anything
  to "prepare" a better baseline.
- Evidence: hashed baseline bundle, redacted where paths name the operator.
- Pass/fail: baseline complete and reproducible, else re-snapshot.
- Abort: baseline tooling writes outside the VM.
- Reviewer: reviewer confirms baseline integrity hash.
- Cleanup: baselines stored with the test report, never on production systems.

### Phase 4 — Controlled test execution
- Purpose: run only the pre-authorized steps from the dimension matrix (§2).
- Prerequisites: baseline hash confirmed; authorization window open.
- Permitted: exactly the operations named in the authorization record.
- Prohibited: everything else — especially writes outside the VM, credential or
  personal-data access, elevation without prior approval, and improvisation.
- Evidence: per-step observations mapped to the ten dimensions.
- Pass/fail: per-dimension criteria in §2; any abort condition fails the run.
- Abort: any §4 abort condition; stop, contain, do not restart without review.
- Reviewer: operator executes; reviewer observes or replays from logs.
- Cleanup: per-step cleanup obligations in the authorization record.

### Phase 5 — Post-test comparison
- Purpose: diff post-test state against baseline to find leakage and shared state.
- Prerequisites: test steps complete or aborted; VM intact.
- Permitted: read-only diffing; copying redacted diffs out of the VM.
- Prohibited: "fixing" the app or environment to get a cleaner diff.
- Evidence: diff artifacts with redactions applied.
- Pass/fail: dimensions evaluated per §2 criteria; ambiguity is inconclusive, never
  a pass.
- Abort: diff tooling touches production systems.
- Reviewer: independent reviewer reproduces the comparison.
- Cleanup: artifacts leave the VM only through the redaction step.

### Phase 6 — Evidence review and independent sign-off
- Purpose: turn observations into evidence bundles (`docs/WINDOWS-EVIDENCE-BUNDLES.md`).
- Prerequisites: redacted artifacts; unresolved questions listed, not hidden.
- Permitted: writing bundles, evaluations, and review notes.
- Prohibited: upgrading statuses to fit a preferred outcome; backfilling methods.
- Evidence: bundles with integrity hashes plus the review record.
- Pass/fail: bundles validate structurally; evaluation verdicts follow mechanically.
- Abort: pressure to pre-populate success (stop and report).
- Reviewer: independent reviewer signs each bundle separately from the operator.
- Cleanup: drafts that did not validate are discarded, not repaired silently.

### Phase 7 — Cleanup, rollback, and environment disposal
- Purpose: leave no trace outside the report and no live test residue anywhere.
- Prerequisites: report complete; bundles signed.
- Permitted: snapshot restore verification, secure VM disposal, log archival per
  retention limits.
- Prohibited: retaining test profiles, copied binaries, or credentials (only
  synthetic data ever existed, and it is destroyed too).
- Evidence: disposal checklist with snapshot-restore proof.
- Pass/fail: disposal verified; anything unverifiable is reported, not assumed.
- Abort: failed restoration — escalate, do not re-test around it.
- Reviewer: reviewer confirms disposal evidence.
- Cleanup: this phase IS cleanup; retention applies to reports only.

## 2. Ten-dimension test matrix

Conventions: each dimension lists the verification question, what static/vendor
review can establish without touching the app, what controlled test may later be
needed, baseline vs post-test observations, artifacts plus redaction, objective
pass/fail/inconclusive criteria, limitations and false positives, prohibited
actions, and specialist review. Unknowns stay unknown until an authorized run
observes them.

### D1 — Writable application data and profile directories
- Question: which directories does an instance write, and are they per-instance?
- Static: vendor docs for data-directory flags and portable layouts.
- Controlled test: run with a fresh private dir; list created files; run a second
  instance with another dir; diff.
- Baseline/post: directory trees with hashes before first launch vs after each run.
- Artifacts: redacted tree listings (drop operator account names).
- Pass: disjoint write sets per instance. Fail: shared files. Inconclusive: app
  refuses to start in the harness (record why).
- Limitations: first-run migrations may differ from steady state; false positive
  if temp files look shared but are per-process.
- Prohibited: seeding real user data; copying an existing profile.
- Review: none specialist by default.

### D2 — Registry and configuration state
- Question: which keys change per instance, and are any shared?
- Static: vendor docs; prior installer manifests.
- Controlled test: export relevant hives before/after each instance run; diff.
- Artifacts: redacted .reg diffs (machine GUIDs and account SIDs removed).
- Pass: per-instance keys only. Fail: shared keys written by both instances.
- Limitations: installer-time keys vs runtime keys; background OS writes are noise.
- Prohibited: editing registry to "isolate" an app; touching hives outside the VM.
- Review: Windows internals familiarity recommended.

### D3 — Mutexes, named pipes, single-instance enforcement, IPC
- Question: can two instances run concurrently without blocking each other?
- Static: vendor docs rarely cover this; strings inspection is inference only.
- Controlled test: launch instance A, then B with a separate profile; record
  blocking dialogs, exit codes, and named kernel objects (Sysinternals-class tools).
- Pass: both run concurrently with separate profiles. Fail: second instance
  attaches to, blocks on, or exits in favor of the first. Inconclusive: flaky
  timing (repeat with delays, still inconclusive if unstable).
- Limitations: timing-sensitive; false positive if a global mutex is unrelated
  (e.g. installer mutex).
- Prohibited: killing or patching mutex handling; kernel tampering.
- Review: Windows systems reviewer for ambiguous IPC findings.

### D4 — Protocol handlers, file associations, external-link routing
- Question: do links and files route to the intended instance?
- Static: registration tables for the candidate's schemes.
- Controlled test: trigger a registered scheme/file from inside the VM; observe
  which instance receives it.
- Pass: deterministic routing to the originating instance. Fail: always the
  original/default instance. Inconclusive: OS broker behavior varies (record build).
- Limitations: `-no-remote`-style instances are known to reject external links
  (Mozilla-documented caveat) — that is a finding, not a failure to hide.
- Prohibited: changing host-level associations outside the VM.
- Review: none specialist by default.

### D5 — Background services, scheduled tasks, shared processes
- Question: does the app share services/tasks/processes across instances?
- Static: vendor docs; installer task/service inventory.
- Controlled test: snapshot service/task/process tables per instance run; diff.
- Pass: no shared mutable service state. Fail: one service serves both instances
  with shared data. Inconclusive: vendor services update on their own schedule.
- Limitations: OS-scheduled vendor maintenance is noise, not signal.
- Prohibited: disabling, reconfiguring, or removing services/tasks.
- Review: Windows systems reviewer if services are involved.

### D6 — Updates, self-updaters, version coexistence
- Question: do updates preserve both instances and their data?
- Static: vendor update documentation.
- Controlled test (only if authorized): run the vendor updater in the VM; verify
  both instances and both data dirs survive with contents intact.
- Pass: both instances functional with data intact. Fail: updater merges,
  migrates, or deletes an instance. Inconclusive: updater refuses in a VM.
- Limitations: staged rollouts differ; VM detection may alter updater behavior.
- Prohibited: forcing updates outside vendor flow; testing on production installs.
- Review: release-engineering familiarity advised.

### D7 — Credentials, password stores, cookies, tokens, sensitive data
- **Strict rule: synthetic test data only.** Generate fresh test accounts, dummy
  vaults, and placeholder tokens inside the VM. Never import or inspect real
  credentials, password databases, cookies, tokens, or private user data.
- Question: does any instance read or write another instance's secrets?
- Static: vendor storage documentation.
- Controlled test: place distinct synthetic secrets per instance; operate both;
  check for cross-reads via file/registry diffs (never by opening secret stores
  beyond verifying separation markers).
- Pass: secrets stay per-instance. Fail: any cross-instance read/write.
  Inconclusive: app encrypts opaquely and refuses inspection (record, do not break).
- Limitations: encryption prevents content checks — separation is proven by
  container boundaries, not plaintext.
- Prohibited: real credentials, real vaults, bypassing encryption, exfiltration.
- Review: security reviewer mandatory for this dimension.

### D8 — Installation/runtime prerequisites and launch behavior
- Question: what must be present for an instance to start, and does it start
  cleanly from a private location?
- Static: vendor requirements (.NET/VC++ runtimes, OS build).
- Controlled test: launch from the private location with dependency logging;
  record exit codes and missing-dependency errors.
- Pass: clean launch with documented prerequisites. Fail: hard dependency on the
  original install path. Inconclusive: first-launch setup differs (record).
- Limitations: WebView/runtime downloads need a network decision in authorization.
- Prohibited: installing prerequisites system-wide outside the VM to "make it work".
- Review: none specialist by default.

### D9 — Recovery, rollback, and removal implications
- Question: can a broken instance be removed or rolled back without harming others?
- Static: vendor uninstall/repair documentation.
- Controlled test: break one instance (authorized step only), remove it per the
  documented procedure, verify the other instance unaffected.
- Pass: removal is scoped. Fail: shared uninstaller removes both. Inconclusive:
  uninstaller requires interaction the harness cannot give.
- Limitations: uninstallers are destructive by nature — test only in the VM.
- Prohibited: testing removal anywhere except the disposable VM.
- Review: none specialist by default.

### D10 — Licensing, vendor restrictions, security requirements
- Question: does the vendor permit this use, and do security controls allow it?
- Static: license text, portable-use statements, DRM/package protections.
  This dimension is substantially completable without live tests.
- Controlled test: none by default; attempted circumvention is prohibited, not tested.
- Pass: explicit vendor permission for the exact use. Fail: protection or license
  forbids it (e.g. read-only signed packages). Inconclusive: ambiguous terms
  (escalate to counsel, do not interpret).
- Limitations: license interpretation is not engineering; counsel decides.
- Prohibited: bypassing DRM, package protections, or license checks to "see if it works".
- Review: legal review mandatory before any non-`Rejected` disposition here.

## 3. Disposable test environment specification

- Windows version/build and architecture pinned in the authorization record
  (match the app's supported floor; default to a current Windows 11 build).
- Clean snapshot or image with recorded provenance (source ISO/image hash,
  creation date, builder).
- Dedicated non-administrator test account where feasible; elevation only if the
  authorization explicitly allows a step, never by default.
- No personal accounts, no synced profiles, no production credentials, private
  keys, personal documents, or real app databases — synthetic data only.
- Network policy per test: default deny; outbound allowed only when a named step
  requires it (e.g. runtime download, updater test), recorded in advance.
- Snapshot before installation, after installation, and before each destructive
  step; restoration verified by hash comparison.
- Reproducible tool versions (Process Monitor-class tools, diff tooling, scripting
  runtime) pinned by version and hash where practical.
- Time synchronized (NTP) with UTC timestamps on all artifacts.
- Disk capacity planned for images, snapshots, and logs; retention limits set in
  the authorization (default: report + redacted logs only, 90 days).
- Full isolation from the operator's workstation and the project repository: no
  shared folders; artifacts leave the VM only as redacted text through an
  explicit, reviewed export step.
- Disposal: snapshot-restore proof or secure VM deletion, verified by the reviewer;
  no residue (binaries, profiles, synthetic secrets) survives anywhere.

## 4. Authorization and abort controls

Authorization record fields: candidate ID + version; exact application and
vendor/version; test environment identifier + snapshot IDs; permitted operations
(enumerated steps); prohibited operations (explicit list, default-deny remainder);
test start/end window; operator and reviewer labels (non-sensitive); network
access decision; data-handling constraints (synthetic-only rule restated);
rollback and disposal plan; approval date and expiry; conditions requiring renewed
approval (any scope change, any abort, any new application version, expiry).

Immediate abort conditions: any unexpected write outside the disposable
environment; any contact with real credentials or personal data; unexpected
registry, service, scheduled-task, or system-wide changes; unexpected outbound
network activity; evidence of shared state or cross-instance leakage; a step
needing elevation without prior approval; an ambiguous result that cannot be
safely interpreted; failed cleanup or snapshot restoration.

On abort: stop the test immediately, preserve only redacted evidence, perform no
further experimentation, dispose or freeze the environment per the rollback plan,
and require a fresh review before any restart. An aborted run is never a pass.

## 5. Evidence collection and review alignment

Reviewers classify every finding exactly one way: vendor-documented capability;
static source/configuration evidence; direct observation from an authorized
controlled test; inference (marked as such, never counted); unknown or unverified;
failed; or not-applicable with written justification. Required record fields:
UTC timestamps, environment + snapshot IDs, source URLs with access dates,
reproducibility details (tool versions, steps, inputs), redactions applied,
integrity hashes with their stated limitation (accidental-change detection, not
tamper-proofing or confidentiality). Contradictory evidence is recorded side by
side with both sources; the dimension stays non-`Verified` until resolved by
further authorized observation. Independent review is a separate signed record per
bundle. Technical completeness is not legal clearance, not owner approval, and
not authorization to execute a strategy — each needs its own record and reviewer.

## 6. Candidate-specific planning

Dispositions from `docs/WINDOWS-STRATEGY-RESEARCH.md` are preserved unchanged:

| Candidate | Disposition | Unresolved questions carried forward |
|---|---|---|
| documented-profile-flags | EligibleForImplementation (review only) | Writable-data isolation, registry writes, update survival, mutex/IPC behavior, protocol-handler separation, version coexistence — per app |
| vendor-portable-copy | NeedsReview | License confirmation per app, threat-model review, proof that databases/credentials stay out (categorically excluded regardless) |
| store-msix-copy | Rejected | None — package read-only, servicing OS-owned |
| generic-registry-copy | Rejected | None — no permission, no evidence for any dimension |
| vendor-side-by-side-channels | Rejected | None — nothing to build |

No candidate is promoted, no strategy registered, no gate behavior changed.

## 7. Reproducibility and test reporting

Future report template: candidate + application version; authorization reference;
environment + snapshot IDs; tool/script versions; test-case ID + revision;
preconditions and expected results; actual observations; evidence artifact
references; status per dimension (pass / fail / inconclusive); deviations and
limitations; cleanup/rollback result; operator and independent reviewer;
follow-up actions and approval state. Rule: an incomplete or inconclusive result
must never be represented as a pass — in reports, in bundles, or in conversation.
