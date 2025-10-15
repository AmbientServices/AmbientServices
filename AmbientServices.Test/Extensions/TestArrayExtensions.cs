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
}
