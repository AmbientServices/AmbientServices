using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientServices.Test;

[TestClass]
public class TestStringExtensions
{
    [TestMethod]
    public void StringCompareNatural()
    {
        foreach (bool ignoreCase in new bool[] { false, true })
        {
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase));
            // these should all be equal
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("", "", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("x", "x", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("0", "0", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("1", "1", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
            // depending on the algorithm, this might be less or equal, but shouldn't be greater
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("0", "00", ignoreCase));
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("1", "01", ignoreCase));
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("01", "001", ignoreCase));
            // these should be greater
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("0.", "", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase));

            // this is a weird one and could be either depending on the algorithm, but should probably be less
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3", "3-4", ignoreCase));

            // these should be less
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("", "0.", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("0", "0.", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("0", "0.0", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2", "10", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2", "010", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "b", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "a10b", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "a1b10", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "-3.3.3", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase));
        }
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "A10B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A2B", "a10b", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "A10B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A2B", "a10b", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true));
    }
    [TestMethod]
    public void StringCompareNaturalLongString()
    {
        foreach (bool ignoreCase in new bool[] { false, true })
        {
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase));
            // these should all be equal
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("", "", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("x", "x", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("0", "0", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("1", "1", ignoreCase));
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
            // depending on the algorithm, this might be less or equal, but shouldn't be greater
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("0", "00", ignoreCase));
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("1", "01", ignoreCase));
            Assert.IsLessThanOrEqualTo(0, StringExtensions.CompareNaturalInvariant("01", "001", ignoreCase));
            // these should be greater
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("0.", "", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase));

            // this is a weird one and could be either depending on the algorithm, but should probably be less
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3", "3-4", ignoreCase));

            // these should be less
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("", "0.", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("0", "0.", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("0", "0.0", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2", "10", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2", "010", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "b", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "a10b", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "a1b10", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3-3-3", "3-4-3", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "3.-4.3", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("3.3.3", "-3.3.3", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase));
        }
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "A10B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A2B", "a10b", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A", "B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a2b", "A10B", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A2B", "a10b", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("a1b2", "A1B10", true));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("A1B2", "a1b10", true));
    }
    [TestMethod]
    public void StringCompareNaturalWeird()
    {
        foreach (bool ignoreCase in new bool[] { false, true })
        {
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-,-.--3--3.-3--", "-,-.--3--4.-3.--", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-,-.--3-3.-3--", "-,-.--3-4.-3.--", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-,-.--3,3,3--", "-,-.--3,4,3.--", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-,-.--3,,3,3--", "-,-.--3,,4,3.--", ignoreCase));
        }
    }
    [TestMethod]
    public void StringCompareNaturalDates()
    {
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2020-2-2", "2020-10-10"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2020-01-01", "2020-2-2"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2020.2.2", "2020.10.10"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("2020.01.01", "2020.2.2"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2020-2-2", "2020-1-1"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2020.2.2", "2020.1.1"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@2020@2@2@", "@2020@10@10@"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@2020@01@01@", "@2020@2@2@"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@2020@2@2@", "@2020@10@10@"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@2020@01@01@", "@2020@2@2@"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@-2020-2-2@", "@2020-1-1@"));
        Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("@-2020.2.2@", "@2020.1.1@"));
    }
    [TestMethod]
    public void StringCompareNaturalNegatives()
    {
        foreach (bool ignoreCase in new bool[] { false, true })
        {
            // these should all be equal
            Assert.AreEqual(0, StringExtensions.CompareNaturalInvariant("-1", "-1", ignoreCase));
            // these should be less
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "-1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2", "1", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-2.2", "-2.1", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("0.3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant(".3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-.3", "-4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-.3", "-.4", ignoreCase));
            Assert.IsGreaterThan(0, StringExtensions.CompareNaturalInvariant("-.4", "-3", ignoreCase));
            Assert.IsLessThan(0, StringExtensions.CompareNaturalInvariant("-.3", "4", ignoreCase));
        }
    }
#if !NET8_0_OR_GREATER
    [TestMethod]
    public void StarsWithEndsWith()
    {
        Assert.IsTrue("abcdef".StartsWith("abc"));
        Assert.IsTrue("abcdef".EndsWith("def"));
    }
#endif
    [TestMethod]
    public void Ordinal()
    {
        Assert.AreEqual(1, "abcdef".IndexOfOrdinal('b'));
        Assert.IsTrue("abcdef".ContainsOrdinal('b'));
    }
}
