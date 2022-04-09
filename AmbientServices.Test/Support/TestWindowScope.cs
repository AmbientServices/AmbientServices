using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for date time extension methods.
    /// </summary>
    [TestClass]
    public class TestWindowScope
    {
        /// <summary>
        /// Performs basic tests on date time extension methods.
        /// </summary>
        [TestMethod]
        public void WindowId()
        {
            DateTime dt = new(2020, 6, 26, 18, 53, 7, 294);
            Assert.AreEqual("2020-06", WindowScope.WindowId(dt, TimeSpan.FromDays(700)));
            Assert.AreEqual("06-26", WindowScope.WindowId(dt, TimeSpan.FromDays(90)));
            Assert.AreEqual("26_18", WindowScope.WindowId(dt, TimeSpan.FromDays(2)));
            Assert.AreEqual("18:53", WindowScope.WindowId(dt, TimeSpan.FromMinutes(120)));
            Assert.AreEqual("18:53:07", WindowScope.WindowId(dt, TimeSpan.FromSeconds(90)));
            Assert.AreEqual("53:07.294", WindowScope.WindowId(dt, TimeSpan.FromMilliseconds(2000)));
            Assert.AreEqual("07.294",    WindowScope.WindowId(dt, TimeSpan.FromMilliseconds(10)));
        }
        /// <summary>
        /// Performs basic tests on date time extension methods.
        /// </summary>
        [TestMethod]
        public void WindowSize()
        {
            Assert.AreEqual("-5.6Y", WindowScope.WindowSize(TimeSpan.FromDays(-2037.53)));
            Assert.AreEqual("5.6Y", WindowScope.WindowSize(TimeSpan.FromDays(2037.53)));
            Assert.AreEqual("7.8M", WindowScope.WindowSize(TimeSpan.FromDays(237.53)));
            Assert.AreEqual("38D", WindowScope.WindowSize(TimeSpan.FromDays(37.53)));
            Assert.AreEqual("38h", WindowScope.WindowSize(TimeSpan.FromHours(37.53)));
            Assert.AreEqual("38m", WindowScope.WindowSize(TimeSpan.FromMinutes(37.53)));
            Assert.AreEqual("38s", WindowScope.WindowSize(TimeSpan.FromSeconds(37.53)));
            Assert.AreEqual("2754ms", WindowScope.WindowSize(TimeSpan.FromMilliseconds(2753.643)));
            Assert.AreEqual("3.5ms", WindowScope.WindowSize(new TimeSpan((long)(TimeSpan.TicksPerMillisecond * 3.452))));
        }
    }
}
