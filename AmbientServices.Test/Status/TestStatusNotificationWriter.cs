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
    public class TestStatusNotificationWriter
    {
        [TestMethod]
        public void NotificationWriter()
        {
            StatusNotificationWriter writer = new();
            string terse;
            string details;

            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(StatusRating.Fail);
            writer.EnterTarget("SampleTarget", StatusRating.Fail - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("FAIL"));
            Assert.IsTrue(terse.Contains("SampleTarget"));
            Assert.IsTrue(details.Contains("Fail"));
            Assert.IsTrue(details.Contains("SampleTarget"));

            writer = new StatusNotificationWriter();
            writer.EnterHtmlAndBody(StatusRating.Fail);
            writer.EnterStatusRange(StatusRating.Fail);
            writer.EnterTarget("SampleTarget", StatusRating.Fail - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            writer.LeaveBodyAndHtml();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("FAIL"));
            Assert.IsTrue(terse.Contains("SampleTarget"));
            Assert.IsTrue(details.Contains("Fail"));
            Assert.IsTrue(details.Contains("SampleTarget"));

            writer = new StatusNotificationWriter();
            writer.EnterHtmlAndBody(StatusRating.Fail);
            writer.EnterStatusRange(StatusRating.Okay);
            writer.EnterTarget("SampleTarget", StatusRating.Superlative - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            writer.LeaveBodyAndHtml();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("OKAY"));
            Assert.IsTrue(terse.Contains("SampleTarget"));
            Assert.IsTrue(details.Contains("Okay"));
            Assert.IsTrue(details.Contains("SampleTarget"));

            writer = new StatusNotificationWriter();
            StatusResults r = new("Source", "Target", new StatusResults[0]);
            AggregatedAlert a = new(r);
            a.PropertyRanges.Add(new StatusPropertyRange(new StatusProperty("property", "value1")));
            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(a.AverageRating);
            writer.WriteAggregatedAlert(a);
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("OKAY"));
            Assert.IsTrue(terse.Contains("Target"));
            Assert.IsTrue(details.Contains("Okay"));
            Assert.IsTrue(details.Contains("Target"));

            writer = new StatusNotificationWriter();
            r = new StatusResults("Source", "Target", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5)));
            a = new AggregatedAlert(r);
            a.PropertyRanges.Add(new StatusPropertyRange(new StatusProperty("property", "value1")));
            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(a.AverageRating);
            writer.WriteAggregatedAlert(a);
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("OKAY"));
            Assert.IsTrue(terse.Contains("Target"));
            Assert.IsTrue(details.Contains("Okay"));
            Assert.IsTrue(details.Contains("Target"));

            writer = new StatusNotificationWriter();
            r = new StatusResults("Source1", "Target", new StatusResults[0]);
            a = new AggregatedAlert(r);
            a.PropertyRanges.Add(new StatusPropertyRange(new StatusProperty("property", "value1")));
            a.PropertyRanges.Add(new StatusPropertyRange(new StatusProperty("property", "value2")));
            a.PropertyRanges.Add(new StatusPropertyRange(new StatusProperty("property", "value5")));
            r = new StatusResults("Source2", "Target", new StatusResults[0]);
            a.Aggregate(r);
            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(a.AverageRating);
            writer.WriteAggregatedAlert(a);
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;
            Assert.IsTrue(terse.Contains("OKAY"));
            Assert.IsTrue(terse.Contains("Target"));
            Assert.IsTrue(details.Contains("Okay"));
            Assert.IsTrue(details.Contains("Target"));
        }
        [TestMethod]
        public void NotificationWriterWithProperties()
        {
            StatusNotificationWriter writer = new();
            string terse;
            string details;

            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(StatusRating.Fail);
            writer.EnterTarget("SampleTarget", StatusRating.Fail - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;

            writer = new StatusNotificationWriter();
            writer.EnterHtmlAndBody(StatusRating.Fail);
            writer.EnterStatusRange(StatusRating.Fail);
            writer.EnterTarget("SampleTarget", StatusRating.Fail - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            writer.LeaveBodyAndHtml();
            terse = writer.Terse;
            details = writer.Details;

            writer = new StatusNotificationWriter();
            StatusResults r = new("Source", "Target", new StatusResults[0]);
            AggregatedAlert a = new(r);
            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(a.AverageRating);
            writer.WriteAggregatedAlert(a);
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;

            writer = new StatusNotificationWriter();
            r = new StatusResults("Source1", "Target", new StatusResults[0]);
            a = new AggregatedAlert(r);
            r = new StatusResults("Source2", "Target", new StatusResults[0]);
            a.Aggregate(r);
            writer = new StatusNotificationWriter();
            writer.EnterStatusRange(a.AverageRating);
            writer.WriteAggregatedAlert(a);
            writer.LeaveStatusRange();
            terse = writer.Terse;
            details = writer.Details;
        }
        [TestMethod]
        public void NotificationWriterExceptions()
        {
            StatusNotificationWriter writer = new();
            Assert.ThrowsException<InvalidOperationException>(() => writer.LeaveStatusRange());
            Assert.ThrowsException<InvalidOperationException>(() => writer.LeaveTarget());
            Assert.ThrowsException<InvalidOperationException>(() => writer.EnterTarget(nameof(NotificationWriterExceptions), StatusRating.Okay));
            Assert.ThrowsException<InvalidOperationException>(() => writer.WriteAggregatedAlert(new AggregatedAlert("Source", "Target", AmbientClock.UtcNow, StatusAuditReport.Pending)));
            writer.EnterStatusRange(StatusRating.Okay);
            Assert.ThrowsException<InvalidOperationException>(() => writer.EnterStatusRange(StatusRating.Okay));
            writer.EnterTarget(nameof(NotificationWriterExceptions), StatusRating.Okay);
            for (int loop = 0; loop < 7; ++loop) writer.EnterTarget(nameof(NotificationWriterExceptions) + loop.ToString(), StatusRating.Okay);
        }
    }
}
