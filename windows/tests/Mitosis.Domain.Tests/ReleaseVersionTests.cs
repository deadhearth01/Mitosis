using Mitosis.Domain.Updates;

namespace Mitosis.Domain.Tests;

/// <summary>Version rules ported from macOS <c>Versioning.swift</c>. The Windows build
/// carries <c>+&lt;git-sha&gt;</c> metadata on every informational version, so build
/// suffixes must never make two equal releases look different.</summary>
[TestClass]
public sealed class ReleaseVersionTests
{
    [TestMethod]
    [DataRow("0.2.2", 3)]
    [DataRow("v0.2.2", 3)]
    [DataRow("V1", 1)]
    [DataRow("1.2.3.4", 4)]
    [DataRow("0.2.2+6f9ee6b", 3)]
    [DataRow("0.2.2-alpha.1+abc", 3)]
    [DataRow("  v0.3.0  ", 3)]
    public void TryParse_AcceptsRealReleaseTags(string raw, int coreLength)
    {
        Assert.IsTrue(ReleaseVersion.TryParse(raw, out var version), raw);
        Assert.AreEqual(coreLength, version.Core.Count, raw);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("nightly")]
    [DataRow("1..2")]
    [DataRow("1.2.x")]
    [DataRow("1.2.3.4.5")]
    [DataRow("-1.2")]
    [DataRow("1.2.3-")]
    [DataRow("1.2.3-..")]
    public void TryParse_RejectsAnythingNotComparable(string raw)
    {
        Assert.IsFalse(ReleaseVersion.TryParse(raw, out _), raw);
    }

    [TestMethod]
    [DataRow("0.2.3", "0.2.2")]
    [DataRow("v0.3.0", "0.2.2+6f9ee6b")]
    [DataRow("1.0", "0.9.9")]
    [DataRow("0.10.0", "0.9.0")]
    [DataRow("1.0.0", "1.0.0-rc.1")]
    [DataRow("1.0.0-rc.2", "1.0.0-rc.1")]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha")]
    public void IsNewer_ComparesNumericallyAndBySemverPrecedence(string candidate, string current)
    {
        Assert.IsTrue(ReleaseVersion.IsNewer(candidate, current), $"{candidate} > {current}");
        Assert.IsFalse(ReleaseVersion.IsNewer(current, candidate), $"{current} is not newer than {candidate}");
    }

    [TestMethod]
    [DataRow("0.2.2", "0.2.2")]
    [DataRow("0.2.2+aaaa", "0.2.2+bbbb")]
    [DataRow("v0.2.2", "0.2.2")]
    [DataRow("0.2.1", "0.2.2")]
    public void IsNewer_IsStrictlyNewer(string candidate, string current)
    {
        Assert.IsFalse(ReleaseVersion.IsNewer(candidate, current));
    }

    [TestMethod]
    public void MalformedTags_NeverProduceAnUpdate()
    {
        Assert.IsFalse(ReleaseVersion.IsNewer("nightly", "0.2.2"));
        Assert.IsFalse(ReleaseVersion.IsNewer("0.3.0", "not-a-version"));
        Assert.IsFalse(ReleaseVersion.IsNewer(null, "0.2.2"));
        Assert.IsFalse(ReleaseVersion.IsNewer("0.3.0", null));
    }

    [TestMethod]
    [DataRow("v0.2.3", "0.2.3")]
    [DataRow("0.2.3+6f9ee6b", "0.2.3")]
    [DataRow("  v0.2.3+sha  ", "0.2.3")]
    [DataRow("0.3.0-beta.1", "0.3.0-beta.1")]
    public void DisplayVersion_DropsPrefixAndBuildMetadata(string raw, string expected)
    {
        Assert.AreEqual(expected, ReleaseVersion.DisplayVersion(raw));
    }

    [TestMethod]
    public void DisplayVersion_IsHonestAboutMissingInput()
    {
        Assert.AreEqual("unknown", ReleaseVersion.DisplayVersion(null));
        Assert.AreEqual("unknown", ReleaseVersion.DisplayVersion("  "));
        Assert.AreEqual("nightly", ReleaseVersion.DisplayVersion("nightly"));
    }
}
