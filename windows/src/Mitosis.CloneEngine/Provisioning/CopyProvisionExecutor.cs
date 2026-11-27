using System.Text.Json;
using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>How one instance is started. Executable and data directory are relative to the
/// instance directory; arguments may contain <c>{instance}</c> and <c>{dataDirectory}</c>
/// placeholders, substituted by <see cref="InstanceLauncher"/>. A strategy supplies this —
/// the executor never guesses an executable name.</summary>
public sealed record LaunchSpecification(
    string ExecutableRelativePath,
    IReadOnlyList<string> Arguments,
    string DataDirectoryRelativePath = "data");

/// <summary>Instance manifest written beside the copied files. Identity and provenance only:
/// no user documents, credentials, or environment data.</summary>
public sealed record InstanceManifest(
    int SchemaVersion,
    Guid PlanId,
    string StrategyId,
    string StrategyVersion,
    Guid ProfileId,
    string AppId,
    string SourceRoot,
    string InstancePath,
    DateTimeOffset CreatedAt);

/// <summary>Launch configuration written into the instance. The launcher revalidates every
/// path against the instance directory before starting anything.</summary>
public sealed record LaunchConfig(
    int SchemaVersion,
    string Executable,
    IReadOnlyList<string>? Arguments,
    string DataDirectory,
    string WorkingDirectory);

/// <summary>First real <see cref="IProvisionExecutor"/>: copies an application's install
/// directory into a private instance directory, writes the manifest and launch
/// configuration, and creates the private data directory. Ordering is fixed: validate,
/// copy, write — and any failure removes the directory this executor created. It never
/// launches the application, never touches the source, the registry, or profile records.
/// A successful copy is still not isolation evidence for a third-party app; the strategy
/// and its evidence decide whether execution is ever offered.</summary>
public sealed class CopyProvisionExecutor : IProvisionExecutor
{
    public const string ManifestFileName = "mitosis-instance.json";
    public const string LaunchConfigFileName = "mitosis-launch.json";
    public const int SchemaVersion = 1;

    /// <summary>Bounds that turn a runaway or hostile source tree into a refusal instead of
    /// an unbounded copy. Both are far above any real application install.</summary>
    public const int MaxFileCount = 100_000;
    public const long MaxTotalBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>Depth cap for the manual traversal; a directory tree deeper than this is
    /// treated as hostile rather than walked.</summary>
    private const int MaxDepth = 64;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly LaunchSpecification launch;

    public CopyProvisionExecutor(LaunchSpecification launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(launch.Arguments);
        if (!DestinationSafety.IsValidRelativePath(launch.ExecutableRelativePath, out var executableError))
        {
            throw new ArgumentException(executableError, nameof(launch));
        }

        if (!DestinationSafety.IsValidRelativePath(launch.DataDirectoryRelativePath, out var dataError))
        {
            throw new ArgumentException(dataError, nameof(launch));
        }

        this.launch = launch;
    }

    public LaunchSpecification Launch => launch;

    public Task<ProvisionResult> ExecuteAsync(
        OperationPlan plan,
        IProgress<ProvisionProgress>? progress,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Execute(plan, progress, cancellationToken), cancellationToken);

    private ProvisionResult Execute(
        OperationPlan plan,
        IProgress<ProvisionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!plan.CanExecute || plan.Failures.Count > 0)
        {
            return ProvisionResult.Refused(plan.PlanId, "Plan was not admitted for execution.");
        }

        if (!TryNormalizeRoot(plan.InstanceRoot, out var root, out var rootError))
        {
            return ProvisionResult.Refused(plan.PlanId, rootError);
        }

        if (string.IsNullOrWhiteSpace(plan.SourceRoot)
            || !Path.IsPathRooted(plan.SourceRoot)
            || !Directory.Exists(plan.SourceRoot))
        {
            return ProvisionResult.Refused(plan.PlanId,
                "The application install location is missing or no longer readable.");
        }

        var source = Path.GetFullPath(plan.SourceRoot!);
        if (IsUnder(root, source) || IsUnder(source, root))
        {
            return ProvisionResult.Refused(plan.PlanId,
                "The source and the instance root must not contain each other.");
        }

        string destination;
        try
        {
            destination = Path.GetFullPath(DestinationSafety.CombineChecked(root, plan.InstanceRelativePath));
        }
        catch (InvalidProfileException ex)
        {
            return ProvisionResult.Refused(plan.PlanId, ex.Message);
        }

        if (!IsUnder(destination, root) || string.Equals(destination, root, StringComparison.OrdinalIgnoreCase))
        {
            return ProvisionResult.Refused(plan.PlanId, "The destination escapes the instance root.");
        }

        if (Directory.Exists(destination) || File.Exists(destination))
        {
            return ProvisionResult.Refused(plan.PlanId,
                "The destination already exists; refusing to overwrite an existing instance.");
        }

        if (HasReparseAncestor(destination))
        {
            return ProvisionResult.Refused(plan.PlanId,
                "The instance root traverses a reparse point; refusing to write through it.");
        }

        var launchSource = Path.GetFullPath(Path.Combine(source, launch.ExecutableRelativePath));
        if (!IsUnder(launchSource, source) || !File.Exists(launchSource))
        {
            return ProvisionResult.Refused(plan.PlanId,
                $"The launch executable '{launch.ExecutableRelativePath}' is not part of the application.");
        }

        List<(string SourcePath, string RelativePath, long Length)> files;
        List<string> directories;
        try
        {
            if (!TryEnumerateSource(source, cancellationToken, out files, out directories, out var enumerateError))
            {
                return ProvisionResult.Refused(plan.PlanId, enumerateError);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.Security.SecurityException)
        {
            return ProvisionResult.Refused(plan.PlanId,
                $"The application install could not be read: {ex.Message}");
        }

        var created = false;
        try
        {
            progress?.Report(new ProvisionProgress(0, 4, "Creating the private instance directory."));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(destination);
            created = true;

            foreach (var relative in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            progress?.Report(new ProvisionProgress(1, 4,
                $"Copying {files.Count} application file(s) into the instance."));
            long copiedBytes = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.Combine(destination, file.RelativePath);
                var parent = Path.GetDirectoryName(target);
                if (parent != null)
                {
                    Directory.CreateDirectory(parent);
                }

                File.Copy(file.SourcePath, target, overwrite: false);
                // A read-only source file (common under Program Files) must not make the
                // instance read-only: the copy is ours and the application must be able to
                // update it, and a failed run must be able to remove it.
                File.SetAttributes(target, FileAttributes.Normal);
                copiedBytes += file.Length;
            }

            var dataDirectory = Path.Combine(destination, launch.DataDirectoryRelativePath);
            Directory.CreateDirectory(dataDirectory);

            progress?.Report(new ProvisionProgress(2, 4, "Writing the instance manifest."));
            var manifestPath = Path.Combine(destination, ManifestFileName);
            var manifest = new InstanceManifest(
                SchemaVersion,
                plan.PlanId,
                plan.StrategyId,
                plan.StrategyVersion,
                plan.ProfileId,
                plan.AppId,
                source,
                destination,
                DateTimeOffset.UtcNow);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

            progress?.Report(new ProvisionProgress(3, 4, "Writing the launch configuration."));
            var launchConfigPath = Path.Combine(destination, LaunchConfigFileName);
            var config = new LaunchConfig(
                SchemaVersion,
                launch.ExecutableRelativePath,
                launch.Arguments.ToList(),
                launch.DataDirectoryRelativePath,
                ".");
            File.WriteAllText(launchConfigPath, JsonSerializer.Serialize(config, JsonOptions));

            if (copiedBytes > MaxTotalBytes)
            {
                throw new IOException("The copied tree exceeded the instance size limit.");
            }

            progress?.Report(new ProvisionProgress(4, 4, "Instance prepared; the application was not launched."));

            return ProvisionResult.Completed(plan.PlanId, new[]
            {
                destination,
                manifestPath,
                launchConfigPath,
                dataDirectory,
            });
        }
        catch (OperationCanceledException)
        {
            CleanUp(destination, created);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidProfileException or NotSupportedException or System.Security.SecurityException)
        {
            var cleaned = CleanUp(destination, created);
            return ProvisionResult.Refused(plan.PlanId,
                $"Provisioning failed: {ex.Message}"
                + (cleaned ? " The partial instance directory was removed." : string.Empty));
        }
    }

    private static bool TryNormalizeRoot(string? instanceRoot, out string root, out string error)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(instanceRoot) || !Path.IsPathRooted(instanceRoot))
        {
            error = "Instance root must be an absolute application-owned directory.";
            return false;
        }

        var full = Path.GetFullPath(instanceRoot);
        if (!string.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                instanceRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Instance root must be a normalized absolute path.";
            return false;
        }

        var pathRoot = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Instance root must be a directory, not a drive or share root.";
            return false;
        }

        root = full.TrimEnd(Path.DirectorySeparatorChar);
        error = string.Empty;
        return true;
    }

    private static bool TryEnumerateSource(
        string source,
        CancellationToken cancellationToken,
        out List<(string SourcePath, string RelativePath, long Length)> files,
        out List<string> directories,
        out string error)
    {
        files = new List<(string, string, long)>();
        directories = new List<string>();
        error = string.Empty;
        long totalBytes = 0;

        var pending = new Stack<(DirectoryInfo Directory, int Depth)>();
        pending.Push((new DirectoryInfo(source), 0));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Pop();
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                error = $"'{directory.FullName}' is a reparse point; refusing to copy it.";
                return false;
            }

            if (depth > MaxDepth)
            {
                error = "The application tree is nested too deeply; refusing to copy it.";
                return false;
            }

            foreach (var entry in directory.EnumerateFileSystemInfos("*", new EnumerationOptions
                     {
                         RecurseSubdirectories = false,
                         IgnoreInaccessible = false,
                         AttributesToSkip = 0,
                         ReturnSpecialDirectories = false,
                     }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    error = $"'{entry.FullName}' is a reparse point; refusing to copy it.";
                    return false;
                }

                var relative = Path.GetRelativePath(source, entry.FullName);
                if (!DestinationSafety.IsValidRelativePath(relative, out var pathError))
                {
                    error = pathError;
                    return false;
                }

                if (entry is DirectoryInfo subdirectory)
                {
                    directories.Add(relative);
                    pending.Push((subdirectory, depth + 1));
                    continue;
                }

                if (entry is not FileInfo file)
                {
                    continue;
                }

                if (files.Count >= MaxFileCount)
                {
                    error = $"The application has more than {MaxFileCount} files; refusing to copy it.";
                    return false;
                }

                totalBytes += file.Length;
                if (totalBytes > MaxTotalBytes)
                {
                    error = "The application is larger than the instance size limit; refusing to copy it.";
                    return false;
                }

                files.Add((file.FullName, relative, file.Length));
            }
        }

        return true;
    }

    private static bool CleanUp(string destination, bool created)
    {
        if (!created)
        {
            return false;
        }

        try
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            return true;
        }
        catch
        {
            // Best effort: report the original failure, never mask it with cleanup noise.
            return false;
        }
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static bool IsUnder(string path, string parent) =>
        path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar),
            parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
