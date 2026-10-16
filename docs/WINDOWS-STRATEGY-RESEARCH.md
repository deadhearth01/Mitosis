# Windows strategy research (Stage 7)

Evidence-backed review of how a Windows application could become an independent
instance. Researched 2026-10-16 via vendor documentation (no binaries executed,
no applications modified). Structured records live in
`ResearchCatalog` (`Mitosis.CloneEngine/Provisioning/ResearchCatalog.cs`).

## 1. Candidate categories reviewed

### A. Documented separate-profile flags — EligibleForImplementation
Run the vendor binary unmodified with a documented data-directory flag, one
private directory per instance. No files copied.
**Disposition rationale:** the mechanism is vendor-documented for parallel
instances, needs no copying, and keeps the vendor binary, registration, and
updater untouched. Eligibility means "worth engineering review", not approval.

### B. Vendor-shipped portable copies — NeedsReview
Place a vendor portable tree (binary plus local config) per instance.
**Disposition rationale:** permission and local-config behavior are documented for
verified examples, but per-app validation and a threat model are still required.
User databases and credentials are never copied under any mechanism.

### C. Store/MSIX package copies — Rejected
The package is read-only and ACL-protected; third-party copies cannot take over
package identity or servicing.

### D. Arbitrary Win32 install copies — Rejected
No vendor permission and no isolation evidence for any dimension.

### E. Vendor side-by-side channels — Rejected
Parallel identities already exist (browser Beta/Dev, preview IDEs); nothing for
Mitosis to build.

## 2. Evidence sources and dates (accessed 2026-10-16)

| Source | Supports |
|---|---|
| Chromium Docs — User Data Directory (`chromium.googlesource.com/.../user_data_dir.md`) | `--user-data-dir` override incl. Windows example; separate dirs for separate instances |
| Chromium Projects — Creating and Using Profiles (`chromium.org/...`) | Shortcut-per-profile workflow; empty folder bootstraps a profile |
| VS Code CLI docs (`code.visualstudio.com/docs/configure/command-line`) | `--user-data-dir` runs "multiple isolated instances ... with separate environments, settings, and extensions" |
| VS Code Portable mode (`code.visualstudio.com/docs/setup/portable`) | ZIP + `data` folder keeps all state local; update by moving `data` |
| MDN — Multiple Firefox profiles (archived) + Mozilla Support (`-no-remote`) | `-P/-profile` plus `-no-remote` runs parallel instances with separate profiles |
| KeePass Installation / Portability (`keepass.info/help/v2/setup.html`) | Portable ZIP needs no install; config lives beside it; updates preserve it |
| MS Learn — How packaged desktop apps run (`learn.microsoft.com/.../desktop-to-uwp-behind-the-scenes`) | Package read-only; writes under `WindowsApps\<package>` denied |

## 3. Confirmed facts versus inference

Facts (vendor-documented): the flags exist and select per-instance data directories;
empty directories bootstrap fresh profiles; portable forms run from user locations
with local config; MSIX packages are read-only; KeePass ZIP updates preserve config;
`-no-remote` instances cannot receive external links (Mozilla Support).

Inference (unverified, recorded as outstanding questions): per-app single-instance
and mutex behavior; per-app registry writes (including the community VS Code
HKCU-classes report); update survival per app; protocol-handler and file-association
separation; license confirmation beyond the verified examples.

## 4. Isolation dimensions and unresolved questions

For the eligible candidate, proven: install files readable, no copy needed,
launch from the original location, runtimes present, running unmodified binaries
is permitted use. Unknown: writable-data isolation per app, registry isolation
per app, update preservation per app. Those three unknowns are exactly what
engineering review plus per-app verification must close.

## 5. Legal/licensing review limitations

Vendor documentation was checked; no counsel review was performed. Running
vendor binaries as documented is ordinary use, but per-app confirmation is still
required, and password stores, databases, and credentials are categorically
excluded from any mechanism.

## 6. Candidate dispositions

`documented-profile-flags` → **EligibleForImplementation** (review, not approval).
`vendor-portable-copy` → **NeedsReview**. `store-msix-copy`,
`generic-registry-copy`, `vendor-side-by-side-channels` → **Rejected**.
Candidates are data records, never strategies: the planner cannot select them and
`ProductionCatalog` remains empty.

## 7. Verification checklist (Stage 8)

Per-candidate isolation dimensions are all **unverified**: no live verification runs
are authorized, so no dimension is marked `Verified` for any candidate. The
evidence framework (`docs/WINDOWS-EVIDENCE-BUNDLES.md`) defines the ten required
dimensions, the bundle schema, and the evaluation rules that a future authorized
stage would use:

| Candidate | Data dirs | Registry | Mutex/IPC | Handlers | Services | Updates | Sensitive data | Launch | Recovery | Licensing |
|---|---|---|---|---|---|---|---|---|---|---|
| documented-profile-flags | ? (flag docs exist) | ? | ? | ? (`-no-remote` caveat noted) | ? | ? | n/a by design (no user data touched) | ? | ? | ? (ordinary use; per-app confirm pending) |
| vendor-portable-copy | ? (local config docs exist) | ? | ? | ? | ? | ? (vendor update notes exist) | excluded by rule | ? | ? | ? (per-app confirm pending) |
| store-msix-copy | rejected | rejected | rejected | rejected | rejected | rejected | rejected | rejected | rejected | rejected |
| generic-registry-copy | rejected (no evidence for any dimension) | | | | | | | | | |
| side-by-side channels | out of scope (nothing to build) | | | | | | | | | |

`?` = unknown: vendor documentation suggests where to look, but nothing is verified.

## 7. Approval requirements for implementation

Per-application verification of the three unknown dimensions; a reviewed threat
model covering single-instance primitives, protocol handlers, and updates;
a lawful-evidence bundle per app; deterministic safety tests against real
fixtures; owner approval of the strategy record. Only then may a strategy record
be promoted to an `IProvisioningStrategy` — a Stage 8+ decision, not this stage.
