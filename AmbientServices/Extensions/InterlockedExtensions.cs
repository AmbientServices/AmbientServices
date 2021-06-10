using System;
using System.Diagnostics;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A static class to hold enhanced functions for performing interlocked operations.
    /// </summary>
    /// <remarks>
    /// Note that we could increase the code coverage by making a single function with a delegate to reduce the uncovered lines to two,
    /// but this would likely have a significant performance impace without affecting the actual testabilty or reliability of the code.
    /// </remarks>
    internal static class InterlockedExtensions
    {
        /// <summary>
        /// Replaces the value with the specified value if the specified value is greater.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="possibleNewMin">The value to replace the value with if it is greater.</param>
        /// <returns>The new minimum value.</returns>
        public static long TryOptomisticMin(ref long valueReference, long possibleNewMin)
        {
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the latest value
                long oldValue = valueReference;
                // done but not the new min?
                if (possibleNewMin >= oldValue) return oldValue;
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref valueReference, possibleNewMin, oldValue))
                {
                    // we're done and we were the new min
                    return possibleNewMin;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldValue;
            }
        }
        /// <summary>
        /// Replaces the value with the specified value if the specified value is greater.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="possibleNewMax">The value to replace the value with if it is greater.</param>
        /// <returns>The new maximum value.</returns>
        public static long TryOptomisticMax(ref long valueReference, long possibleNewMax)
        {
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the latest value
                long oldValue = valueReference;
                // done but not the new max?
                if (possibleNewMax <= oldValue) return oldValue;
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref valueReference, possibleNewMax, oldValue))
                {
                    // we're done and we were the new max
                    return possibleNewMax;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldValue;
            }
        }
        /// <summary>
        /// Replaces the value with the specified value if the specified value is greater.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="possibleNewMin">The value to replace the value with if it is greater.</param>
        /// <returns>The new minimum value.</returns>
        public static double TryOptomisticMin(ref double valueReference, double possibleNewMin)
        {
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the latest value
                double oldValue = valueReference;
                // done but not the new min?
                if (possibleNewMin >= oldValue) return oldValue;
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref valueReference, possibleNewMin, oldValue))
                {
                    // we're done and we were the new min
                    return possibleNewMin;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldValue;
            }
        }
        /// <summary>
        /// Replaces the value with the specified value if the specified value is greater.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="possibleNewMax">The value to replace the value with if it is greater.</param>
        /// <returns>The new maximum value.</returns>
        public static double TryOptomisticMax(ref double valueReference, double possibleNewMax)
        {
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the latest value
                double oldValue = valueReference;
                // done but not the new max?
                if (possibleNewMax <= oldValue) return oldValue;
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref valueReference, possibleNewMax, oldValue))
                {
                    // we're done and we were the new max
                    return possibleNewMax;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldValue;
            }
        }
        /// <summary>
        /// Attempts to add the specified amount to the value using interlocked operations.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="toAdd">The value to add to the value.</param>
        /// <returns>The new value.</returns>
        public static double TryOptomisticAdd(ref double valueReference, double toAdd)
        {
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the latest value
                double oldValue = valueReference;
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref valueReference, oldValue + toAdd, oldValue))
                {
                    // return the new value
                    return oldValue + toAdd;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldValue;
            }
        }
        /// <summary>
        /// Updates an exponential moving average, which is a type of average where the age of recently added samples affect the value more than less recently added values,
        /// with the effect of samples decreasing exponentially over time.
        /// Whenever a new sample is added, the sum of all old samples are weighted in comparison to the new sample based on the given half life.
        /// If there is too much contention on <paramref name="valueReference"/>, the attempt will fail and no exception will be thrown.
        /// </summary>
        /// <param name="valueReference">A reference to the value being manipulated.</param>
        /// <param name="decayHalfLives">The number of half-lives the old value should be discounted due to age.</param>
        /// <param name="sampleValue">The sample value to add to the moving average.</param>
        /// <returns>The new minimum value.</returns>
        public static double TryOptomisticAddExponentialMovingAverageSample(ref double valueReference, double decayHalfLives, double sampleValue)
        {
            if (decayHalfLives < 0) throw new ArgumentOutOfRangeException(nameof(decayHalfLives), "The number of half lives must not be negative!");
            if (decayHalfLives == 0) return valueReference;
            int attempt = 0;
            // loop attempting to put it in until we win the race or timeout
            while (true)
            {
                // get the old average
                double oldAverage = valueReference;
                double newAverage = sampleValue + (oldAverage - sampleValue) * (1 / Math.Pow(2.0, decayHalfLives));
                // try to put in our value--did we win the race?
                if (oldAverage == System.Threading.Interlocked.CompareExchange(ref valueReference, newAverage, oldAverage))
                {
                    // we're done--return the new average
                    return newAverage;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!TryAgainAfterOptomisticMissDelay(attempt++)) return oldAverage;
            }
        }
        internal static bool TryAgainAfterOptomisticMissDelay(int attempt)
        {
            // don't delay at all on the first miss (other than this function call and if)
            if (attempt > 0)
            {
                // if we hit ten attempts, bail out and just ingore the attempted operation
                if (attempt >= 10) return false;
                int delay;
                // between five and ten misses, sleep for a random amount before continuing
                if (attempt >= 5)
                {
                    delay = _Rand.NextInt32 % (int)(50 * Math.Pow(2, attempt - 4));
                    System.Threading.Thread.Sleep(delay);
                }
                // between one and three misses, loop for a random number of iterations before continuing
                else if (attempt >= 3)
                {
                    delay = _Rand.NextInt32 % (int)(500 * Math.Pow(2, attempt - 2));
                    for (int spin = 0; spin < delay; ++spin) { }
                }
            }
            return true;
        }
        private static Pseudorandom _Rand = new AmbientServices.Utility.Pseudorandom(true);
    }
}
