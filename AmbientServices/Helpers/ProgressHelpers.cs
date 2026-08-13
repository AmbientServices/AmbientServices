using System;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// A static class that holds a property used to more conveniently access the ambient <see cref="IAmbientProgress"/>.
/// </summary>
/// <remarks>
/// <pitch>The one-liner way to get the current operation's progress tracker without holding a service reference.</pitch>
/// <pledge>Returns the calling execution context's progress from the local (or, for <see cref="GlobalProgress"/>, the global) ambient service, or null when the service is unregistered or suppressed — callers must tolerate null, which is what keeps progress tracking optional.</pledge>
/// <plan>A static facade over <c>Ambient.GetService&lt;IAmbientProgressService&gt;()</c> delegating to <see cref="IAmbientProgressService.Progress"/>.</plan>
/// </remarks>
public static class AmbientProgressService
{
    private static readonly AmbientService<IAmbientProgressService> _Progress = Ambient.GetService<IAmbientProgressService>();
    /// <summary>
    /// Gets the <see cref="IAmbientProgress"/> from the current local (or global) ambient progress service.
    /// </summary>
    public static IAmbientProgress? Progress => _Progress.Local?.Progress;
    /// <summary>
    /// Gets the <see cref="IAmbientProgress"/> from the global ambient progress service.
    /// </summary>
    [ExcludeFromCoverage]   // this can't be fully tested without possibly affecting other tests and their coverage because this is a *global* item, so changing it during a test obviously has non-local effects
    public static IAmbientProgress? GlobalProgress => _Progress.Global?.Progress;
}


/// <summary>
/// A cancellation token source that works with ambient timers in addition to system timers.
/// </summary>
/// <remarks>
/// <pitch>A <see cref="CancellationTokenSource"/> stand-in whose scheduled cancellations follow the ambient clock, so timeout-driven cancellation can be tested by skipping virtual time rather than waiting for it; it also supports cancelling after a set number of cancellation checks, for fault-injection testing of error handling and recovery.  With no ambient clock it behaves like the system source.</pitch>
/// <pledge>
/// Which clock schedules timed cancellations is fixed at construction; under a paused clock, a scheduled cancellation fires synchronously when virtual time is skipped past the deadline.  Delays are accepted and rejected exactly as <see cref="CancellationTokenSource"/> accepts and rejects them — including zero, which cancels at once, and -1, which never cancels — so the interval rules of the timer used to schedule them never show through; only <see cref="ArgumentException.ParamName"/> differs, naming this type's own parameter.  After disposal the source is inert but safe: <see cref="Token"/> returns an already-cancelled token rather than throwing.  <see cref="CancelAfterChecks(int)"/> arms cancellation after the given number of <see cref="IsCancellationRequested"/> polls (replacing an already-cancelled underlying source with a fresh one) and leaves any time-based cancellation in place.
/// </pledge>
/// <plan>Wraps a system <see cref="CancellationTokenSource"/> and schedules timed cancellation with a one-shot <see cref="AmbientEventTimer"/> instead of the system source's built-in timer, which is what routes timeouts through the ambient clock.  Because that timer rejects intervals a cancellation source must accept, delays are validated here against the cancellation source's own rules and the degenerate ones — cancel now, and never cancel — are handled without creating a timer at all.  Check-based cancellation counts polls with <see cref="Interlocked"/>.  A static pre-cancelled token serves the disposed state.</plan>
/// </remarks>
public class AmbientCancellationTokenSource : IDisposable
{
    private static readonly AmbientService<IAmbientClock> _AmbientClock = Ambient.GetService<IAmbientClock>();
    private static readonly CancellationToken _AlreadyCancelled = AlreadyCancelledToken();
    private static CancellationToken AlreadyCancelledToken()
    {
        CancellationTokenSource source = new(); source.Cancel(); return source.Token;
    }

#pragma warning disable IDE0052 // Remove unread private members    I'd like to keep this around for debugging and just in case
    private readonly IAmbientClock? _clock;
#pragma warning restore IDE0052 // Remove unread private members
    private CancellationTokenSource? _tokenSource;      // note that if this is not nullable, you can't tell if the token source has been disposed, which causes all sorts of problems
    private AmbientEventTimer? _ambientTimer;
    private int _cancelAfterChecks;
    private int _checks;

    /// <summary>
    /// Constructs an ambient cancellation token source using a system <see cref="CancellationTokenSource"/>.
    /// </summary>
    /// <param name="tokenSource">A <see cref="CancellationTokenSource"/> from the system.  If null, makes a cancellation token source that must be cancelled manually.</param>
    public AmbientCancellationTokenSource(CancellationTokenSource? tokenSource = null)
    {
        _tokenSource = tokenSource ?? new CancellationTokenSource();
    }
    /// <summary>
    /// Constructs an ambient cancellation token source using the ambient clock.
    /// </summary>
    /// <param name="timeout">A <see cref="TimeSpan"/> indicating how long to wait before timing out.</param>
    public AmbientCancellationTokenSource(TimeSpan timeout)
        : this(_AmbientClock.Override ?? _AmbientClock.Local, ValidatedDelay(timeout, nameof(timeout)))
    {
    }
    /// <summary>
    /// Constructs an ambient cancellation token source using the ambient clock.
    /// </summary>
    /// <param name="timeoutMilliseconds">The number of milliseconds to wait before timing out.</param>
    public AmbientCancellationTokenSource(int timeoutMilliseconds)
        : this(_AmbientClock.Override ?? _AmbientClock.Local, ValidatedDelay(timeoutMilliseconds, nameof(timeoutMilliseconds)))
    {
    }
    /// <summary>
    /// Constructs an ambient cancellation token source using the specified clock.
    /// </summary>
    /// <param name="clock">The <see cref="IAmbientClock"/> to use for the token source.</param>
    /// <param name="timeout">An optional timeout indicating how long before the associated cancellation token should be cancelled.</param>
    public AmbientCancellationTokenSource(IAmbientClock? clock, TimeSpan? timeout = null)
    {
        if (timeout != null) ValidatedDelay(timeout.Value, nameof(timeout));
        _clock = clock;
        _tokenSource = new CancellationTokenSource();
        if (timeout != null)
        {
            ScheduleCancellation(timeout.Value);
        }
    }

    /// <summary>
    /// Validates a cancellation delay the way <see cref="CancellationTokenSource"/> does and returns it as a <see cref="TimeSpan"/>.
    /// </summary>
    /// <remarks>
    /// Note that this deliberately does not defer to the scheduling timer's validation: <see cref="CancellationTokenSource"/> accepts zero (cancel at once) and -1 (never), both of which are invalid timer intervals.
    /// </remarks>
    /// <param name="milliseconds">The number of milliseconds to delay, with -1 meaning never.</param>
    /// <param name="parameterName">The name of the parameter being validated, for the exception.</param>
    private static TimeSpan ValidatedDelay(double milliseconds, string parameterName)
    {
        if (milliseconds < -1 || milliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(parameterName);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
    /// <summary>
    /// Validates a cancellation delay the way <see cref="CancellationTokenSource"/> does and returns it unchanged.
    /// </summary>
    /// <param name="delay">A <see cref="TimeSpan"/> indicating how long to delay, with -1 milliseconds meaning never.</param>
    /// <param name="parameterName">The name of the parameter being validated, for the exception.</param>
    private static TimeSpan ValidatedDelay(TimeSpan delay, string parameterName)
    {
        return ValidatedDelay(delay.TotalMilliseconds, parameterName);
    }

    private void ScheduleCancellation(TimeSpan delay)
    {
        double milliseconds = delay.TotalMilliseconds;
        // never?  then there is nothing to schedule
        if (milliseconds < 0) return;
        // already due?  then cancel right now, because a timer cannot be given a zero interval
        if (milliseconds == 0)
        {
            _tokenSource?.Cancel();
            return;
        }
        AmbientEventTimer timer = new AmbientEventTimer(delay);
        _ambientTimer = timer;
        void handler(object? source, System.Timers.ElapsedEventArgs e)
        {
            // Use the captured timer: Dispose() may null _ambientTimer concurrently while SkipAhead raises Elapsed.
            timer.Elapsed -= handler;
            _tokenSource?.Cancel();
            timer.Dispose();
            if (ReferenceEquals(_ambientTimer, timer))
                _ambientTimer = null;
        }

        timer.Elapsed += handler;   // note that the handler will keep the timer and the token source alive until the event is raised, but the event is only raised once anyway, and there is no need to unsubscribe because the owner of the event is disposed when the event is triggered anyway
        timer.Enabled = true;
    }

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> associated with the source.
    /// </summary>
    public CancellationToken Token => _tokenSource?.Token ?? _AlreadyCancelled;
    /// <summary>
    /// Gets whether or not a cancellation has been requested.
    /// </summary>
    public bool IsCancellationRequested
    {
        get
        {
            if (_cancelAfterChecks != 0 && Interlocked.Increment(ref _checks) > _cancelAfterChecks) _tokenSource?.Cancel(false);
            return _tokenSource?.IsCancellationRequested ?? true;
        }
    }
    /// <summary>
    /// Gets the number of checks that have been made towards cancellation (see <see cref="CancelAfterChecks(int)"/>.
    /// </summary>
    public int Checks => _checks;
    /// <summary>
    /// Marks the associated token as canceled.
    /// </summary>
    public void Cancel() { Cancel(false); }
    /// <summary>
    /// Marks the associated token as canceled.
    /// </summary>
    /// <param name="throwOnFirstException">true if exceptions should immediately propagate, otherwise false.</param>
    public void Cancel(bool throwOnFirstException) { _tokenSource?.Cancel(throwOnFirstException); }
    /// <summary>
    /// Schedules a cancellation after the specified time.
    /// </summary>
    /// <param name="millisecondsDelay">The number of milliseconds to delay before cancelling.</param>
    public void CancelAfter(int millisecondsDelay)
    {
        TimeSpan delay = ValidatedDelay(millisecondsDelay, nameof(millisecondsDelay));
        if (_ambientTimer != null) _ambientTimer.Dispose();
        ScheduleCancellation(delay);
    }
    /// <summary>
    /// Schedules a cancellation after the specified time.
    /// </summary>
    /// <param name="delay">A <see cref="TimeSpan"/> indicating how long to delay before cancelling.</param>
    public void CancelAfter(TimeSpan delay)
    {
        TimeSpan validatedDelay = ValidatedDelay(delay, nameof(delay));
        if (_ambientTimer != null) _ambientTimer.Dispose();
        ScheduleCancellation(validatedDelay);
    }
    /// <summary>
    /// Schedules a cancellation after a certain number of checks to see if the token was canceled.
    /// This is useful mainly for aborting processes part way through in order to test error handling and recovery.
    /// Leaves any time-delayed cancellation in place.  If the underlying token source has been canceled, a new not-yet-canceled one will be created.
    /// </summary>
    /// <param name="numberOfChecks">The number of checks to cancel after.</param>
    public void CancelAfterChecks(int numberOfChecks)
    {
        // already canceled?  create a new underlying cancellation source
        if (_tokenSource?.IsCancellationRequested == true) _tokenSource = new();
        Interlocked.Exchange(ref _checks, 0);
        Interlocked.Exchange(ref _cancelAfterChecks, numberOfChecks);
    }

    #region IDisposable Support
    /// <summary>
    /// Implementation of the standard dispose pattern.
    /// </summary>
    /// <param name="disposing">Whether or not this instance is being disposed, as opposed to finalized.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tokenSource?.Dispose();
            _tokenSource = null;
            _ambientTimer?.Dispose();
            _ambientTimer = null;
        }
    }
    /// <summary>
    /// Disposes of this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
[AttributeUsage(AttributeTargets.All)]
internal sealed class ExcludeFromCoverageAttribute : Attribute
{
}
