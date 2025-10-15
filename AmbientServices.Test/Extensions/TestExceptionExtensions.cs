using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AmbientServices.Test;

[TestClass]
public class TestExceptionExtensions
{
    [TestMethod]
    public void ExceptionFilteredString()
    {
        Exception inner = new("inner");
        Exception outer = new("outer", inner);
        string s = outer.ToFilteredString();
        Assert.Contains("[Exception]", s);
    }
    class WeirdNamed : Exception
    {
    }
    [TestMethod]
    public void NonExceptionFilteredString()
    {
        WeirdNamed weird = new();
        Assert.AreEqual("WeirdNamed", weird.TypeName());
        string s = weird.ToFilteredString();
        Assert.Contains("[WeirdNamed]", s);
    }
    [TestMethod]
    public void ExceptionTypeNameNull()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionExtensions.TypeName(null!));
    }
    [TestMethod]
    public void ExceptionNullArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionExtensions.ToFilteredString(null!));
    }
}
