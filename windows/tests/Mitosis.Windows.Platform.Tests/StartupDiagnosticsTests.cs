using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mitosis.Windows.Platform.Startup;

namespace Mitosis.Windows.Platform.Tests;

[TestClass]
public sealed class StartupDiagnosticsTests
{
    [DataTestMethod]
    [DataRow(unchecked((int)0x80070002))]
    [DataRow(unchecked((int)0x80004005))]
    public void DescribeMissingAppSdk_NamesPrerequisiteAndHresult(int hresult)
    {
        var message = StartupDiagnostics.DescribeMissingAppSdk(hresult);

        Assert.IsTrue(message.Contains("Windows App SDK"), message);
        Assert.IsTrue(message.Contains("2.5"), message);
        // The failing HRESULT is the single most useful diagnostic for support.
        Assert.IsTrue(
            message.Contains(hresult.ToString("X8").ToLower()) ||
            message.Contains("0x" + hresult.ToString("X8").ToUpper()),
            $"Message does not carry the HRESULT {hresult:X8}: {message}");
        Assert.IsTrue(
            message.Contains("Install") && message.Contains("relaunch Mitosis"),
            $"Message must say how to recover: {message}");
    }

    [TestMethod]
    public void DescribeMissingAppSdk_LeaksNoLocalPaths()
    {
        // The old failure path surfaced through the CLR unhandled-exception dialog,
        // whose stack trace exposes local install paths. The replacement message is
        // static text — pin that it cannot carry drive letters or UNC prefixes.
        var message = StartupDiagnostics.DescribeMissingAppSdk(unchecked((int)0x80070002));

        Assert.IsFalse(message.Contains(":\\"), message);
        Assert.IsFalse(message.Contains("\\\\"), message);
        Assert.IsFalse(message.Contains("%LOCALAPPDATA%"), message);
    }

    [TestMethod]
    public void DialogTitle_IsSet()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(StartupDiagnostics.PrerequisiteDialogTitle));
    }
}
