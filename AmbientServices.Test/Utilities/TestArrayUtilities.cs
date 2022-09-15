using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for array extension methods.
    /// </summary>
    [TestClass]
    public class TestArrayUtilities
    {
        [TestMethod]
        public void CompareArraysByValueMultidimensional()
        {
            int[,] a = new int[,] { { 0, 1, 2 }, { 3, 4, 5 } };
            int[,] b = new int[,] { { 0, 1, 2 }, { 3, 4, 5 } };
            Assert.IsTrue(ArrayUtilities.ValueEquals(typeof(int), a, a));
            Assert.IsTrue(ArrayUtilities.ValueEquals(typeof(int), a, b));
            Assert.IsFalse(ArrayUtilities.ValueEquals(typeof(int), a, new int[] { 0, 1, 2 }));
            Assert.IsFalse(ArrayUtilities.ValueEquals(typeof(int), a, new int[,] { { 0 }, { 1 } }));
            Assert.ThrowsException<ArgumentNullException>(() => ArrayUtilities.ValueEquals(null!, a, a));
        }
        [TestMethod]
        public void CompareArraysOfArraysByValue()
        {
            int[][] a = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } };
            int[][] b = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } };
            Assert.IsTrue(ArrayUtilities.ValueEquals(typeof(int[]), a, a));
            Assert.IsTrue(ArrayUtilities.ValueEquals(typeof(int[]), a, b));
            Assert.IsFalse(ArrayUtilities.ValueEquals(typeof(int[]), a, new int[] { 0, 1, 2 }));
            Assert.IsFalse(ArrayUtilities.ValueEquals(typeof(int[]), a, new int[,] { { 0 }, { 1 } }));
        }
    }
}
