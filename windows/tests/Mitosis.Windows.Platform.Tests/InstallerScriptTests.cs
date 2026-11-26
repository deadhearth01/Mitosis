using System.Diagnostics;
using System.Text.Json;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>Behavioral tests for the per-user installer that ships in the release
/// archive (Stage 26). Each test runs the real script in a child
/// <c>powershell.exe</c> against a small fixture payload under a fresh temporary
/// directory; the install root and the profile-data root are both redirected
/// there, and shell integration is always disabled, so a test can never touch the
/// Start menu, HKCU, or the developer's real %LOCALAPPDATA%\Mitosis data.
///
/// Prerequisite detection is skipped here on purpose: it inspects the host
/// (installed .NET/Windows App Runtime), and a test must not depend on, or make
/// claims about, the machine running it. The detection itself was observed
/// manually (docs/WINDOWS-INSTALL.md).</summary>
[TestClass]
public sealed class InstallerScriptTests
{
    private static readonly string ScriptPath =
        FindRepoFile("scripts", "install.ps1");

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

    private static string TempDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Creates the fixture "archive": a minimal payload that mirrors the
    /// real layout (app exe, PRI, pose asset, cli/ subfolder, the installer
    /// itself). The exe is not a real PE, so the installer records "unknown" as
    /// its version - the copy/marker logic under test does not depend on it.</summary>
    private static string CreatePayload(string parent)
    {
        var payload = Path.Combine(parent, "payload");
        Directory.CreateDirectory(Path.Combine(payload, "Assets"));
        Directory.CreateDirectory(Path.Combine(payload, "cli"));
        File.WriteAllText(Path.Combine(payload, "Mitosis.exe"), "fixture");
        File.WriteAllText(Path.Combine(payload, "Mitosis.pri"), "pri");
        File.WriteAllText(Path.Combine(payload, "Assets", "pose-empty.png"), "png");
        File.WriteAllText(Path.Combine(payload, "cli", "mitosis.exe"), "cli");
        File.Copy(ScriptPath, Path.Combine(payload, "install.ps1"));
        return payload;
    }

    private static (int ExitCode, string Output) Run(params string[] arguments)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var powershell = Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(powershell)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(ScriptPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(true);
            throw new InvalidOperationException("installer script did not exit");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static (int ExitCode, string Output) RunInstall(string payload, string installRoot,
        string dataRoot, params string[] extra)
    {
        var args = new List<string>
        {
            "-Source", payload,
            "-InstallRoot", installRoot,
            "-UserDataRoot", dataRoot,
            "-NoShellIntegration",
            "-SkipPrerequisiteCheck",
        };
        args.AddRange(extra);
        return Run(args.ToArray());
    }

    private static (int ExitCode, string Output) RunUninstall(string installRoot, string dataRoot,
        params string[] extra)
    {
        var args = new List<string>
        {
            "-Uninstall",
            "-InstallRoot", installRoot,
            "-UserDataRoot", dataRoot,
            "-NoShellIntegration",
        };
        args.AddRange(extra);
        return Run(args.ToArray());
    }

    [TestMethod]
    public void Install_RequiresSourceExecutable()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var empty = Path.Combine(root, "empty");
            Directory.CreateDirectory(empty);
            var (exitCode, output) = RunInstall(empty, Path.Combine(root, "install"),
                Path.Combine(root, "data"), "-DryRun");
            Assert.AreEqual(1, exitCode, output);
            Assert.IsTrue(output.Contains("does not contain Mitosis.exe"), output);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "install")), "nothing may be created");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Install_DryRunWritesNothing()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var (exitCode, output) = RunInstall(payload, installRoot, Path.Combine(root, "data"),
                "-DryRun");
            Assert.AreEqual(0, exitCode, output);
            Assert.IsTrue(output.Contains("dry run"), output);
            Assert.IsFalse(Directory.Exists(installRoot), "dry run must not create the install root");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Install_CopiesPayloadAndWritesMarker()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var (exitCode, output) = RunInstall(payload, installRoot, Path.Combine(root, "data"));
            Assert.AreEqual(0, exitCode, output);
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "Mitosis.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "Mitosis.pri")));
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "Assets", "pose-empty.png")));
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "cli", "mitosis.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "install.ps1")),
                "the uninstall entry calls install.ps1 from the install folder");

            using var marker = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(installRoot, ".mitosis-install.json")));
            Assert.AreEqual("Mitosis", marker.RootElement.GetProperty("Product").GetString());
            Assert.AreEqual(5, marker.RootElement.GetProperty("Files").GetArrayLength(),
                "every payload file (including install.ps1) is recorded for uninstall");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Install_RefusesReinstallWithoutForceAndForceReplaces()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var dataRoot = Path.Combine(root, "data");
            Assert.AreEqual(0, RunInstall(payload, installRoot, dataRoot).ExitCode);
            var (secondExit, secondOutput) = RunInstall(payload, installRoot, dataRoot);
            Assert.AreEqual(2, secondExit, secondOutput);
            Assert.IsTrue(secondOutput.Contains("already installed"), secondOutput);
            var (forcedExit, forcedOutput) = RunInstall(payload, installRoot, dataRoot, "-Force");
            Assert.AreEqual(0, forcedExit, forcedOutput);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Install_RefusesUnownedNonEmptyFolder()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            Directory.CreateDirectory(installRoot);
            var sentinel = Path.Combine(installRoot, "someone-elses-file.txt");
            File.WriteAllText(sentinel, "keep");
            var (exitCode, output) = RunInstall(payload, installRoot, Path.Combine(root, "data"));
            Assert.AreEqual(2, exitCode, output);
            Assert.IsTrue(output.Contains("was not created by Mitosis"), output);
            Assert.AreEqual("keep", File.ReadAllText(sentinel));
            Assert.IsFalse(File.Exists(Path.Combine(installRoot, "Mitosis.exe")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Install_RefusesUnsafeInstallRoot()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var (exitCode, output) = RunInstall(payload, "C:\\",
                Path.Combine(root, "data"));
            Assert.AreEqual(1, exitCode, output);
            Assert.IsTrue(output.Contains("drive root"), output);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Uninstall_RemovesRecordedFilesAndKeepsUserFilesAndData()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var dataRoot = Path.Combine(root, "data");
            Assert.AreEqual(0, RunInstall(payload, installRoot, dataRoot).ExitCode);

            var userFile = Path.Combine(installRoot, "user-notes.txt");
            File.WriteAllText(userFile, "mine");
            Directory.CreateDirectory(dataRoot);
            var profile = Path.Combine(dataRoot, "profiles.json");
            File.WriteAllText(profile, "[]");

            var (exitCode, output) = RunUninstall(installRoot, dataRoot);
            Assert.AreEqual(0, exitCode, output);
            Assert.IsFalse(File.Exists(Path.Combine(installRoot, "Mitosis.exe")),
                "recorded payload files must be removed");
            Assert.IsFalse(File.Exists(Path.Combine(installRoot, ".mitosis-install.json")),
                "the install marker must be removed");
            Assert.IsTrue(File.Exists(userFile), "files the installer did not create must be kept");
            Assert.IsTrue(File.Exists(profile), "profile data is kept without -RemoveUserData");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Uninstall_DryRunChangesNothing()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var dataRoot = Path.Combine(root, "data");
            Assert.AreEqual(0, RunInstall(payload, installRoot, dataRoot).ExitCode);
            var (exitCode, output) = RunUninstall(installRoot, dataRoot, "-DryRun");
            Assert.AreEqual(0, exitCode, output);
            Assert.IsTrue(output.Contains("nothing was changed"), output);
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, "Mitosis.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(installRoot, ".mitosis-install.json")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Uninstall_RemoveUserDataDeletesInstallAndDataRoots()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var payload = CreatePayload(root);
            var installRoot = Path.Combine(root, "install");
            var dataRoot = Path.Combine(root, "data");
            Assert.AreEqual(0, RunInstall(payload, installRoot, dataRoot).ExitCode);
            Directory.CreateDirectory(dataRoot);
            File.WriteAllText(Path.Combine(dataRoot, "profiles.json"), "[]");

            var (exitCode, output) = RunUninstall(installRoot, dataRoot, "-RemoveUserData");
            Assert.AreEqual(0, exitCode, output);
            Assert.IsFalse(Directory.Exists(installRoot),
                "a fully owned install folder must be removed without leftovers");
            Assert.IsFalse(Directory.Exists(dataRoot),
                "-RemoveUserData deletes the profile data root explicitly");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Uninstall_RefusesWithoutMarker()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var installRoot = Path.Combine(root, "install");
            Directory.CreateDirectory(installRoot);
            var sentinel = Path.Combine(installRoot, "keep.txt");
            File.WriteAllText(sentinel, "keep");
            var (exitCode, output) = RunUninstall(installRoot, Path.Combine(root, "data"));
            Assert.AreEqual(2, exitCode, output);
            Assert.IsTrue(output.Contains("Refusing"), output);
            Assert.IsTrue(File.Exists(sentinel));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void RemoveUserDataWithoutUninstall_IsRejected()
    {
        var root = TempDirectory("mitosis-installer-tests-");
        try
        {
            var (exitCode, output) = Run("-RemoveUserData",
                "-InstallRoot", Path.Combine(root, "install"),
                "-UserDataRoot", Path.Combine(root, "data"));
            Assert.AreEqual(1, exitCode, output);
            Assert.IsTrue(output.Contains("only valid together with -Uninstall"), output);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
