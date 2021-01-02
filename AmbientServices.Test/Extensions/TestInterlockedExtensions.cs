using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestInterlockedExtensions
    {
        [TestMethod]
        public void InterlockedMaxLong()
        {
            long x = 0;
            Assert.AreEqual(0, InterlockedExtensions.TryOptomisticMax(ref x, -1));

            x = 0;
            Assert.AreEqual(1, InterlockedExtensions.TryOptomisticMax(ref x, 1));
        }

        [TestMethod]
        public void InterlockedMinLong()
        {
            long x = 0;
            Assert.AreEqual(0, InterlockedExtensions.TryOptomisticMin(ref x, 1));

            x = 0;
            Assert.AreEqual(-1, InterlockedExtensions.TryOptomisticMin(ref x, -1));
        }
        [TestMethod]
        public void TryAgainAfterOptomisticMissDelay()
        {
            Assert.IsTrue(InterlockedExtensions.TryAgainAfterOptomisticMissDelay(0));
            Assert.IsTrue(InterlockedExtensions.TryAgainAfterOptomisticMissDelay(3));
            Assert.IsTrue(InterlockedExtensions.TryAgainAfterOptomisticMissDelay(5));
            Assert.IsFalse(InterlockedExtensions.TryAgainAfterOptomisticMissDelay(10));
        }
        [TestMethod]
        public void InterlockedMaxDouble()
        {
            double x = 0;
            Assert.AreEqual(0, InterlockedExtensions.TryOptomisticMax(ref x, -1));

            x = 0;
            Assert.AreEqual(1, InterlockedExtensions.TryOptomisticMax(ref x, 1));
        }

        [TestMethod]
        public void InterlockedMinDouble()
        {
            double x = 0;
            Assert.AreEqual(0, InterlockedExtensions.TryOptomisticMin(ref x, 1));

            x = 0;
            Assert.AreEqual(-1, InterlockedExtensions.TryOptomisticMin(ref x, -1));
        }
        [TestMethod]
        public void InterlockedAddDouble()
        {
            double x = Double.MaxValue - 5;
            Assert.AreEqual(Double.MaxValue, InterlockedExtensions.TryOptomisticAdd(ref x, 5));
        }
        [TestMethod]
        public void InterlockedExponentialMovingAverage()
        {
            double x = 0;
            Assert.AreEqual(0.0, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.5, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.5, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.75, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.75, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));
            Assert.AreEqual(0.875, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 1.0, 1.0));
            Assert.AreEqual(0.875, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 0.0, 1.0));

            x = 0;
            Assert.AreEqual(0.75, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 2.0, 1.0));
            Assert.AreEqual(0.9375, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, 2.0, 1.0));

            x = 0;
            double halfHalfLife = Math.Log(4.0 / 3.0) / Math.Log(2.0);
            Assert.AreEqual(0.25, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, halfHalfLife, 1.0));
            Assert.AreEqual(0.4375, InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, halfHalfLife, 1.0));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => InterlockedExtensions.TryOptomisticAddExponentialMovingAverageSample(ref x, -0.0000001, 1.0));
        }
    }
}
