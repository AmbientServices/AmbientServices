using AmbientServices.Performance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices.Performance
{
    [TestClass]
    public class TestInterlockedExtensions
    {
        [TestMethod]
        public void InterlockedAddDouble()
        {
            double x = Double.MaxValue - 5;
            Assert.AreEqual(Double.MaxValue, InterlockedExtensions.TryOptomisticAdd(ref x, 5));
        }

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
    }
}
