using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// A cancellation token source that works with ambient timers in addition to system timers.
    /// </summary>
    public class AmbientCancellationTokenSource : IDisposable
    {
        private static readonly ServiceReference<IAmbientClockProvider> _ClockProviderAccessor = Service.GetReference<IAmbientClockProvider>();
        private static readonly CancellationToken _AlreadyCancelled = AlreadyCancelledToken();
        private static CancellationToken AlreadyCancelledToken()
        {
            CancellationTokenSource source = new CancellationTokenSource(); source.Cancel(); return source.Token;
        }

        private IAmbientClockProvider _clockProvider;
        private CancellationTokenSource _tokenSource;
        private AmbientEventTimer _ambientTimer;

        /// <summary>
        /// Constructs an ambient cancellation token source using a system <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="tokenSource">A <see cref="CancellationTokenSource"/> from the system.  If null, makes a cancellation token source that must be cancelled manually.</param>
        public AmbientCancellationTokenSource(CancellationTokenSource tokenSource = null)
        {
            _tokenSource = tokenSource ?? new CancellationTokenSource();
        }
        /// <summary>
        /// Constructs an ambient cancellation token source using the ambient clock.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating how long to wait before timing out.</param>
        public AmbientCancellationTokenSource(TimeSpan timeout)
            : this(_ClockProviderAccessor.Provider, timeout)
        {
        }
        /// <summary>
        /// Constructs an ambient cancellation token source using the specified clock.
        /// </summary>
        /// <param name="clockProvider">The <see cref="IAmbientClockProvider"/> to use for the token source.</param>
        /// <param name="timeout">An optional timeout indicating how long before the associated cancellation token should be cancelled.</param>
        public AmbientCancellationTokenSource(IAmbientClockProvider clockProvider, TimeSpan? timeout = null)
        {
            _clockProvider = clockProvider;
            if (clockProvider != null)
            {
                _tokenSource = new CancellationTokenSource();
                if (timeout != null)
                {
                    _ambientTimer = new AmbientEventTimer(timeout.Value);
                    System.Timers.ElapsedEventHandler handler = null;
                    handler = (source, e)
                        =>
                    {
                        _ambientTimer.Elapsed -= handler;
                        _tokenSource.Cancel();
                        _ambientTimer.Dispose();
                    };
                    _ambientTimer.Elapsed += handler;   // note that the handler will keep the timer and the token source alive until the event is raised, but the event is only raised once anyway, and there is no need to unsubscribe because the owner of the event is disposed when the event is triggered anyway
                    _ambientTimer.Enabled = true;
                }
            }
            else
            {
                _tokenSource = (timeout == null) ? new CancellationTokenSource() : new CancellationTokenSource(timeout.Value);
            }
        }
        /// <summary>
        /// Gets the <see cref="CancellationToken"/> associated with the source.
        /// </summary>
        public CancellationToken Token { get { return _tokenSource?.Token ?? _AlreadyCancelled; } }
        /// <summary>
        /// Gets whether or not a cancellation has been requested.
        /// </summary>
        public bool IsCancellationRequested { get { return _tokenSource?.IsCancellationRequested ?? true; } }

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
}
