using System;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// An interface that callers implement to receive ambient clock time changed notifications.
    /// </summary>
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
    /// An interface that abstracts an ambient clock which can be overridden in order to provide a different resolution or to artificially manipulate the current date-time and timing.
    /// </summary>
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
}
