# Windows evidence bundles (Stage 8)

Deterministic framework for recording what must be established before any Windows
application isolation strategy can be approved. This stage verifies nothing live:
it defines the record, the validation, and the evaluation. No candidate has passed
live verification; no strategy is registered; nothing executes.

The future live-testing protocol — phases, environment spec, authorization and
abort controls, report template — is specified separately in
`docs/WINDOWS-ISOLATION-VERIFICATION-PLAN.md` (Stage 9A, plan only).

## 1. Evidence schema and status semantics

`EvidenceBundle` (`Mitosis.Domain/Evidence.cs`, schema version 1) carries candidate
id/version, optional app id/version, record GUID, UTC verification date, a
non-sensitive verifier label, exactly the ten required dimensions each with a
status, source references, test method plus reproducible steps, platform
assumptions, outstanding questions, a separate approval record, and a SHA-256
integrity hash over the stored bytes.

Statuses: `Unknown` (nothing established), `NotVerified` (looked at, not proven),
`Verified` (demonstrated), `Failed` (demonstrated broken), `NotApplicable`
(dimension does not apply). Evaluation preserves every status verbatim — nothing
is promoted, nothing is silently converted.

## 2. What qualifies as evidence versus an inference

Evidence: an observed result from stated reproducible steps (a lab run, a measured
behavior, a quoted vendor statement with URL and access date). Inference: anything
concluded without observation — a flag's existence, a portable ZIP's availability,
an assumption that updates behave. The model records both (observed vs expected
plus discrepancy), and evaluation counts only `Verified` dimensions as established.

A documented `--user-data-dir`, `-profile`, or similar option is evidence for one
configuration capability: the application accepts a separate data directory. It is
not evidence of registry isolation, update survival, mutex behavior, handler
separation, or licensing permission, and the framework never treats it as such.

## 3. Required isolation dimensions

Writable data/profile dirs · registry and configuration state · mutexes, named
pipes, single-instance enforcement, IPC · protocol handlers, file associations,
external-link routing · background services, scheduled tasks, shared processes ·
updates, self-updaters, version coexistence · credentials, password stores, cookies,
tokens, sensitive data · install/runtime prerequisites and launch behavior ·
recovery, rollback, removal · licensing, vendor restrictions, security requirements.

## 4. Candidate-by-candidate verification gaps

All research candidates (`ResearchCatalog`) currently verify **zero** isolation
dimensions — their records hold research dispositions, not verification:

- `documented-profile-flags` (eligible for review): unknowns on writable-data
  isolation, registry writes, update survival, mutex/IPC behavior, and handler
  separation per app. Vendor flag docs are the starting evidence, not the verdict.
- `vendor-portable-copy` (needs review): unknowns on per-app license confirmation,
  threat-model review, registry cleanliness, and proof that databases/credentials
  stay out. Password stores and credentials remain categorically excluded.
- `store-msix-copy`, `generic-registry-copy`, vendor channels: rejected; no
  verification authorized or needed.

## 5. Privacy and redaction rules

Bundles hold metadata only: ids, labels, statuses, methods, observations,
summaries. Never passwords, tokens, cookies, databases, user documents, copied
application files, environment dumps, unrestricted command lines, or full local
paths where a redacted summary suffices. Verifier labels reject emails and paths;
store errors name exception types only. UI summaries (`EvidenceSummary`) are
counts-only, so no free text can leak through display or logs. The integrity hash
detects accidental alteration of stored bytes; it is not tamper-proofing and says
nothing about confidentiality.

## 6. Separation of concerns

Evidence (observed facts) → technical evaluation (counts, verdict, missing items)
→ research disposition (is this worth reviewing?) → legal review → owner approval
→ strategy registration (future). Each step is a different record, decided by
different evidence. A technically `Complete` evaluation with `NotRequested`
approval still reports `AwaitingApproval`. `EligibleForImplementation` authorizes
review, never execution.

## 7. Checks requiring separately authorized work

Live per-app verification runs (launching copies in a lab, observing mutexes,
registry, updates, handlers); counsel-level license confirmation; owner approval
of strategy records; any registration of an `IProvisioningStrategy`. None are
authorized by this stage — the framework here is the checklist and the ledger
that such a stage would fill in.
