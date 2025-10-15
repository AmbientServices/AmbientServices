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
            Assert.Contains("FAIL", terse);
            Assert.Contains("SampleTarget", terse);
            Assert.Contains("Fail", details);
            Assert.Contains("SampleTarget", details);

            writer = new StatusNotificationWriter();
            writer.EnterHtmlAndBody(StatusRating.Fail);
            writer.EnterStatusRange(StatusRating.Fail);
            writer.EnterTarget("SampleTarget", StatusRating.Fail - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            writer.LeaveBodyAndHtml();
            terse = writer.Terse;
            details = writer.Details;
            Assert.Contains("FAIL", terse);
            Assert.Contains("SampleTarget", terse);
            Assert.Contains("Fail", details);
            Assert.Contains("SampleTarget", details);

            writer = new StatusNotificationWriter();
            writer.EnterHtmlAndBody(StatusRating.Fail);
            writer.EnterStatusRange(StatusRating.Okay);
            writer.EnterTarget("SampleTarget", StatusRating.Superlative - 0.5f);
            writer.LeaveTarget();
            writer.LeaveStatusRange();
            writer.LeaveBodyAndHtml();
            terse = writer.Terse;
            details = writer.Details;
            Assert.Contains("OKAY", terse);
            Assert.Contains("SampleTarget", terse);
            Assert.Contains("Okay", details);
            Assert.Contains("SampleTarget", details);

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
            Assert.Contains("OKAY", terse);
            Assert.Contains("Target", terse);
            Assert.Contains("Okay", details);
            Assert.Contains("Target", details);

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
            Assert.Contains("OKAY", terse);
            Assert.Contains("Target", terse);
            Assert.Contains("Okay", details);
            Assert.Contains("Target", details);

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
            Assert.Contains("OKAY", terse);
            Assert.Contains("Target", terse);
            Assert.Contains("Okay", details);
            Assert.Contains("Target", details);
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
            Assert.Throws<InvalidOperationException>(() => writer.LeaveStatusRange());
            Assert.Throws<InvalidOperationException>(() => writer.LeaveTarget());
            Assert.Throws<InvalidOperationException>(() => writer.EnterTarget(nameof(NotificationWriterExceptions), StatusRating.Okay));
            Assert.Throws<InvalidOperationException>(() => writer.WriteAggregatedAlert(new AggregatedAlert("Source", "Target", AmbientClock.UtcNow, StatusAuditReport.Pending)));
            writer.EnterStatusRange(StatusRating.Okay);
            Assert.Throws<InvalidOperationException>(() => writer.EnterStatusRange(StatusRating.Okay));
            writer.EnterTarget(nameof(NotificationWriterExceptions), StatusRating.Okay);
            for (int loop = 0; loop < 7; ++loop) writer.EnterTarget(nameof(NotificationWriterExceptions) + loop.ToString(), StatusRating.Okay);
        }
    }
}
