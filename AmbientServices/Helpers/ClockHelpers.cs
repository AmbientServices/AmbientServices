using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// An static class that utilizes the <see cref="IAmbientClockProvider"/> if one is registered, or the system clock if not.
    /// </summary>
    public static class AmbientClock
    {
        private static readonly ServiceAccessor<IAmbientClockProvider> _ClockAccessor = Service.GetAccessor<IAmbientClockProvider>();
        /// <summary>
        /// Gets the number of virtual ticks elapsed.  Ticks must be measured in units of <see cref="Stopwatch.Frequency"/>.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static long Ticks { get { return _ClockAccessor.LocalProvider?.Ticks ?? Stopwatch.GetTimestamp(); } }
        /// <summary>
        /// Gets a <see cref="TimeSpan"/> indicating the amount of virtual time that has elapsed.  Much more convenient than <see cref="Ticks"/>, but based entirely on it's value.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static TimeSpan Elapsed { get { return TimeSpan.FromTicks((_ClockAccessor.LocalProvider?.Ticks ?? Stopwatch.GetTimestamp()) * TimeSpan.TicksPerSecond / Stopwatch.Frequency); } }
        /// <summary>
        /// Gets the current virtual UTC <see cref="DateTime"/>.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static DateTime UtcNow { get { return _ClockAccessor.LocalProvider?.UtcDateTime ?? DateTime.UtcNow; } }
        /// <summary>
        /// Creates an <see cref="AmbientCancellationTokenSource"/> that cancels after the specified timeout.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating how long to wait before timing out.</param>
        /// <returns>An <see cref="AmbientCancellationTokenSource"/> that will cancel after the specified timeout (if any).</returns>
        public static AmbientCancellationTokenSource CreateCancellationTokenSource(TimeSpan timeout)
        {
            return new AmbientCancellationTokenSource(timeout);
        }
        /// <summary>
        /// Creates an <see cref="AmbientCancellationTokenSource"/> that cancels after the specified timeout.
        /// </summary>
        /// <param name="cancellationTokenSource">An optional <see cref="CancellationTokenSource"/> from the framework to use.  If null (the default), creates a cancellation token source that must be manually cancelled.</param>
        /// <returns>An <see cref="AmbientCancellationTokenSource"/> for the specified <see cref="CancellationTokenSource"/>.</returns>
        public static AmbientCancellationTokenSource CreateCancellationTokenSource(CancellationTokenSource cancellationTokenSource = null)
        {
            return new AmbientCancellationTokenSource(cancellationTokenSource);
        }
        /// <summary>
        /// Pauses the time within the current call context so that no time passes until the returned <see cref="IDisposable"/> is disposed or <see cref="SkipAhead"/> is called.
        /// </summary>
        /// The returned instance must be disposed in the same call context.
        /// <remarks>
        /// </remarks>
        public static IDisposable Pause()
        {
            return new ClockPauser();
        }
        /// <summary>
        /// Skips a paused clock ahead the specified amount.
        /// If the clock is not paused in the current call context, nothing is done.
        /// </summary>
        /// <param name="skipTime">The amount of time to skip ahead.</param>
        public static void SkipAhead(TimeSpan skipTime)
        {
            PausedAmbientClockProvider controllable = _ClockAccessor.LocalProviderOverride as PausedAmbientClockProvider;
            if (controllable != null) controllable.SkipAhead(skipTime.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        }
        sealed class ClockPauser : IDisposable
        {
            private IAmbientClockProvider _clockToRestore;

            internal ClockPauser()
            {
                _clockToRestore = _ClockAccessor.LocalProviderOverride;
                _ClockAccessor.LocalProviderOverride = new PausedAmbientClockProvider();
            }

            #region IDisposable Support
            private bool _disposed = false; // To detect redundant calls

            private void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _ClockAccessor.LocalProviderOverride = _clockToRestore;
                    }
                    _disposed = true;
                }
            }
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }
            #endregion
        }
        /// <summary>
        /// An ambient clock which only moves time forward when explicitly told to do so.
        /// </summary>
        internal class PausedAmbientClockProvider : IAmbientClockProvider
        {
            private readonly DateTime _baseDateTime;
            private long _stopwatchTicks;

            /// <summary>
            /// Constructs a test ambient clock that starts at the current date-time and only moves time forward when explicitly told to do so.
            /// </summary>
            public PausedAmbientClockProvider()
            {
                _baseDateTime = DateTime.UtcNow;
                _stopwatchTicks = 0;
            }

            /// <summary>
            /// Gets the number of ticks elapsed.  (In units of <see cref="Stopwatch.Frequency"/>).
            /// </summary>
            /// <remarks>
            /// This property is thread-safe.
            /// </remarks>
            public long Ticks { get { return _stopwatchTicks; } }

            /// <summary>
            /// Gets the current UTC <see cref="DateTime"/>.
            /// </summary>
            /// <remarks>
            /// This property is thread-safe.
            /// </remarks>
            public DateTime UtcDateTime { get { return UtcDateTimeFromStopwatchTicks(_baseDateTime, _stopwatchTicks); } }

            private static DateTime UtcDateTimeFromStopwatchTicks(DateTime baseUtcDateTime, long stopwatchTicks)
            {
                return baseUtcDateTime.AddTicks(stopwatchTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            }

            /// <summary>
            /// An event indicating that the ambient clock's time has changed.
            /// </summary>
            public event EventHandler<AmbientClockProviderTimeChangedEventArgs> OnTimeChanged;

            /// <summary>
            /// Moves the clock forward by the specified number of ticks.  Ticks are the same units as <see cref="Stopwatch"/> ticks, with <see cref="Stopwatch.Frequency"/> ticks per second.
            /// </summary>
            /// <param name="ticks">The number of ticks to move forward.</param>
            /// <remarks>This function is not thread-safe and must only be called by one thread at a time.  It must not be called while <see cref="IAmbientClockProvider.OnTimeChanged"/> is being raised.</remarks>
            public void SkipAhead(long ticks)
            {
                long newTicks = System.Threading.Interlocked.Add(ref _stopwatchTicks, ticks);
                long oldTicks = newTicks - ticks;
                // notify any subscribers
                OnTimeChanged?.Invoke(this, new AmbientClockProviderTimeChangedEventArgs { Clock = this, OldTicks = oldTicks, NewTicks = newTicks, OldUtcDateTime = UtcDateTimeFromStopwatchTicks(_baseDateTime, oldTicks), NewUtcDateTime = UtcDateTimeFromStopwatchTicks(_baseDateTime, newTicks) });
            }
        }
    }
    /// <summary>
    /// A helper class similar to <see cref="Stopwatch"/> that uses an ambient clock if one is available, or the system clock if no ambient clock is available.
    /// </summary>
    /// <remarks>
    /// AmbientStopwatch measures elapsed time.  It has two states, running and paused.  It can be constructed in either state, but uses the running state by default.
    /// While in the running state, <see cref="TicksElapsed"/> will return successively increasing values (or equal values if it is called faster than the resolution of the underlying clock).
    /// While in the paused state, <see cref="TicksElapsed"/> will return the same value, indicating the number of ticks that were previously accumulated after construction or the last call to <see cref="Reset"/> and while running.
    /// AmbientStopwatch is not thread-safe.  Threadsafe versions would be possible to implement but are much more complicated due to the race caused by the state, the start time, and the previously-accumulated ticks being stored separately.
    /// AmbientStopwatch does not support changing the clock provider after construction.
    /// </remarks>
    public sealed class AmbientStopwatch
    {
        private static readonly ServiceAccessor<IAmbientClockProvider> _ClockAccesor = Service.GetAccessor<IAmbientClockProvider>();

        private readonly IAmbientClockProvider _clock;
        private long _accumulatedTicks;
        private long _resumeTicks;
        private bool _running;

        /// <summary>
        /// Constructs an AmbientStopwatch using the local ambient clock if there is one.
        /// </summary>
        /// <param name="run">Whether or not to start the stopwatch running.</param>
        public AmbientStopwatch(bool run = true)
            : this(_ClockAccesor.LocalProvider, run)
        {
        }
        /// <summary>
        /// Constructs an AmbientStopwatch using a specfied <see cref="IAmbientClockProvider"/>.  This overload is mainly for testing functionality without depending on the ambient environment.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClockProvider"/> to use, or null to use the system clock.</param>
        /// <param name="run">Whether or not the stopwatch should start in a running state (as opposed to a paused state).</param>
        public AmbientStopwatch(IAmbientClockProvider clock, bool run = true)
        {
            _clock = clock;
            _resumeTicks = Ticks;
            _running = run;
        }
        /// <summary>
        /// Gets the ticks as determined by the clock, or the system clock if there is no clock.
        /// </summary>
        private long Ticks => _clock?.Ticks ?? Stopwatch.GetTimestamp();
        /// <summary>
        /// Gets the virtual number of ticks elapsed while the stopwatch was (or is) running.
        /// </summary>
        /// <remarks>
        /// The number of accumulated ticks can remain the same or go up on subsequent calls, but should never go down.  
        /// Bugs in .NET implementations prior to 4.0 caused the system clock to sometimes go backwards.
        /// Even in .NET 4.0+, the clock can incorrectly jump forward and then freeze, but this should only happen on systems with buggy hardware, system drivers, or buggy virtualization implementations.
        /// Arithmetic wraparound is technically possible, though in practice, at least on Windows, this should not happen unless the stopwatch has run for at least 100 years (50 years before it goes negative).
        /// In most cases, time spans measured in years should use <see cref="DateTime"/> instead of stopwatches.
        /// </remarks>
        public long TicksElapsed => _running ? Ticks - _resumeTicks + _accumulatedTicks : _accumulatedTicks;
        /// <summary>
        /// Gets a <see cref="TimeSpan"/> representing the number of ticks elapsed.  Based entirely on <see cref="TicksElapsed"/> and the (system-constant) resolution of the clock.
        /// </summary>
        public TimeSpan Elapsed => TimeSpan.FromTicks(TicksElapsed * TimeSpan.TicksPerSecond / Stopwatch.Frequency);

        /// <summary>
        /// Pauses the stopwatch so that it temporarily stops accumulating time.  While paused, <see cref="TicksElapsed"/> will return the same value.
        /// </summary>
        public void Pause()
        {
            // pause--was it *not* paused before?
            if (_running)
            {
                long ticksAccumulated = Ticks - _resumeTicks;
                _running = false;
                _accumulatedTicks = ticksAccumulated;
            }
        }
        /// <summary>
        /// Resumes the stopwatch so that it starts running again.  When resumed, <see cref="TicksElapsed"/> will return increasing values (or the same value if called faster than the resolution of the underlying clock).
        /// </summary>
        public void Resume()
        {
            // resume--was it *not* resumed before?
            if (!_running)
            {
                long resumeTicks = Ticks;
                _running = true;
                _resumeTicks = resumeTicks;
            }
        }
        /// <summary>
        /// Resets the stopwatch, causing <see cref="TicksElapsed"/> to reset to zero.
        /// If the stopwatch was running, it keeps running and <see cref="TicksElapsed"/> will return the number of ticks since it was reset.  
        /// If the stopwatch was stopped, <see cref="TicksElapsed"/> will return zero until <see cref="Resume"/> is called.
        /// </summary>
        public void Reset()
        {
            _accumulatedTicks = 0;
            // if we're running, we need to pretend that we just started
            if (_running) _resumeTicks = Ticks;
        }
    }
    /// <summary>
    /// An event class that contains information about the timer whose <see cref="AmbientTimer.Elapsed"/> event is raised.
    /// </summary>
    public sealed class AmbientTimerElapsedEventHandler
    {
        /// <summary>
        /// The <see cref="AmbientTimer"/> whose <see cref="AmbientTimer.Elapsed"/> event is being raised.
        /// </summary>
        public AmbientTimer Timer { get; set; }
    }
    /// <summary>
    /// An event class that contains information about the timer whose <see cref="AmbientTimer.Disposed"/> event is raised.
    /// </summary>
    public sealed class AmbientTimerDisposedEventHandler
    {
        /// The <see cref="AmbientTimer"/> whose <see cref="AmbientTimer.Disposed"/> event is being raised.
        public AmbientTimer Timer { get; set; }
    }
    /// <summary>
    /// A helper class that raises an event after a specified time, periodically if desired.
    /// </summary>
    /// <remarks>
    /// AmbientTimer is thread-safe.
    /// </remarks>
    public sealed class AmbientTimer : IDisposable
    {
        private static readonly ServiceAccessor<IAmbientClockProvider> _ClockAccessor = Service.GetAccessor<IAmbientClockProvider>();

        private IAmbientClockProvider _clock;           // exactly one of _clock and _timer should be null
        private long _periodStopwatchTicks;
        private long _nextRaiseStopwatchTicks;
        private int _autoReset;
        private int _enabled;
        private System.Timers.Timer _timer;
        private LazyUnsubscribeWeakEventListenerProxy<AmbientTimer, object, AmbientClockProviderTimeChangedEventArgs> _weakTimeChanged;
        private LazyUnsubscribeWeakEventListenerProxy<AmbientTimer, object, System.Timers.ElapsedEventArgs> _weakElapsed;

        /// <summary>
        /// Constructs an AmbientTimer using the ambient clock and no period.
        /// </summary>
        public AmbientTimer()
            : this(_ClockAccessor.LocalProvider, TimeSpan.Zero)
        {
        }
        /// <summary>
        /// Constructs an AmbientTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="period">A <see cref="TimeSpan"/> indicating how often the <see cref="Elapsed"/> event should be raised.</param>
        public AmbientTimer(TimeSpan period)
            : this(_ClockAccessor.LocalProvider, period)
        {
        }
        /// <summary>
        /// Constructs an AmbientTimer that will use the specified clock to determine when to raise the <see cref="Elapsed"/> event.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClockProvider"/> to use to determine when to raise the <see cref="Elapsed"/> event.</param>
        public AmbientTimer(IAmbientClockProvider clock)
            : this(clock, TimeSpan.Zero)
        {
        }

        /// <summary>
        /// Constructs an AmbientTimer that will use the specified period and clock to determine when to raise the <see cref="Elapsed"/> event.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClockProvider"/> to use to determine when to raise the <see cref="Elapsed"/> event.</param>
        /// <param name="period">A <see cref="TimeSpan"/> indicating how often the <see cref="Elapsed"/> event should be raised.  If zero, the timer will not be enabled.  If non-zero, the timer will start immediately.</param>
        public AmbientTimer(IAmbientClockProvider clock, TimeSpan period)
        {
            _clock = clock;
            // is there a clock?
            if (clock != null)
            {
                long nowStopwatchTicks = clock.Ticks;
                IAmbientClockProvider tempClock = clock;
                _weakTimeChanged = new LazyUnsubscribeWeakEventListenerProxy<AmbientTimer, object, AmbientClockProviderTimeChangedEventArgs>(
                    this, OnTimeChanged, wtc => tempClock.OnTimeChanged -= wtc.WeakEventHandler);
                clock.OnTimeChanged += _weakTimeChanged.WeakEventHandler;
                _periodStopwatchTicks = period.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
                _nextRaiseStopwatchTicks = nowStopwatchTicks + _periodStopwatchTicks;
                _enabled = (period.Ticks > 0) ? 1 : 0;
            }
            else // no clock, so use a system timer
            {
                double milliseconds = (period.Ticks == 0) ? (double)Int32.MaxValue : period.TotalMilliseconds;
                _timer = new System.Timers.Timer(milliseconds);
                _timer.AutoReset = false;
                // start with the timer disabled?
                _timer.Enabled = (period.Ticks > 0);
                System.Timers.Timer tempTimer = _timer;
                _weakElapsed = new LazyUnsubscribeWeakEventListenerProxy<AmbientTimer, object, System.Timers.ElapsedEventArgs>(
                    this, OnTimerElapsed, we => tempTimer.Elapsed -= we.WeakEventHandler);
                _timer.Elapsed += _weakElapsed.WeakEventHandler;
            }
        }

        private static void OnTimerElapsed(AmbientTimer timer, object sender, System.Timers.ElapsedEventArgs e)
        {
            // there should be a timer and NOT a clock
            System.Diagnostics.Debug.Assert(timer._timer != null && timer._clock == null);
            timer.Elapsed?.Invoke(sender, new AmbientTimerElapsedEventHandler { Timer = timer });
        }

        // when there is an ambient clock, events are raised ONLY when the clock changes, and we get notified here every time that happens
        private static void OnTimeChanged(AmbientTimer timer, object sender, AmbientClockProviderTimeChangedEventArgs e)
        {
            // there should be a clock and NOT a timer if we get here
            System.Diagnostics.Debug.Assert(timer._clock != null && timer._timer == null);
            // is the timer enabled?
            if (timer._enabled != 0)
            {
                // was it not time to raise before, but it is now?
                long nextRaiseStopwatchTicks = timer._nextRaiseStopwatchTicks;
                long periodStopwatchTicks = timer._periodStopwatchTicks;
                bool autoReset = (timer._autoReset != 0);
                EventHandler<AmbientTimerElapsedEventHandler> elapsed = timer.Elapsed;
                AmbientTimerElapsedEventHandler args = new AmbientTimerElapsedEventHandler { Timer = timer };
                while (elapsed != null && nextRaiseStopwatchTicks > e.OldTicks && nextRaiseStopwatchTicks <= e.NewTicks && timer._enabled != 0)
                {
                    elapsed.Invoke(sender, args);
                    // should we reset for another period?
                    if (autoReset && periodStopwatchTicks != 0)
                    {
                        nextRaiseStopwatchTicks = System.Threading.Interlocked.Add(ref timer._nextRaiseStopwatchTicks, periodStopwatchTicks);
                        // we may loop around again in case the event should have been raised more than once
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether or not the event resets and fires again after being raised.
        /// </summary>
        public bool AutoReset
        {
            get { return _timer?.AutoReset ?? (_autoReset != 0); }
            set
            {
                if (_timer != null)
                {
                    _timer.AutoReset = value;
                }
                else
                {
                    System.Threading.Interlocked.Exchange(ref _autoReset, value ? 1 : 0);
                }
            }
        }
        /// <summary>
        /// Gets or sets whether or not the timer is enabled (ie. whether or not it will raise the <see cref="Elapsed"/> event.
        /// </summary>
        public bool Enabled
        {
            get { return _timer?.Enabled ?? (_enabled != 0); }
            set
            {
                if (_timer != null)
                {
                    _timer.Enabled = value;
                }
                else
                {
                    int oldValue = System.Threading.Interlocked.Exchange(ref _enabled, value ? 1 : 0);
                    // are we enabling and it was NOT enabled before?  set up the next raise
                    if (value && oldValue == 0) SetupNextRaise();
                }
            }
        }
        /// <summary>
        /// Gets or sets the period of the timer.
        /// </summary>
        public TimeSpan Period
        {
            get { return (_timer != null) ? TimeSpan.FromMilliseconds(_timer.Interval) : new TimeSpan(_periodStopwatchTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency); }
            set
            {
                if (_timer != null)
                {
                    _timer.Interval = value.TotalMilliseconds;
                }
                else
                {
                    System.Threading.Interlocked.Exchange(ref _periodStopwatchTicks, value.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                    // are we enabled?
                    if (_enabled != 0) SetupNextRaise();
                }
            }
        }

        private void SetupNextRaise()
        {
            System.Diagnostics.Debug.Assert(_timer == null && _clock != null);
            long now = _clock.Ticks;
            System.Threading.Interlocked.Exchange(ref _nextRaiseStopwatchTicks, now + _periodStopwatchTicks);
        }

        /// <summary>
        /// Starts the timer running so that the <see cref="Elapsed"/> event can be raised.
        /// </summary>
        public void Start()
        {
            if (_timer != null)
            {
                _timer.Start();
            }
            else
            {
                System.Threading.Interlocked.Exchange(ref _enabled, 1);
                SetupNextRaise();
            }
        }

        /// <summary>
        /// Stops the timer running so that the <see cref="Elapsed"/> will not be raised until <see cref="Start"/> is called.
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
            }
            else
            {
                System.Threading.Interlocked.Exchange(ref _enabled, 0);
            }
        }

        /// <summary>
        /// An event that is raised each time the specified period elapses.
        /// </summary>
        public event EventHandler<AmbientTimerElapsedEventHandler> Elapsed;
        /// <summary>
        /// An event that is raised when the timer is disposed.
        /// </summary>
        public event EventHandler<AmbientTimerDisposedEventHandler> Disposed;

        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Does the work of disposing the AmbientTimer.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Disposed?.Invoke(this, new AmbientTimerDisposedEventHandler { Timer = this });
                    _timer?.Dispose();
                }
                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes of the instance, notifying any registered subscribers.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
