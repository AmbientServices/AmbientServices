using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace AmbientServices.Test
{
    [TestClass]
    public class TestInterlockedUtilities
    {
        [TestMethod]
        public void InterlockedMaxLong()
        {
            long x = 0;
            Assert.AreEqual(0, InterlockedUtilities.TryOptomisticMax(ref x, -1));

            x = 0;
            Assert.AreEqual(1, InterlockedUtilities.TryOptomisticMax(ref x, 1));
        }

        [TestMethod]
        public void InterlockedMinLong()
        {
            long x = 0;
            Assert.AreEqual(0, InterlockedUtilities.TryOptomisticMin(ref x, 1));

            x = 0;
            Assert.AreEqual(-1, InterlockedUtilities.TryOptomisticMin(ref x, -1));
        }
        [TestMethod]
        public void TryAgainAfterOptomisticMissDelay()
        {
            Assert.IsTrue(InterlockedUtilities.TryAgainAfterOptomisticMissDelay(0));
            Assert.IsTrue(InterlockedUtilities.TryAgainAfterOptomisticMissDelay(3));
            Assert.IsTrue(InterlockedUtilities.TryAgainAfterOptomisticMissDelay(5));
            Assert.IsFalse(InterlockedUtilities.TryAgainAfterOptomisticMissDelay(10));
        }
        [TestMethod]
        public void InterlockedMaxDouble()
        {
            double x = 0;
            Assert.AreEqual(0, InterlockedUtilities.TryOptomisticMax(ref x, -1));

            x = 0;
            Assert.AreEqual(1, InterlockedUtilities.TryOptomisticMax(ref x, 1));
        }

        [TestMethod]
        public void InterlockedMinDouble()
        {
            double x = 0;
            Assert.AreEqual(0, InterlockedUtilities.TryOptomisticMin(ref x, 1));

            x = 0;
            Assert.AreEqual(-1, InterlockedUtilities.TryOptomisticMin(ref x, -1));
        }
        [TestMethod]
        public void InterlockedAddDouble()
        {
            double x = double.MaxValue - 5;
            Assert.AreEqual(double.MaxValue, InterlockedUtilities.TryOptomisticAdd(ref x, 5));
        }
        [TestMethod]
        public void InterlockedExponentialMovingAverage()
        {
            double x = 0;
            Assert.AreEqual(0.0, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.5, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.5, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.75, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.75, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.875, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.875, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));

            x = 0;
            Assert.AreEqual(0.75, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 2.0, 1.0));
            Assert.AreEqual(0.9375, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, 2.0, 1.0));

            x = 0;
            double halfHalfLife = Math.Log(4.0 / 3.0) / Math.Log(2.0);
            Assert.AreEqual(0.25, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, halfHalfLife, 1.0));
            Assert.AreEqual(0.4375, InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, halfHalfLife, 1.0));

            Assert.Throws<ArgumentOutOfRangeException>(() => InterlockedUtilities.TryOptomisticAddExponentialMovingAverageSample(ref x, -0.0000001, 1.0));
        }
        const int ThreadCount = 10;
        const int LoopCount = 10000;
        [TestMethod]
        public void InterlockedExtensionsHammer()
        {
            double addDouble = 0;
            Hammer(nameof(Single), ThreadCount, () =>
            {
                for (double i = 0; i < LoopCount; ++i)
                {
                    double newDouble = InterlockedUtilities.TryOptomisticAdd(ref addDouble, 1);
                    if (newDouble < 0 || newDouble > LoopCount * ThreadCount + 1) return $"{newDouble}";
                }
                return null;
            }, TimeSpan.FromMinutes(1));
            double maxDouble = 0;
            Hammer(nameof(Double), ThreadCount, () =>
            {
                for (double i = 0; i < LoopCount; ++i)
                {
                    double newMaxDouble = InterlockedUtilities.TryOptomisticMax(ref maxDouble, i);
                    if (newMaxDouble < 0 || newMaxDouble > LoopCount) return $"{newMaxDouble}";
                }
                return null;
            }, TimeSpan.FromMinutes(1));
            double minDouble = LoopCount;
            Hammer(nameof(Double), ThreadCount, () =>
            {
                for (double i = 0; i < LoopCount; ++i)
                {
                    double newMinDouble = InterlockedUtilities.TryOptomisticMin(ref minDouble, LoopCount - i);
                    if (newMinDouble < 0 || newMinDouble > LoopCount) return $"{newMinDouble}";
                }
                return null;
            }, TimeSpan.FromMinutes(1));
            long maxInt64 = 0;
            Hammer(nameof(Int64), ThreadCount, () =>
            {
                for (long i = 0; i < LoopCount; ++i)
                {
                    long newMaxInt64 = InterlockedUtilities.TryOptomisticMax(ref maxInt64, i);
                    if (newMaxInt64 < 0 || newMaxInt64 > LoopCount) return $"{newMaxInt64}";
                }
                return null;
            }, TimeSpan.FromMinutes(1));
            long minInt64 = LoopCount;
            Hammer(nameof(Int64), ThreadCount, () =>
            {
                for (long i = 0; i < LoopCount; ++i)
                {
                    long newMinInt64 = InterlockedUtilities.TryOptomisticMin(ref minInt64, LoopCount - i);
                    if (newMinInt64 < 0 || newMinInt64 > LoopCount) return $"{newMinInt64}";
                }
                return null;
            }, TimeSpan.FromMinutes(1));
        }

        private static void Hammer(string typeName, int threadCount, Func<string?> f, TimeSpan timeout)
        {
            using ManualResetEvent continueEvent = new(false);
            string?[] results = new string?[threadCount];
            Thread[] threads = new Thread[threadCount];
            ManualResetEvent[] startedEvent = new ManualResetEvent[threadCount];
            for (int t = 0; t < threads.Length; ++t)
            {
                int index = t;
                startedEvent[index] = new ManualResetEvent(false);
                threads[index] = new Thread(new ThreadStart(() =>
                {
                    startedEvent[index].Set();
                    continueEvent.WaitOne(timeout);
                    results[index] = f();
                }));
                threads[index].Start();
            }
            for (int t = 0; t < threads.Length; ++t)
            {
                startedEvent[t].WaitOne(timeout);
            }
            // set them all running simultaneously
            continueEvent.Set();
            // wait for them all to finish
            for (int t = 0; t < threads.Length; ++t)
            {
                threads[t].Join(timeout);
            }
            // check the results
            StringBuilder sb = new();
            for (int t = 0; t < threads.Length; ++t)
            {
                if (!string.IsNullOrEmpty(results[t]))
                {
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(results[t]);
                }
            }
            if (sb.Length > 0) Assert.Fail(typeName + ": " + sb.ToString());
        }
    }
}
