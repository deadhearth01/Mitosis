using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>Result of a launch attempt. <see cref="Started"/> is false when validation
/// refused before any process existed; <see cref="TimedOut"/> means the process was
/// stopped after the timeout.</summary>
public sealed record LaunchOutcome(
    bool Started,
    int? ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError,
    string? Error);

/// <summary>Starts the executable recorded in an instance's launch configuration. Every
/// path is revalidated against the instance directory: the executable, the working
/// directory, and the data directory must all resolve inside it, so a tampered
/// configuration cannot launch an arbitrary program. Output capture is bounded.</summary>
public static class InstanceLauncher
{
    public const int MaxCapturedCharacters = 64 * 1024;

    public static async Task<LaunchOutcome> LaunchAsync(
        string instanceDirectory,
        string launchConfigFileName = CopyProvisionExecutor.LaunchConfigFileName,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory) || !Path.IsPathRooted(instanceDirectory))
        {
            return Failed("The instance directory must be an absolute path.");
        }

        var instance = Path.GetFullPath(instanceDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(instance))
        {
            return Failed("The instance directory does not exist.");
        }

        if (!DestinationSafety.IsValidRelativePath(launchConfigFileName, out var configError))
        {
            return Failed(configError);
        }

        var configPath = Path.Combine(instance, launchConfigFileName);
        if (!File.Exists(configPath))
        {
            return Failed("The instance has no launch configuration.");
        }

        LaunchConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<LaunchConfig>(
                File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Failed($"The launch configuration could not be read: {ex.Message}");
        }

        if (config == null || string.IsNullOrWhiteSpace(config.Executable))
        {
            return Failed("The launch configuration names no executable.");
        }

        if (!DestinationSafety.IsValidRelativePath(config.Executable, out var executableError))
        {
            return Failed(executableError);
        }

        if (!DestinationSafety.IsValidRelativePath(config.DataDirectory, out var dataError))
        {
            return Failed(dataError);
        }

        // "." is the instance directory itself: a valid working directory, but it is not a
        // destination path, so DestinationSafety (which rejects ".") does not apply to it.
        var workingRelative = string.IsNullOrWhiteSpace(config.WorkingDirectory)
            ? "."
            : config.WorkingDirectory;
        if (!string.Equals(workingRelative, ".", StringComparison.Ordinal)
            && !DestinationSafety.IsValidRelativePath(workingRelative, out var workingError))
        {
            return Failed(workingError);
        }

        var executable = Path.GetFullPath(Path.Combine(instance, config.Executable));
        if (!IsUnder(executable, instance) || !File.Exists(executable))
        {
            return Failed("The executable is not inside the instance directory.");
        }

        var workingDirectory = string.Equals(workingRelative, ".", StringComparison.Ordinal)
            ? instance
            : Path.GetFullPath(Path.Combine(instance, workingRelative));
        if (!IsUnder(workingDirectory, instance) || !Directory.Exists(workingDirectory))
        {
            return Failed("The working directory is not inside the instance directory.");
        }

        var dataDirectory = Path.GetFullPath(Path.Combine(instance, config.DataDirectory));
        if (!IsUnder(dataDirectory, instance))
        {
            return Failed("The data directory is not inside the instance directory.");
        }

        Directory.CreateDirectory(dataDirectory);

        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in config.Arguments ?? Array.Empty<string>())
        {
            info.ArgumentList.Add(Substitute(argument, instance, dataDirectory));
        }

        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start())
            {
                return Failed("The process did not start.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Failed($"The process could not start: {ex.Message}");
        }

        var outputTask = ReadBoundedAsync(process.StandardOutput);
        var errorTask = ReadBoundedAsync(process.StandardError);

        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
                // The process may already have exited; the timeout result still stands.
            }

            return new LaunchOutcome(
                Started: true,
                ExitCode: null,
                TimedOut: true,
                StandardOutput: await SafeAwait(outputTask).ConfigureAwait(false),
                StandardError: await SafeAwait(errorTask).ConfigureAwait(false),
                Error: "The application did not exit before the launch timeout.");
        }

        return new LaunchOutcome(
            Started: true,
            ExitCode: process.ExitCode,
            TimedOut: false,
            StandardOutput: await SafeAwait(outputTask).ConfigureAwait(false),
            StandardError: await SafeAwait(errorTask).ConfigureAwait(false),
            Error: null);
    }

    private static LaunchOutcome Failed(string error) =>
        new(false, null, false, string.Empty, string.Empty, error);

    private static string Substitute(string argument, string instance, string dataDirectory) =>
        argument
            .Replace("{instance}", instance, StringComparison.Ordinal)
            .Replace("{dataDirectory}", dataDirectory, StringComparison.Ordinal);

    private static async Task<string> SafeAwait(Task<string> read)
    {
        try
        {
            return await read.ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        var captured = new StringBuilder();
        // Read to end of stream even after the capture cap is reached: stopping early
        // leaves the child blocked on a full pipe and it would only ever exit by timeout.
        while (true)
        {
            var read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (captured.Length < MaxCapturedCharacters)
            {
                captured.Append(buffer, 0, Math.Min(read, MaxCapturedCharacters - captured.Length));
            }
        }

        return captured.ToString();
    }

    private static bool IsUnder(string path, string parent) =>
        path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar),
            parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
