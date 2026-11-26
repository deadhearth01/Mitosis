using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Mitosis.Windows.Platform.Startup;

/// <summary>Which side of single-instancing this process ended up on.</summary>
public enum SingleInstanceRole
{
    /// <summary>This process owns the instance lock and should show the main window.</summary>
    Primary,

    /// <summary>Another process owns the lock; this launch should exit.</summary>
    Secondary,

    /// <summary>The guard could not decide (unusual OS failure); the caller should
    /// continue as primary rather than refuse to start.</summary>
    Failed,
}

/// <summary>Outcome of <see cref="SingleInstanceCoordinator.TryBecomePrimary"/>.</summary>
public sealed record SingleInstanceAttempt(
    SingleInstanceRole Role,
    bool RecoveredFromAbandonedMutex,
    string? FailureReason)
{
    public static SingleInstanceAttempt Primary(bool recovered) =>
        new(SingleInstanceRole.Primary, recovered, null);

    public static SingleInstanceAttempt Secondary() =>
        new(SingleInstanceRole.Secondary, false, null);

    public static SingleInstanceAttempt Failed(string reason) =>
        new(SingleInstanceRole.Failed, false, reason);
}

/// <summary>Single-instance coordination for the unpackaged Windows app.
///
/// The Windows App SDK's <c>AppInstance</c> redirection is built around package
/// identity, so an unpackaged app uses the documented alternative: one named mutex
/// keyed to the current user, plus named events for activation redirection.
///
/// Flow:
/// <list type="bullet">
/// <item>The first process waits on the mutex and becomes primary. It creates the
/// focus and acknowledgement events and listens for activation requests on a
/// background thread.</item>
/// <item>A later process fails the mutex wait, becomes secondary, asks the primary
/// to surface its window, and waits a bounded time for the acknowledgement before
/// exiting. It never opens a second main window.</item>
/// <item>A crash leaves the mutex abandoned; the next launch recovers it and becomes
/// primary, which is reported in the attempt.</item>
/// </list>
///
/// All object names use the <c>Local\</c> namespace, so they are per logon session;
/// the scope is additionally derived from the current user's SID. No elevation and
/// no machine-wide state.</summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    public const string ApplicationKey = "Mitosis.Windows";

    /// <summary>How long a secondary waits for the primary to acknowledge. Long
    /// enough for a cold WinUI start, short enough that a stuck primary does not
    /// leave a second process hanging around.</summary>
    public static readonly TimeSpan DefaultActivationTimeout = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly TimeSpan activationTimeout;
    private Mutex? mutex;
    private EventWaitHandle? activationEvent;
    private EventWaitHandle? acknowledgementEvent;
    private EventWaitHandle? stopEvent;
    private Thread? listener;
    private int ownerThreadId;
    private bool disposed;

    public SingleInstanceCoordinator(string scope, TimeSpan? activationTimeout = null)
    {
        ValidateScope(scope);
        this.activationTimeout = activationTimeout ?? DefaultActivationTimeout;
        if (this.activationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activationTimeout), "The activation timeout must be positive.");
        }

        MutexName = $@"Local\{ApplicationKey}.{scope}.lock";
        ActivationEventName = $@"Local\{ApplicationKey}.{scope}.focus";
        AcknowledgementEventName = $@"Local\{ApplicationKey}.{scope}.ack";
    }

    /// <summary>Coordinator scoped to the current user in the current logon session.</summary>
    public static SingleInstanceCoordinator ForCurrentUser() => new(CurrentUserScope());

    public string MutexName { get; }

    public string ActivationEventName { get; }

    public string AcknowledgementEventName { get; }

    /// <summary>Attempts to take the instance lock. Must be called once per process,
    /// before the UI is created. On <see cref="SingleInstanceRole.Secondary"/> the
    /// caller should request activation and exit.</summary>
    public SingleInstanceAttempt TryBecomePrimary()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            var recovered = false;
            try
            {
                mutex = new Mutex(initiallyOwned: false, MutexName);
                bool owned;
                try
                {
                    owned = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner died without releasing. We now own it.
                    owned = true;
                    recovered = true;
                }

                if (!owned)
                {
                    ReleaseMutexHandle();
                    return SingleInstanceAttempt.Secondary();
                }

                ownerThreadId = Environment.CurrentManagedThreadId;
                activationEvent = new EventWaitHandle(
                    initialState: false, EventResetMode.AutoReset, ActivationEventName);
                acknowledgementEvent = new EventWaitHandle(
                    initialState: false, EventResetMode.AutoReset, AcknowledgementEventName);
                stopEvent = new ManualResetEvent(initialState: false);
                return SingleInstanceAttempt.Primary(recovered);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or WaitHandleCannotBeOpenedException or NotSupportedException
                or ArgumentException or SecurityException)
            {
                StopListenerLocked();
                ReleaseMutexHandle();
                return SingleInstanceAttempt.Failed(ex.GetType().Name);
            }
        }
    }

    /// <summary>Starts the background thread that turns activation requests from later
    /// launches into <paramref name="activate"/> calls. The callback runs off the UI
    /// thread and must marshal as needed. Only the primary can listen, and only once.</summary>
    public bool StartActivationListener(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        lock (gate)
        {
            if (disposed
                || listener != null
                || activationEvent is null
                || acknowledgementEvent is null
                || stopEvent is null)
            {
                return false;
            }

            var focus = activationEvent;
            var acknowledgement = acknowledgementEvent;
            var stop = stopEvent;
            listener = new Thread(() => Listen(activate, focus, acknowledgement, stop))
            {
                IsBackground = true,
                Name = "Mitosis activation listener",
            };
            listener.Start();
            return true;
        }
    }

    /// <summary>Asks the running primary to surface its window and waits a bounded
    /// time for its acknowledgement. Returns false when no primary is reachable (it is
    /// starting, shutting down, hung, or already gone).</summary>
    public bool RequestPrimaryActivation(TimeSpan? timeout = null)
    {
        var wait = timeout ?? activationTimeout;
        if (wait <= TimeSpan.Zero)
        {
            return false;
        }

        var deadline = DateTime.UtcNow + wait;
        using var focus = OpenNamedEvent(ActivationEventName, deadline);
        if (focus is null)
        {
            return false;
        }

        using var acknowledgement = OpenNamedEvent(AcknowledgementEventName, deadline);
        if (acknowledgement is null)
        {
            return false;
        }

        try
        {
            focus.Set();
            var remaining = deadline - DateTime.UtcNow;
            return remaining > TimeSpan.Zero && acknowledgement.WaitOne(remaining);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Stops the listener and releases the instance lock, so a launch during
    /// shutdown can take over immediately instead of waiting for process exit.
    /// Idempotent.</summary>
    public void Shutdown()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopListenerLocked();
            ReleaseMutexHandle();
        }
    }

    public void Dispose() => Shutdown();

    private void Listen(
        Action activate,
        EventWaitHandle focus,
        EventWaitHandle acknowledgement,
        EventWaitHandle stop)
    {
        var handles = new WaitHandle[] { focus, stop };
        while (true)
        {
            int signaled;
            try
            {
                signaled = WaitHandle.WaitAny(handles);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            if (signaled != 0)
            {
                return;
            }

            try
            {
                activate();
            }
            catch
            {
                // A failed activation must never take down the app or the listener.
            }

            try
            {
                acknowledgement.Set();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void StopListenerLocked()
    {
        try
        {
            stopEvent?.Set();
            listener?.Join(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ex is ObjectDisposedException or ThreadStateException)
        {
            // Best effort: the thread is a background thread and cannot keep the
            // process alive.
        }
        finally
        {
            listener = null;
            stopEvent?.Dispose();
            stopEvent = null;
            activationEvent?.Dispose();
            activationEvent = null;
            acknowledgementEvent?.Dispose();
            acknowledgementEvent = null;
        }
    }

    private void ReleaseMutexHandle()
    {
        if (mutex is null)
        {
            return;
        }

        try
        {
            // Release only from the owning thread; otherwise closing the handle makes
            // the mutex abandoned when that thread exits, which the next launch
            // recovers explicitly.
            if (ownerThreadId == Environment.CurrentManagedThreadId)
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception ex) when (ex is ApplicationException or ObjectDisposedException)
        {
            // Not owned by this thread (or already released): closing is enough.
        }

        try
        {
            mutex.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }

        mutex = null;
        ownerThreadId = 0;
    }

    private static EventWaitHandle? OpenNamedEvent(string name, DateTime deadline)
    {
        while (true)
        {
            try
            {
                return EventWaitHandle.OpenExisting(name);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return null;
                }

                Thread.Sleep(25);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }
    }

    private static void ValidateScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("The instance scope must not be empty.", nameof(scope));
        }

        if (scope.Length > 64)
        {
            throw new ArgumentException("The instance scope must be 64 characters or fewer.", nameof(scope));
        }

        foreach (var c in scope)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            {
                throw new ArgumentException(
                    "The instance scope may contain only letters, digits, '.', '-' and '_'.",
                    nameof(scope));
            }
        }
    }

    private static string CurrentUserScope()
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (!string.IsNullOrEmpty(sid))
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sid));
                return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException
            or PlatformNotSupportedException)
        {
            // Fall through to the account-name scope below.
        }

        var fallback = new StringBuilder("user-");
        foreach (var c in Environment.UserName)
        {
            if (fallback.Length == 48)
            {
                break;
            }

            fallback.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        }

        return fallback.Length == 5 ? "user-unknown" : fallback.ToString();
    }
}
