using Mitosis.Domain.Updates;

namespace Mitosis.Application.Tests;

[TestClass]
public sealed class ProductVersionTests
{
    [TestMethod]
    public void Describe_ReturnsTheAssemblyInformationalVersion()
    {
        var described = ProductVersion.Describe(typeof(ProductVersion).Assembly);

        Assert.IsFalse(string.IsNullOrWhiteSpace(described));
        Assert.IsTrue(ReleaseVersion.TryParse(described, out _),
            $"'{described}' must be comparable with a release tag.");
    }

    [TestMethod]
    public void Describe_RejectsMissingInput()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProductVersion.Describe(null!));
    }
}
