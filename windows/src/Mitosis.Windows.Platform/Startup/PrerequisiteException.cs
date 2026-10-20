namespace Mitosis.Windows.Platform.Startup;

/// <summary>Thrown when a machine runtime the Windows port depends on is missing or
/// too old (currently only the unpackaged app's Windows App SDK requirement). The
/// app's entry point catches this specifically so the plain diagnostic message is
/// shown instead of a crash dialog, and so other exceptions still surface normally.</summary>
public sealed class PrerequisiteException : Exception
{
    public PrerequisiteException(string message, int hresult) : base(message)
    {
        HResult = hresult;
    }
}
