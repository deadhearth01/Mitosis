using Mitosis.Domain;

namespace Mitosis.Domain.Tests;

[TestClass]
public sealed class DiscoveryModelsTests
{
    [TestMethod]
    public void Identity_IsStableForSameInput()
    {
        var a = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Slack Technologies");
        var b = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Slack Technologies");

        Assert.AreEqual(a, b);
        Assert.IsTrue(a.StartsWith("currentuseruninstall:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Identity_IgnoresCaseAndSurroundingWhitespace()
    {
        var a = AppIdentity.Create(DiscoverySource.AppxPackage, "Signal", "Signal Foundation");
        var b = AppIdentity.Create(DiscoverySource.AppxPackage, "  signal ", "SIGNAL FOUNDATION");

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Identity_DiffersAcrossSourcesNamesAndPublishers()
    {
        var baseline = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Slack Technologies");

        Assert.AreNotEqual(baseline, AppIdentity.Create(DiscoverySource.LocalMachineUninstall64, "Slack", "Slack Technologies"));
        Assert.AreNotEqual(baseline, AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack Beta", "Slack Technologies"));
        Assert.AreNotEqual(baseline, AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Someone Else"));
    }

    [TestMethod]
    public void Identity_ToleratesMissingPublisher()
    {
        var a = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Tool", null);
        var b = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Tool", "  ");

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void DiscoveredApp_KeepsMissingMetadataMissing()
    {
        var app = new DiscoveredApp(
            Id: AppIdentity.Create(DiscoverySource.AppxPackage, "Tool", null),
            DisplayName: "Tool",
            Publisher: null,
            Version: null,
            InstallLocation: null,
            ExecutablePath: null,
            PrimarySource: DiscoverySource.AppxPackage,
            Sources: new[] { DiscoverySource.AppxPackage },
            Scope: InstallScope.Unknown,
            IsSystemComponent: false,
            IconReference: null,
            Warnings: Array.Empty<string>());

        Assert.IsNull(app.Publisher);
        Assert.IsNull(app.Version);
        Assert.IsNull(app.ExecutablePath, "Executable paths are never inferred from names or locations.");
    }
}
