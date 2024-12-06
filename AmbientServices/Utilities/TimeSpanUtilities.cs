using System;
using System.Diagnostics;
using System.Linq;

namespace AmbientServices.Utilities
{
    /// <summary>
    /// A static class that contains utility functions for <see cref="System.TimeSpan"/>.
    /// </summary>
    internal static class TimeSpanUtilities
    {
        internal static readonly long TimeSpanToStopwatchMultiplier;
        internal static readonly long TimeSpanToStopwatchDivisor;
        private static readonly double TimeSpanToStopwatchRatio;
        internal static readonly long StopwatchToTimeSpanMultiplier;
        internal static readonly long StopwatchToTimeSpanDivisor;
        private static readonly double StopwatchToTimeSpanRatio;
        private static readonly long BaselineStopwatchTimestamp;
        private static readonly long BaselineDateTimeTicks;
#pragma warning disable CA1810  // it should be faster in this case to do it this way because the values depend on each other
        static TimeSpanUtilities()
#pragma warning restore CA1810
        {
            ulong timeSpanTicksPerSecond = TimeSpan.TicksPerSecond;
            ulong stopwatchTicksPerSecond = (ulong)Stopwatch.Frequency;
            // adjust the multipliers and divisors to reduce the range of values that will cause overflow during conversion by removing all common divisors
            ulong gcd = GCD(timeSpanTicksPerSecond, stopwatchTicksPerSecond);
            TimeSpanToStopwatchMultiplier = (long)(stopwatchTicksPerSecond / gcd);
            TimeSpanToStopwatchDivisor = (long)(timeSpanTicksPerSecond / gcd);
            TimeSpanToStopwatchRatio = TimeSpanToStopwatchMultiplier / (double)TimeSpanToStopwatchDivisor;
            StopwatchToTimeSpanMultiplier = (long)(timeSpanTicksPerSecond / gcd);
            StopwatchToTimeSpanDivisor = (long)(stopwatchTicksPerSecond / gcd);
            StopwatchToTimeSpanRatio = StopwatchToTimeSpanMultiplier / (double)StopwatchToTimeSpanDivisor;
            BaselineStopwatchTimestamp = Stopwatch.GetTimestamp();     // note that it doesn't matter if AmbientClock is in use--these are *relative* numbers, so the conversion should work either way
#if DEBUG   // delay this a little bit just in case it makes a difference
            System.Threading.Thread.Sleep(73);
#endif
            BaselineDateTimeTicks = DateTime.UtcNow.Ticks;
            // make sure that nobody else gets times before these
            System.Threading.Thread.MemoryBarrier();
            TimeSpanStopwatchConversionLeastCommonMultiple = TimeSpanToStopwatchMultiplier * TimeSpanToStopwatchDivisor;
        }
        internal static ulong GCD(ulong a, ulong b)
        {
            while (a != 0 && b != 0)
            {
                if (a > b) a %= b; else b %= a;
            }
            return a | b;
        }
        /// <summary>
        /// Gets the smallest number of ticks than can be successfully roundtripped between stopwatch ticks and timespan ticks without any loss of accuracy.
        /// </summary>
        internal static long TimeSpanStopwatchConversionLeastCommonMultiple { get; private set; }
        /// <summary>
        /// Converts <see cref="TimeSpan"/> ticks to <see cref="System.Diagnostics.Stopwatch"/> ticks as accurately as possible using integer conversion if possible without overflow, or <see cref="double"/> multipliation if not.
        /// </summary>
        /// <param name="timeSpanTicks">The number of <see cref="TimeSpan"/> ticks.</param>
        /// <returns>The equivalent number of <see cref="System.Diagnostics.Stopwatch"/> ticks</returns>
        public static long TimeSpanTicksToStopwatchTicks(long timeSpanTicks)
        {
            try
            {
                checked
                {
                    return timeSpanTicks * TimeSpanToStopwatchMultiplier / TimeSpanToStopwatchDivisor;
                }
            }
            // Coverage note: the testability of this code depends on the values of system constants which cannot be altered by the test code
            catch (OverflowException)
            {
                return (long)(timeSpanTicks * TimeSpanToStopwatchRatio);
            }
        }
        /// <summary>
        /// Converts <see cref="System.Diagnostics.Stopwatch"/> ticks to <see cref="TimeSpan"/> ticks as accurately as possible using integer conversion if possible without overflow, or <see cref="double"/> multipliation if not.
        /// </summary>
        /// <param name="timeSpanTicks">The number of <see cref="System.Diagnostics.Stopwatch"/> ticks.</param>
        /// <returns>The equivalent number of <see cref="TimeSpan"/> ticks</returns>
        public static long StopwatchTicksToTimeSpanTicks(long timeSpanTicks)
        {
            try
            {
                checked
                {
                    return timeSpanTicks * StopwatchToTimeSpanMultiplier / StopwatchToTimeSpanDivisor;
                }
            }
            // Coverage note: the testability of this code depends on the values of system constants which cannot be altered by the test code
            catch (OverflowException)
            {
                return (long)(timeSpanTicks * StopwatchToTimeSpanRatio);
            }
        }
        /// <summary>
        /// Converts a stopwatch timestamp to UTC <see cref="DateTime"/> ticks.
        /// </summary>
        /// <param name="stopwatchTimestamp">A timestamp gathered from <see cref="Stopwatch.GetTimestamp()"/>.</param>
        /// <returns>The number of UTC <see cref="DateTime "/>ticks.</returns>
        public static long StopwatchTimestampToDateTime(long stopwatchTimestamp)
        {
            long stopwatchTicksAgo = stopwatchTimestamp - BaselineStopwatchTimestamp;
            long dateTimeTicksAgo = StopwatchTicksToTimeSpanTicks(stopwatchTicksAgo);
            return BaselineDateTimeTicks + dateTimeTicksAgo;
        }
        /// <summary>
        /// Converts UTC <see cref="DateTime"/> ticks to a stopwatch timestamp.
        /// </summary>
        /// <param name="dateTimeTicks">Ticks from a UTC <see cref="DateTime"/>.</param>
        /// <returns>The corresponding <see cref="Stopwatch"/> timestamp.</returns>
        public static long DateTimeToStopwatchTimestamp(long dateTimeTicks)
        {
            long dateTimeTicksAgo = dateTimeTicks - BaselineDateTimeTicks;
            long stopwatchTicksAgo = TimeSpanTicksToStopwatchTicks(dateTimeTicksAgo);
            return BaselineStopwatchTimestamp + stopwatchTicksAgo;
        }
    }
}
