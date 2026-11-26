using Mitosis.Windows.Platform.Startup;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>Decision logic for single-instancing: who owns the lock, what a second
/// launch does, abandoned-lock recovery, and shutdown hand-over. The OS-level focus
/// effect of <c>ForegroundPermission</c> is a manual check, not a unit test.</summary>
[TestClass]
public sealed class SingleInstanceCoordinatorTests
{
    private static string NewScope() => "test" + Guid.NewGuid().ToString("N");

    /// <summary>Emulates a separate process: acquires the lock on its own thread and
    /// holds it until disposed, so the test thread is a genuinely different owner.</summary>
    private sealed class HeldPrimary : IDisposable
    {
        private readonly ManualResetEventSlim acquired = new();
        private readonly ManualResetEventSlim release = new();
        private readonly Thread thread;

        public HeldPrimary(SingleInstanceCoordinator coordinator)
        {
            thread = new Thread(() =>
            {
                try
                {
                    Attempt = coordinator.TryBecomePrimary();
                }
                catch (Exception ex)
                {
                    Failure = ex;
                }
                finally
                {
                    acquired.Set();
                }

                release.Wait(TimeSpan.FromSeconds(30));
            })
            {
                IsBackground = true,
            };
            thread.Start();

            if (!acquired.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new AssertFailedException("The primary thread did not start.");
            }

            if (Failure != null)
            {
                throw new AssertFailedException(Failure.ToString());
            }

            if (Attempt is not { Role: SingleInstanceRole.Primary })
            {
                throw new AssertFailedException($"Expected primary, got {Attempt?.Role}.");
            }
        }

        public SingleInstanceAttempt? Attempt { get; private set; }

        public Exception? Failure { get; private set; }

        public void Dispose()
        {
            release.Set();
            thread.Join(TimeSpan.FromSeconds(10));
            acquired.Dispose();
            release.Dispose();
        }
    }

    [TestMethod]
    public void FirstAcquisition_BecomesPrimaryAndNamesItsObjects()
    {
        var scope = NewScope();
        using var coordinator = new SingleInstanceCoordinator(scope);

        var attempt = coordinator.TryBecomePrimary();

        Assert.AreEqual(SingleInstanceRole.Primary, attempt.Role);
        Assert.IsFalse(attempt.RecoveredFromAbandonedMutex);
        Assert.IsNull(attempt.FailureReason);
        Assert.AreEqual($@"Local\Mitosis.Windows.{scope}.lock", coordinator.MutexName);
        Assert.AreEqual($@"Local\Mitosis.Windows.{scope}.focus", coordinator.ActivationEventName);
        Assert.AreEqual($@"Local\Mitosis.Windows.{scope}.ack", coordinator.AcknowledgementEventName);
    }

    [TestMethod]
    public void SecondAcquisition_FromAnotherThread_IsSecondary()
    {
        var scope = NewScope();
        using var primary = new SingleInstanceCoordinator(scope);
        using var holder = new HeldPrimary(primary);

        using var secondary = new SingleInstanceCoordinator(scope);
        var attempt = secondary.TryBecomePrimary();

        Assert.AreEqual(SingleInstanceRole.Secondary, attempt.Role);
        Assert.IsNull(attempt.FailureReason);
    }

    [TestMethod]
    public void AbandonedOwner_IsRecoveredAsPrimary()
    {
        var scope = NewScope();
        using var probe = new SingleInstanceCoordinator(scope);

        // Deliberately leak the handle and let the thread exit while owning the
        // mutex, which is exactly what a crashed process leaves behind.
        var crashedOwner = new Thread(() =>
        {
            var abandoned = new Mutex(initiallyOwned: false, probe.MutexName);
            abandoned.WaitOne();
        });
        crashedOwner.Start();
        Assert.IsTrue(crashedOwner.Join(TimeSpan.FromSeconds(10)));

        using var coordinator = new SingleInstanceCoordinator(scope);
        var attempt = coordinator.TryBecomePrimary();

        Assert.AreEqual(SingleInstanceRole.Primary, attempt.Role);
        Assert.IsTrue(attempt.RecoveredFromAbandonedMutex);
    }

    [TestMethod]
    public void ActivationRequest_ReachesTheListenerAndIsAcknowledged()
    {
        var scope = NewScope();
        using var primary = new SingleInstanceCoordinator(scope, TimeSpan.FromSeconds(5));
        using var holder = new HeldPrimary(primary);

        using var handled = new ManualResetEventSlim();
        Assert.IsTrue(primary.StartActivationListener(() => handled.Set()));
        Assert.IsFalse(
            primary.StartActivationListener(() => { }),
            "One listener per instance; a second registration must be refused.");

        using var secondary = new SingleInstanceCoordinator(scope, TimeSpan.FromSeconds(5));
        Assert.AreEqual(SingleInstanceRole.Secondary, secondary.TryBecomePrimary().Role);

        Assert.IsTrue(secondary.RequestPrimaryActivation());
        Assert.IsTrue(handled.Wait(TimeSpan.FromSeconds(5)), "The primary never ran the activation.");
    }

    [TestMethod]
    public void ActivationRequest_WithoutAListener_TimesOut()
    {
        var scope = NewScope();
        using var primary = new SingleInstanceCoordinator(scope);
        using var holder = new HeldPrimary(primary);

        using var secondary = new SingleInstanceCoordinator(scope, TimeSpan.FromMilliseconds(250));
        Assert.AreEqual(SingleInstanceRole.Secondary, secondary.TryBecomePrimary().Role);

        Assert.IsFalse(
            secondary.RequestPrimaryActivation(),
            "A primary that never listens must not be reported as activated.");
    }

    [TestMethod]
    public void ActivationRequest_WithoutAPrimary_Fails()
    {
        using var lone = new SingleInstanceCoordinator(NewScope(), TimeSpan.FromMilliseconds(200));

        Assert.IsFalse(lone.RequestPrimaryActivation());
    }

    [TestMethod]
    public void Shutdown_StopsTheListenerAndHandsTheLockToTheNextLaunch()
    {
        var scope = NewScope();
        using var primary = new SingleInstanceCoordinator(scope, TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(SingleInstanceRole.Primary, primary.TryBecomePrimary().Role);
        var activations = 0;
        Assert.IsTrue(primary.StartActivationListener(() => Interlocked.Increment(ref activations)));

        primary.Shutdown();

        Assert.IsFalse(
            primary.StartActivationListener(() => { }),
            "A shut-down coordinator must not start a listener.");

        // A launch during shutdown must be able to take over immediately, from a
        // different thread, rather than becoming a secondary of a dying process.
        using var newcomer = new SingleInstanceCoordinator(scope, TimeSpan.FromMilliseconds(500));
        SingleInstanceAttempt? attempt = null;
        var thread = new Thread(() => attempt = newcomer.TryBecomePrimary());
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));

        Assert.IsNotNull(attempt);
        Assert.AreEqual(SingleInstanceRole.Primary, attempt.Role);
        Assert.AreEqual(0, Volatile.Read(ref activations));
    }

    [TestMethod]
    public void Constructor_RejectsEmptyUnsafeOrUnboundedInput()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstanceCoordinator(""));
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstanceCoordinator("  "));
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstanceCoordinator("has space"));
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstanceCoordinator(@"has\slash"));
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstanceCoordinator(new string('a', 65)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new SingleInstanceCoordinator("ok", TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new SingleInstanceCoordinator("ok", TimeSpan.FromSeconds(-1)));
    }

    [TestMethod]
    public void ForCurrentUser_ProducesAUserScopedObjectName()
    {
        using var coordinator = SingleInstanceCoordinator.ForCurrentUser();

        StringAssert.StartsWith(coordinator.MutexName, @"Local\Mitosis.Windows.");
        StringAssert.EndsWith(coordinator.MutexName, ".lock");
        Assert.IsFalse(
            coordinator.MutexName.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase),
            "The object name must not expose the account name.");
    }
}
