using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for array extension methods.
/// </summary>
[TestClass]
public class TestArrayExtensions
{
    [TestMethod]
    public void CompareArraysByValue()
    {
        int[] a = new int[] { 0, 1, 2 };
        int[] b = new int[] { 0, 1, 2 };
        Assert.IsTrue(a.ValueEquals(b));
        b = new int[] { 0, 1 };
        Assert.IsFalse(a.ValueEquals(b));
        b = new int[] { 1, 2 };
        Assert.IsFalse(a.ValueEquals(b));
        b = new int[] { 0, 1, 2, 3 };
        Assert.IsFalse(a.ValueEquals(b));
        b = new int[] { 0, 1, 3 };
        Assert.IsFalse(a.ValueEquals(b));
        b = new int[] { 1, 1, 2 };
        Assert.IsFalse(a.ValueEquals(b));
    }
    [TestMethod]
    public void CompareArraysByValueWithNulls()
    {
        int[] a = new int[] { 0, 1, 2 };
        int[] b = null!;
        Assert.IsFalse(a.ValueEquals(b));
        Assert.IsFalse(b.ValueEquals(a));
        Assert.IsTrue(b.ValueEquals(b));
        Assert.IsTrue(a.ValueEquals(a));
    }
    [TestMethod]
    public void ArrayValueHash()
    {
        int[] a = new int[] { 0, 1, 2 };
        int hashCode = a.ValueHashCode();
        Assert.AreNotEqual(0, hashCode);
        a = null!;
        Assert.Throws<ArgumentNullException>(() => a.ValueHashCode());
    }
    [TestMethod]
    public void ArrayValueHashWithNull()
    {
        int?[] a = new int?[] { 0, null, 2 };
        int hashCode = a.ValueHashCode();
        Assert.AreNotEqual(0, hashCode);
    }
    [TestMethod]
    public void ArrayValueHashJaggedAgreesWithEquals()
    {
        // two distinct-but-value-equal jagged arrays must hash equally (the hash recurses by value, matching ValueEquals)
        int[][] a = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4 } };
        int[][] b = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4 } };
        Assert.IsTrue(a.ValueEquals(b));
        Assert.AreEqual(a.ValueHashCode(), b.ValueHashCode());
        // a value difference in a nested array should (normally) change the hash
        int[][] c = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 5 } };
        Assert.IsFalse(a.ValueEquals(c));
        Assert.AreNotEqual(a.ValueHashCode(), c.ValueHashCode());
    }
}
