﻿using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A class containing a random seed from which pseudorandom data of various types can be generated.  
    /// </summary>
    /// <remarks>
    /// Data generated using <see cref="Pseudorandom"/> should be more random than that generated by the system's <see cref="Random"/> class, which generates obvious repeating patterns when the results are divided by small constants.
    /// Additionally, instances constructed using <see cref="Pseudorandom.Pseudorandom(bool)"/> with true as a parameter or by using the static <see cref="Next"/> property, even simultaneously, will rarely get the same seed.
    /// The random data generated from any particular seed is consistent, ie. given the same seed and sequence of property and method calls, the same values will be generated.
    /// This class should *not* ever be used for generating encryption keys of any type for production use.
    /// </remarks>
    public class Pseudorandom : IEquatable<Pseudorandom>
    {
        private static readonly long _startTickCount = Environment.TickCount;
        private static readonly AmbientStopwatch _stopwatch = AmbientStopwatch.StartNew();  // use this stopwatch to get seeds whose numbers change much faster than the tick count (which is generally every 15ms)
        private static long _rotator;      // interlocked

        /// <summary>
        /// Gets a thread-safe non-repeating seed to initialize a <see cref="Pseudorandom"/>, attempting to avoid using the same seed as much as possible.
        /// Uses timing information including the high frequency performance counter from <see cref="System.Diagnostics.Stopwatch"/> to get increase the randomness of the seed.
        /// </summary>
        private static ulong NextGlobalSeed
        {
            get
            {
                return (ulong)((_startTickCount + _stopwatch.ElapsedTicks) ^ System.Threading.Interlocked.Increment(ref _rotator));   // note that, yes, the stopwatch ticks are in different units than the environment ticks they're being added to, but we don't care about that here
            }
        }
        /// <summary>
        /// Gets a new <see cref="Pseudorandom"/> using a thread-safe seed generator.
        /// Because <see cref="Pseudorandom"/> is a value type, even getting a single random number through this property is efficient.
        /// </summary>
        public static Pseudorandom Next
        {
            get
            {
                return new Pseudorandom((int)NextGlobalSeed);
            }
        }

        private ulong _seed;

        /// <summary>
        /// Constructs a pseduorandom with a zero seed.
        /// </summary>
        public Pseudorandom()
        {
            _seed = 0;
        }
        /// <summary>
        /// Constructs a pseduorandom with the specified seed value.
        /// </summary>
        /// <param name="seed">The seed to use.</param>
        public Pseudorandom(long seed)
        {
            _seed = (ulong)seed;
        }
        /// <summary>
        /// Constructs a pseduorandom with the specified seed value.
        /// </summary>
        /// <param name="seed">The seed to use.</param>
        [CLSCompliant(false)]
        public Pseudorandom(ulong seed)
        {
            _seed = seed;
        }
        /// <summary>
        /// Clones this <see cref="Pseudorandom"/> such that identical calls on the clone will return the same pseudorandom data as this instance.
        /// </summary>
        /// <returns>A new <see cref="Pseudorandom"/> in the same state as this one.</returns>
        public Pseudorandom Clone()
        {
            return new Pseudorandom(_seed);
        }
        /// <summary>
        /// Constructs a pseduorandom with a seed generated from the system time combined with a global rotating number.
        /// </summary>
        /// <param name="generateSeed">Whether or not to create a seed using the current system time combined with a global rotating number.  If not true, uses a seed of zero.</param>
        public Pseudorandom(bool generateSeed)
        {
            _seed = generateSeed ? (uint)NextGlobalSeed : 0;
        }
        /// <summary>
        /// Gets the next <see cref="UInt64"/> based on the current seed.  Values will be roughly evenly distributed across all possible <see cref="ulong"/> values.
        /// </summary>
        [CLSCompliant(false)]
        public ulong NextUInt64
        {
            get
            {
                unchecked
                {
                    ulong x = ++_seed * 1_111_111_111_111_111_111UL;        // note that this is a prime number (but not a mersenne prime)
                    x = (((x & 0xaaaaaaaaaaaaaaaa) >> 1) | ((x & 0x5555555555555555) << 1));
                    x = (((x & 0xcccccccccccccccc) >> 2) | ((x & 0x3333333333333333) << 2));
                    x = (((x & 0xf0f0f0f0f0f0f0f0) >> 4) | ((x & 0x0f0f0f0f0f0f0f0f) << 4));
                    x = (((x & 0xff00ff00ff00ff00) >> 8) | ((x & 0x00ff00ff00ff00ff) << 8));
                    x = (((x & 0xffff0000ffff0000) >> 16)| ((x & 0x0000ffff0000ffff) << 16));
                    return ((x >> 32) | (x << 32));
                }
            }
        }
        /// <summary>
        /// Gets the next <see cref="Int64"/> based on the current seed.  Note that this may return a negative number.  Values will be roughly evenly distributed across all possible signed <see cref="long"/> values.
        /// </summary>
        public long NextInt64Signed
        {
            get
            {
                return (long)NextUInt64 * NextSignMultiplier;
            }
        }
        /// <summary>
        /// Gets the next positive <see cref="Int64"/> based on the current seed.  Values will be roughly evenly distributed across all non-negative <see cref="long"/> values.
        /// </summary>
        public long NextInt64
        {
            get
            {
                return (long)(NextUInt64 & 0x7fffffffffffffff);
            }
        }
        /// <summary>
        /// Gets the next positive <see cref="Int64"/> based on the current seed.  Values will be roughly evenly distributed across the full range of possible values, between zero and <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="maxValue">The possible maximum value (exclusive).</param>
        /// <returns>A random value between zero (inclusive) and <paramref name="maxValue"/> (exclusive).</returns>
        public long NextInt64Ranged(long maxValue)
        {
            return (long)((NextUInt64 & 0x7fffffffffffffffUL) % (ulong)maxValue);
        }
        /// <summary>
        /// Gets a random <see cref="Int64"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        public long NextInt64Ranged(long lowerLimit, long upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            ulong difference = (ulong)(upperLimit - lowerLimit);
            return (difference == 0) ? lowerLimit : (lowerLimit + (long)(NextUInt64 % difference));
        }
        /// <summary>
        /// Gets the next <see cref="UInt32"/> based on the current seed.  Values will be roughly evenly distributed across all possible <see cref="uint"/> values.
        /// </summary>
        [CLSCompliant(false)]
        public uint NextUInt32
        {
            get
            {
                unchecked
                {
                    uint x = (uint)(++_seed * 777_767_777);     // note that this is a prime number (but not a mersenne prime)
                    x = (((x & 0xaaaaaaaa) >> 1) | ((x & 0x55555555) << 1));
                    x = (((x & 0xcccccccc) >> 2) | ((x & 0x33333333) << 2));
                    x = (((x & 0xf0f0f0f0) >> 4) | ((x & 0x0f0f0f0f) << 4));
                    x = (((x & 0xff00ff00) >> 8) | ((x & 0x00ff00ff) << 8));
                    return ((x >> 16) | (x << 16));
                }
            }
        }
        /// <summary>
        /// Gets the next <see cref="Int32"/> based on the current seed.  Note that this may return a negative number.  Values will be roughly evenly distributed across all possible signed <see cref="int"/> values.
        /// </summary>
        public int NextInt32Signed
        {
            get
            {
                return (int)NextUInt32 * NextSignMultiplier;
            }
        }
        /// <summary>
        /// Gets the next positive <see cref="Int32"/> based on the current seed.  Values will be roughly evenly distributed across all non-negative <see cref="int"/> values.
        /// </summary>
        public int NextInt32
        {
            get
            {
                return (int)(NextUInt32 & 0x7fffffff);
            }
        }
        /// <summary>
        /// Gets the next positive <see cref="Int32"/> based on the current seed.  Values will be roughly evenly distributed across the full range of possible values, between zero and <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="maxValue">The possible maximum value (exclusive).</param>
        /// <returns>The selected value.</returns>
        public int NextInt32Ranged(int maxValue)
        {
            return (int)((NextUInt32 & 0x7fffffff) % maxValue);
        }
        /// <summary>
        /// Gets a random <see cref="Int32"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        public int NextInt32Ranged(int lowerLimit, int upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            uint difference = (uint)(upperLimit - lowerLimit);
            return (difference == 0) ? lowerLimit : (lowerLimit + (int)(NextUInt32 % difference));
        }




        /// <summary>
        /// Gets a random <see cref="UInt32"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        [CLSCompliant(false)]
        public uint NextUInt32Ranged(uint lowerLimit, uint upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            uint difference = upperLimit - lowerLimit;
            return (difference == 0) ? lowerLimit : (lowerLimit + NextUInt32 % difference);
        }
        /// <summary>
        /// Gets a random signed <see cref="Int32"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        public int NextInt32SignedRanged(int lowerLimit, int upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            uint difference = (uint)(upperLimit - lowerLimit);
            return (difference == 0) ? lowerLimit : (lowerLimit + (int)(NextUInt32 % difference));
        }
        /// <summary>
        /// Gets a random <see cref="UInt32"/> that is usually small.  All <see cref="uint"/> values are possible, but smaller values will be returned much more frequently.
        /// </summary>
        [CLSCompliant(false)]
        public uint NextUInt32UsuallySmall
        {
            get
            {
                return NextUInt32RangedUsuallySmall(uint.MaxValue);
            }
        }
        /// <summary>
        /// Gets a random <see cref="Int32"/> that is usually small.  All non-negative <see cref="int"/> values are possible, but smaller values will be returned much more frequently.
        /// </summary>
        public int NextInt32UsuallySmall
        {
            get
            {
                return NextInt32RangedUsuallySmall(int.MaxValue);
            }
        }
        /// <summary>
        /// Gets a random signed <see cref="Int32"/> that is usually small.  All <see cref="int"/> values are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        public int NextInt32SignedUsuallySmall
        {
            get
            {
                return NextInt32SignedRangedUsuallySmall(int.MinValue, int.MaxValue, 8);
            }
        }
        /// <summary>
        /// Gets a random <see cref="UInt32"/> that is usually small.  All <see cref="uint"/> values up to <paramref name="upperLimit"/> are possible, but smaller values will be returned much more frequently.
        /// </summary>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 8.  Values greater than 31 will result in a zero return value.</param>
        /// <returns>A random <see cref="UInt32"/> that is usually small.</returns>
        [CLSCompliant(false)]
        public uint NextUInt32RangedUsuallySmall(uint upperLimit, int iterations = 8)
        {
            uint baseAdjuster = NextUInt32;
            uint maxDivisor = (1U << iterations);
            uint divisor = (iterations > 31) ? int.MaxValue : (1U + (baseAdjuster % maxDivisor));
            ushort downShift = (ushort)(baseAdjuster % (iterations));
            uint baseNumber = NextUInt32Ranged(0, (uint)(upperLimit >> downShift));
            return baseNumber / divisor;
        }
        /// <summary>
        /// Gets a random <see cref="Int32"/> that is usually small.  All <see cref="int"/> values between <paramref name="lowerLimit"/> and <paramref name="upperLimit"/> are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        /// <param name="lowerLimit">The lower limit (inclusive).</param>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 8.  Values greater than 31 will result in a zero return value.</param>
        /// <returns>A random <see cref="Int32"/> that is usually nearer to zero.</returns>
        public int NextInt32SignedRangedUsuallySmall(int lowerLimit, int upperLimit, int iterations = 8)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            // always positive?
            if (lowerLimit >= 0)
            {
                return (int)(lowerLimit + NextUInt32RangedUsuallySmall((uint)(upperLimit - lowerLimit), iterations));
            }
            // always negative?
            if (upperLimit <= 0)
            {
                return (int)(upperLimit - 1 - NextUInt32RangedUsuallySmall((uint)(lowerLimit - upperLimit), iterations));
            }
            uint range = (uint)(upperLimit - lowerLimit);
            // pick a positive?
            if ((NextUInt32 * NextUInt32) % range < upperLimit)
            {
                return (int)NextUInt32RangedUsuallySmall((uint)upperLimit, iterations);
            }
            else // negative
            {
                return -(int)NextUInt32RangedUsuallySmall((uint)(-lowerLimit), iterations) - 1;
            }
        }
        /// <summary>
        /// Gets a random <see cref="Int32"/> that is usually small.  All non-negative <see cref="int"/> values up to <paramref name="upperLimit"/> are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 8.  Values greater than 31 will result in a zero return value.</param>
        /// <returns>A random <see cref="Int32"/> between zero and <paramref name="upperLimit"/> (exclusive), that is usually nearer to zero.</returns>
        public int NextInt32RangedUsuallySmall(int upperLimit, int iterations = 8)
        {
            return (int)NextUInt32RangedUsuallySmall((uint)upperLimit, iterations);
        }
        /// <summary>
        /// Gets a random <see cref="UInt64"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        [CLSCompliant(false)]
        public ulong NextUInt64Ranged(ulong lowerLimit, ulong upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            ulong difference = upperLimit - lowerLimit;
            return (difference == 0) ? lowerLimit : (lowerLimit + NextUInt64 % difference);
        }
        /// <summary>
        /// Gets a random signed <see cref="Int64"/> in the specified range.  Values will be roughly evenly distributed across all values between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).
        /// </summary>
        /// <param name="lowerLimit">The lower limit for the number, inclusive.</param>
        /// <param name="upperLimit">The upper limit for the number, exclusive.</param>
        /// <returns>A pseudorandom number between <paramref name="lowerLimit"/> (inclusive) and <paramref name="upperLimit"/> (exclusive).</returns>
        public long NextInt64SignedRanged(long lowerLimit, long upperLimit)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            ulong difference = (ulong)(upperLimit - lowerLimit);
            return (difference == 0) ? lowerLimit : (lowerLimit + (long)(NextUInt64 % difference));
        }
        /// <summary>
        /// Gets a random <see cref="UInt64"/> that is usually small.  All <see cref="ulong"/> values are possible, but smaller values will be returned much more frequently.
        /// </summary>
        [CLSCompliant(false)]
        public ulong NextUInt64UsuallySmall
        {
            get
            {
                return NextUInt64RangedUsuallySmall(ulong.MaxValue);
            }
        }
        /// <summary>
        /// Gets a random <see cref="Int64"/> that is usually small.  All non-negative <see cref="long"/> values are possible, but smaller values will be returned much more frequently.
        /// </summary>
        public long NextInt64UsuallySmall
        {
            get
            {
                return NextInt64RangedUsuallySmall(long.MaxValue);
            }
        }
        /// <summary>
        /// Gets a random <see cref="Int64"/> that is usually small.  All <see cref="long"/> values are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        public long NextInt64SignedUsuallySmall
        {
            get 
            { 
                return NextInt64SignedRangedUsuallySmall(long.MinValue, long.MaxValue); 
            }
        }
        /// <summary>
        /// Gets a random <see cref="UInt64"/> that is usually small.  All <see cref="ulong"/> values up to <paramref name="upperLimit"/> are possible, but smaller values will be returned much more frequently.
        /// </summary>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 20.  Values greater than 63 will result in a zero return value.</param>
        /// <returns>A random <see cref="UInt64"/> that is usually small.</returns>
        [CLSCompliant(false)]
        public ulong NextUInt64RangedUsuallySmall(ulong upperLimit, int iterations = 20)
        {
            ulong baseAdjuster = NextUInt64;
            uint maxDivisor = (1U << iterations);
            ulong divisor = (iterations > 63) ? long.MaxValue : (1U + (baseAdjuster % maxDivisor));
            ushort downShift = (ushort)(baseAdjuster % (uint)iterations);
            ulong baseNumber = NextUInt64Ranged(0, (ulong)(upperLimit >> downShift));
            return baseNumber / divisor;
        }
        /// <summary>
        /// Gets a random <see cref="Int64"/> that is usually small.  All <see cref="int"/> values between <paramref name="lowerLimit"/> and <paramref name="upperLimit"/> are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        /// <param name="lowerLimit">The lower limit (inclusive).</param>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 20.  Values greater than 63 will result in a zero return value.</param>
        /// <returns>A random <see cref="Int64"/> that is usually nearer to zero.</returns>
        public long NextInt64SignedRangedUsuallySmall(long lowerLimit, long upperLimit, int iterations = 20)
        {
            if (upperLimit < lowerLimit) throw new ArgumentException("The upper limit must be higher than the lower limit!", nameof(upperLimit));
            // always positive?
            if (lowerLimit >= 0)
            {
                return lowerLimit + (long)NextUInt64RangedUsuallySmall((ulong)(upperLimit - lowerLimit), iterations);
            }
            // always negative?
            if (upperLimit <= 0)
            {
                return upperLimit - 1 - (long)NextUInt64RangedUsuallySmall((ulong)(lowerLimit - upperLimit), iterations);
            }
            ulong range = (ulong)(upperLimit - lowerLimit);
            // pick a positive?
            if ((NextUInt64 * NextUInt64) % range < (ulong)upperLimit)
            {
                return (long)NextUInt64RangedUsuallySmall((ulong)upperLimit, iterations);
            }
            else // negative
            {
                return -(long)NextUInt64RangedUsuallySmall((ulong)(-lowerLimit), iterations) - 1;
            }
        }
        /// <summary>
        /// Gets a random <see cref="Int64"/> that is usually small.  All non-negative <see cref="long"/> values up to <paramref name="upperLimit"/> are possible, but values closer to zero will be returned much more frequently.
        /// </summary>
        /// <param name="upperLimit">The upper limit (exclusive).</param>
        /// <param name="iterations">The number of iterations (more iterations means more preference for the small numbers).  Defaults to 20.  Values greater than 63 will result in a zero return value.</param>
        /// <returns>A random <see cref="Int64"/> between zero and <paramref name="upperLimit"/> (exclusive), that is usually nearer to zero.</returns>
        public long NextInt64RangedUsuallySmall(long upperLimit, int iterations = 20)
        {
            return (long)NextUInt64RangedUsuallySmall((ulong)upperLimit, iterations);
        }


        /// <summary>
        /// Gets a random sign multiplier.  Values returned should be 1 roughly 50% of the time and -1 roughly 50% of the time.
        /// </summary>
        public int NextSignMultiplier
        {
            get { return (2 * (int)NextUInt32Ranged(0, 2)) - 1; }
        }
        /// <summary>
        /// Gets a random boolean, either true or false, with a roughly even distribution between those values.
        /// </summary>
        public bool NextBoolean
        {
            get { return NextUInt32Ranged(0, 2) == 0; }
        }
        /// <summary>
        /// Returns an array of the specified length populated with random values.
        /// </summary>
        /// <param name="length">The number of random bytes to put in the returned array.</param>
        /// <returns>An array of bytes of the specified length populated with random byte values.</returns>
        public byte[] NextNewBytes(int length)
        {
            byte[] bytes = new byte[length];
            NextBytes(bytes);
            return bytes;
        }
        /// <summary>
        /// Populates an existing byte array with random values.
        /// </summary>
        /// <param name="target">The target array of bytes.</param>
        /// <param name="offset">The offset in the byte array to begin filling with random bytes.</param>
        /// <param name="length">The number of random bytes to put in the returned array.</param>
        /// <returns>An array of bytes of the specified length populated with random byte values.</returns>
        public void NextBytes(byte[] target, int offset = 0, int length = -1)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            int index = 0;
            ulong rawData;
            for (int endOffset = (length < 0 || offset + length > target.Length) ? target.Length : (offset + length); offset < endOffset; offset += 8, index = ((index + 1) % 8))
            {
                rawData = NextUInt32;
                target[offset] = (byte)(rawData & 0xff);
                if (offset + 1 < endOffset) target[offset + 1] = (byte)((rawData & 0xff00) >> 8);
                if (offset + 2 < endOffset) target[offset + 2] = (byte)((rawData & 0xff0000) >> 16);
                if (offset + 3 < endOffset) target[offset + 3] = (byte)((rawData & 0xff000000) >> 24);
                if (offset + 4 < endOffset) target[offset + 4] = (byte)((rawData & 0xff00000000) >> 32);
                if (offset + 5 < endOffset) target[offset + 5] = (byte)((rawData & 0xff0000000000) >> 40);
                if (offset + 6 < endOffset) target[offset + 6] = (byte)((rawData & 0xff000000000000) >> 48);
                if (offset + 7 < endOffset) target[offset + 7] = (byte)((rawData & 0xff00000000000000) >> 56);
            }
        }
        const int DecimalMaxScalePlusOne = 29;
        /// <summary>
        /// Gets a <see cref="Decimal"/> with a random value.  All possible values should be roughly evenly distributed.
        /// </summary>
        public Decimal NextDecimal
        {
            get
            {
                return new Decimal(NextInt32Signed, NextInt32Signed, NextInt32Signed, NextBoolean, (byte)(NextUInt32 % DecimalMaxScalePlusOne));
            }
        }
        /// <summary>
        /// Gets a <see cref="Double"/> with a random value.  All possible values should be roughly evenly distributed, including special values like <see cref="Double.NaN"/>, <see cref="Double.Epsilon"/>, <see cref="Double.PositiveInfinity"/>, etc.
        /// </summary>
        public Double NextDouble
        {
            get
            {
                return BitConverter.Int64BitsToDouble(NextInt64Signed);
            }
        }
        /// <summary>
        /// Gets a <see cref="Guid"/> with a random value.  All possible values should be roughly evenly distributed.  
        /// Note that these "GUID"s should *not* be used publicly where GUIDs are expected to have certain characteristics to prevent collisions.
        /// These GUIDs should only be used for testing.
        /// </summary>
        public Guid NextGuid
        {
            get
            {
                byte[] bytes = NextNewBytes(16);
                return new Guid(bytes);
            }
        }
        /// <summary>
        /// Gets a random value for an enum of the specified type.  All possible values should be roughly evenly distributed.  
        /// If the specified enum type is marked with <see cref="FlagsAttribute"/>, a random set of possible values are combined.
        /// </summary>
        /// <param name="type">The type for the enum a random value is to be selected for.  Must be a non-null enum type.</param>
        /// <returns>A random value for that enum.</returns>
        public object NextEnum(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!(typeof(System.Enum)).IsAssignableFrom(type)) throw new ArgumentException("The type must be an enum type!", nameof(type));
            FieldInfo[] enumValues = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            // is this a flags type?
            if (type.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0)
            {
                // build a randomized sequential array
                int[] enumValueSelectorIndex = new int[enumValues.Length];
                for (int value = 0; value < enumValues.Length; ++value)
                {
                    enumValueSelectorIndex[value] = value;
                }
                for (int value = 0; value < enumValues.Length * 2; ++value)
                {
                    int pick1 = value % enumValues.Length;
                    int pick2 = NextInt32 % enumValues.Length;
                    int temp = enumValueSelectorIndex[pick1];
                    enumValueSelectorIndex[pick1] = enumValueSelectorIndex[pick2];
                    enumValueSelectorIndex[pick2] = temp;
                }
                // build a string containing random flag values
                string enumStringValue = string.Empty;
                int values = NextInt32 % enumValues.Length;
                // no values specified--use default value (0)
                if (values == 0)
                {
                    return 0;
                }
                for (int value = 0; value < values; ++value)
                {
                    // Coverage note: the optimizer seems to insert a huge amount of code here with at least one branch, despite there being no branching logic here, so I'm not sure how to get full coverage here.  perhaps it's inlining something or unwrapping one of the loops?
                    enumStringValue = "," + enumValues[enumValueSelectorIndex[value]].GetValue(type);
                }
                // have the CLR parse that value and give us a typed enum back
                return Enum.Parse(type, enumStringValue.Substring(1));
            }
            return enumValues[NextUInt32 % enumValues.Length].GetValue(type)!;  // enum field values better not be null!
        }
        /// <summary>
        /// Gets a random typed enum value for a specific enum type.  Values will be roughly evenly distributed across all possible values.
        /// If the specified enum type is marked with <see cref="FlagsAttribute"/>, a random set of possible values are combined.
        /// </summary>
        /// <typeparam name="TEnum">The type of enum to get a random value for.</typeparam>
        /// <returns>A random value for that enum.</returns>
        public TEnum NextEnum<TEnum>()
        {
            return (TEnum)NextEnum(typeof(TEnum));
        }





        /// <summary>
        /// Gets a 32-bit hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return _seed.GetHashCode();
        }
        /// <summary>
        /// Checks to see if the specified object is logically equal to this one.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>true if <paramref name="obj"/> is logically equal to this instance, false if it is not.</returns>
        public override bool Equals(object? obj)
        {
            if (!(obj is Pseudorandom)) return false;
            return Equals((Pseudorandom)obj);
        }
        /// <summary>
        /// Checks to see if the specified Pseudorandom is logically equal to this one.
        /// </summary>
        /// <param name="other">The Pseudorandom to compare to.</param>
        /// <returns>true if <paramref name="other"/> is logically equal to this instance, false if it is not.</returns>
        public bool Equals(Pseudorandom? other)
        {
            if (ReferenceEquals(other, null)) return false;
            return _seed.Equals(other._seed);
        }
        /// <summary>
        /// Checks to see if two Pseudorandoms are logically equal.
        /// </summary>
        /// <param name="a">The first Pseudorandom to compare.</param>
        /// <param name="b">The second Pseudorandom to compare.</param>
        /// <returns>true if the Pseudorandoms are the same, otherwise false.</returns>
        public static bool operator ==(Pseudorandom? a, Pseudorandom? b)
        {
            if (Object.ReferenceEquals(a, b)) return true;
            if (Object.ReferenceEquals(a, null) || Object.ReferenceEquals(b, null)) return false;
            return a._seed == b._seed;
        }
        /// <summary>
        /// Checks to see if two Pseudorandoms are logically not equal.
        /// </summary>
        /// <param name="a">The first Pseudorandom to compare.</param>
        /// <param name="b">The second Pseudorandom to compare.</param>
        /// <returns>true if the Pseudorandoms are logically not equal, otherwise false.</returns>
        public static bool operator !=(Pseudorandom? a, Pseudorandom? b)
        {
            if (Object.ReferenceEquals(a, b)) return false;
            if (Object.ReferenceEquals(a, null) || Object.ReferenceEquals(b, null)) return true;
            return a._seed != b._seed;
        }
    }
}
