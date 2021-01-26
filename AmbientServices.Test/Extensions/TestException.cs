using AmbientServices;
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
    public class TestExceptionExtensions
    {
        [TestMethod]
        public void ExceptionFilteredString()
        {
            Exception inner = new Exception("inner");
            Exception outer = new Exception("outer", inner);
            string s = outer.ToFilteredString();
            Assert.IsTrue(s.Contains("[Exception]"));
        }
        class WeirdNamed : Exception
        {
        }
        [TestMethod]
        public void NonExceptionFilteredString()
        {
            WeirdNamed weird = new WeirdNamed();
            Assert.AreEqual("WeirdNamed", weird.TypeName());
            string s = weird.ToFilteredString();
            Assert.IsTrue(s.Contains("[WeirdNamed]"));
        }
        [TestMethod]
        public void ExceptionTypeNameNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ExceptionExtensions.TypeName(null));
        }
    }
}
