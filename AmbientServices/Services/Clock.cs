using System;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// A class that holds values related to the <see cref="IAmbientClockProvider.OnTimeChanged"/> event.
    /// </summary>
    public class AmbientClockProviderTimeChangedEventArgs
    {
        /// <summary>
        /// Gets or sets the <see cref="IAmbientClockProvider"/> that triggered the event.
        /// </summary>
        public IAmbientClockProvider Clock { get; set; }
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
    public interface IAmbientClockProvider
    {
        /// <summary>
        /// Gets the number of ticks elapsed.  Ticks must be measured in units of <see cref="Stopwatch.Frequency"/>.
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
        /// An event indicating that the ambient clock's time has changed.
        /// </summary>
        event EventHandler<AmbientClockProviderTimeChangedEventArgs> OnTimeChanged;
    }
}
