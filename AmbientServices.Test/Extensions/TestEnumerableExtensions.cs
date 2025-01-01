using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for array extension methods.
/// </summary>
[TestClass]
public class TestEnumerableExtensions
{
    [TestMethod]
    public void WhereNotNullTest()
    {
        int?[] a = new int?[] { 0, 1, 2 };
        int?[] b = new int?[] { null, 1, 2 };

        Assert.AreEqual(3, a.WhereNotNull().Count());
        Assert.AreEqual(2, b.WhereNotNull().Count());
    }
}
