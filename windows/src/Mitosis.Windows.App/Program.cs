using System.Threading;
using Microsoft.UI.Dispatching;

namespace Mitosis.Windows.App;

/// <summary>Genuine entry point. For an unpackaged app the Windows App SDK runtime must be
/// initialized before any WinUI type is activated, so bootstrapping happens here — ahead of
/// the XAML-generated startup path (see DISABLE_XAML_GENERATED_MAIN in the project file).</summary>
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Bootstrap.Initialize();
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
