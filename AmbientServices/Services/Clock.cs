using System;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// A class that holds values related to the <see cref="IAmbientClock.TimeChanged"/> event.
    /// </summary>
    public class AmbientClockTimeChangedEventArgs
    {
        /// <summary>
        /// Gets or sets the <see cref="IAmbientClock"/> that raised the event.
        /// </summary>
        public IAmbientClock Clock { get; set; }
        /// <summary>
        /// The old number of elapsed ticks.
        /// </summary>
        public long OldTicks { get; set; }
        /// <summary>
        /// The new number of elapsed ticks.
        /// </summary>
        public long NewTicks { get; set; }
        /// <summary>
        /// The old UTC <see cref="DateTime"/>.
        /// </summary>
        public DateTime OldUtcDateTime { get; set; }
        /// <summary>
        /// The new UTC <see cref="DateTime"/>.
        /// </summary>
        public DateTime NewUtcDateTime { get; set; }
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
        /// An event that is raised when the ambient clock's time is changed.
        /// </summary>
        event EventHandler<AmbientClockTimeChangedEventArgs> TimeChanged;
    }
}
