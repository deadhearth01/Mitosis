using Mitosis.Windows.Platform.Icons;

namespace Mitosis.Windows.Platform.Tests;

[TestClass]
public sealed class AppIconReferenceTests
{
    // A real registry value: "C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe,0".
    private static readonly string BravePath =
        Path.Combine("C:" + Path.DirectorySeparatorChar, "Program Files", "BraveSoftware", "Brave-Browser", "Application", "brave.exe");

    [TestMethod]
    public void PlainPath_IsReturned()
    {
        Assert.AreEqual(BravePath, AppIconReference.Parse(BravePath));
    }

    [TestMethod]
    public void TrailingIndex_IsStripped()
    {
        Assert.AreEqual(BravePath, AppIconReference.Parse(BravePath + ",0"));
        Assert.AreEqual(BravePath, AppIconReference.Parse(BravePath + ",-12"));
    }

    [TestMethod]
    public void QuotedPath_IsUnquoted()
    {
        Assert.AreEqual(BravePath, AppIconReference.Parse("\"" + BravePath + "\",0"));
    }

    [TestMethod]
    public void CommaInDirectory_IsKeptWhenTheSuffixIsNotAnIndex()
    {
        var path = Path.Combine("C:" + Path.DirectorySeparatorChar, "Tools, Old", "editor.exe");
        Assert.AreEqual(path, AppIconReference.Parse(path));
    }

    [TestMethod]
    public void ExtensionsOutsideTheAllowList_AreRejected()
    {
        Assert.IsNull(AppIconReference.Parse(Path.Combine("C:" + Path.DirectorySeparatorChar, "Windows", "notepad.txt")));
        Assert.IsNull(AppIconReference.Parse("C:\\Windows\\System32\\shell32.dll.bak"));
    }

    [TestMethod]
    public void RelativeAndRootedPaths_AreRejected()
    {
        Assert.IsNull(AppIconReference.Parse("brave.exe"));
        Assert.IsNull(AppIconReference.Parse(@"\Windows\notepad.exe"));
        Assert.IsNull(AppIconReference.Parse(@"\\server\share\app.exe"));
    }

    [TestMethod]
    public void TraversalSegments_AreRejected()
    {
        Assert.IsNull(AppIconReference.Parse(@"C:\Program Files\..\Windows\notepad.exe"));
    }

    [TestMethod]
    public void BlankAndMissingValues_AreRejected()
    {
        Assert.IsNull(AppIconReference.Parse(null));
        Assert.IsNull(AppIconReference.Parse(""));
        Assert.IsNull(AppIconReference.Parse("   "));
        Assert.IsNull(AppIconReference.Parse("\"\""));
    }
}
