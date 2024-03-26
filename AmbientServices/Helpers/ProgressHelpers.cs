using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// A static class that holds a property used to more conveniently access the ambient <see cref="IAmbientProgress"/>.
    /// </summary>
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
            : this(_AmbientClock.Local, timeout)
        {
        }
        /// <summary>
        /// Constructs an ambient cancellation token source using the ambient clock.
        /// </summary>
        /// <param name="timeoutMilliseconds">The number of milliseconds to wait before timing out.</param>
        public AmbientCancellationTokenSource(int timeoutMilliseconds)
            : this(_AmbientClock.Local, TimeSpan.FromMilliseconds(timeoutMilliseconds))
        {
        }
        /// <summary>
        /// Constructs an ambient cancellation token source using the specified clock.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> to use for the token source.</param>
        /// <param name="timeout">An optional timeout indicating how long before the associated cancellation token should be cancelled.</param>
        public AmbientCancellationTokenSource(IAmbientClock? clock, TimeSpan? timeout = null)
        {
            _clock = clock;
            _tokenSource = new CancellationTokenSource();
            if (timeout != null)
            {
                ScheduleCancellation(timeout.Value);
            }
        }

        private void ScheduleCancellation(TimeSpan timeout)
        {
            _ambientTimer = new AmbientEventTimer(timeout);
            void handler(object? source, System.Timers.ElapsedEventArgs e)
            {
                _ambientTimer.Elapsed -= handler;
                _tokenSource?.Cancel();
                _ambientTimer.Dispose();
            }

            _ambientTimer.Elapsed += handler;   // note that the handler will keep the timer and the token source alive until the event is raised, but the event is only raised once anyway, and there is no need to unsubscribe because the owner of the event is disposed when the event is triggered anyway
            _ambientTimer.Enabled = true;
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
        /// Schedules a cancellation after the sepecified time.
        /// </summary>
        /// <param name="millisecondsDelay">The number of milliseconds to delay before cancelling.</param>
        public void CancelAfter(int millisecondsDelay)
        {
            if (_ambientTimer != null) _ambientTimer.Dispose();
            ScheduleCancellation(TimeSpan.FromMilliseconds(millisecondsDelay));
        }
        /// <summary>
        /// Schedules a cancellation after the sepecified time.
        /// </summary>
        /// <param name="delay">A <see cref="TimeSpan"/> indicating how long to delay before cancelling.</param>
        public void CancelAfter(TimeSpan delay)
        {
            CancelAfter((int)delay.TotalMilliseconds);
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
        /// Disposes of ths instance.
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
}
