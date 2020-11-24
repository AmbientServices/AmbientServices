using System;
using System.Diagnostics;

namespace AmbientServices
{
    /// <summary>
    /// A static class to hold enhanced functions for performing interlocked operations.
    /// </summary>
    internal static class InterlockedExtensions
    {
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
                    delay = Pseudorandom % (int)(50 * Math.Pow(2, attempt - 4));
                    System.Threading.Thread.Sleep(delay);
                }
                // between one and three misses, loop for a random number of iterations before continuing
                else if (attempt >= 3)
                {
                    delay = Pseudorandom % (int)(500 * Math.Pow(2, attempt - 2));
                    for (int spin = 0; spin < delay; ++spin) { }
                }
            }
            return true;
        }
        private static int _PseudorandomRotator;
        /// <summary>
        /// Gets a superfast pseudorandom number.  This number, based on a combination of a simple interlocked rotator and the tick count, will jump rapidly from low numbers to high numbers.
        /// </summary>
        /// <returns>A singed but always positive pseudorandom number.</returns>
        /// <remarks>
        /// This number will be useful for certain types of algorithms where a fast, even statistical distribution is needed, but where no semblance of cryptographic security is required.
        /// For example, load distribution.
        /// </remarks>
        private static int Pseudorandom
        {
            get
            {
                unchecked
                {
                    uint x = (uint)Environment.TickCount ^ (uint)System.Threading.Interlocked.Increment(ref _PseudorandomRotator) * 433494437;
                    x = (((x & 0xaaaaaaaa) >> 1) | ((x & 0x55555555) << 1));
                    x = (((x & 0xcccccccc) >> 2) | ((x & 0x33333333) << 2));
                    x = (((x & 0xf0f0f0f0) >> 4) | ((x & 0x0f0f0f0f) << 4));
                    x = (((x & 0xff00ff00) >> 8) | ((x & 0x00ff00ff) << 8));
                    return (int)(((x >> 16) | (x << 16)) & 0x7fffffff);
                }
            }
        }
    }
}
