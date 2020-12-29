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
    public class TestStatusRating
    {
        [TestMethod]
        public void StatusRatingUtilityFunctions()
        {
            float previousValue = float.MinValue;
            StatusRatingRange previousRange = StatusRatingRange.Pending;
            foreach (StatusRatingRange range in Enum.GetValues(typeof(StatusRatingRange)).Cast<StatusRatingRange>())
            {
                float lowerBound = StatusRating.GetRangeLowerBound(range);
                Assert.IsTrue(float.IsNaN(lowerBound) || lowerBound > previousValue);
                if (!float.IsNaN(lowerBound)) previousValue = lowerBound;
                Assert.AreEqual(previousRange, StatusRating.FindRange(lowerBound));
                previousRange = float.IsNaN(lowerBound) ? StatusRatingRange.Fail : range;
                float upperBound = StatusRating.GetRangeUpperBound(range);
                Assert.AreEqual(range, StatusRating.FindRange(upperBound));
                string rangeSymbol = StatusRating.GetRangeSymbol(range);
                Assert.IsFalse(string.IsNullOrEmpty(rangeSymbol));
                Assert.IsFalse(string.IsNullOrEmpty(StatusRating.GetRangeBackgroundColor(upperBound)));
                Assert.IsFalse(string.IsNullOrEmpty(StatusRating.GetRangeForegroundColor(upperBound)));
                Assert.IsFalse(string.IsNullOrEmpty(StatusRating.GetRatingRgbForegroundColor(upperBound)));
            }
            Assert.AreEqual(StatusRating.Okay, StatusResultsOrganizer.ClampedRating(StatusRating.Pending));
            Assert.AreEqual(StatusRating.Catastrophic, StatusResultsOrganizer.ClampedRating(StatusRating.Catastrophic - 1));
            Assert.AreEqual(StatusRating.Okay, StatusResultsOrganizer.ClampedRating(StatusRating.Superlative + 1));
            Assert.AreEqual(StatusRating.GetRatingRgbForegroundColor(StatusRating.Catastrophic), StatusRating.GetRatingRgbForegroundColor(StatusRating.Catastrophic - 1));
            Assert.AreNotEqual(StatusRating.GetRatingRgbForegroundColor(StatusRating.Superlative), StatusRating.GetRatingRgbForegroundColor(StatusRating.Superlative + 1));
        }
        [TestMethod]
        public void StatusRatingCompare()
        {
            Assert.AreEqual(0, Status.RatingCompare(
                new StatusResults("Source", "Target", DateTime.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(DateTime.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddSeconds(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))),
                new StatusResults("Source", "Target", DateTime.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(DateTime.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddSeconds(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details")))
                )
            );
            Assert.AreEqual(1, Status.RatingCompare(
                new StatusResults("Source", "Target", DateTime.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(DateTime.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddSeconds(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))),
                new StatusResults("Source", "Target", DateTime.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(DateTime.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddSeconds(1), null))
                )
            );
        }
    }
}
