using System.Text.RegularExpressions;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>Structural pins for the Stage 18 version/identity scheme. These read
/// repository files (located by walking up from the test's base directory, which
/// preserves the repo's directory shape on any machine) so the documented scheme
/// cannot silently drift: the single version source, its validation, and the
/// publish script deriving the artifact name from the built metadata rather than
/// restating a version. The msbuild target that enforces the same format runs in
/// every build — a deliberately wrong -p:WindowsPortVersion was exercised once
/// during Stage 18's manual verification and rejected.</summary>
[TestClass]
public sealed class WindowsReleaseIdentityTests
{
    private static string FindRepoFile(params string[] relativeSegments)
    {
        var current = AppContext.BaseDirectory;
        for (var depth = 0; depth < 12; depth++)
        {
            var candidate = current;
            foreach (var segment in relativeSegments)
            {
                candidate = Path.Combine(candidate, segment);
            }
            if (File.Exists(candidate))
            {
                return candidate;
            }
            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }
            current = parent.FullName;
        }
        throw new InvalidOperationException(
            string.Join('/', relativeSegments) + " not found above " + AppContext.BaseDirectory);
    }

    [TestMethod]
    public void DirectoryBuildProps_SetsSingleSemanticVersionSource()
    {
        var props = File.ReadAllText(FindRepoFile("Directory.Build.props"));

        // The default version source exists and matches the documented scheme.
        var match = Regex.Match(props, "<WindowsPortVersion[^>]*>([^<]+)<");
        Assert.IsTrue(match.Success,
            "Directory.Build.props must define an explicit WindowsPortVersion default");
        Assert.IsTrue(Regex.IsMatch(match.Groups[1].Value, @"\d+\.\d+\.\d+$"),
            $"'{match.Groups[1].Value}' is not MAJOR.MINOR.PATCH");

        // The three assembly properties flow from that one value. Informational
        // version carries the git hash automatically via the SDK's
        // SourceRevisionId when a git repository is present (no fabrication).
        Assert.IsTrue(props.Contains("<Version>$(WindowsPortVersion)</Version>"),
            "Version must flow from WindowsPortVersion");
        Assert.IsTrue(props.Contains("<AssemblyVersion>$(WindowsPortVersion).0</AssemblyVersion>"),
            "AssemblyVersion must flow from WindowsPortVersion");
        Assert.IsTrue(props.Contains("<FileVersion>$(WindowsPortVersion).0</FileVersion>"),
            "FileVersion must flow from WindowsPortVersion");
    }

    [TestMethod]
    public void DirectoryBuildProps_ValidatesOverriddenVersion()
    {
        var props = File.ReadAllText(FindRepoFile("Directory.Build.props"));

        // The msbuild target enforces the documented MAJOR.MINOR.PATCH scheme on
        // -p:WindowsPortVersion overrides; release commands may only succeed when
        // a valid SemVer string is given.
        Assert.IsTrue(props.Contains("ValidateWindowsPortVersion"),
            "the version validation target must exist");
        Assert.IsTrue(props.Contains(@"'^\d+\.\d+\.\d+$'"),
            "the validation only accepts MAJOR.MINOR.PATCH");
    }

    [TestMethod]
    public void PublishScript_DerivesNamingAndChecksExecutableMetadata()
    {
        var script = File.ReadAllText(FindRepoFile("scripts", "publish-release.ps1"));

        // Distributed identity: the executable of record is Mitosis.exe.
        Assert.IsTrue(script.Contains("Mitosis.exe"), "script must check the renamed executable");
        // The version is read back from the built exe, never restated in the script.
        Assert.IsTrue(script.Contains("$versionInfo.FileVersion"),
            "the archive naming follows the built metadata");
        Assert.IsTrue(script.Contains("$versionInfo.ProductVersion"),
            "the script cross-checks ProductVersion against the file version");
        // The archive name carries the version and a git revision (or nogit).
        Assert.IsTrue(script.Contains("Mitosis-windows-$shortVersion-x64-"),
            "archive naming is version-anchored");
        Assert.IsTrue(script.Contains("nogit"),
            "a build without git metadata must not fabricate a hash");
    }

    [TestMethod]
    public void AppProject_UsesMitosisExecutableNameAndDerivedIcon()
    {
        var project = File.ReadAllText(FindRepoFile("src", "Mitosis.Windows.App", "Mitosis.Windows.App.csproj"));

        // Identity: exe name Mitosis, icon derived from the product artwork, and
        // the unpackaged deployment model is unchanged (no invented MSIX identity).
        Assert.IsTrue(project.Contains("<AssemblyName>Mitosis</AssemblyName>"),
            "the distributed executable is Mitosis");
        Assert.IsTrue(project.Contains("<ApplicationIcon>app.ico</ApplicationIcon>"),
            "application icon must be set");
        Assert.IsTrue(project.Contains("<EnableMsixTooling>false</EnableMsixTooling>"),
            "no MSIX identity was invented");
        Assert.IsTrue(File.Exists(FindRepoFile("src", "Mitosis.Windows.App", "app.ico")),
            "app.ico must be committed next to the project so CI builds embed the icon");
    }
}
