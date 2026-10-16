using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>Stage 7 research catalog: evidence-backed strategy candidates with explicit
/// dispositions. These records are not strategies — no <see cref="IProvisioningStrategy"/>
/// instance exists for any of them, so the planner and gate cannot select them.
/// Full analysis: docs/WINDOWS-STRATEGY-RESEARCH.md (sources accessed 2026-10-16).</summary>
public static class ResearchCatalog
{
    public static IReadOnlyList<StrategyCandidate> Candidates { get; } = new[]
    {
        new StrategyCandidate(
            Id: "documented-profile-flags",
            Version: "1",
            Category: "Vendor-documented separate-profile flags (no copying)",
            ApplicabilityLimits: "Only applications whose vendor documents a data-directory or profile flag " +
                "(verified examples: Chromium --user-data-dir, VS Code --user-data-dir, Firefox -P/-profile). " +
                "Per-application validation still required.",
            Sources: new[]
            {
                new EvidenceSource("Chromium Docs - User Data Directory",
                    "https://chromium.googlesource.com/chromium/src/+/HEAD/docs/user_data_dir.md", "2026-10-16"),
                new EvidenceSource("Creating and Using Profiles - The Chromium Projects",
                    "https://www.chromium.org/developers/creating-and-using-profiles", "2026-10-16"),
                new EvidenceSource("VS Code Command Line Interface (--user-data-dir)",
                    "https://code.visualstudio.com/docs/configure/command-line", "2026-10-16"),
                new EvidenceSource("VS Code Portable mode",
                    "https://code.visualstudio.com/docs/setup/portable", "2026-10-16"),
                new EvidenceSource("Multiple Firefox profiles - MDN (archived)",
                    "https://web.archive.org/web/20150604004940/https://developer.mozilla.org/en-US/docs/Mozilla/Multiple_Firefox_Profiles",
                    "2026-10-16"),
                new EvidenceSource("Mozilla Support: -no-remote runs another instance with its own profile",
                    "https://support.mozilla.org/en-US/questions/1082396", "2026-10-16"),
            },
            Prerequisites: new[]
            {
                "Vendor documents the flag for the exact application and version",
                "A private, user-writable data directory per instance",
                "Single-instance behavior verified for the flag combination",
            },
            IsolationModel: "Original binary runs unmodified; each instance gets its own writable data " +
                "directory via the documented flag. No files are copied.",
            RegistryImplications: "Largely untouched by design; community reports of VS Code writing " +
                "HKCU file associations are unverified and must be checked per app.",
            UpdateImplications: "In-place updates reuse the same directories (unverified per app); " +
                "profiles are expected to survive updates.",
            LegalReview: LegalReviewStatus.VendorDocsChecked,
            Evidence: new IsolationEvidence(
                InstallFilesReadable: true,
                CopyPermitted: true,
                LaunchFromPrivateLocation: true,
                WritableDataIsolated: false,
                RegistryStateIsolated: false,
                UpdatesPreserveInstance: false,
                RuntimesAvailable: true,
                LicensingPermits: true),
            OutstandingQuestions: new[]
            {
                "Per-app single-instance and mutex behavior with the flag",
                "Registry writes per application (verify, do not assume)",
                "Update survival per application (verify with real updates)",
                "Protocol-handler and file-association separation per app",
                "External-link routing for -no-remote-style instances",
            },
            Disposition: CandidateDisposition.EligibleForImplementation),
        new StrategyCandidate(
            Id: "vendor-portable-copy",
            Version: "1",
            Category: "Vendor-shipped portable copies (binary + local config, no installer)",
            ApplicabilityLimits: "Only applications whose vendor ships and permits a portable form " +
                "(verified examples: KeePass portable ZIP, VS Code ZIP + data folder). Databases, " +
                "credentials, and user content are never copied regardless of mechanism.",
            Sources: new[]
            {
                new EvidenceSource("KeePass Installation / Portability",
                    "https://keepass.info/help/v2/setup.html", "2026-10-16"),
                new EvidenceSource("VS Code Portable mode",
                    "https://code.visualstudio.com/docs/setup/portable", "2026-10-16"),
            },
            Prerequisites: new[]
            {
                "Vendor ships a portable form and permits running it from user locations",
                "Configuration location proven local to the copy",
                "Per-application threat model reviewed",
            },
            IsolationModel: "A vendor portable tree is placed per instance with its own local " +
                "configuration; user databases and credentials are never copied.",
            RegistryImplications: "Portable form must be proven registry-clean per app; installer " +
                "variants of the same app are out of scope for this candidate.",
            UpdateImplications: "Replace-the-tree updates preserve local config per vendor docs " +
                "(KeePass); verify per app.",
            LegalReview: LegalReviewStatus.VendorDocsChecked,
            Evidence: new IsolationEvidence(
                InstallFilesReadable: true,
                CopyPermitted: false,
                LaunchFromPrivateLocation: true,
                WritableDataIsolated: false,
                RegistryStateIsolated: false,
                UpdatesPreserveInstance: false,
                RuntimesAvailable: true,
                LicensingPermits: false),
            OutstandingQuestions: new[]
            {
                "Per-application license confirmation beyond the two verified examples",
                "Single-instance behavior per app",
                "Registry writes per app (verify, do not assume)",
                "Guarantee that no user database is ever co-located with the portable tree",
            },
            Disposition: CandidateDisposition.NeedsReview),
        new StrategyCandidate(
            Id: "store-msix-copy",
            Version: "1",
            Category: "Copying Microsoft Store / MSIX packaged installations",
            ApplicabilityLimits: "None: the package is read-only and ACL-protected.",
            Sources: new[]
            {
                new EvidenceSource("Understanding how packaged desktop apps run on Windows - MSIX",
                    "https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes",
                    "2026-10-16"),
            },
            Prerequisites: Array.Empty<string>(),
            IsolationModel: "None: writes inside the package are not allowed.",
            RegistryImplications: "Package identity and registration are OS-managed; third-party " +
                "copies cannot take them over.",
            UpdateImplications: "Store servicing owns the package; copies would detach and rot.",
            LegalReview: LegalReviewStatus.VendorDocsChecked,
            Evidence: IsolationEvidence.None,
            OutstandingQuestions: Array.Empty<string>(),
            Disposition: CandidateDisposition.Rejected),
        new StrategyCandidate(
            Id: "generic-registry-copy",
            Version: "1",
            Category: "Copying arbitrary Win32 install directories found via uninstall registrations",
            ApplicabilityLimits: "None: no vendor permission and no isolation evidence.",
            Sources: Array.Empty<EvidenceSource>(),
            Prerequisites: Array.Empty<string>(),
            IsolationModel: "Unknown: data locations, registry use, services, single-instance " +
                "behavior, and update behavior are all unverified per app.",
            RegistryImplications: "Unknown and potentially shared; copying cannot separate them.",
            UpdateImplications: "In-place updaters would target the original or break the copy.",
            LegalReview: LegalReviewStatus.NotStarted,
            Evidence: IsolationEvidence.None,
            OutstandingQuestions: new[]
            {
                "Everything: this category has no evidence for any isolation dimension.",
            },
            Disposition: CandidateDisposition.Rejected),
        new StrategyCandidate(
            Id: "vendor-side-by-side-channels",
            Version: "1",
            Category: "Vendor-shipped parallel channels (e.g. browser Beta/Dev, preview IDEs)",
            ApplicabilityLimits: "These identities already exist; there is nothing for Mitosis to build.",
            Sources: Array.Empty<EvidenceSource>(),
            Prerequisites: Array.Empty<string>(),
            IsolationModel: "Vendor-owned separate identities; out of scope for Mitosis-created copies.",
            RegistryImplications: "Owned by the vendor installers.",
            UpdateImplications: "Owned by the vendor updaters.",
            LegalReview: LegalReviewStatus.NotStarted,
            Evidence: IsolationEvidence.None,
            OutstandingQuestions: Array.Empty<string>(),
            Disposition: CandidateDisposition.Rejected),
    };
}
