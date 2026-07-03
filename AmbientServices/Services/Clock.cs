using System;

namespace AmbientServices;

/// <summary>
/// An interface that callers implement to receive ambient clock time changed notifications.
/// </summary>
/// <remarks>
/// <pitch>The push side of virtual time: implement this to be told whenever an ambient clock's time is explicitly moved.  The ambient timer classes (<see cref="AmbientEventTimer"/>, <see cref="AmbientCallbackTimer"/>, <see cref="AmbientRegisteredWaitHandle"/>) are the canonical implementers — this notification is how their events fire during a virtual time skip.</pitch>
/// <pledge>
/// <see cref="TimeChanged"/> is called once per explicit time change, after the change has been applied, with both the old and new tick counts and their UTC date-time equivalents, which fully determine the change.  Calls arrive synchronously on the thread that changed the clock, so implementations should be fast, must not block indefinitely, and must not change the clock's time again from inside the notification (no reentrancy).
/// </pledge>
/// </remarks>
public interface IAmbientClockTimeChangedNotificationSink
{
    /// <summary>
    /// Receives notification that the ambient clock time was changed.
    /// </summary>
    /// <param name="clock">The <see cref="IAmbientClock"/> whose time was changed.</param>
    /// <param name="oldTicks">The old number of elapsed ticks.</param>
    /// <param name="newTicks">The new number of elapsed ticks.</param>
    /// <param name="oldUtcDateTime">The old UTC <see cref="DateTime"/>.</param>
    /// <param name="newUtcDateTime">The new UTC <see cref="DateTime"/>.</param>
    void TimeChanged(IAmbientClock clock, long oldTicks, long newTicks, DateTime oldUtcDateTime, DateTime newUtcDateTime);
}
/// <summary>
/// An interface that abstracts an ambient clock which can be overridden in order to provide a different resolution or to artificially manipulate the current date-time and timing for testing.
/// </summary>
/// <remarks>
/// Note that there is no default implementation for this interface.  This results in a default of null, which falls back to using the native system calls.
/// <pitch>
/// Swap in a controllable time source and everything that reads time through <see cref="AmbientClock"/> — timestamps, stopwatches, timers, timeouts, and timed cancellation — observes it, which makes time-dependent logic deterministically testable (pause time, skip it ahead) without sleeping.  There is deliberately no default realization: a null service means the system clock, at essentially zero overhead.
/// </pitch>
/// <pledge>
/// <see cref="Ticks"/> and <see cref="UtcDateTime"/> are thread-safe views of the same virtual instant — ticks are measured in <see cref="System.Diagnostics.Stopwatch.Frequency"/> units and the two always agree and advance together.  Time moves only as the realization dictates, and every explicit change is announced to each registered <see cref="IAmbientClockTimeChangedNotificationSink"/> after it takes effect, per that interface's Pledge; a realization that changes time without notifying its sinks breaks the ambient timers built on it.
/// </pledge>
/// </remarks>
public interface IAmbientClock
{
    /// <summary>
    /// Gets the number of ticks elapsed.  Ticks must be measured in units of <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe.
    /// </remarks>
    long Ticks { get; }
    /// <summary>
    /// Gets the current UTC <see cref="DateTime"/>.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe.
    /// </remarks>
    DateTime UtcDateTime { get; }
    /// <summary>
    /// Registers a time changed notification sink with this ambient clock.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientClockTimeChangedNotificationSink"/> that will receive notifications when the time is changed.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    bool RegisterTimeChangedNotificationSink(IAmbientClockTimeChangedNotificationSink sink);
    /// <summary>
    /// Deregisters a time changed notification sink with this ambient clock.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientClockTimeChangedNotificationSink"/> that will receive notifications when the time is changed.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    bool DeregisterTimeChangedNotificationSink(IAmbientClockTimeChangedNotificationSink sink);
}
