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
    public class TestStatusResultsBuilder
    {
        [TestMethod]
        public void StatusResultsBuilder()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsBuilder test = new(nameof(StatusResultsBuilder));

                Assert.IsNull(test.AuditDuration);
                test.AuditDuration = TimeSpan.FromSeconds(137);
                Assert.AreEqual(TimeSpan.FromSeconds(137), test.AuditDuration);

                Assert.AreEqual(AmbientClock.UtcNow, test.AuditStartTime);
                test.AuditStartTime = DateTime.MinValue;
                Assert.AreEqual(DateTime.MinValue, test.AuditStartTime);

                test.NatureOfSystem = StatusNatureOfSystem.Leaf;
                Assert.AreEqual(StatusNatureOfSystem.Leaf, test.NatureOfSystem);

                Assert.IsNull(test.NextAuditTime);
                test.NextAuditTime = DateTime.MaxValue;
                Assert.AreEqual(DateTime.MaxValue, test.NextAuditTime);

                test.RelativeDetailLevel = 1;
                Assert.AreEqual(1, test.RelativeDetailLevel);

                test.SourceSystem = nameof(StatusResultsBuilder);
                Assert.AreEqual(nameof(StatusResultsBuilder), test.SourceSystem);

                Assert.AreEqual(nameof(StatusResultsBuilder), test.TargetSystem);
            }
        }
        [TestMethod]
        public void StatusResultsBuilderChildren()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsBuilder test = new(nameof(StatusResultsBuilderChildren));
                StatusResultsBuilder child = test.AddChild("Child");
                Assert.AreEqual("Child", child.TargetSystem);
            }
        }
        [TestMethod]
        public void StatusResultsBuilderProperties()
        {
            using (AmbientClock.Pause())
            {
                using StatusCheckerTest checker = new();
                StatusResultsBuilder test = new(checker, new StatusProperty[] { new("Property1", "Value1"), new("Property2", "Value2") });
                StatusProperty property = test.FindProperty("Property1");
                Assert.AreEqual("Value1", property?.Value);
            }
        }
        [TestMethod]
        public void StatusResultsBuilderFinalResults()
        {
            using (AmbientClock.Pause())
            {
                StatusResults results;
                StatusResultsBuilder test;

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderFinalResults))
                {
                    AuditDuration = null
                };
                test.AddAlert("Alert", "Terse", "Details", 0.5f);
                results = test.FinalResults;
                Assert.AreEqual(StatusRating.Alert - 0.5f, results.Report?.Alert?.Rating);

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderFinalResults))
                {
                    AuditDuration = TimeSpan.FromMilliseconds(100)
                };
                test.AddOkay("Okay", "Terse", "Details", 0.5f);
                results = test.FinalResults;
                Assert.AreEqual(StatusRating.Okay - 0.5f, results.Report?.Alert?.Rating);
            }
        }
        [TestMethod]
        public void StatusResultsBuilderWorstAlert()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsBuilder test;

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderWorstAlert));
                Assert.IsNull(test.WorstAlert);
                test.AddException(new ExpectedException(), 0.5f);
                Assert.AreEqual(StatusRating.Fail - 0.5f, test.WorstAlert?.Rating);
                test.AddException(new ExpectedException(), 0.6f);
                Assert.AreEqual(StatusRating.Fail - 0.6f, test.WorstAlert?.Rating);
                test.AddException(new ExpectedException(), 0.5f);
                Assert.AreEqual(StatusRating.Fail - 0.6f, test.WorstAlert?.Rating);

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderWorstAlert));
                Assert.IsNull(test.WorstAlert);
                test.AddFailure("Fail", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Fail - 0.5f, test.WorstAlert?.Rating);
                test.AddFailure("Fail", "Terse", "Details", 0.6f);
                Assert.AreEqual(StatusRating.Fail - 0.6f, test.WorstAlert?.Rating);
                test.AddFailure("Fail", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Fail - 0.6f, test.WorstAlert?.Rating);

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderWorstAlert));
                Assert.IsNull(test.WorstAlert);
                test.AddAlert("Alert", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Alert - 0.5f, test.WorstAlert?.Rating);
                test.AddAlert("Alert", "Terse", "Details", 0.6f);
                Assert.AreEqual(StatusRating.Alert - 0.6f, test.WorstAlert?.Rating);
                test.AddAlert("Alert", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Alert - 0.6f, test.WorstAlert?.Rating);

                test = new StatusResultsBuilder(nameof(StatusResultsBuilderWorstAlert));
                Assert.IsNull(test.WorstAlert);
                test.AddOkay("Okay", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Okay - 0.5f, test.WorstAlert?.Rating);
                test.AddOkay("Okay", "Terse", "Details", 0.6f);
                Assert.AreEqual(StatusRating.Okay - 0.6f, test.WorstAlert?.Rating);
                test.AddOkay("Okay", "Terse", "Details", 0.5f);
                Assert.AreEqual(StatusRating.Okay - 0.6f, test.WorstAlert?.Rating);
            }
        }
        [TestMethod]
        public void StatusResultsBuilderExceptions()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsBuilder test = new(nameof(StatusResultsBuilder));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => test.AddException(new ApplicationException(), -1.0f));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => test.AddFailure("Fail", "Terse", "Details", -0.1f));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => test.AddAlert("Alert", "Terse", "Details", -0.001f));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => test.AddAlert("Alert", "Terse", "Details", 1.001f));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => test.AddOkay("Okay", "Terse", "Details", 1.1f));
                Assert.ThrowsException<ArgumentNullException>(() => new StatusResultsBuilder((StatusResults)null!));
                Assert.ThrowsException<ArgumentNullException>(() => new StatusResultsBuilder((StatusChecker)null!));
            }
        }
    }
}
