using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestStatusChecker
    {
        [TestMethod]
        public async Task StatusChecker()
        {
            using (AmbientClock.Pause())
            {
                using StatusCheckerTest test = new();
                StatusResults results = await test.GetStatus();
                Assert.AreEqual(results, test.LatestResults);
                Assert.AreEqual(results, test.History.LastOrDefault());   // the most recent history results should be the last one (history is a FIFO queue)
                Assert.AreEqual("StatusCheckerTest", test.TargetSystem);
                await test.BeginStop();
                await test.FinishStop();
            }
        }
    }
    internal class StatusCheckerTest : StatusChecker
    {
        public StatusCheckerTest()
            : base("StatusCheckerTest")
        {
            StatusResultsBuilder sb = new(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            sb.AddProperty("TestProperty1", Environment.MachineName);
            sb.AddProperty("TestProperty2", AmbientClock.UtcNow);
            StatusResults results = sb.FinalResults;
            SetLatestResults(results);
            SetLatestResults(results);  // set the results twice so the first results (which are the same as the second) end up in the history
        }
        protected internal override bool Applicable => true;
    }
}
