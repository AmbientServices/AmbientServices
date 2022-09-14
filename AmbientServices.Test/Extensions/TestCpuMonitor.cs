using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace AmbientServices.Test
{
    [TestClass]
    public class TestCpuMonitor
    {
        [TestMethod]
        public void Constructors()
        {
            CpuMonitor c1 = new(10);
            CpuMonitor c2 = new(TimeSpan.FromMilliseconds(10));
            Assert.IsTrue(c1.RecentUsage >= 0);
            Assert.IsTrue(c2.RecentUsage >= 0);
        }
    }
}
