using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A static class that utilizes the ambient <see cref="IAmbientClock"/> service if there is one, or the system clock if not.
    /// </summary>
    public static class AmbientClock
    {
        private static readonly AmbientService<IAmbientClock> _Clock = Ambient.GetService<IAmbientClock>();
        /// <summary>
        /// Gets whether or not the ambient clock is just the system clock.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static bool IsSystemClock { get { return _Clock.Local == null; } }
        /// <summary>
        /// Gets the number of virtual ticks elapsed.  Ticks must be measured in units of <see cref="Stopwatch.Frequency"/>.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static long Ticks { get { return _Clock.Local?.Ticks ?? Stopwatch.GetTimestamp(); } }
        /// <summary>
        /// Gets a <see cref="TimeSpan"/> indicating the amount of virtual time that has elapsed.  Often more convenient than <see cref="Ticks"/>, but based entirely on its value.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static TimeSpan Elapsed { get { return TimeSpan.FromTicks((_Clock.Local?.Ticks ?? Stopwatch.GetTimestamp()) * TimeSpan.TicksPerSecond / Stopwatch.Frequency); } }
        /// <summary>
        /// Gets the current virtual UTC <see cref="DateTime"/>.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static DateTime UtcNow { get { return _Clock.Local?.UtcDateTime ?? DateTime.UtcNow; } }
        /// <summary>
        /// Gets the current virtual local <see cref="DateTime"/>.
        /// </summary>
        /// <remarks>
        /// This property is thread-safe.
        /// </remarks>
        public static DateTime Now { get { return _Clock.Local?.UtcDateTime.ToLocalTime() ?? DateTime.Now; } }
        /// <summary>
        /// Creates an <see cref="AmbientCancellationTokenSource"/> that cancels after the specified timeout.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating how long to wait before timing out.</param>
        /// <returns>An <see cref="AmbientCancellationTokenSource"/> that will cancel after the specified timeout (if any).</returns>
        [Obsolete("Use new AmbientCancellationTokenSource directly")]
        public static AmbientCancellationTokenSource CreateCancellationTokenSource(TimeSpan timeout)
        {
            return new AmbientCancellationTokenSource(timeout);
        }
        /// <summary>
        /// Creates an <see cref="AmbientCancellationTokenSource"/> that cancels after the specified timeout.
        /// </summary>
        /// <param name="cancellationTokenSource">An optional <see cref="CancellationTokenSource"/> from the framework to use.  If null (the default), creates a cancellation token source that must be manually cancelled.</param>
        /// <returns>An <see cref="AmbientCancellationTokenSource"/> for the specified <see cref="CancellationTokenSource"/>.</returns>
        [Obsolete("Use new AmbientCancellationTokenSource directly")]
        public static AmbientCancellationTokenSource CreateCancellationTokenSource(CancellationTokenSource cancellationTokenSource = null)
        {
            return new AmbientCancellationTokenSource(cancellationTokenSource);
        }
        /// <summary>
        /// Pauses the time within the current call context so that no time passes until the returned <see cref="IDisposable"/> is disposed or <see cref="SkipAhead"/> is called.
        /// </summary>
        /// <remarks>
        /// The returned instance must be disposed in the same call context.  If disposed in a child call context, the first disposal will unpause the clock for all child call contexts.
        /// </remarks>
        public static IDisposable Pause()
        {
            return new ScopedClockPauser();
        }
        /// <summary>
        /// Skips a paused clock ahead the specified amount of time.
        /// If the clock is not paused in the current call context, nothing is done.
        /// </summary>
        /// <remarks>
        /// Note that negative times are allowed, but should only be used to test weird clock issues.
        /// </remarks>
        /// <param name="skipTime">The amount of time to skip ahead.</param>
        public static void SkipAhead(TimeSpan skipTime)
        {
            PausedAmbientClock controllable = _Clock.Override as PausedAmbientClock;
            if (controllable != null) controllable.SkipAhead(skipTime.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        }
        /// <summary>
        /// The ambient clock equivalent of <see cref="Thread.Sleep(int)"/>.
        /// If the clock is paused, skips the virtual clock forward by the specified number of milliseconds, otherwise calls <see cref="Thread.Sleep(int)"/>.
        /// </summary>
        /// <remarks>
        /// Note that negative times are allowed, but should only be used to test weird clock issues.
        /// </remarks>
        /// <param name="millisecondsToSleep">The number of milliseconds to sleep.</param>
        public static void ThreadSleep(int millisecondsToSleep)
        {
            PausedAmbientClock controllable = _Clock.Override as PausedAmbientClock;
            if (controllable != null)
            {
                controllable.SkipAhead(millisecondsToSleep * Stopwatch.Frequency / 1000);
            }
            else
            {
                Thread.Sleep(millisecondsToSleep);
            }
        }
        /// <summary>
        /// The ambient clock equivalent of <see cref="Thread.Sleep(TimeSpan)"/>.
        /// Skips a paused clock ahead the specified amount of time.
        /// If the clock is paused, skips the virtual clock forward by the specified amount, otherwise calls <see cref="Thread.Sleep(TimeSpan)"/>.
        /// </summary>
        /// <remarks>
        /// Note that negative times are allowed, but should only be used to test weird clock issues.
        /// </remarks>
        /// <param name="skipTime">The amount of time to skip ahead.</param>
        public static void ThreadSleep(TimeSpan skipTime)
        {
            ThreadSleep((int)skipTime.TotalMilliseconds);
        }
        /// <summary>
        /// Asynchronously delays for a specified amount of time.
        /// </summary>
        /// <param name="millisecondsToDelay">The number of milliseconds to delay.</param>
        private static Task Delay(long millisecondsToDelay)
        {
            PausedAmbientClock controllable = _Clock.Override as PausedAmbientClock;
            if (controllable != null)
            {
                controllable.SkipAhead(millisecondsToDelay * Stopwatch.Frequency / 1000);
                return Task.Delay(0);
            }
            else
            {
                return Task.Delay(TimeSpan.FromMilliseconds(millisecondsToDelay));
            }
        }
        /// <summary>
        /// Asynchronously delays for a specified amount of time.
        /// </summary>
        /// <param name="millisecondsToDelay">The number of milliseconds to delay.</param>
        public static Task TaskDelay(int millisecondsToDelay)
        {
            return Delay((long)millisecondsToDelay);
        }
        /// <summary>
        /// Asynchronously delays for a specified amount of time.
        /// </summary>
        /// <param name="delayTime">The amount of time to skip ahead.</param>
        public static Task TaskDelay(TimeSpan delayTime)
        {
            return Delay((long)delayTime.TotalMilliseconds);
        }
        private static Task Delay(long millisecondsToDelay, CancellationToken cancel)
        {
            PausedAmbientClock controllable = _Clock.Override as PausedAmbientClock;
            if (controllable != null)
            {
                controllable.SkipAhead(millisecondsToDelay * Stopwatch.Frequency / 1000);
                return Task.Delay(0, cancel);
            }
            else
            {
                return Task.Delay(TimeSpan.FromMilliseconds(millisecondsToDelay), cancel);
            }
        }
        /// <summary>
        /// Asynchronously delays for a specified amount of time.
        /// </summary>
        /// <param name="millisecondsToDelay">The number of milliseconds to delay</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> that may be used to cancel the delay.</param>
        public static Task TaskDelay(int millisecondsToDelay, CancellationToken cancel)
        {
            return Delay((long)millisecondsToDelay, cancel);
        }
        /// <summary>
        /// Asynchronously delays for a specified amount of time.
        /// </summary>
        /// <param name="delayTime">The amount of time to skip ahead.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> that may be used to cancel the delay.</param>
        public static Task TaskDelay(TimeSpan delayTime, CancellationToken cancel)
        {
            return Delay((long)delayTime.TotalMilliseconds, cancel);
        }
        sealed class ScopedClockPauser : IDisposable
        {
            private IAmbientClock _clockToRestore;

            internal ScopedClockPauser()
            {
                _clockToRestore = _Clock.Override;
                _Clock.Override = new PausedAmbientClock();
            }

            #region IDisposable Support
            private bool _disposed; // to detect redundant calls

            private void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _Clock.Override = _clockToRestore;
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
        internal class PausedAmbientClock : IAmbientClock
        {
            private readonly ConcurrentHashSet<IAmbientClockTimeChangedNotificationSink> _notificationSinks = new ConcurrentHashSet<IAmbientClockTimeChangedNotificationSink>();
            private readonly long _baseStopwatchTicks;
            private long _elapsedStopwatchTicks;

            /// <summary>
            /// Constructs an ambient clock that is paused at the current date-time and only moves time forward when explicitly told to do so.
            /// </summary>
            public PausedAmbientClock()
            {
                _baseStopwatchTicks = Stopwatch.GetTimestamp();
            }
            /// <summary>
            /// Gets the number of ticks elapsed.  (In units of <see cref="Stopwatch.Frequency"/>).
            /// </summary>
            /// <remarks>
            /// This property is thread-safe.
            /// </remarks>
            public long Ticks { get { return _baseStopwatchTicks + _elapsedStopwatchTicks; } }
            /// <summary>
            /// Gets the current UTC <see cref="DateTime"/>.
            /// </summary>
            /// <remarks>
            /// This property is thread-safe.
            /// </remarks>
            public DateTime UtcDateTime { get { return UtcDateTimeFromStopwatchTicks(_baseStopwatchTicks + _elapsedStopwatchTicks); } }

            private static DateTime UtcDateTimeFromStopwatchTicks(long stopwatchTicks)
            {
                return new DateTime(TimeSpanExtensions.StopwatchTimestampToDateTime(stopwatchTicks), DateTimeKind.Utc);
            }

            /// <summary>
            /// Registers a time changed notification sink with this ambient clock.
            /// </summary>
            /// <param name="sink">An <see cref="IAmbientClockTimeChangedNotificationSink"/> that will receive notifications when the time is changed.</param>
            /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
            public bool RegisterTimeChangedNotificationSink(IAmbientClockTimeChangedNotificationSink sink)
            {
                return _notificationSinks.Add(sink);
            }
            /// <summary>
            /// Deregisters a time changed notification sink with this ambient clock.
            /// </summary>
            /// <param name="sink">An <see cref="IAmbientClockTimeChangedNotificationSink"/> that will receive notifications when the time is changed.</param>
            /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
            public bool DeregisterTimeChangedNotificationSink(IAmbientClockTimeChangedNotificationSink sink)
            {
                return _notificationSinks.Remove(sink);
            }

            /// <summary>
            /// Moves the clock forward by the specified number of ticks.  Ticks are the same units as <see cref="Stopwatch"/> ticks, with <see cref="Stopwatch.Frequency"/> ticks per second.
            /// </summary>
            /// <remarks>
            /// Note that negative times are allowed, but should only be used to test weird clock issues.
            /// </remarks>
            /// <param name="ticks">The number of ticks to move forward.</param>
            /// <remarks>This function is not thread-safe and must only be called by one thread at a time.  It must not be called directly or indirectly from a <see cref="IAmbientClockTimeChangedNotificationSink.TimeChanged"/> implementation.</remarks>
            public void SkipAhead(long ticks)
            {
                long newTicks = _baseStopwatchTicks + System.Threading.Interlocked.Add(ref _elapsedStopwatchTicks, ticks);
                long oldTicks = newTicks - ticks;
                // notify any subscribers
                foreach (IAmbientClockTimeChangedNotificationSink notificationSink in _notificationSinks)
                {
                    notificationSink.TimeChanged(this, oldTicks, newTicks, UtcDateTimeFromStopwatchTicks(oldTicks), UtcDateTimeFromStopwatchTicks(newTicks));
                }
            }
            /// <summary>
            /// Moves the clock forward by the specified amount of time.
            /// </summary>
            /// <remarks>
            /// Note that negative times are allowed, but should only be used to test weird clock issues.
            /// </remarks>
            /// <param name="time">A <see cref="TimeSpan"/> indicating how much to move forward.</param>
            /// <remarks>This function is not thread-safe and must only be called by one thread at a time.  It must not be called directly or indirectly from a <see cref="IAmbientClockTimeChangedNotificationSink.TimeChanged"/> implementation.</remarks>
            public void SkipAhead(TimeSpan time)
            {
                SkipAhead(TimeSpanExtensions.TimeSpanTicksToStopwatchTicks(time.Ticks));
            }
        }
    }
    /// <summary>
    /// A helper class that implements the same methods and properties as <see cref="Stopwatch"/> but uses an ambient clock if one is available.
    /// When an ambient clock is not available, should behave identically to <see cref="Stopwatch"/>.
    /// </summary>
    /// <remarks>
    /// AmbientStopwatch measures elapsed time.  It has two states, running and paused.  It can be constructed in either state, but uses the running state by default.
    /// While running, <see cref="ElapsedTicks"/> will return successively increasing values (or equal values if it is called faster than the resolution of the underlying clock).
    /// While not running, <see cref="ElapsedTicks"/> will return the same value, indicating the number of ticks that were previously accumulated after construction or the last call to <see cref="Reset"/> and while running.
    /// AmbientStopwatch is not thread-safe, but neither is <see cref="Stopwatch"/>.
    /// Threadsafe versions would be possible to implement but are much more complicated due to the race caused by the state, the start time, and the previously-accumulated ticks being stored separately.
    /// AmbientStopwatch does not support changing the clock implementation after construction.
    /// </remarks>
    public sealed class AmbientStopwatch
    {
        private static readonly AmbientService<IAmbientClock> _Clock = Ambient.GetService<IAmbientClock>();

        private readonly IAmbientClock _clock;
        private long _accumulatedTicks;
        private long _resumeTicks;
        private bool _running;

        /// <summary>
        /// Returns a newly constructed <see cref="AmbientStopwatch"/> that has been started.
        /// </summary>
#pragma warning disable CA1711  // the analyzer incorrectly thinks this method is a replacement to Start, but it is not--it literally starts a *new* stopwatch (exactly like the framework itself)
        public static AmbientStopwatch StartNew()
#pragma warning restore CA1711
        {
            return new AmbientStopwatch(true);
        }

        /// <summary>
        /// Constructs an AmbientStopwatch using the local ambient clock if there is one.
        /// </summary>
        /// <param name="run">Whether or not to start the stopwatch running.  Default is false (to match <see cref="Stopwatch"/>).</param>
        public AmbientStopwatch(bool run = false)
            : this(_Clock.Local, run)
        {
        }
        /// <summary>
        /// Constructs an AmbientStopwatch using a specfied <see cref="IAmbientClock"/>.  This overload is mainly for testing functionality without depending on the ambient environment.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> to use, or null to use the system clock.</param>
        /// <param name="run">Whether or not the stopwatch should start in a running state (as opposed to a paused state).</param>
        public AmbientStopwatch(IAmbientClock clock, bool run = true)
        {
            _clock = clock;
            _resumeTicks = Ticks;
            _running = run;
        }
        /// <summary>
        /// Gets a timestamp number that may be used to determine how many ticks have elapsed between calls.
        /// </summary>
        /// <returns>A timestamp.</returns>
        public static long GetTimestamp()
        {
            return _Clock.Local?.Ticks ?? Stopwatch.GetTimestamp();
        }
        /// <summary>
        /// Gets the frequency of the stopwatch.
        /// </summary>
        public static long Frequency => Stopwatch.Frequency;
        /// <summary>
        /// Gets whether or not the stopwatch supports high resolution.
        /// </summary>
        public static bool IsHighResolution => Stopwatch.IsHighResolution;
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
        public long ElapsedTicks => _running ? Ticks - _resumeTicks + _accumulatedTicks : _accumulatedTicks;
        /// <summary>
        /// Gets a <see cref="TimeSpan"/> representing the number of ticks elapsed.  Based entirely on <see cref="ElapsedTicks"/> and the (system-constant) resolution of the clock.
        /// </summary>
        public TimeSpan Elapsed => TimeSpan.FromTicks(ElapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
        /// <summary>
        /// Gets a <see cref="TimeSpan"/> representing the number of ticks elapsed.  Based entirely on <see cref="ElapsedTicks"/> and the (system-constant) resolution of the clock.
        /// </summary>
        public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;
        /// <summary>
        /// Gets whether or not the stopwatch is currently running.
        /// </summary>
        public bool IsRunning => _running;

        /// <summary>
        /// Stops the stopwatch so that it temporarily stops accumulating time.  While paused, <see cref="ElapsedTicks"/> will return the same value.
        /// </summary>
        public void Stop()
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
        /// Starts the stopwatch so that time begins accumulating (again).
        /// <see cref="ElapsedTicks"/> will subsequently return increasing values (or the same value if called faster than the resolution of the underlying clock).
        /// </summary>
        public void Start()
        {
            // start--was it *not* started before?
            if (!_running)
            {
                long resumeTicks = Ticks;
                _running = true;
                _resumeTicks = resumeTicks;
            }
        }
        /// <summary>
        /// Starts the stopwatch so that time begins accumulating (again).
        /// <see cref="ElapsedTicks"/> will subsequently return increasing values (or the same value if called faster than the resolution of the underlying clock).
        /// </summary>
        public void Restart()
        {
            _accumulatedTicks = 0;
            _resumeTicks = Ticks;
            _running = true;
        }
        /// <summary>
        /// Stops the stopwatch and resets the elapsed time to zero.
        /// </summary>
        public void Reset()
        {
            Stop();
            _accumulatedTicks = 0;
        }
    }
    /// <summary>
    /// A helper class that implements the same methods and properties as <see cref="System.Timers.Timer"/> but uses an ambient clock if one is available.
    /// When an ambient clock is not available, should behave identically to <see cref="System.Timers.Timer"/>.
    /// Note that whether the timer uses the system time or the ambient time is only determined at construction time.
    /// </summary>
    /// <remarks>
    /// AmbientEventTimer is thread-safe.
    /// </remarks>
    public class AmbientEventTimer : System.Timers.Timer, IAmbientClockTimeChangedNotificationSink
    {
        private static readonly System.Reflection.ConstructorInfo _ElapsedEventArgsConstructor = typeof(System.Timers.ElapsedEventArgs).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            new Type[] { typeof(long) }, null);
        private static readonly AmbientService<IAmbientClock> _Clock = Ambient.GetService<IAmbientClock>();

        private readonly IAmbientClock _clock;           // if this is null, everything falls through to the base class (ie. the system implementation)
        private long _periodStopwatchTicks;
        private long _nextRaiseStopwatchTicks;
        private int _autoReset;
        private int _enabled;
        private EventHolder _eventHolder;

        struct EventHolder
        {
            public event System.Timers.ElapsedEventHandler Elapsed;
            public void RaiseElapsed(object sender)
            {
                DateTime now = AmbientClock.UtcNow;
                long fileTime = now.ToFileTime();
                System.Timers.ElapsedEventArgs args = (System.Timers.ElapsedEventArgs)_ElapsedEventArgsConstructor.Invoke(new object[] { fileTime });
                Elapsed?.Invoke(sender, args);
            }
        }

        /// <summary>
        /// Constructs an AmbientEventTimer using the ambient clock with a period of 100ms.
        /// The timer starts with <see cref="AutoReset"/> set to true and <see cref="Enabled"/> set to false.
        /// </summary>
        public AmbientEventTimer()
            : this(_Clock.Local, TimeSpan.FromMilliseconds(100))
        {
        }
        /// <summary>
        /// Constructs an AmbientEventTimer using the ambient clock and the specified period.
        /// The timer starts with <see cref="AutoReset"/> set to true and <see cref="Enabled"/> set to false.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds indicating how often the <see cref="Elapsed"/> event should be raised.</param>
        public AmbientEventTimer(double milliseconds)
            : this(_Clock.Local, TimeSpan.FromMilliseconds(milliseconds))
        {
        }
        /// <summary>
        /// Constructs an AmbientEventTimer using the ambient clock and the specified period.
        /// The timer starts with <see cref="AutoReset"/> set to true and <see cref="Enabled"/> set to false.
        /// </summary>
        /// <param name="period">A <see cref="TimeSpan"/> indicating how often the <see cref="Elapsed"/> event should be raised.</param>
        public AmbientEventTimer(TimeSpan period)
            : this(_Clock.Local, period)
        {
        }
        /// <summary>
        /// Constructs an AmbientEventTimer that will use the specified clock to determine when to raise the <see cref="Elapsed"/> event.
        /// The timer starts with <see cref="AutoReset"/> set to true and <see cref="Enabled"/> set to false.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> to use to determine when to raise the <see cref="Elapsed"/> event.</param>
        public AmbientEventTimer(IAmbientClock clock)
            : base()
        {
            _clock = clock;
            // is there a clock?
            if (clock != null)
            {
                // disable the system timer (it should be disabled anyway, but just in case)
                base.Enabled = false;

                long nowStopwatchTicks = clock.Ticks;
                clock.RegisterTimeChangedNotificationSink(this);
                _periodStopwatchTicks = 0;
                _nextRaiseStopwatchTicks = nowStopwatchTicks + _periodStopwatchTicks;
                _enabled = 0;
            }
            // else no clock, so use the base system timer
        }
        /// <summary>
        /// Constructs an AmbientEventTimer that will use the specified period and clock to determine when to raise the <see cref="Elapsed"/> event.
        /// The timer starts with <see cref="AutoReset"/> set to true and <see cref="Enabled"/> set to false.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> to use to determine when to raise the <see cref="Elapsed"/> event.</param>
        /// <param name="period">A <see cref="TimeSpan"/> indicating how often the <see cref="Elapsed"/> event should be raised.  If zero, the timer will not be enabled.  If non-zero, the timer will start immediately.</param>
        public AmbientEventTimer(IAmbientClock clock, TimeSpan period)
            : base(period.TotalMilliseconds)
        {
            _clock = clock;
            // is there a clock?
            if (clock != null)
            {
                // disable the system timer (it should be disabled anyway, but just in case)
                base.Enabled = false;

                long nowStopwatchTicks = clock.Ticks;
                clock.RegisterTimeChangedNotificationSink(this);
                _periodStopwatchTicks = period.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
                _nextRaiseStopwatchTicks = nowStopwatchTicks + _periodStopwatchTicks;
                _enabled = 0;
            }
            // else no clock, so use the base system timer
        }
        /// <summary>
        /// Receives notification that the ambient clock time was changed.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> whose time was changed.</param>
        /// <param name="oldTicks">The old number of elapsed ticks.</param>
        /// <param name="newTicks">The new number of elapsed ticks.</param>
        /// <param name="oldUtcDateTime">The old UTC <see cref="DateTime"/>.</param>
        /// <param name="newUtcDateTime">The new UTC <see cref="DateTime"/>.</param>
        public void TimeChanged(IAmbientClock clock, long oldTicks, long newTicks, DateTime oldUtcDateTime, DateTime newUtcDateTime)
        {
            // there should be a clock if we get here
            System.Diagnostics.Debug.Assert(_clock != null && !((System.Timers.Timer)this).Enabled);
            // is the timer enabled?
            if (_enabled != 0)
            {
                // was it not time to raise before, but it is now?
                long nextRaiseStopwatchTicks = _nextRaiseStopwatchTicks;
                long periodStopwatchTicks = _periodStopwatchTicks;
                bool autoReset = (_autoReset != 0);
                // loop because it's possible that we need to raise the event more than once
                while (nextRaiseStopwatchTicks > oldTicks && nextRaiseStopwatchTicks <= newTicks && _enabled != 0)
                {
                    _eventHolder.RaiseElapsed(this);
                    // should we reset for another period?
                    if (autoReset && periodStopwatchTicks != 0)
                    {
                        nextRaiseStopwatchTicks = System.Threading.Interlocked.Add(ref _nextRaiseStopwatchTicks, periodStopwatchTicks);
                        // loop around again to check to see if we need to be raised again
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
        public new bool AutoReset
        {
            get
            {
                return (_clock != null) ? (_autoReset != 0) : base.AutoReset;
            }
            set
            {
                if (_clock != null)
                {
                    System.Threading.Interlocked.Exchange(ref _autoReset, value ? 1 : 0);
                }
                else
                {
                    base.AutoReset = value;
                }
            }
        }
        /// <summary>
        /// Gets or sets whether or not the timer is enabled (ie. whether or not it will raise the <see cref="Elapsed"/> event.
        /// </summary>
        public new bool Enabled
        {
            get { return (_clock != null) ? (_enabled != 0) : base.Enabled; }
            set
            {
                if (_clock != null)
                {
                    int oldValue = System.Threading.Interlocked.Exchange(ref _enabled, value ? 1 : 0);
                    // are we enabling and it was NOT enabled before?  set up the next raise
                    if (value && oldValue == 0) SetupNextRaise();
                }
                else
                {
                    base.Enabled = value;
                }
            }
        }
        /// <summary>
        /// Gets or sets the interval (in milliseconds) for the timer.
        /// </summary>
        public new double Interval
        {
            get
            {
                return (_clock != null) ? _periodStopwatchTicks * 1000.0 / Stopwatch.Frequency : base.Interval;
            }
            set
            {
                if (_clock != null)
                {
                    System.Threading.Interlocked.Exchange(ref _periodStopwatchTicks, (long)(value / 1000.0 * Stopwatch.Frequency));
                    // are we enabled?
                    if (_enabled != 0) SetupNextRaise();
                }
                else
                {
                    base.Interval = value;
                }
            }
        }
        private void SetupNextRaise()
        {
            System.Diagnostics.Debug.Assert(_clock != null);
            long now = _clock.Ticks;
            System.Threading.Interlocked.Exchange(ref _nextRaiseStopwatchTicks, now + _periodStopwatchTicks);
        }

        /// <summary>
        /// Starts the timer running so that the <see cref="Elapsed"/> event can be raised.
        /// </summary>
        public new void Start()
        {
            if (_clock != null)
            {
                System.Threading.Interlocked.Exchange(ref _enabled, 1);
                SetupNextRaise();
            }
            else
            {
                base.Start();
            }
        }

        /// <summary>
        /// Stops the timer running so that the <see cref="Elapsed"/> will not be raised until <see cref="Start"/> is called.
        /// </summary>
        public new void Stop()
        {
            if (_clock != null)
            {
                System.Threading.Interlocked.Exchange(ref _enabled, 0);
            }
            else
            {
                base.Stop();
            }
        }
        /// <summary>
        /// An event that is raised each time the specified period elapses.
        /// </summary>
        public new event System.Timers.ElapsedEventHandler Elapsed
        {
            add
            {
                if (_clock == null)
                {
                    base.Elapsed += value;
                }
                else
                {
                    _eventHolder.Elapsed += value;
                }
            }
            remove
            {
                if (_clock == null)
                {
                    base.Elapsed -= value;
                }
                else
                {
                    _eventHolder.Elapsed -= value;
                }
            }
        }
        /// <summary>
        /// Disposes of this instance.  Call this base class when overriding.
        /// </summary>
        /// <param name="disposing">Whether or not the instance is being disposed (as opposed to finalized).</param>
        protected override void Dispose(bool disposing)
        {
            if (_clock != null)
            {
                Enabled = false;
                _clock.DeregisterTimeChangedNotificationSink(this);
            }
            base.Dispose(disposing);
        }
    }
    /// <summary>
    /// A helper class that implements the same methods and properties as <see cref="System.Threading.Timer"/> but uses an ambient clock if one is available.
    /// When an ambient clock is not available, should behave identically to <see cref="System.Threading.Timer"/>.
    /// Note that whether the timer uses the system time or the ambient time is only determined at construction time.
    /// </summary>
    /// <remarks>
    /// AmbientCallbackTimer is thread-safe.
    /// </remarks>
    public sealed class AmbientCallbackTimer : MarshalByRefObject, IAmbientClockTimeChangedNotificationSink, 
#if !NETSTANDARD2_0
        IAsyncDisposable, 
#endif
        IDisposable
    {
        private static readonly AmbientService<IAmbientClock> _Clock = Ambient.GetService<IAmbientClock>();
        private static readonly ManualResetEvent _AlwaysSignaled = new ManualResetEvent(true);
        private static readonly object _UseTimerInstanceForStateIndicator = new object();
        private static long _TimerCount;

#if !NETSTANDARD2_0
        /// <summary>
        /// Gets the number of <see cref="AmbientCallbackTimer"/>s and <see cref="System.Threading.Timer"/> that are currently active.
        /// Does not double-count <see cref="AmbientCallbackTimer"/> that pass through to a <see cref="System.Threading.Timer"/>.
        /// </summary>
        public static long ActiveCount
        {
            get
            {
                return _TimerCount + System.Threading.Timer.ActiveCount;
            }
        }
#endif

        private readonly TimerCallback _callback;
        private readonly object _state;
        private readonly IAmbientClock _clock;           // exactly one of _clock and _timer should be null
        private readonly System.Threading.Timer _timer;

        private long _periodStopwatchTicks;
        private long _nextRaiseStopwatchTicks;
        private int _autoReset;
        private int _enabled;
        private bool _disposed; // To detect redundant calls

        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock.  The timer will not be set to call he callback.
        /// </summary>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        public AmbientCallbackTimer(TimerCallback callback)
            : this(_Clock.Local, callback, _UseTimerInstanceForStateIndicator, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
        {
        }
        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        /// <param name="state">The state <see cref="Object"/> to pass to the callback.</param>
        /// <param name="dueTime">The number of milliseconds to delay before calling the callback.  <see cref="Timeout.Infinite"/> to prevent the timer from starting.  Zero to start the timer immediately.</param>
        /// <param name="period">The number of milliseconds between callbacks.  <see cref="Timeout.Infinite"/> to disable periodic signaling.</param>
        public AmbientCallbackTimer(TimerCallback callback, object state, int dueTime, int period)
            : this(_Clock.Local, callback, state, TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period))
        {
        }
        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        /// <param name="state">The state <see cref="Object"/> to pass to the callback.</param>
        /// <param name="dueTime">The number of milliseconds to delay before calling the callback.  <see cref="Timeout.Infinite"/> to prevent the timer from starting.  Zero to start the timer immediately.</param>
        /// <param name="period">The number of milliseconds between callbacks.  <see cref="Timeout.Infinite"/> to disable periodic signaling.</param>
        [CLSCompliant(false)]
        public AmbientCallbackTimer(TimerCallback callback, object state, uint dueTime, uint period)
            : this(_Clock.Local, callback, state, TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period))
        {
        }
        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        /// <param name="state">The state <see cref="Object"/> to pass to the callback.</param>
        /// <param name="dueTime">The number of milliseconds to delay before calling the callback.  <see cref="Timeout.Infinite"/> to prevent the timer from starting.  Zero to start the timer immediately.</param>
        /// <param name="period">The number of milliseconds between callbacks.  <see cref="Timeout.Infinite"/> to disable periodic signaling.</param>
        public AmbientCallbackTimer(TimerCallback callback, object state, long dueTime, long period)
            : this(_Clock.Local, callback, state, TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period))
        {
        }
        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        /// <param name="state">The state <see cref="Object"/> to pass to the callback.</param>
        /// <param name="dueTime">A <see cref="TimeSpan"/> indicating the number of milliseconds to delay before calling the callback.  <see cref="Timeout.InfiniteTimeSpan"/> to prevent the timer from starting.  Zero to start the timer immediately.</param>
        /// <param name="period">A <see cref="TimeSpan"/> indicating the number of milliseconds between callbacks.  <see cref="Timeout.InfiniteTimeSpan"/> to disable periodic signaling.</param>
        public AmbientCallbackTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
            : this(_Clock.Local, callback, state, dueTime, period)
        {
        }
        /// <summary>
        /// Constructs an AmbientCallbackTimer using the ambient clock and the specified period.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> to use to determine when to invoke the callback.</param>
        /// <param name="callback">A <see cref="TimerCallback"/> that is called when the time elapses.</param>
        /// <param name="state">The state <see cref="Object"/> to pass to the callback.</param>
        /// <param name="dueTime">A <see cref="TimeSpan"/> indicating the number of milliseconds to delay before calling the callback.  <see cref="Timeout.InfiniteTimeSpan"/> to prevent the timer from starting.  Zero to start the timer immediately.</param>
        /// <param name="period">A <see cref="TimeSpan"/> indicating the number of milliseconds between callbacks.  <see cref="Timeout.InfiniteTimeSpan"/> to disable periodic signaling.</param>
        public AmbientCallbackTimer(IAmbientClock clock, TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (dueTime != Timeout.InfiniteTimeSpan && dueTime.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(dueTime), "The dueTime parameter must not be negative unless it is Infinite!");
            if (period != Timeout.InfiniteTimeSpan && period.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(period), "The period parameter must not be negative unless it is Infinite!");

            _callback = callback;
            _state = Object.ReferenceEquals(state, _UseTimerInstanceForStateIndicator) ? this : state;
            _clock = clock;
            // is there a clock?
            if (clock != null)
            {
                _autoReset = (period == Timeout.InfiniteTimeSpan) ? 0 : 1;
                _enabled = (dueTime == Timeout.InfiniteTimeSpan) ? 0 : 1;
                if (_enabled != 0)
                {
                    Enable(dueTime, period);
                }
            }
            else // no clock, so just fall through to using a system threading timer
            {
                _timer = new System.Threading.Timer(callback, state, dueTime, period);
            }
        }

        // when there is an ambient clock, events are raised ONLY when the clock changes, and we get notified here every time that happens
        /// <summary>
        /// Receives notification that the ambient clock time was changed.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> whose time was changed.</param>
        /// <param name="oldTicks">The old number of elapsed ticks.</param>
        /// <param name="newTicks">The new number of elapsed ticks.</param>
        /// <param name="oldUtcDateTime">The old UTC <see cref="DateTime"/>.</param>
        /// <param name="newUtcDateTime">The new UTC <see cref="DateTime"/>.</param>
        public void TimeChanged(IAmbientClock clock, long oldTicks, long newTicks, DateTime oldUtcDateTime, DateTime newUtcDateTime)
        {
            // there should be a clock and NOT a timer if we get here
            System.Diagnostics.Debug.Assert(_clock != null && _timer == null);
            // is the timer enabled?
            if (_enabled != 0)
            {
                // was it not time to raise before, but it is now?
                long nextRaiseStopwatchTicks = _nextRaiseStopwatchTicks;
                long periodStopwatchTicks = _periodStopwatchTicks;
                bool autoReset = (_autoReset != 0);
                while (_callback != null && nextRaiseStopwatchTicks > oldTicks && nextRaiseStopwatchTicks <= newTicks && _enabled != 0)
                {
                    // should we reset for another period?
                    if (autoReset && periodStopwatchTicks != Timeout.Infinite)
                    {
                        nextRaiseStopwatchTicks = System.Threading.Interlocked.Add(ref _nextRaiseStopwatchTicks, periodStopwatchTicks);
                        // we might loop around again and invoke the callback again depending on how much the time changed
                    }
                    else // we're no longer active, as the period indicates that we shouldn't invoke the callback again
                    {
                        Disable();
                        // _enabled getting set to zero should cause us to break out of the loop, unless the callback reenables us or someone else changes it asynchronously
                    }
                    _callback.Invoke(_state);
                }
            }
        }

        private void Disable()
        {
            // race to disable us-- did we win the race?
            if (1 == System.Threading.Interlocked.Exchange(ref _enabled, 0))
            {
                _clock.DeregisterTimeChangedNotificationSink(this);
                System.Threading.Interlocked.Decrement(ref _TimerCount);
            }
        }
        private void Enable(TimeSpan dueTime, TimeSpan period)
        {
            System.Threading.Interlocked.Increment(ref _TimerCount);
            long nowStopwatchTicks = _clock.Ticks;
            IAmbientClock tempClock = _clock;
            _clock.RegisterTimeChangedNotificationSink(this);
            _periodStopwatchTicks = period.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
            long ticksToNextInvocation = dueTime.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
            _nextRaiseStopwatchTicks = nowStopwatchTicks + ticksToNextInvocation;
        }

        /// <summary>
        /// Changes the time when the timer will activate, ignoring all previous activations and timing settings.
        /// </summary>
        /// <param name="dueTime">The number of milliseconds before the timer will go off for the first time, with <see cref="Timeout.Infinite"/> meaning that the timer will be disabled.</param>
        /// <param name="period">The number of milliseconds indicating how often the timer will go off after the first activation, with <see cref="Timeout.Infinite"/> meaning that the timer will only go off once.</param>
        /// <returns>true if the timer was successfully updated, otherwise false.</returns>
        public bool Change(int dueTime, int period)
        {
            return Change(TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period));
        }
        /// <summary>
        /// Changes the time when the timer will activate, ignoring all previous activations and timing settings.
        /// </summary>
        /// <param name="dueTime">The number of milliseconds before the timer will go off for the first time, with <see cref="Timeout.Infinite"/> meaning that the timer will be disabled.</param>
        /// <param name="period">The number of milliseconds indicating how often the timer will go off after the first activation, with <see cref="Timeout.Infinite"/> meaning that the timer will only go off once.</param>
        /// <returns>true if the timer was successfully updated, otherwise false.</returns>
        public bool Change(long dueTime, long period)
        {
            return Change(TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period));
        }
        /// <summary>
        /// Changes the time when the timer will activate, ignoring all previous activations and timing settings.
        /// </summary>
        /// <param name="dueTime">The number of milliseconds before the timer will go off for the first time, with <see cref="Timeout.Infinite"/> meaning that the timer will be disabled.</param>
        /// <param name="period">The number of milliseconds indicating how often the timer will go off after the first activation, with <see cref="Timeout.Infinite"/> meaning that the timer will only go off once.</param>
        /// <returns>true if the timer was successfully updated, otherwise false.</returns>
        [CLSCompliant(false)]
        public bool Change(uint dueTime, uint period)
        {
            return Change(TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period));
        }
        /// <summary>
        /// Changes the time when the timer will activate, ignoring all previous activations and timing settings.
        /// </summary>
        /// <param name="dueTime">A <see cref="TimeSpan"/> indicating the amount of time before the timer will go off for the first time, with <see cref="Timeout.InfiniteTimeSpan"/> meaning that the timer will be disabled.</param>
        /// <param name="period">A <see cref="TimeSpan"/> indicating how often the timer will go off after the first activation, with <see cref="Timeout.InfiniteTimeSpan"/> meaning that the timer will only go off once.</param>
        /// <returns>true if the timer was successfully updated, otherwise false.</returns>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_clock != null)
            {
                if (dueTime != Timeout.InfiniteTimeSpan && dueTime.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(dueTime), "The dueTime parameter must not be negative unless it is Infinite!");
                if (period != Timeout.InfiniteTimeSpan && period.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(period), "The period parameter must not be negative unless it is Infinite!");

                // were we enabled before?
                if (_enabled != 0)
                {
                    Disable();
                }
                _autoReset = (period == Timeout.InfiniteTimeSpan) ? 0 : 1;
                // race to enable us--did we win the race?
                int newEnabled = (dueTime == Timeout.InfiniteTimeSpan) ? 0 : 1;
                if (newEnabled != 0 && System.Threading.Interlocked.Exchange(ref _enabled, newEnabled) == 0)
                {
                    Enable(dueTime, period);
                }
                return true;
            }
            else
            {
                return _timer.Change(dueTime, period);
            }
        }

#region IDisposable Support
        /// <summary>
        /// Disposes of the timer, signaling an optional <see cref="WaitHandle"/> when the disposal is complete (meaning that the callback is not in progress and will not be subsequently called).
        /// </summary>
        /// <param name="waitHandle">The <see cref="WaitHandle"/> to signal when the disposal is complete, or null if no notification is needed.</param>
        /// <returns>true if the disposal was successful and neede, otherwise false.</returns>
        public bool Dispose(WaitHandle waitHandle)
        {
            bool ret = false;
            if (!_disposed)
            {
                if (_clock != null)
                {
                    bool enabled = (_enabled != 0);
                    Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    // since notification when we have an ambient clock service is synchronous, there is no need to wait for full disposal
                    WaitHandle.SignalAndWait(waitHandle, _AlwaysSignaled);
                    // return whether or not we were already canceled
                    ret = enabled;
                }
                else
                {
                    ret = _timer.Dispose(waitHandle);
                }
                System.Threading.Interlocked.Decrement(ref _TimerCount);
                _disposed = true;
            }
            return ret;
        }

#if !NETSTANDARD2_0
        /// <summary>
        /// Asynchronously disposes the instance.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> allowing the caller to control and monitor the asynchronous execution.</returns>
        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore().ConfigureAwait(false);

            // Dispose of unmanaged resources.
            Dispose();
            // Suppress finalization.
#pragma warning disable CA1816      // this is the *recommended* implementation!  see https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync
            GC.SuppressFinalize(this);
#pragma warning disable CA1816
        }
        private ValueTask DisposeAsyncCore()    // note that this would be protected virtual if this class were not sealed
        {
            if (_timer != null)
            {
                return _timer.DisposeAsync();
            }
            return default;
        }
#endif
        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_clock != null)
                {
                    Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _timer.Dispose();
                }
                System.Threading.Interlocked.Decrement(ref _TimerCount);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        #endregion
    }
    /// <summary>
    /// A sealed class that emulates <see cref="RegisteredWaitHandle"/> but uses the ambient clock if one is registered.
    /// </summary>
    public sealed class AmbientRegisteredWaitHandle : IAmbientClockTimeChangedNotificationSink
    {
        private static readonly AmbientService<IAmbientClock> _Clock = Ambient.GetService<IAmbientClock>();
        private static readonly ManualResetEvent _ManualResetEvent = new ManualResetEvent(true);


        private readonly RegisteredWaitHandle _registeredWaitHandle;
        private readonly IAmbientClock _clock;
        private readonly WaitOrTimerCallback _callback;
        private readonly bool _executeOnlyOnce;
        private readonly object _state;
        private readonly long _periodStopwatchTicks;
        private readonly ExecutionContext _executionContext;
        private long _nextCallbackTimeStopwatchTicks;

        internal AmbientRegisteredWaitHandle(bool safe, WaitHandle waitHandle, WaitOrTimerCallback callback, object state, int millisecondTimeoutInterval, bool executeOnlyOnce)
            : this(waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce, safe)
        {
        }
        internal AmbientRegisteredWaitHandle(bool safe, WaitHandle waitHandle, WaitOrTimerCallback callback, object state, uint millisecondTimeoutInterval, bool executeOnlyOnce)
            : this(waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce, safe)
        {
        }
        internal AmbientRegisteredWaitHandle(bool safe, WaitHandle waitHandle, WaitOrTimerCallback callback, object state, long millisecondTimeoutInterval, bool executeOnlyOnce)
            : this(waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce, safe)
        {
        }
        internal AmbientRegisteredWaitHandle(bool safe, WaitHandle waitHandle, WaitOrTimerCallback callback, object state, TimeSpan timeoutInterval, bool executeOnlyOnce)
            : this(waitHandle, callback, state, (long)timeoutInterval.TotalMilliseconds, executeOnlyOnce, safe)
        {
        }
        private AmbientRegisteredWaitHandle(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, long millisecondTimeoutInterval, bool executeOnlyOnce, bool safe)
        {
            if ((_clock = _Clock.Local) == null)
            {
                _registeredWaitHandle = safe
                    ? ThreadPool.RegisterWaitForSingleObject(waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce)
                    : ThreadPool.UnsafeRegisterWaitForSingleObject(waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
            }
            else
            {
                _callback = callback;
                _state = state;
                _registeredWaitHandle = safe
                    ? ThreadPool.RegisterWaitForSingleObject(waitHandle, OnWaitHandleSignaled, null, -1, executeOnlyOnce)
                    : ThreadPool.UnsafeRegisterWaitForSingleObject(waitHandle, OnWaitHandleSignaled, null, -1, executeOnlyOnce);
                if (safe)
                {
                    _executionContext = ExecutionContext.Capture();
                }
                long timeoutIntervalStopwatchTicks = millisecondTimeoutInterval * Stopwatch.Frequency / 1000;
                _nextCallbackTimeStopwatchTicks = (millisecondTimeoutInterval == Timeout.Infinite) ? Timeout.Infinite : (_clock.Ticks + timeoutIntervalStopwatchTicks);
                _periodStopwatchTicks = executeOnlyOnce ? Timeout.Infinite : timeoutIntervalStopwatchTicks;
                _executeOnlyOnce = executeOnlyOnce;
                _clock.RegisterTimeChangedNotificationSink(this);
            }
        }

        private void OnWaitHandleSignaled(object state, bool timedOut)
        {
            // only execute once?
            if (_executeOnlyOnce)
            {
                // disable further signal invocations
                _registeredWaitHandle.Unregister(_ManualResetEvent);
            }
            // no period (ie. this is the only callback)?
            if (_periodStopwatchTicks == Timeout.Infinite)
            {   // cancel all further timed callbacks
                System.Threading.Interlocked.Exchange(ref _nextCallbackTimeStopwatchTicks, Timeout.Infinite);
            }
            else
            {   // schedule the next callback
                System.Threading.Interlocked.Exchange(ref _nextCallbackTimeStopwatchTicks, _clock.Ticks + _periodStopwatchTicks);
            }
            // the wait handle was signaled--we should always call the callback in this case
            _callback(_state, false);

        }
        /// <summary>
        /// Receives notification that the ambient clock time was changed.
        /// </summary>
        /// <param name="clock">The <see cref="IAmbientClock"/> whose time was changed.</param>
        /// <param name="oldTicks">The old number of elapsed ticks.</param>
        /// <param name="newTicks">The new number of elapsed ticks.</param>
        /// <param name="oldUtcDateTime">The old UTC <see cref="DateTime"/>.</param>
        /// <param name="newUtcDateTime">The new UTC <see cref="DateTime"/>.</param>
        public void TimeChanged(IAmbientClock clock, long oldTicks, long newTicks, DateTime oldUtcDateTime, DateTime newUtcDateTime)
        {// when there is an ambient clock, events are raised ONLY when the clock changes, and we get notified here every time that happens
            // there should be a clock if we get here
            System.Diagnostics.Debug.Assert(_clock != null);
            // loop until we process all the scheduled callbacks
            while (_nextCallbackTimeStopwatchTicks != Timeout.Infinite && _nextCallbackTimeStopwatchTicks > oldTicks && _nextCallbackTimeStopwatchTicks <= newTicks)
            {
                // should we reset for another period?
                if (_periodStopwatchTicks != Timeout.Infinite)
                {
                    System.Threading.Interlocked.Add(ref _nextCallbackTimeStopwatchTicks, _periodStopwatchTicks);
                    // we may loop around again in case the event should have been raised more than once
                }
                else
                {
                    // this should cause the loop to stop, but only AFTER we invoke the callback
                    _nextCallbackTimeStopwatchTicks = Timeout.Infinite;
                }
                // only execute once?
                if (_executeOnlyOnce)
                {
                    // disable further signal invocations
                    _registeredWaitHandle.Unregister(_ManualResetEvent);
                }
                if (_executionContext != null)
                {
                    // run in the execution context of the constructor
                    ExecutionContext.Run(_executionContext, state => _callback(state, true), _state); 
                }
                else
                {
                    _callback(_state, true);
                }
            }
        }
        /// <summary>
        /// Cancels a registered wait operation issued by the <see cref="System.Threading.ThreadPool.RegisterWaitForSingleObject(System.Threading.WaitHandle,System.Threading.WaitOrTimerCallback,System.Object,System.UInt32,System.Boolean)"/>.
        /// method.
        /// </summary>
        /// <param name="waitObject">The <see cref="System.Threading.WaitHandle"/> to be signaled.</param>
        /// <returns>true if the function succeeds; otherwise, false.</returns>
        public bool Unregister(WaitHandle waitObject)
        {
            bool ret = _registeredWaitHandle.Unregister(waitObject);
            _clock?.DeregisterTimeChangedNotificationSink(this);
            return ret;
        }
    }
    /// <summary>
    /// A static class that contains ambient replacements for <see cref="ThreadPool"/>.
    /// </summary>
    public static class AmbientThreadPool
    {
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 32-bit signed integer for the time-out in milliseconds.
        /// </summary>
        /// <param name="waitHandle">Registers a delegate to wait for a System.Threading.WaitHandle, specifying a 32-bit signed integer for the time-out in milliseconds.</param>
        /// <param name="callback">The System.Threading.WaitOrTimerCallback delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out in milliseconds. If the millisecondsTimeOutInterval parameter is 0 (zero), the function tests the object's state and returns immediately. If millisecondsTimeOutInterval is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The System.Threading.RegisteredWaitHandle that encapsulates the native handle.</returns>
        public static AmbientRegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, int millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(true, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 32-bit unsigned integer for the time-out in milliseconds.
        /// </summary>
        /// <param name="waitHandle">Registers a delegate to wait for a System.Threading.WaitHandle, specifying a 32-bit signed integer for the time-out in milliseconds.</param>
        /// <param name="callback">The System.Threading.WaitOrTimerCallback delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out in milliseconds. If the millisecondsTimeOutInterval parameter is 0 (zero), the function tests the object's state and returns immediately. If millisecondsTimeOutInterval is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The System.Threading.RegisteredWaitHandle that encapsulates the native handle.</returns>
        [CLSCompliant(false)]
        public static AmbientRegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, uint millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(true, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 64-bit signed integer for the time-out in milliseconds.
        /// </summary>
        /// <param name="waitHandle">Registers a delegate to wait for a System.Threading.WaitHandle, specifying a 32-bit signed integer for the time-out in milliseconds.</param>
        /// <param name="callback">The System.Threading.WaitOrTimerCallback delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out in milliseconds. If the millisecondsTimeOutInterval parameter is 0 (zero), the function tests the object's state and returns immediately. If millisecondsTimeOutInterval is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The System.Threading.RegisteredWaitHandle that encapsulates the native handle.</returns>
        public static AmbientRegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, long millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(true, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a <see cref="TimeSpan"/> for the time-out.
        /// </summary>
        /// <param name="waitHandle">Registers a delegate to wait for a System.Threading.WaitHandle, specifying a 32-bit signed integer for the time-out in milliseconds.</param>
        /// <param name="callback">The System.Threading.WaitOrTimerCallback delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="timeoutInterval">The time-out represented by a <see cref="System.TimeSpan"/>. If timeout is 0 (zero), the function tests the object's state and returns immediately. If timeout is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The System.Threading.RegisteredWaitHandle that encapsulates the native handle.</returns>
        public static AmbientRegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, TimeSpan timeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(true, waitHandle, callback, state, timeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 32-bit signed integer value for the time-out in milliseconds. This method does not propagate the calling stack to the worker thread.
        /// </summary>
        /// <param name="waitHandle">The <see cref="System.Threading.WaitHandle"/> to register. Use a <see cref="System.Threading.WaitHandle"/> other than <see cref="System.Threading.Mutex"/>.</param>
        /// <param name="callback">The delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out represented by a System.TimeSpan. If timeout is 0 (zero), the function tests the object's state and returns immediately. If timeout is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The <see cref="System.Threading.RegisteredWaitHandle"/> object that can be used to cancel the registered wait operation.</returns>
        public static AmbientRegisteredWaitHandle UnsafeRegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, int millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(false, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 32-bit unsigned integer value for the time-out in milliseconds. This method does not propagate the calling stack to the worker thread.
        /// </summary>
        /// <param name="waitHandle">The <see cref="System.Threading.WaitHandle"/> to register. Use a <see cref="System.Threading.WaitHandle"/> other than <see cref="System.Threading.Mutex"/>.</param>
        /// <param name="callback">The delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out represented by a System.TimeSpan. If timeout is 0 (zero), the function tests the object's state and returns immediately. If timeout is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The <see cref="System.Threading.RegisteredWaitHandle"/> object that can be used to cancel the registered wait operation.</returns>
        [CLSCompliant(false)]
        public static AmbientRegisteredWaitHandle UnsafeRegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, uint millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(false, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a 64-bit signed integer value for the time-out in milliseconds. This method does not propagate the calling stack to the worker thread.
        /// </summary>
        /// <param name="waitHandle">The <see cref="System.Threading.WaitHandle"/> to register. Use a <see cref="System.Threading.WaitHandle"/> other than <see cref="System.Threading.Mutex"/>.</param>
        /// <param name="callback">The delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="millisecondTimeoutInterval">The time-out represented by a System.TimeSpan. If timeout is 0 (zero), the function tests the object's state and returns immediately. If timeout is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The <see cref="System.Threading.RegisteredWaitHandle"/> object that can be used to cancel the registered wait operation.</returns>
        public static AmbientRegisteredWaitHandle UnsafeRegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, long millisecondTimeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(false, waitHandle, callback, state, millisecondTimeoutInterval, executeOnlyOnce);
        }
        /// <summary>
        /// Registers a delegate to wait for a <see cref="System.Threading.WaitHandle"/>, specifying a <see cref="System.TimeSpan"/> value for the time-out. This method does not propagate the calling stack to the worker thread.
        /// </summary>
        /// <param name="waitHandle">The <see cref="System.Threading.WaitHandle"/> to register. Use a <see cref="System.Threading.WaitHandle"/> other than <see cref="System.Threading.Mutex"/>.</param>
        /// <param name="callback">The delegate to call when the waitObject parameter is signaled.</param>
        /// <param name="state">The object that is passed to the delegate.</param>
        /// <param name="timeoutInterval">The time-out represented by a <see cref="System.TimeSpan"/>. If timeout is 0 (zero), the function tests the object's state and returns immediately. If timeout is -1, the function's time-out interval never elapses.</param>
        /// <param name="executeOnlyOnce">true to indicate that the thread will no longer wait on the waitObject parameter after the delegate has been called; false to indicate that the timer is reset every time the wait operation completes until the wait is unregistered.</param>
        /// <returns>The <see cref="System.Threading.RegisteredWaitHandle"/> object that can be used to cancel the registered wait operation.</returns>
        public static AmbientRegisteredWaitHandle UnsafeRegisterWaitForSingleObject(WaitHandle waitHandle, WaitOrTimerCallback callback, object state, TimeSpan timeoutInterval, bool executeOnlyOnce)
        {
            return new AmbientRegisteredWaitHandle(false, waitHandle, callback, state, timeoutInterval, executeOnlyOnce);
        }
    }
}
