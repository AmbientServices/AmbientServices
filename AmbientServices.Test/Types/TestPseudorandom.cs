using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestPseudorandom
    {
        [TestMethod]
        public void PseudorandomMultithreadedDistributionUInt32()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            int count = 655360;
            Parallel.For(0, count,
                i =>
                {
                    ++distribution[Pseudorandom.Next.NextUInt32 >> 16];
                }
            );
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            double df = AverageFrequencyDecreaseFactor(distribution);
            Assert.IsTrue(df < 1000, df.ToString());
        }
        [TestMethod]
        public void PseudorandomSerialDistributionUInt32()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = Pseudorandom.Next;
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextUInt32 >> 16];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);

            distribution = new int[ushort.MaxValue + 1];
            rand = Pseudorandom.Next;
            count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextUInt32 & 0xffff)];
            }
            // compute the standard deviation
            standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);
        }
        [TestMethod]
        public void PseudorandomSerialDistributionInt32()
        {
            int[] distribution = new int[short.MaxValue + 1];
            Pseudorandom rand = Pseudorandom.Next;
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextInt32 >> 16];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);

            distribution = new int[ushort.MaxValue + 1];
            rand = Pseudorandom.Next;
            count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextInt32 & 0xffff)];
            }
            // compute the standard deviation
            standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);
        }
        [TestMethod]
        public void PseudorandomMultithreadedDistributionUInt64()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            int count = 655360;
            Parallel.For(0, count,
                i =>
                {
                    ++distribution[(int)(Pseudorandom.Next.NextUInt64 >> 48)];
                }
            );
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 200);
        }
        [TestMethod]
        public void PseudorandomSerialDistributionUInt64()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = Pseudorandom.Next;
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextUInt64 >> 48)];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);

            distribution = new int[ushort.MaxValue + 1];
            rand = Pseudorandom.Next;
            count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextUInt64 & 0xffff)];
            }
            // compute the standard deviation
            standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);
        }
        [TestMethod]
        public void PseudorandomSerialDistributionInt64()
        {
            int[] distribution = new int[short.MaxValue + 1];
            Pseudorandom rand = Pseudorandom.Next;
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextInt64 >> 48)];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);

            distribution = new int[ushort.MaxValue + 1];
            rand = Pseudorandom.Next;
            count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[(int)(rand.NextUInt64 & 0xffff)];
            }
            // compute the standard deviation
            standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation < 25.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) < 2);
        }

        private static double StandardDeviation(IEnumerable<int> values, int count, double mean)
        {
            return Math.Sqrt(values.Select(x => ((double)x - mean) * ((double)x - mean)).Sum() / count);
        }
        private static double AverageFrequencyDecreaseFactor(IReadOnlyList<int> values, int chunkSize = 64)
        {
            int[] summedCounts = new int[values.Count / chunkSize];
            for (int loop = 0; loop < summedCounts.Length; ++loop)
            {
                for (int c = 0; c < chunkSize && loop * chunkSize + c < values.Count; ++c)
                {
                    summedCounts[loop] += values[loop * chunkSize + c];
                }
            }
            double squareDiffTotal = 0.0;
            for (int loop = 0; loop < summedCounts.Length - 1; ++loop)
            {
                double squareDiff = 1.0 * (summedCounts[loop] - summedCounts[loop + 1]) * 1.0 * (summedCounts[loop] - summedCounts[loop + 1]);
                if (summedCounts[loop] < summedCounts[loop + 1]) squareDiff = -squareDiff;
                squareDiffTotal += squareDiff;
            }
            return squareDiffTotal / (1 + summedCounts.Length);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionUInt32()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextUInt32UsuallySmall >> 16];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 200.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionUInt64()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextUInt64UsuallySmall >> 48];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 200.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionInt32Default()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextInt32UsuallySmall >> 16];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 5.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionInt32Ranged()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextInt32UsuallySmall >> 16];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 200.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionInt64Default()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextInt64UsuallySmall >> 48];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 5.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomSerialUsuallySmallDistributionInt64Ranged()
        {
            int[] distribution = new int[ushort.MaxValue + 1];
            Pseudorandom rand = new Pseudorandom(4387617);
            int count = 655360;
            for (int loop = 0; loop < count; ++loop)
            {
                ++distribution[rand.NextInt64UsuallySmall >> 48];
            }
            // compute the standard deviation
            double standardDeviation = StandardDeviation(distribution, distribution.Length, (double)distribution.Length / count);
            Assert.IsTrue(standardDeviation > 200.0);

            Assert.IsTrue(AverageFrequencyDecreaseFactor(distribution) > 1000);
        }

        [TestMethod]
        public void PseudorandomRanged()
        {
            Pseudorandom rand = new Pseudorandom(4387617);
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt32Ranged(10, 9));
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt32SignedRanged(0, -100));
            Assert.ThrowsException<ArgumentException>(() => rand.NextUInt32Ranged(10, 9));
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt64Ranged(10, 9));
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt64SignedRanged(0, -100));
            Assert.ThrowsException<ArgumentException>(() => rand.NextUInt64Ranged(10, 9));
            Assert.AreEqual(9, rand.NextInt32Ranged(9, 9));
            Assert.AreEqual(-1, rand.NextInt32SignedRanged(-1, -1));
            Assert.AreEqual(9U, rand.NextUInt32Ranged(9, 9));
            Assert.AreEqual(9L, rand.NextInt64Ranged(9, 9));
            Assert.AreEqual(-1L, rand.NextInt64SignedRanged(-1, -1));
            Assert.AreEqual(9UL, rand.NextUInt64Ranged(9, 9));
        }
        [TestMethod]
        public void PseudorandomSigned()
        {
            int loops = 10000;
            double regularAbsSum;
            double regularSum;
            double smallAbsSum;
            double smallSum;
            Pseudorandom rand;

            regularAbsSum = 0;
            regularSum = 0;
            smallAbsSum = 0;
            smallSum = 0;
            rand = new Pseudorandom(4387617);
            for (int loop = 0; loop < loops; ++loop)
            {
                int normal = rand.NextInt32Signed;
                regularAbsSum += Math.Abs(normal);
                regularSum += normal;
                int small = rand.NextInt32SignedUsuallySmall;
                smallAbsSum += Math.Abs(small);
                smallSum += small;
            }
            Assert.IsTrue(regularAbsSum / loops > int.MaxValue / 4);
            Assert.IsTrue(Math.Abs(regularSum) / loops / int.MaxValue < .01);
            Assert.IsTrue(Math.Abs(regularSum / regularAbsSum) < .01);
            Assert.IsTrue(Math.Abs(smallAbsSum / regularAbsSum) < .1);
            Assert.IsTrue(smallAbsSum / loops > int.MaxValue / 2_000);
            Assert.IsTrue(Math.Abs(smallSum) / loops < int.MaxValue / 2_000);
            Assert.IsTrue(Math.Abs(smallSum / smallAbsSum) < .2);

            regularAbsSum = 0;
            regularSum = 0;
            smallAbsSum = 0;
            smallSum = 0;
            rand = new Pseudorandom(4387617);
            for (int loop = 0; loop < loops; ++loop)
            {
                long normal = rand.NextInt64Signed;
                regularAbsSum += Math.Abs(normal);
                regularSum += normal;
                long small = rand.NextInt64SignedUsuallySmall;
                smallAbsSum += Math.Abs(small);
                smallSum += small;
            }
            Assert.IsTrue(regularAbsSum / loops > long.MaxValue / 4);
            Assert.IsTrue(Math.Abs(regularSum) / loops / long.MaxValue < .01);
            Assert.IsTrue(Math.Abs(regularSum / regularAbsSum) < .01);
            Assert.IsTrue(Math.Abs(smallAbsSum / regularAbsSum) < .1);
            Assert.IsTrue(smallAbsSum / loops > long.MaxValue / 5_000_000);
            Assert.IsTrue(Math.Abs(smallSum) / loops < long.MaxValue / 5_000_000);
            Assert.IsTrue(Math.Abs(smallSum / smallAbsSum) < .5);
        }

        enum NormalEnum
        {
            Value1,
            Value2,
        }
        [Flags]
        enum FlagsEnum
        {
            None = 0,
            Bit1 = 1,
            Bit2 = 2,
            Bit3 = 4,
            Bit4 = 8,
            Bit5 = 16,
            Bit6 = 32,
            Bit7 = 64,
            Bit8 = 128,
        }

        [TestMethod]
        public void PseudorandomDataGenerator()
        {
            for (int seed = 0; seed < 4000; seed += 37)
            {
                int multiplier = Pseudorandom.Next.NextSignMultiplier;
            }
            Int32 i32 = Pseudorandom.Next.NextInt32Signed;
            Int64 i64 = Pseudorandom.Next.NextInt64Signed;
            UInt32 ui32 = Pseudorandom.Next.NextUInt32;
            UInt64 ui64 = Pseudorandom.Next.NextUInt64;
            Decimal d = Pseudorandom.Next.NextDecimal;
            i32 = Pseudorandom.Next.NextInt32Ranged(0, 78902);
            i32 = Pseudorandom.Next.NextInt32Ranged(-92, 78902);
            i32 = Pseudorandom.Next.NextInt32Ranged(1, 78902);
            i32 = Pseudorandom.Next.NextInt32Ranged(-92, 78902);
            i32 = Pseudorandom.Next.NextInt32SignedRanged(0, 78902);
            i32 = Pseudorandom.Next.NextInt32SignedRanged(-92, 78902);
            i32 = Pseudorandom.Next.NextInt32SignedRanged(1, 78902);
            i32 = Pseudorandom.Next.NextInt32SignedRanged(-92, 78902);
            ui32 = Pseudorandom.Next.NextUInt32Ranged(0, 78902);
            ui32 = Pseudorandom.Next.NextUInt32Ranged(92, 78902);
            ui32 = Pseudorandom.Next.NextUInt32Ranged(1, 78902);
            ui32 = Pseudorandom.Next.NextUInt32Ranged(92, 78902);
            i64 = Pseudorandom.Next.NextInt64Ranged(0, 48902432L);
            i64 = Pseudorandom.Next.NextInt64Ranged(-1849302423554L, 48902432L);
            i64 = Pseudorandom.Next.NextInt64Ranged(1, 48902432L);
            i64 = Pseudorandom.Next.NextInt64Ranged(-1849302423554L, 48902432L);
            i64 = Pseudorandom.Next.NextInt64SignedRanged(0, 48902432L);
            i64 = Pseudorandom.Next.NextInt64SignedRanged(-1849302423554L, 48902432L);
            i64 = Pseudorandom.Next.NextInt64SignedRanged(1, 48902432L);
            i64 = Pseudorandom.Next.NextInt64SignedRanged(-1849302423554L, 48902432L);
            ui64 = Pseudorandom.Next.NextUInt64Ranged(0, 57890234324L);
            ui64 = Pseudorandom.Next.NextUInt64Ranged(5434L, 57890234324L);
            ui64 = Pseudorandom.Next.NextUInt64Ranged(1, 57890234324L);
            ui64 = Pseudorandom.Next.NextUInt64Ranged(5434L, 57890234324L);
            ui64 = Pseudorandom.Next.NextUInt64Ranged(5434L, 5434L);
            byte[] bytes = new byte[100];
            Pseudorandom.Next.NextBytes(bytes);
            Pseudorandom.Next.NextBytes(bytes, 50, 75);
            Pseudorandom.Next.NextBytes(bytes, 10, 50);
        }
        [TestMethod]
        public void PseudorandomSeededRand()
        {
            for (int seed = 0; seed < 4892; seed += 17)
            {
                Pseudorandom rand = (seed == 0) ? new Pseudorandom() : new Pseudorandom(seed - 1);
                Pseudorandom clone = rand.Clone();
                Assert.AreEqual(rand.NextInt32Signed, clone.NextInt32Signed);
                Assert.AreEqual(rand.NextInt64Signed, clone.NextInt64Signed);
                Assert.AreEqual(rand.NextBoolean, clone.NextBoolean);
                Assert.AreEqual(rand.NextDouble, clone.NextDouble);
                Assert.AreEqual(rand.NextGuid, clone.NextGuid);
                Assert.AreEqual(rand.NextInt32Signed, clone.NextInt32Signed);
                Assert.AreEqual(rand.NextInt64Signed, clone.NextInt64Signed);
                Assert.AreEqual(rand.NextSignMultiplier, clone.NextSignMultiplier);
                Assert.AreEqual(rand.NextUInt32, clone.NextUInt32);
                Assert.AreEqual(rand.NextUInt64, clone.NextUInt64);
                Assert.AreEqual(rand.NextInt32UsuallySmall, clone.NextInt32UsuallySmall);
                Assert.AreEqual(rand.NextInt64SignedUsuallySmall, clone.NextInt64SignedUsuallySmall);
                Assert.AreEqual(rand.GetHashCode(), clone.GetHashCode());
                Assert.AreEqual(rand.NextEnum(typeof(NormalEnum)), clone.NextEnum(typeof(NormalEnum)));
                Assert.AreEqual(rand.NextEnum<FlagsEnum>(), clone.NextEnum<FlagsEnum>());
                Assert.AreEqual(rand.NextInt32Ranged(54789), clone.NextInt32Ranged(54789));
                Assert.AreEqual(rand.NextInt32Ranged(54789, 285702), clone.NextInt32Ranged(54789, 285702));
                Assert.AreEqual(rand.NextInt64Ranged(8902432), clone.NextInt64Ranged(8902432));
                Assert.AreEqual(rand.NextInt64Ranged(8902432, 5427890480323L), clone.NextInt64Ranged(8902432, 5427890480323L));
                Assert.IsTrue(ArrayExtensions.ValueEquals(rand.NextNewBytes(5), clone.NextNewBytes(5)));
                Assert.AreEqual(rand.NextInt32RangedUsuallySmall(2), clone.NextInt32RangedUsuallySmall(2));
                Assert.AreEqual(rand.NextInt32RangedUsuallySmall(90432, 2), clone.NextInt32RangedUsuallySmall(90432, 2));
                Assert.AreEqual(rand.NextInt64RangedUsuallySmall(2), clone.NextInt64RangedUsuallySmall(2));
                Assert.AreEqual(rand.NextInt64RangedUsuallySmall(90464, 2), clone.NextInt64RangedUsuallySmall(90464, 2));
                Assert.AreEqual(rand.ToString(), clone.ToString());
            }
        }
        [TestMethod]
        public void PseudorandomEquality()
        {
            Pseudorandom rand = new Pseudorandom(5);
            Assert.AreEqual(new Pseudorandom(5).GetHashCode(), rand.GetHashCode());
            Assert.IsFalse(rand.Equals(this));
            Assert.IsFalse(rand.Equals(null));
            Pseudorandom clone = rand!.Clone(); // not sure how the analyzer gets confused here?  maybe it's seeing the Equals(null) above and getting confused about that
            Assert.IsTrue(rand.Equals(clone));
            Assert.IsTrue(rand.Equals((object)clone));
            Pseudorandom rand2 = new Pseudorandom(6);
            Assert.IsFalse(rand == rand2);
            Assert.IsTrue(rand != rand2);
        }
        [TestMethod]
        public void PseudorandomSeeds()
        {
            Pseudorandom rand = new Pseudorandom(true);
            int matches = 0;
            for (int attempt = 0; attempt < 10; ++attempt)
            {
                matches += (new Pseudorandom(true) == rand) ? 1 : 0;
                System.Threading.Thread.Sleep(15);
            }
            Assert.IsTrue(matches < 2); // there is some randomness due to the time factor integrated into the global seed generator, which could cause the seeds to be the same in rare cases

            Assert.AreNotEqual(new Pseudorandom(false), rand);
        }
        [TestMethod]
        public void PseudorandomExceptions()
        {
            Pseudorandom rand = new Pseudorandom(false);
            Assert.ThrowsException<ArgumentNullException>(() => rand.NextEnum(null!));
            Assert.ThrowsException<ArgumentNullException>(() => rand.NextBytes(null!, 1));
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt32SignedRangedUsuallySmall(10, 5));
            Assert.ThrowsException<ArgumentException>(() => rand.NextInt64SignedRangedUsuallySmall(10, 5));
            Assert.ThrowsException<ArgumentException>(() => rand.NextEnum(typeof(int)));
        }
        [TestMethod]
        public void PseudorandomUsuallyVerySmall()
        {
            int loops = 1000;
            Pseudorandom rand;

            rand = new Pseudorandom(4387617);
            for (int loop = 0; loop < loops; ++loop)
            {
                uint ui = rand.NextUInt32RangedUsuallySmall(uint.MaxValue, 37);
                ulong ul = rand.NextUInt64RangedUsuallySmall(uint.MaxValue, 67);
            }
        }
        [TestMethod]
        public void PseudorandomSignedAlwaysNegative()
        {
            int loops = 10000;
            Pseudorandom rand;

            rand = new Pseudorandom(4387617);
            for (int loop = 0; loop < loops; ++loop)
            {
                int i = rand.NextInt32SignedRangedUsuallySmall(int.MinValue, -1);
                Assert.IsTrue(i < 0);
                long l = rand.NextInt64SignedRangedUsuallySmall(long.MinValue, -1);
                Assert.IsTrue(l < 0);
            }
        }
        [TestMethod]
        public void PseudorandomSignedAlwaysPositive()
        {
            int loops = 10000;
            Pseudorandom rand;

            rand = new Pseudorandom(4387617);
            for (int loop = 0; loop < loops; ++loop)
            {
                int i = rand.NextInt32SignedRangedUsuallySmall(1, int.MaxValue);
                Assert.IsTrue(i > 0);
                long l = rand.NextInt64SignedRangedUsuallySmall(1, long.MaxValue);
                Assert.IsTrue(l > 0);
            }
        }
        [TestMethod]
        public void PseudorandomNullReferences()
        {
            Pseudorandom a = new Pseudorandom(0);
            Pseudorandom b = new Pseudorandom(0);
            Pseudorandom c = new Pseudorandom(1);
            Pseudorandom n = null!;
            Assert.IsTrue(a == b);
            Assert.IsFalse(a == c);
            Assert.IsFalse(a == n);
            Assert.IsFalse(n == a);
            Assert.IsTrue(n == null);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a != c);
            Assert.IsFalse(n != null);
            Assert.IsTrue(a != n);
            Assert.IsTrue(n != a);
        }
    }
}
