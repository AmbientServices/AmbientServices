using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices.Test
{
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
        public void CompareArraysByValueMultidimensional()
        {
            int[,] a = new int[,] { { 0, 1, 2 }, { 3, 4, 5 } };
            int[,] b = new int[,] { { 0, 1, 2 }, { 3, 4, 5 } };
            Assert.IsTrue(ArrayExtensions.ValueEquals(typeof(int), a, a));
            Assert.IsTrue(ArrayExtensions.ValueEquals(typeof(int), a, b));
            Assert.IsFalse(ArrayExtensions.ValueEquals(typeof(int), a, new int[] { 0, 1, 2 }));
            Assert.IsFalse(ArrayExtensions.ValueEquals(typeof(int), a, new int[,] { { 0 }, { 1 } }));
            Assert.ThrowsException<ArgumentNullException>(() => ArrayExtensions.ValueEquals(null!, a, a));
        }
        [TestMethod]
        public void CompareArraysOfArraysByValue()
        {
            int[][] a = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } };
            int[][] b = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } };
            Assert.IsTrue(ArrayExtensions.ValueEquals(typeof(int[]), a, a));
            Assert.IsTrue(ArrayExtensions.ValueEquals(typeof(int[]), a, b));
            Assert.IsFalse(ArrayExtensions.ValueEquals(typeof(int[]), a, new int[] { 0, 1, 2 }));
            Assert.IsFalse(ArrayExtensions.ValueEquals(typeof(int[]), a, new int[,] { { 0 }, { 1 } }));
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
            Assert.IsTrue(hashCode != 0);
            a = null!;
            Assert.ThrowsException<ArgumentNullException>(() => a.ValueHashCode());
        }
        [TestMethod]
        public void ArrayValueHashWithNull()
        {
            int?[] a = new int?[] { 0, null, 2 };
            int hashCode = a.ValueHashCode();
            Assert.IsTrue(hashCode != 0);
        }
    }
}
