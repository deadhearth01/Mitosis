using Mitosis.Domain;

namespace Mitosis.CloneEngine.Tests;

internal static class EvidenceBundles
{
    public static EvidenceBundle Valid(
        string candidateId = "documented-profile-flags",
        EvidenceStatus status = EvidenceStatus.Unknown,
        ApprovalStatus approval = ApprovalStatus.NotRequested) => new(
            CandidateId: candidateId,
            CandidateVersion: "1",
            AppId: "app:example",
            AppVersion: "1.0",
            RecordId: Guid.NewGuid(),
            SchemaVersion: EvidenceEvaluator.CurrentSchemaVersion,
            VerifiedAtUtc: new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero),
            Verifier: "lab-notebook-7",
            Dimensions: EvidenceEvaluator.RequiredDimensions
                .Select(d => new DimensionEvidence(d, status, "Read vendor docs", "Flag documented", "Flag documented", null))
                .ToList(),
            Sources: new[]
            {
                new EvidenceSourceRef("Vendor docs", "https://example.com/docs", "2026-10-16"),
            },
            TestMethod: "Read the vendor documentation and record the stated behavior.",
            ReproSteps: "1. Open the docs URL. 2. Locate the flag section. 3. Record the claim.",
            PlatformAssumptions: "Windows 11, vendor binary unmodified.",
            OutstandingQuestions: new[] { "Per-app mutex behavior." },
            Approval: new ApprovalInfo(approval, null, null),
            IntegrityHash: null);
}

[TestClass]
public sealed class EvidenceValidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ValidBundle_PassesValidation()
    {
        Assert.AreEqual(0, EvidenceEvaluator.Validate(EvidenceBundles.Valid()).Count);
    }

    [TestMethod]
    public void MissingIdentifiers_AreRejected()
    {
        var bundle = EvidenceBundles.Valid() with { CandidateId = "  ", RecordId = Guid.Empty };
        var errors = EvidenceEvaluator.Validate(bundle);

        Assert.IsTrue(errors.Any(e => e.Contains("Candidate ID")));
        Assert.IsTrue(errors.Any(e => e.Contains("Record ID")));
    }

    [TestMethod]
    public void UnsupportedSchema_IsRejected()
    {
        var errors = EvidenceEvaluator.Validate(EvidenceBundles.Valid() with { SchemaVersion = 99 });

        Assert.IsTrue(errors.Any(e => e.Contains("Schema version 99")));
    }

    [TestMethod]
    public void NonUtcTimestamp_IsRejected()
    {
        var local = EvidenceBundles.Valid() with
        {
            VerifiedAtUtc = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(2)),
        };

        Assert.IsTrue(EvidenceEvaluator.Validate(local).Any(e => e.Contains("UTC")));
    }

    [TestMethod]
    public void Verifier_MustBeANonSensitiveLabel()
    {
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = "user@example.com" }).Any());
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = @"C:\Users\someone" }).Any());
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = "   " }).Any());
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = new string('x', 81) }).Any());
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = "lab:7" }).Any());
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Verifier = "lab\x007" }).Any());
    }

    [TestMethod]
    public void AccessDate_RequiresIsoFormat()
    {
        EvidenceBundle WithDate(string date) => EvidenceBundles.Valid() with
        {
            Sources = new[] { new EvidenceSourceRef("Doc", "https://example.com/docs", date) },
        };

        Assert.AreEqual(0, EvidenceEvaluator.Validate(WithDate("2026-10-16")).Count);
        Assert.IsTrue(EvidenceEvaluator.Validate(WithDate("10/11/2026")).Any(e => e.Contains("yyyy-MM-dd")),
            "Ambiguous culture-dependent dates must be rejected.");
        Assert.IsTrue(EvidenceEvaluator.Validate(WithDate("someday")).Any());
    }

    [TestMethod]
    public void NullApprovalAndDimensions_AreRejected()
    {
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Approval = null! }).Any(e => e.Contains("Approval")));
        Assert.IsTrue(EvidenceEvaluator.Validate(
            EvidenceBundles.Valid() with { Dimensions = null! }).Any(e => e.Contains("missing")));
    }

    [TestMethod]
    public void NonHttpUrls_AreRejected()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Sources = new[] { new EvidenceSourceRef("Doc", "ftp://example.com/docs", "2026-10-16") },
        };

        Assert.IsTrue(EvidenceEvaluator.Validate(bundle).Any(e => e.Contains("malformed URL")));
    }

    [TestMethod]
    public void MissingDimension_IsRejectedByName()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Dimensions = EvidenceBundles.Valid().Dimensions.Where(d => d.Dimension != IsolationDimension.MutexIpc).ToList(),
        };

        var errors = EvidenceEvaluator.Validate(bundle);
        Assert.IsTrue(errors.Any(e => e.Contains("MutexIpc")));
    }

    [TestMethod]
    public void DuplicateDimension_IsRejected()
    {
        var first = EvidenceBundles.Valid();
        var bundle = first with { Dimensions = first.Dimensions.Concat(first.Dimensions.Take(1)).ToList() };

        Assert.IsTrue(EvidenceEvaluator.Validate(bundle).Any(e => e.Contains("twice")));
    }

    [TestMethod]
    public void InvalidStatusValue_IsRejected()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Dimensions = new[] { new DimensionEvidence(IsolationDimension.Updates, (EvidenceStatus)99, null, null, null, null) },
        };

        Assert.IsTrue(EvidenceEvaluator.Validate(bundle).Any(e => e.Contains("invalid status")));
    }

    [TestMethod]
    public void EmptyBundle_IsRejectedNeverSuccessful()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Dimensions = Array.Empty<DimensionEvidence>(),
            Sources = Array.Empty<EvidenceSourceRef>(),
            TestMethod = string.Empty,
            ReproSteps = string.Empty,
        };

        var errors = EvidenceEvaluator.Validate(bundle);
        Assert.IsTrue(errors.Count >= 4, "An empty bundle must fail loudly, never verify.");
    }

    [TestMethod]
    public void MalformedSources_AreRejected()
    {
        var noTitle = EvidenceBundles.Valid() with
        {
            Sources = new[] { new EvidenceSourceRef("  ", "https://example.com", "2026-10-16") },
        };
        var badUrl = EvidenceBundles.Valid() with
        {
            Sources = new[] { new EvidenceSourceRef("Doc", "not a url", "2026-10-16") },
        };
        var badDate = EvidenceBundles.Valid() with
        {
            Sources = new[] { new EvidenceSourceRef("Doc", null, "someday") },
        };

        Assert.IsTrue(EvidenceEvaluator.Validate(noTitle).Any(e => e.Contains("title")));
        Assert.IsTrue(EvidenceEvaluator.Validate(badUrl).Any(e => e.Contains("malformed URL")));
        Assert.IsTrue(EvidenceEvaluator.Validate(badDate).Any(e => e.Contains("access date")));
    }

    [TestMethod]
    public void MissingMethodAndApproval_AreRejected()
    {
        var bundle = EvidenceBundles.Valid() with { TestMethod = " ", ReproSteps = "" };
        Assert.IsTrue(EvidenceEvaluator.Validate(bundle).Any(e => e.Contains("Test method")));
    }
}

[TestClass]
public sealed class EvidenceEvaluationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UnknownDimensions_StayUnknown()
    {
        var evaluation = EvidenceEvaluator.Evaluate(EvidenceBundles.Valid());

        Assert.AreEqual(10, evaluation.UnknownCount);
        Assert.AreEqual(0, evaluation.VerifiedCount);
        Assert.AreEqual(0, evaluation.FailedCount);
        Assert.IsFalse(evaluation.Complete);
        Assert.IsFalse(evaluation.HasFailures);
        Assert.AreEqual(10, evaluation.MissingEvidence.Count);
    }

    [TestMethod]
    public void MixedStatuses_PassThrough()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Dimensions = new[]
            {
                new DimensionEvidence(IsolationDimension.WritableDataDirs, EvidenceStatus.Verified, "Lab", "Isolated", "Isolated", null),
                new DimensionEvidence(IsolationDimension.RegistryConfig, EvidenceStatus.Failed, "Lab", "Shared", "Isolated", "Writes go to HKCU shared key."),
                new DimensionEvidence(IsolationDimension.MutexIpc, EvidenceStatus.NotApplicable, "Review", "No mutex", "No mutex", null),
            }.Concat(EvidenceBundles.Valid().Dimensions.Skip(3)).ToList(),
        };

        var evaluation = EvidenceEvaluator.Evaluate(bundle);

        Assert.AreEqual(1, evaluation.VerifiedCount);
        Assert.AreEqual(1, evaluation.FailedCount);
        Assert.IsTrue(evaluation.HasFailures);
        Assert.IsFalse(evaluation.Complete);
        var failed = evaluation.Dimensions.First(d => d.Dimension == IsolationDimension.RegistryConfig);
        Assert.AreEqual(EvidenceStatus.Failed, failed.Status);
        Assert.IsTrue(failed.HasDiscrepancy);
    }

    [TestMethod]
    public void Eligibility_NeverImpliesVerifiedIsolation()
    {
        // The eligible research disposition is not even an input to evaluation.
        var evaluation = EvidenceEvaluator.Evaluate(
            EvidenceBundles.Valid(candidateId: "documented-profile-flags"));

        Assert.AreEqual(0, evaluation.VerifiedCount);
        Assert.IsFalse(evaluation.Complete);
    }

    [TestMethod]
    public void TechnicalCompleteness_DoesNotImplyApproval()
    {
        var bundle = EvidenceBundles.Valid(status: EvidenceStatus.Verified) with
        {
            Approval = new ApprovalInfo(ApprovalStatus.NotRequested, null, null),
        };

        var evaluation = EvidenceEvaluator.Evaluate(bundle);

        Assert.IsTrue(evaluation.Complete);
        Assert.IsTrue(evaluation.AwaitingApproval,
            "Complete technical evidence still awaits legal review and owner approval.");
        Assert.AreEqual(ApprovalStatus.NotRequested, evaluation.Approval.Status);
    }

    [TestMethod]
    public void Approval_SeparatesCleanly()
    {
        var bundle = EvidenceBundles.Valid(status: EvidenceStatus.Verified) with
        {
            Approval = new ApprovalInfo(ApprovalStatus.Approved, "owner", "Reviewed 2026-10-16."),
        };

        var evaluation = EvidenceEvaluator.Evaluate(bundle);

        Assert.IsTrue(evaluation.Complete);
        Assert.IsFalse(evaluation.AwaitingApproval);
        Assert.AreEqual("Reviewed 2026-10-16.", evaluation.Approval.Rationale);
    }

    [TestMethod]
    public void Evaluation_IsDeterministic()
    {
        var first = EvidenceEvaluator.Evaluate(EvidenceBundles.Valid());
        var second = EvidenceEvaluator.Evaluate(EvidenceBundles.Valid() with
        {
            RecordId = first.RecordId,
        });

        Assert.AreEqual(first.CandidateId, second.CandidateId);
        Assert.AreEqual(first.VerifiedCount, second.VerifiedCount);
        Assert.AreEqual(first.FailedCount, second.FailedCount);
        Assert.AreEqual(first.UnknownCount, second.UnknownCount);
        CollectionAssert.AreEqual(first.MissingEvidence.ToList(), second.MissingEvidence.ToList());
        Assert.AreEqual(first.Complete, second.Complete);
        Assert.AreEqual(first.AwaitingApproval, second.AwaitingApproval);
    }

    [TestMethod]
    public void Summary_ContainsNoFreeText()
    {
        var bundle = EvidenceBundles.Valid() with
        {
            Dimensions = new[]
            {
                new DimensionEvidence(IsolationDimension.WritableDataDirs, EvidenceStatus.Failed,
                    "Method text", "SECRET-OBSERVED-VALUE", "Expected text", "Discrepancy text"),
            }.Concat(EvidenceBundles.Valid().Dimensions.Skip(1)).ToList(),
        };

        var summary = EvidenceEvaluator.Summarize(bundle);
        var text = summary.ToString();

        Assert.AreEqual(10, summary.TotalDimensions);
        Assert.AreEqual(1, summary.FailedDimensions);
        Assert.IsTrue(summary.HasFailures);
        Assert.IsFalse(text.Contains("SECRET-OBSERVED-VALUE"));
        Assert.IsFalse(text.Contains("Discrepancy text"));
    }
}
