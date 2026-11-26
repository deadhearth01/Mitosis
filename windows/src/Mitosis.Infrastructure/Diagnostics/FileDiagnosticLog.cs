using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Mitosis.Application.Diagnostics;

namespace Mitosis.Infrastructure.Diagnostics;

/// <summary>Best-effort append-only log under the Mitosis data folder. One line per
/// event, home paths redacted, bounded by a size cap and a fixed number of rotated
/// files. A named mutex serializes writers from different processes (the single-instance
/// primary and a short-lived secondary share the file); when that mutex cannot be
/// acquired within a short bound, one entry is skipped rather than delaying the app.
/// Filesystem failures disable the logger for the rest of the process instead of
/// throwing into application code.</summary>
public sealed class FileDiagnosticLog : IDiagnosticLog
{
    public const string FileName = "mitosis.log";

    /// <summary>Current file cap. Three files (current plus two archives) keep the
    /// trail useful without allowing unbounded growth.</summary>
    public const long DefaultMaxFileBytes = 512 * 1024;

    public const int DefaultMaxFiles = 3;

    /// <summary>Upper bound for waiting on another process to finish one append.
    /// Missing that bound only drops the entry; it never disables the logger.</summary>
    public const int ProcessLockTimeoutMilliseconds = 1000;

    private const int MaxEventNameLength = 64;

    private readonly object gate = new();
    private readonly long maxFileBytes;
    private readonly int maxFiles;
    private readonly Func<DateTimeOffset> clock;
    private readonly string? homeDirectory;
    private readonly string processLockName;
    private Mutex? processMutex;
    private bool processMutexUnavailable;
    private string? disabledReason;

    public FileDiagnosticLog(
        string directoryPath,
        long maxFileBytes = DefaultMaxFileBytes,
        int maxFiles = DefaultMaxFiles,
        Func<DateTimeOffset>? clock = null,
        string? homeDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Log directory must not be empty.", nameof(directoryPath));
        }

        if (maxFileBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes), "Use at least 1 KB per file.");
        }

        if (maxFiles < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Keep at least the current file and one archive.");
        }

        DirectoryPath = directoryPath;
        this.maxFileBytes = maxFileBytes;
        this.maxFiles = maxFiles;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.homeDirectory = homeDirectory;
        processLockName = ProcessLockNameFor(directoryPath);
    }

    public string DirectoryPath { get; }

    public string CurrentFilePath => Path.Combine(DirectoryPath, FileName);

    /// <summary>False once a write has failed; later writes become no-ops.</summary>
    public bool IsEnabled
    {
        get
        {
            lock (gate)
            {
                return disabledReason == null;
            }
        }
    }

    /// <summary>Exception type that disabled the logger, or null while healthy.</summary>
    public string? DisabledReason
    {
        get
        {
            lock (gate)
            {
                return disabledReason;
            }
        }
    }

    public void Write(DiagnosticLevel level, string eventName, string message)
    {
        lock (gate)
        {
            if (disabledReason != null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var processLock = TryAcquireProcessLock();
                try
                {
                    var line = Format(level, eventName, message);
                    RotateIfNeeded(line.Length);
                    File.AppendAllText(CurrentFilePath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
                finally
                {
                    ReleaseProcessLock(processLock);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or NotSupportedException or ArgumentException or SecurityException)
            {
                // Logging must never be the reason the app fails.
                disabledReason = ex.GetType().Name;
            }
        }
    }

    private static string ProcessLockNameFor(string directoryPath)
    {
        var normalized = directoryPath.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "Mitosis.Windows.log." + Convert.ToHexString(hash, 0, 8);
    }

    /// <summary>Serializes append+rotation across processes. Returns null when the lock
    /// is unavailable (timeout or an environment without named mutexes); the caller
    /// then writes unlocked, which is the same best-effort behavior as Stage 22.</summary>
    private Mutex? TryAcquireProcessLock()
    {
        if (processMutexUnavailable)
        {
            return null;
        }

        try
        {
            processMutex ??= new Mutex(initiallyOwned: false, processLockName);
            try
            {
                return processMutex.WaitOne(ProcessLockTimeoutMilliseconds) ? processMutex : null;
            }
            catch (AbandonedMutexException)
            {
                // The other writer died mid-append; we now own the lock.
                return processMutex;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or WaitHandleCannotBeOpenedException)
        {
            processMutexUnavailable = true;
            return null;
        }
    }

    private static void ReleaseProcessLock(Mutex? processLock)
    {
        if (processLock is null)
        {
            return;
        }

        try
        {
            processLock.ReleaseMutex();
        }
        catch (Exception ex) when (ex is ApplicationException or ObjectDisposedException)
        {
            // Ownership was already lost; the append itself already happened.
        }
    }

    private string Format(DiagnosticLevel level, string eventName, string message)
    {
        var timestamp = clock().ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var levelText = level switch
        {
            DiagnosticLevel.Warning => "WARN",
            DiagnosticLevel.Error => "ERROR",
            _ => "INFO",
        };
        var name = SanitizeEventName(eventName);
        var text = DiagnosticRedaction.SingleLine(
            DiagnosticRedaction.RedactHome(message, homeDirectory));
        return $"{timestamp} {levelText} {name} | {text}{Environment.NewLine}";
    }

    private static string SanitizeEventName(string? eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return "event";
        }

        var builder = new StringBuilder(Math.Min(eventName.Length, MaxEventNameLength));
        foreach (var c in eventName)
        {
            if (builder.Length == MaxEventNameLength)
            {
                break;
            }

            builder.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        }

        return builder.Length == 0 ? "event" : builder.ToString();
    }

    private void RotateIfNeeded(int incomingLength)
    {
        var current = new FileInfo(CurrentFilePath);
        if (!current.Exists || current.Length + incomingLength <= maxFileBytes)
        {
            return;
        }

        // Shift archives first: current -> .1, .1 -> .2, ..., deleting the oldest.
        for (var index = maxFiles - 1; index >= 1; index--)
        {
            var source = index == 1 ? CurrentFilePath : ArchivePath(index - 1);
            if (File.Exists(source))
            {
                File.Move(source, ArchivePath(index), overwrite: true);
            }
        }

        // Remove archives left behind by a previously higher cap.
        for (var index = maxFiles; index <= maxFiles + 4; index++)
        {
            var stale = ArchivePath(index);
            if (File.Exists(stale))
            {
                File.Delete(stale);
            }
        }
    }

    private string ArchivePath(int index) => Path.Combine(DirectoryPath, $"mitosis.{index}.log");
}
