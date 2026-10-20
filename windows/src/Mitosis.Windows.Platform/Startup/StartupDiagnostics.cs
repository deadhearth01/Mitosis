namespace Mitosis.Windows.Platform.Startup;

/// <summary>Pure formatting for the message shown when the app cannot start because a
/// machine runtime prerequisite is missing. Kept free of UI and filesystem calls so
/// unit tests can pin that the dialog text names the prerequisite and its HRESULT
/// without exposing stack traces or local machine paths (those leaked through the
/// CLR unhandled-exception dialog this replaces).</summary>
public static class StartupDiagnostics
{
    public const string PrerequisiteDialogTitle = "Mitosis needs a Windows component";

    /// <summary>Message for the unpackaged app's machine Windows App SDK runtime
    /// requirement (Bootstrap.Initialize fails with a nonzero HRESULT when it is
    /// missing or older than 2.5).</summary>
    public static string DescribeMissingAppSdk(int hresult)
    {
        return "Mitosis could not start because the Windows App SDK runtime is missing "
            + "or is older than 2.5 (HRESULT 0x" + hresult.ToString("X8") + ").\n\n"
            + "Install the Windows App SDK 2.x runtime (2.5 or newer), then relaunch "
            + "Mitosis. See docs/WINDOWS-RELEASE.md for the exact requirements.";
    }
}
