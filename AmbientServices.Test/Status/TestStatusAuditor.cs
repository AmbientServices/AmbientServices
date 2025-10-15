using AmbientServices;
using AmbientServices.Utilities;
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
    public class TestStatusAuditor
    {
        [TestMethod]
        public void StatusAuditAlertMisc()
        {
            Assert.AreNotEqual(StatusAuditAlert.Empty, StatusAuditAlert.None);
            Assert.AreNotEqual(StatusAuditAlert.Empty.GetHashCode(), StatusAuditAlert.None.GetHashCode());
            Assert.IsFalse(StatusAuditAlert.Empty.Equals(null));
            Assert.IsFalse(StatusAuditAlert.Empty!.Equals("test")); // the analyzer obviously gets confused when you test whether something is equal to null, even if you assert that it returns false
            Assert.AreNotEqual(StatusAuditAlert.Empty.ToString(), StatusAuditAlert.None.ToString());
            StatusAuditAlert sa = StatusAuditAlert.Empty;
            Assert.Throws<ArgumentNullException>(() => new StatusAuditAlert(1.0f, null!, "", ""));
            Assert.Throws<ArgumentNullException>(() => new StatusAuditAlert(1.0f, "", null!, ""));
            Assert.Throws<ArgumentNullException>(() => new StatusAuditAlert(1.0f, "", "", null!));
        }
        [TestMethod]
        public void StatusAuditReportMisc()
        {
            StatusAuditAlert sa = new(StatusRating.Okay, "Okay", "terse", "detailed");
            StatusAuditReport sr = new(AmbientClock.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(2), sa);
            Assert.AreNotEqual(StatusAuditReport.Pending, sr);
            Assert.AreNotEqual(StatusAuditReport.Pending.GetHashCode(), sr.GetHashCode());
            Assert.AreNotEqual(sr.GetHashCode(), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5)).GetHashCode());
            Assert.IsFalse(StatusAuditReport.Pending.Equals(null));
            Assert.IsFalse(StatusAuditReport.Pending!.Equals("test"));  // this is a false positive in the analyzer's null detector
            Assert.AreNotEqual(StatusAuditReport.Pending.ToString(), sr.ToString());
        }
        [TestMethod]
        public async Task StatusAuditor()
        {
            using (AmbientClock.Pause())
            {
                using StatusAuditorTest test = new(nameof(StatusAuditorTest));
                StatusResults results = await test.GetStatus();
                Assert.AreEqual("StatusAuditorTest", test.TargetSystem);
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));   // each of these skips should trigger another audit, with rotating values
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
            }
        }
        [TestMethod]
        public async Task StatusAuditorArgumentException()
        {
            using (AmbientClock.Pause())
            {
                StatusResults auditorTestResults;
                StatusResults auditorAuditExceptionTestResults;
                using (StatusAuditorTest test = new(nameof(StatusAuditorArgumentException)))
                {
                    auditorTestResults = await test.GetStatus();
                }
                using (StatusAuditorAuditExceptionTest test = new(nameof(StatusAuditorArgumentException) + "2"))
                {
                    auditorAuditExceptionTestResults = await test.GetStatus();
                    Assert.Throws<ArgumentException>(() => test.SetLatestResults(auditorTestResults));
                }
            }
        }
        [TestMethod]
        public void StatusAuditorAuditException()
        {
            using (AmbientClock.Pause())
            {
                using StatusAuditorAuditExceptionTest test = new(nameof(StatusAuditorAuditExceptionTest));
                Assert.AreEqual("StatusAuditorAuditExceptionTest", test.TargetSystem);
                // run the initial audit manually and synchronously
                test.InitialAuditTimer_Elapsed(null, null);
            }
        }
        [TestMethod]
        public void StatusAuditorNeverAudit()
        {
            using (AmbientClock.Pause())
            {
                using StatusAuditorAuditNeverRunTest test = new(nameof(StatusAuditorAuditNeverRunTest));
                Assert.AreEqual("StatusAuditorAuditNeverRunTest", test.TargetSystem);
                // run the initial audit manually and synchronously
                test.InitialAuditTimer_Elapsed(null, null);
            }
        }
        [TestMethod]
        public void StatusAuditorTriggerAfterDisposed()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditorAuditNeverRunTest testCopy;
                using (StatusAuditorAuditNeverRunTest test = new(nameof(StatusAuditorAuditNeverRunTest)))
                {
                    testCopy = test;
                    Assert.AreEqual("StatusAuditorAuditNeverRunTest", test.TargetSystem);
                }
                testCopy.AuditTimer_Elapsed(null, null);
            }
        }
        [TestMethod]
        public async Task StatusAuditorStopAfterDispose()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditorAuditNeverRunTest toStop;
                using (StatusAuditorAuditNeverRunTest test = new(nameof(StatusAuditorAuditNeverRunTest)))
                {
                    toStop = test;
                }
                await toStop.BeginStop();
            }
        }
        [TestMethod]
        public async Task StatusAuditorHistory()
        {
            BasicAmbientSettingsSet settings = new(nameof(StatusAuditorHistory));
            settings.ChangeSetting(nameof(StatusChecker) + "-HistoryRetentionMinutes", "5");
            settings.ChangeSetting(nameof(StatusChecker) + "-HistoryRetentionEntries", "10");
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> localOverrideTest = new(settings))

            using (AmbientClock.Pause())
            {
                using StatusAuditorTest test = new(nameof(StatusAuditorTest));
                StatusResults results = await test.GetStatus();
                Assert.AreEqual("StatusAuditorTest", test.TargetSystem);
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));   // since we're rotating the status rating, the frequency will vary because of that
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));   // this one starts kicking out history items due to count (this was determined by simply trying it out, but that part doesn't matter for this test)
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));   // this one should start kicking history items out due to time
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(30));
                Assert.IsTrue(test.History.Count() <= 10);
                Assert.IsTrue(test.History.First().Time >= AmbientClock.UtcNow.AddMinutes(-5));
            }
        }
    }
    internal class StatusAuditorTest : StatusAuditor
    {
        private int _auditNumber;

        public StatusAuditorTest(string targetSystem, Status status = null)
            : base(targetSystem, TimeSpan.FromSeconds(10), status)
        {
            StatusResultsBuilder sb = new(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            sb.AddProperty("TestProperty1", Environment.MachineName);
            sb.AddProperty("TestProperty2", AmbientClock.UtcNow);
            StatusResults results = sb.FinalResults;
            SetLatestResults(results);
            SetLatestResults(results);  // set the results twice so the first results (which are the same as the second) end up in the history
        }
        protected internal override bool Applicable => true;
        public override ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant;
            StatusRatingRange currentAuditRating = (StatusRatingRange)(_auditNumber++ % (int)EnumUtilities.MaxEnumValue<StatusRatingRange>());
            float rating = (StatusRating.GetRangeUpperBound(currentAuditRating) + StatusRating.GetRangeLowerBound(currentAuditRating)) / 2;
            if (rating <= StatusRating.Fail)
            {
                statusBuilder.AddFailure("FailCode", "Fail", "The system has failed!", StatusRating.Fail - rating);
            }
            else if (rating <= StatusRating.Alert)
            {
                statusBuilder.AddAlert("AlertCode", "Alert", "The system has alerted!", StatusRating.Alert - rating);
            }
            else if (rating <= StatusRating.Okay)
            {
                statusBuilder.AddOkay("OkayCode", "Okay", "The system is okay", StatusRating.Okay - rating);
            }
            else
            {
                statusBuilder.AddOkay("SuperCode", "Superlative", "The system is superlative", StatusRating.Superlative - rating);
            }
            return default;
        }
    }
    internal class StatusAuditorAuditExceptionTest : StatusAuditor
    {
        public StatusAuditorAuditExceptionTest(string targetSystem) // note that this parameter prevents this auditor from being used in the default status instance
            : base(targetSystem, TimeSpan.Zero, null)
        {
            StatusResultsBuilder sb = new(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            sb.AddProperty("TestProperty1", Environment.MachineName);
            sb.AddProperty("TestProperty2", AmbientClock.UtcNow);
        }
        protected internal override bool Applicable => true;
        public override ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
        {
            throw new ExpectedException("This exception is expected!");
        }
    }
    internal class StatusAuditorAuditNeverRunTest : StatusAuditor
    {
        public StatusAuditorAuditNeverRunTest(string targetSystem)
            : base(targetSystem, TimeSpan.MaxValue, null)
        {
            StatusResultsBuilder sb = new(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            sb.AddProperty("TestProperty1", Environment.MachineName);
            sb.AddProperty("TestProperty2", AmbientClock.UtcNow);
        }
        protected internal override bool Applicable => true;
        public override ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
        {
            return default;
        }
    }
}
