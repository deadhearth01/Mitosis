using Mitosis.Application.Preferences;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Regression coverage for the Stage 27 startup crash: a view-model await that
/// resumed on a thread-pool thread raised <c>PropertyChanged</c> there, and x:Bind applied
/// the change to XAML on that thread, which surfaced as
/// <c>RPC_E_WRONG_THREAD</c> inside <c>Application.Resources</c>. The Windows view models
/// therefore keep the caller's synchronization context; these tests pin that state changes
/// are delivered through the context even when the awaited work completes elsewhere.</summary>
[TestClass]
public sealed class ViewModelThreadAffinityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LoadAsync_DeliversStateChangesThroughTheCallersContext()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<DiscoveredApp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new FakeDiscoveryService();
        discovery.Blocks(gate);
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());
        var observed = new List<SynchronizationContext?>();
        model.PropertyChanged += (_, _) => observed.Add(SynchronizationContext.Current);
        using var context = new InlineSynchronizationContext();

        var load = model.LoadAsync(TestContext.CancellationToken);
        gate.SetResult(Array.Empty<DiscoveredApp>());
        await load;

        Assert.IsTrue(observed.Count > 0, "Loading must notify at least once.");
        Assert.IsTrue(
            observed.All(c => ReferenceEquals(c, context)),
            "Every notification must arrive through the caller's synchronization context.");
    }

    [TestMethod]
    public async Task FinishAsync_DeliversTheFinishedStateThroughTheCallersContext()
    {
        var store = new DelayedPreferencesStore();
        var model = new OnboardingViewModel(store);
        var observed = new List<SynchronizationContext?>();
        model.PropertyChanged += (_, _) => observed.Add(SynchronizationContext.Current);
        using var context = new InlineSynchronizationContext();

        var finish = model.FinishAsync(startFirstClone: false, TestContext.CancellationToken);
        store.Release();
        await finish;

        Assert.IsTrue(model.Finished);
        Assert.IsTrue(observed.Count > 0, "Finishing must notify at least once.");
        Assert.IsTrue(
            observed.All(c => ReferenceEquals(c, context)),
            "Every notification must arrive through the caller's synchronization context.");
    }

    /// <summary>Runs posted continuations inline and marks the current context, the way a
    /// dispatcher-backed context runs them on its own thread. A continuation that skips
    /// <see cref="SynchronizationContext.Post"/> (the removed ConfigureAwait(false) case)
    /// runs on the pool thread with no context current, which the assertions detect.</summary>
    private sealed class InlineSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? previous = Current;

        public InlineSynchronizationContext() => SetSynchronizationContext(this);

        public override void Post(SendOrPostCallback d, object? state)
        {
            var outer = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(outer);
            }
        }

        public void Dispose() => SetSynchronizationContext(previous);
    }

    /// <summary>A preference load that stays pending until the test releases it, so the
    /// completion continuation necessarily leaves the calling thread.</summary>
    private sealed class DelayedPreferencesStore : IAppPreferencesStore
    {
        private readonly TaskCompletionSource<AppPreferences> gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default) =>
            gate.Task;

        public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Release() => gate.SetResult(AppPreferences.Default);
    }
}
