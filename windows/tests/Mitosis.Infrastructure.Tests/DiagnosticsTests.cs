using Mitosis.Application.Diagnostics;
using Mitosis.Infrastructure.Diagnostics;

namespace Mitosis.Infrastructure.Tests;

[TestClass]
public sealed class DiagnosticRedactionTests
{
    [TestMethod]
    public void RedactHome_ReplacesBothSlashStylesAndIgnoresCase()
    {
        var home = @"C:\Users\Alice";

        Assert.AreEqual(
            @"~\AppData\Local\Mitosis",
            DiagnosticRedaction.RedactHome(@"C:\Users\Alice\AppData\Local\Mitosis", home));
        Assert.AreEqual(
            "~/AppData/Local/Mitosis",
            DiagnosticRedaction.RedactHome("C:/Users/Alice/AppData/Local/Mitosis", home));
        Assert.AreEqual(
            @"~\Docs",
            DiagnosticRedaction.RedactHome(@"c:\users\alice\Docs", home));
    }

    [TestMethod]
    public void RedactHome_LeavesOtherPathsAndTextAlone()
    {
        var home = @"C:\Users\Alice";

        Assert.AreEqual(
            @"D:\Builds\Mitosis\out",
            DiagnosticRedaction.RedactHome(@"D:\Builds\Mitosis\out", home));
        Assert.AreEqual("no paths here", DiagnosticRedaction.RedactHome("no paths here", home));
        Assert.AreEqual(string.Empty, DiagnosticRedaction.RedactHome(null, home));
    }

    [TestMethod]
    public void SingleLine_CollapsesBreaksTabsAndCapsLength()
    {
        Assert.AreEqual(
            "one two three",
            DiagnosticRedaction.SingleLine("one\r\ntwo\tthree"));
        Assert.AreEqual(
            "trimmed",
            DiagnosticRedaction.SingleLine("  trimmed  "),
            "Outer whitespace is trimmed; inner spacing is preserved.");
        Assert.AreEqual("abcde…", DiagnosticRedaction.SingleLine("abcdef", maxLength: 5));
    }
}

[TestClass]
public sealed class DoctorReportTests
{
    private static DoctorFacts Facts(bool writable = true) => new(
        AppVersion: "0.2.2+abc123",
        RuntimeVersion: ".NET 10.0.4",
        OperatingSystem: "Microsoft Windows 11",
        Architecture: "X64",
        WindowsAppSdkVersion: "2.5.1.0",
        DataDirectory: @"C:\Users\Alice\AppData\Local\Mitosis",
        DataDirectoryWritable: writable,
        LogDirectory: @"C:\Users\Alice\AppData\Local\Mitosis\Logs");

    [TestMethod]
    public void Build_ContainsFactsAndRedactsTheHomePath()
    {
        var report = DoctorReport.Build(
            Facts(), @"C:\Users\Alice", DateTimeOffset.Parse("2026-11-26T10:00:00Z"));

        StringAssert.Contains(report, "Generated: 2026-11-26T10:00:00Z");
        StringAssert.Contains(report, "Application version: 0.2.2+abc123");
        StringAssert.Contains(report, "Runtime: .NET 10.0.4");
        StringAssert.Contains(report, "Operating system: Microsoft Windows 11");
        StringAssert.Contains(report, "Architecture: X64");
        StringAssert.Contains(report, "Windows App SDK: 2.5.1.0");
        StringAssert.Contains(report, @"Data folder: ~\AppData\Local\Mitosis");
        StringAssert.Contains(report, @"Log folder: ~\AppData\Local\Mitosis\Logs");
        Assert.IsFalse(report.Contains("Alice"), "The account name must not survive in a shared report.");
        StringAssert.Contains(report, "Recorded: app/runtime versions and Mitosis data paths only.");
        StringAssert.Contains(report, "Not recorded: application data, credentials, file contents, or network activity.");
    }

    [TestMethod]
    public void Build_StatesWritabilityHonestly()
    {
        StringAssert.Contains(
            DoctorReport.Build(Facts(writable: false)), "Data folder writable: no");
        StringAssert.Contains(
            DoctorReport.Build(Facts(writable: true)), "Data folder writable: yes");
    }

    [TestMethod]
    public void Build_UsesUnknownForBlankFacts()
    {
        var facts = new DoctorFacts("", " ", "", "", "", "", false, "");
        var report = DoctorReport.Build(facts, @"C:\Users\Alice");

        StringAssert.Contains(report, "Application version: unknown");
        StringAssert.Contains(report, "Runtime: unknown");
        StringAssert.Contains(report, "Windows App SDK: unknown");
    }

    [TestMethod]
    public void Build_WithLocalState_RendersCountsAndHonestProblems()
    {
        var state = new DoctorLocalState(
            ProfileStoreReadable: true,
            ProfileCount: 2,
            ProfileStoreProblem: null,
            LogFileCount: 3,
            LogBytes: 512 * 1024,
            LatestLogEntryUtc: DateTimeOffset.Parse("2026-11-26T09:30:00Z"),
            DiscoveredApplicationCount: 141,
            DiscoveryProblem: null);

        var report = DoctorReport.Build(Facts(), @"C:\Users\Alice", null, state);

        StringAssert.Contains(report, "Local state");
        StringAssert.Contains(report, "Profiles: 2 (store readable)");
        StringAssert.Contains(report, "Logs: 3 files, 512.0 KB");
        StringAssert.Contains(report, "latest 2026-11-26T09:30:00Z");
        StringAssert.Contains(report, "Discovered applications: 141");
        StringAssert.Contains(report, "Recorded: app/runtime versions, Mitosis data paths, and local counts only.");
        StringAssert.Contains(report, "Not recorded: application data, credentials, file contents, or network activity.");
    }

    [TestMethod]
    public void Build_WithBrokenLocalState_DoesNotHideTheFailure()
    {
        var state = new DoctorLocalState(
            ProfileStoreReadable: false,
            ProfileCount: 0,
            ProfileStoreProblem: "ProfileStoreException",
            LogFileCount: 0,
            LogBytes: 0,
            LatestLogEntryUtc: null,
            DiscoveredApplicationCount: null,
            DiscoveryProblem: "UnauthorizedAccessException",
            LogProblem: "UnauthorizedAccessException");

        var report = DoctorReport.Build(Facts(), null, null, state);

        StringAssert.Contains(report, "Profiles: unreadable (ProfileStoreException)");
        StringAssert.Contains(report, "Logs: unavailable (UnauthorizedAccessException)");
        StringAssert.Contains(report, "Discovered applications: unavailable (UnauthorizedAccessException)");
    }
}

[TestClass]
public sealed class FileDiagnosticLogTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "mitosis-logs-" + Guid.NewGuid().ToString("N"));

    private string Dir(params string[] parts) =>
        Path.Combine(new[] { root }.Concat(parts).ToArray());

    public TestContext TestContext { get; set; } = null!;

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best effort cleanup of isolated test directories.
        }
    }

    [TestMethod]
    public void Write_AppendsOneRedactedLine()
    {
        var log = new FileDiagnosticLog(
            Dir("logs"),
            clock: () => DateTimeOffset.Parse("2026-11-26T10:00:00Z"),
            homeDirectory: @"C:\Users\Alice");

        log.Write(
            DiagnosticLevel.Info,
            "app.start",
            "Opened C:\\Users\\Alice\\AppData\\Local\\Mitosis\r\nsecond line");

        var text = File.ReadAllText(log.CurrentFilePath);
        StringAssert.Contains(
            text,
            "2026-11-26T10:00:00.000Z INFO app.start | Opened ~\\AppData\\Local\\Mitosis second line");
        Assert.AreEqual(
            1,
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
            "One call must be one line, even when the message contains breaks.");
        Assert.IsTrue(log.IsEnabled);
        Assert.IsNull(log.DisabledReason);
    }

    [TestMethod]
    public void Write_SanitizesEventNamesAndCapsThem()
    {
        var log = new FileDiagnosticLog(Dir("logs"), homeDirectory: @"C:\Users\Alice");

        log.Write(DiagnosticLevel.Warning, "bad name|\r\n", "message");

        var text = File.ReadAllText(log.CurrentFilePath);
        StringAssert.Contains(text, "WARN bad_name___ | message");
        Assert.IsFalse(text.Contains("bad name"), "An event name must never break the line format.");
    }

    [TestMethod]
    public void Rotation_KeepsTheCapAndTheNewestEntries()
    {
        var log = new FileDiagnosticLog(
            Dir("logs"), maxFileBytes: 1024, maxFiles: 3, clock: () => DateTimeOffset.UtcNow);
        var payload = new string('x', 300);

        for (var i = 0; i < 10; i++)
        {
            log.Write(DiagnosticLevel.Info, "test", $"message-{i} {payload}");
        }

        var files = Directory.GetFiles(Dir("logs"), "mitosis*.log");
        Assert.AreEqual(3, files.Length, "Current file plus two archives.");
        Assert.IsTrue(File.Exists(log.CurrentFilePath));
        StringAssert.Contains(File.ReadAllText(log.CurrentFilePath), "message-9");
        Assert.IsFalse(File.Exists(Path.Combine(Dir("logs"), "mitosis.3.log")));
    }

    [TestMethod]
    public void ConcurrentWriters_SerializeWithoutDisablingEitherLogger()
    {
        var directory = Dir("logs");
        var first = new FileDiagnosticLog(directory, maxFileBytes: 1024 * 1024);
        var second = new FileDiagnosticLog(directory, maxFileBytes: 1024 * 1024);
        using var start = new ManualResetEventSlim();

        void Writer(FileDiagnosticLog log, string tag)
        {
            start.Wait();
            for (var i = 0; i < 60; i++)
            {
                log.Write(DiagnosticLevel.Info, "concurrent", $"{tag}-{i} {new string('x', 80)}");
            }
        }

        var a = new Thread(() => Writer(first, "a"));
        var b = new Thread(() => Writer(second, "b"));
        a.Start();
        b.Start();
        start.Set();

        Assert.IsTrue(a.Join(TimeSpan.FromSeconds(20)));
        Assert.IsTrue(b.Join(TimeSpan.FromSeconds(20)));

        Assert.IsTrue(first.IsEnabled, first.DisabledReason);
        Assert.IsTrue(second.IsEnabled, second.DisabledReason);

        var lines = File.ReadAllLines(first.CurrentFilePath);
        Assert.AreEqual(120, lines.Length, "Every entry must survive two concurrent writers.");
        Assert.AreEqual(60, lines.Count(line => line.Contains(" | a-", StringComparison.Ordinal)));
        Assert.AreEqual(60, lines.Count(line => line.Contains(" | b-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void UnwritableTarget_DisablesTheLoggerWithoutThrowing()
    {
        Directory.CreateDirectory(root);
        var blocker = Dir("blocked");
        File.WriteAllText(blocker, "x");
        var log = new FileDiagnosticLog(Path.Combine(blocker, "logs"));

        log.Write(DiagnosticLevel.Error, "app.unhandled", "boom");
        log.Write(DiagnosticLevel.Error, "app.unhandled", "again");

        Assert.IsFalse(log.IsEnabled);
        Assert.IsNotNull(log.DisabledReason, "The failure is recorded for support, not hidden.");
    }

    [TestMethod]
    public void Constructor_RejectsInvalidLimitsAndPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FileDiagnosticLog("  "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileDiagnosticLog(Dir("a"), maxFileBytes: 512));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileDiagnosticLog(Dir("a"), maxFiles: 1));
    }
}
