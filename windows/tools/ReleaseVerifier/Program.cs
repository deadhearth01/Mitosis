namespace Mitosis.ReleaseVerifier;

/// <summary>CLI for the release-candidate verifier. Usage:
///
///     dotnet run --project tools/ReleaseVerifier -c Release -- --archive &lt;path-to-zip&gt;
///
/// Prints the archive SHA-256, every finding, and "verified" or a failure
/// summary; exits 0 when the artifact passed, 1 when it did not, 2 on usage
/// errors. CI relies on that exit code to block artifact upload.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? archive = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--archive":
                    if (i + 1 >= args.Length)
                    {
                        return Usage("the --archive option needs a value");
                    }
                    archive = args[++i];
                    break;
                default:
                    if (archive is null && !args[i].StartsWith('-'))
                    {
                        archive = args[i];
                        break;
                    }
                    return Usage($"unknown argument '{args[i]}'");
            }
        }
        if (archive is null)
        {
            return Usage("no archive given");
        }

        var result = ReleaseVerifier.Verify(archive);
        Console.WriteLine($"Release candidate: {Path.GetFileName(result.Archive)}");
        if (result.Sha256.Length == 64)
        {
            Console.WriteLine($"SHA-256: {result.Sha256}");
        }
        foreach (var finding in result.Findings)
        {
            Console.WriteLine($"[FAIL] {finding}");
        }
        if (result.Passed)
        {
            Console.WriteLine("verified: archive name, integrity, required files, "
                + "architecture, embedded icon, and version metadata all agree");
            return 0;
        }
        Console.WriteLine($"not verified: {result.Findings.Count} finding(s), see above");
        return 1;
    }

    private static int Usage(string problem)
    {
        Console.Error.WriteLine($"error: {problem}");
        Console.Error.WriteLine("usage: ReleaseVerifier --archive <path-to-zip>");
        return 2;
    }
}
