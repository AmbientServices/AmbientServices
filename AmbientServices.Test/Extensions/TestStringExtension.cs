using AmbientServices;
using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestStringExtensions
    {
        [TestMethod]
        public void StringCompareNatural()
        {
            foreach (bool ignoreCase in new bool[] { false, true })
            {
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase) < 0);
                // these should all be equal
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("", "", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("x", "x", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("0", "0", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("1", "1", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
                // depending on the algorithm, this might be less or equal, but shouldn't be greater
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "00", ignoreCase) <= 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("1", "01", ignoreCase) <= 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("01", "001", ignoreCase) <= 0);
                // these should be greater
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0.", "", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase) > 0);

                // this is a weird one and could be either depending on the algorithm, but should probably be less
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3", "3-4", ignoreCase) < 0);

                // these should be less
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("", "0.", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "0.", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "0.0", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2", "10", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2", "010", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "b", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "a10b", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "a1b10", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.4.3", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "-3.3.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase) < 0);
            }
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "A10B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A2B", "a10b", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "A10B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A2B", "a10b", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true) < 0);
        }
        [TestMethod]
        public void StringCompareNaturalLongString()
        {
            foreach (bool ignoreCase in new bool[] { false, true })
            {
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase) < 0);
                // these should all be equal
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("", "", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("x", "x", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("0", "0", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("1", "1", ignoreCase));
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
                // depending on the algorithm, this might be less or equal, but shouldn't be greater
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "00", ignoreCase) <= 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("1", "01", ignoreCase) <= 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("01", "001", ignoreCase) <= 0);
                // these should be greater
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0.", "", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase) > 0);

                // this is a weird one and could be either depending on the algorithm, but should probably be less
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3", "3-4", ignoreCase) < 0);

                // these should be less
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("", "0.", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "0.", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0", "0.0", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2", "10", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2", "010", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "b", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "a10b", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "a1b10", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.4.3", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("3.3.3", "-3.3.3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase) < 0);
            }
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "A10B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A2B", "a10b", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A", "B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a2b", "A10B", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A2B", "a10b", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true) < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true) < 0);
        }
        [TestMethod]
        public void StringCompareNaturalWeird()
        {
            foreach (bool ignoreCase in new bool[] { false, true })
            {
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-,-.--3--3.-3--", "-,-.--3--4.-3.--", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-,-.--3-3.-3--", "-,-.--3-4.-3.--", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-,-.--3,3,3--", "-,-.--3,4,3.--", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-,-.--3,,3,3--", "-,-.--3,,4,3.--", ignoreCase) < 0);
            }
        }
        [TestMethod]
        public void StringCompareNaturalDates()
        {
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2020-2-2", "2020-10-10") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2020-01-01", "2020-2-2") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2020.2.2", "2020.10.10") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("2020.01.01", "2020.2.2") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2020-2-2", "2020-1-1") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2020.2.2", "2020.1.1") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@2020@2@2@", "@2020@10@10@") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@2020@01@01@", "@2020@2@2@") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@2020@2@2@", "@2020@10@10@") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@2020@01@01@", "@2020@2@2@") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@-2020-2-2@", "@2020-1-1@") < 0);
            Assert.IsTrue(StringExtensions.CompareNaturalInvariant("@-2020.2.2@", "@2020.1.1@") < 0);
        }
        [TestMethod]
        public void StringCompareNaturalNegatives()
        {
            foreach (bool ignoreCase in new bool[] { false, true })
            {
                // these should all be equal
                Assert.IsTrue(0 == StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
                // these should be less
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase) < 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "-.4", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.4", "-3", ignoreCase) > 0);
                Assert.IsTrue(StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase) < 0);
            }
        }
    }
}
