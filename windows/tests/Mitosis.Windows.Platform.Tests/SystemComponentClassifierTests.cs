using Mitosis.Domain;
using Mitosis.Windows.Platform.Discovery;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>The name-based component classifier that keeps runtimes, installers, media
/// extensions, and optional OS content out of the default sidebar. The false cases are as
/// important as the true ones: a wrong "true" hides an application a person installed.</summary>
[TestClass]
public sealed class SystemComponentClassifierTests
{
    [TestMethod]
    [DataRow("Microsoft .NET Runtime - 8.0.33 (x64)")]
    [DataRow("Microsoft .NET Runtime - 9.0.22 (x64)")]
    [DataRow("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501")]
    [DataRow("AV1 Video Extension")]
    [DataRow("AVC Encoder Video Extension")]
    [DataRow("HEIF Image Extension")]
    [DataRow("HEVC Video Extensions from Device Manufacturer")]
    [DataRow("MPEG-2 Video Extension")]
    [DataRow("English (United Kingdom) Local Experience Pack")]
    [DataRow("Microsoft Windows Application Compatibility Fix Database")]
    [DataRow("GlideX Service Installer")]
    [DataRow("NVIDIA Graphics Driver 582.99")]
    [DataRow("NVIDIA HD Audio Driver 1.4.5.7")]
    [DataRow("Microsoft Ink Handwriting Recognition (en-IN)")]
    [DataRow("Mobile devices")]
    [DataRow("Game Speech Window")]
    [DataRow("App Installer")]
    [DataRow("Microsoft 365 - en-us")]
    [DataRow("Microsoft.Office.ActionsServer")]
    [DataRow("OfficePushNotificationsUtility")]
    [DataRow("Local AI Manager for Microsoft 365")]
    public void ComponentNames_AreRecognized(string name) =>
        Assert.IsTrue(SystemComponentClassifier.IsComponentName(name), name);

    [TestMethod]
    [DataRow("Android Studio")]
    [DataRow("Antigravity 2.35.2")]
    [DataRow("Brave")]
    [DataRow("Bun")]
    [DataRow("Cloudflare One Client")]
    [DataRow("Discord")]
    [DataRow("Docker Desktop")]
    [DataRow("Driver Booster")]
    [DataRow("Git")]
    [DataRow("GitHub CLI")]
    [DataRow("Google Chrome")]
    [DataRow("Microsoft Edge")]
    [DataRow("Microsoft Photos")]
    [DataRow("Microsoft Store")]
    [DataRow("Microsoft Visual Studio Code (User)")]
    [DataRow("Node.js")]
    [DataRow("NVIDIA Control Panel")]
    [DataRow("NVIDIA RTX Desktop Manager 205.40")]
    [DataRow("PC Manager")]
    [DataRow("Phone Link")]
    [DataRow("Power Automate")]
    [DataRow("Python 3.13.17 (64-bit)")]
    [DataRow("qBittorrent")]
    [DataRow("Runtime Commander")]
    public void Applications_StayVisible(string name) =>
        Assert.IsFalse(SystemComponentClassifier.IsComponentName(name), name);

    [TestMethod]
    public void EmptyAndWhitespace_AreNotComponents() =>
        Assert.IsFalse(SystemComponentClassifier.IsComponentName("   "));

    [TestMethod]
    public async Task Merge_MarksInstallersThatCarryNoRegistryFlag()
    {
        var service = new WindowsAppDiscoveryService(new[]
        {
            FakeSource.Returning(
                Records.Make("Microsoft Visual C++ 2014 Redistributable (x64)"),
                Records.Make("Brave", "Brave Software")),
        });

        var merged = await service.DiscoverAsync();

        Assert.IsTrue(merged.Single(a => a.DisplayName.Contains("Redistributable")).IsSystemComponent);
        Assert.IsFalse(merged.Single(a => a.DisplayName == "Brave").IsSystemComponent);
    }
}
