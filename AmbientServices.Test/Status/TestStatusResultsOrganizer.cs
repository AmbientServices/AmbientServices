using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestStatusResultsOrganizer
    {
        [TestMethod]
        public void AggregatedAlertAggregate()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditAlert commonAlert = new(.7f, "TestCode", "TerseTest", "Detailed Test Message");
                StatusAuditReport report1 = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10), commonAlert);
                StatusAuditReport report2 = new(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10), commonAlert);
                AggregatedAlert first = new("TestSource1", "TestTarget", DateTime.MinValue, report1);
                AggregatedAlert second = new("TestSource2", "TestTarget", DateTime.MinValue, report2);
                StatusResults secondResults = new("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, report2);
                first.Aggregate(secondResults);

                report1 = new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10));
                report2 = new StatusAuditReport(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10));
                first = new AggregatedAlert("TestSource1", "TestTarget", DateTime.MinValue, report1);
                second = new AggregatedAlert("TestSource2", "TestTarget", DateTime.MinValue, report2);
                secondResults = new StatusResults("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, report2);
                first.Aggregate(secondResults);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);
                first.Aggregate("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), report2);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);

                //report1 = new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(1));
                //report2 = new StatusAuditReport(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2));
                //first = new AggregatedAlert("TestSource1", "TestTarget", DateTime.MinValue, report1);
                //second = new AggregatedAlert("TestSource2", "TestTarget", DateTime.MinValue, report2);
                //secondResults = new StatusResults("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, report2);
                //first.Aggregate(secondResults);
                //Assert.AreEqual(StatusRating.Okay, first.AverageRating);
                //first.Aggregate("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), report2);
                //Assert.AreEqual(StatusRating.Okay, first.AverageRating);

                report1 = new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(1));
                report2 = new StatusAuditReport(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2));
                first = new AggregatedAlert("TestSource1", "TestTarget", DateTime.MinValue, report1);
                second = new AggregatedAlert("TestSource2", "TestTarget", DateTime.MinValue, report2);
                secondResults = new StatusResults("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(50), AmbientClock.UtcNow.AddSeconds(5)));
                first.Aggregate(secondResults);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);
                first.Aggregate("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(50), AmbientClock.UtcNow.AddSeconds(5)));
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);

                first = new AggregatedAlert("TestSource1", "TestTarget", DateTime.MinValue, null);
                second = new AggregatedAlert("TestSource2", "TestTarget", DateTime.MinValue, null);
                secondResults = new StatusResults("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, null);
                first.Aggregate(secondResults);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);
                first.Aggregate("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), null);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);

                report1 = new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10));
                report2 = new StatusAuditReport(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10));
                first = new AggregatedAlert("TestSource", "TestTarget", DateTime.MinValue, report1);
                second = new AggregatedAlert("TestSource", "TestTarget", DateTime.MinValue, report2);
                secondResults = new StatusResults("TestSource", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(13)));
                first.Aggregate(secondResults);
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);
                first.Aggregate("TestSource", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(13)));
                Assert.AreEqual(StatusRating.Okay, first.AverageRating);
            }
        }
        [TestMethod]
        public void AggregatedDefaultRating()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditAlert commonAlert = new(.7f, "TestCode", "TerseTest", "Detailed Test Message");
                StatusResults results = new("TestSource", "TestTarget", ImmutableArray<StatusResults>.Empty);
                AggregatedAlert alert = new(results);
                Assert.AreEqual(StatusRating.Okay, alert.RatingSum);

                StatusAuditReport report = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10));
                results = new StatusResults("TestSource", "TestTarget", AmbientClock.UtcNow, 0, ImmutableArray<StatusProperty>.Empty, report);
                alert = new AggregatedAlert(results);
                Assert.AreEqual(StatusRating.Okay, alert.RatingSum);
            }
        }
        [TestMethod]
        public void AggregatedAlertProperties()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditAlert commonAlert = new(.7f, "TestCode", "TerseTest", "Detailed Test Message");
                StatusAuditReport report1 = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10), commonAlert);
                AggregatedAlert first = new("TestSource1", "TestTarget", DateTime.MinValue, report1);
                Assert.IsNotNull(first.PropertyRanges);
                Assert.AreEqual(first.Report, report1);
            }
        }
        [TestMethod]
        public void AggregatedAlertNullSourceAndTarget()
        {
            StatusAuditReport report = new(DateTime.MinValue, TimeSpan.FromSeconds(1));
            AggregatedAlert a = new(null, null, DateTime.MinValue, report);
            Assert.AreEqual(Status.DefaultSource, a.Sources[0]);
            Assert.AreEqual(Status.DefaultTarget, a.Target);
        }
        [TestMethod]
        public void AggregatedAlertCanBeAggregated()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditAlert alert1 = new(.7f, "TestCode1", "TerseTest", "Detailed Test Message");
                StatusAuditAlert alert2 = new(.7f, "TestCode2", "TerseTest", "Detailed Test Message");
                StatusAuditReport report1 = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10), alert1);
                StatusAuditReport report1NoAlert = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10), null);
                StatusAuditReport report2 = new(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10), alert2);
                StatusAuditReport report2NoAlert = new(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10), null);
                AggregatedAlert first = new("TestSource1", "TestTarget", DateTime.MinValue, report1);
                AggregatedAlert second = new("TestSource2", "TestTarget", DateTime.MinValue, report2);
                Assert.IsFalse(first.CanBeAggregated("/", report2));
                Assert.IsFalse(first.CanBeAggregated("/", report2NoAlert));
                Assert.IsFalse(first.CanBeAggregated("/", null));
                Assert.IsTrue(first.CanBeAggregated("TestTarget", report1));
                Assert.IsFalse(first.CanBeAggregated("TestTarget", report1NoAlert));
                Assert.IsFalse(first.CanBeAggregated("TestTarget", null));
            }
        }
        [TestMethod]
        public void AggregatedAlertExceptions()
        {
            using (AmbientClock.Pause())
            {
                StatusAuditAlert alert1 = new(.7f, "TestCode1", "TerseTest", "Detailed Test Message");
                StatusAuditAlert alert2 = new(.7f, "TestCode2", "TerseTest", "Detailed Test Message");
                StatusAuditReport report1 = new(AmbientClock.UtcNow, TimeSpan.FromSeconds(1), AmbientClock.UtcNow.AddMinutes(10), alert1);
                StatusAuditReport report2 = new(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10), alert2);
                AggregatedAlert first = new("TestSource1", "TestTarget", DateTime.MinValue, report1);
                AggregatedAlert second = new("TestSource2", "TestTarget", DateTime.MinValue, report2);
                StatusResults secondResults = new("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, report2);
                Assert.Throws<InvalidOperationException>(() => first.Aggregate(secondResults));
                Assert.Throws<InvalidOperationException>(() => first.Aggregate("TestSource2", "TestTarget", AmbientClock.UtcNow.AddSeconds(-10), report2));

                report2 = new StatusAuditReport(AmbientClock.UtcNow.AddSeconds(-10), TimeSpan.FromSeconds(2), AmbientClock.UtcNow.AddSeconds(-10).AddMinutes(10), alert1);
                secondResults = new StatusResults("TestSource2", "MismatchedTarget", AmbientClock.UtcNow.AddSeconds(-10), 0, ImmutableArray<StatusProperty>.Empty, report2);
                Assert.Throws<InvalidOperationException>(() => first.Aggregate(secondResults));
                Assert.Throws<InvalidOperationException>(() => first.Aggregate("TestSource2", "MismatchedTarget", AmbientClock.UtcNow.AddSeconds(-10), report2));
            }
        }
        [TestMethod]
        public void StatusResultsDateTimeRange()
        {
            using (AmbientClock.Pause())
            {
                // first test with a single date time
                StatusResultsDateTimeRange range = new(AmbientClock.UtcNow);

                string longString = range.ToLongTimeString();
                string shortString = range.ToShortTimeString();
                Assert.IsGreaterThan(shortString.Length, longString.Length);
                string defaultString = range.ToString();
                Assert.IsTrue(String.Equals(longString, defaultString) || String.Equals(shortString, defaultString));

                // now add another date time
                range.AddSample(AmbientClock.UtcNow.AddDays(1));

                longString = range.ToLongTimeString();
                shortString = range.ToShortTimeString();
                Assert.IsGreaterThan(shortString.Length, longString.Length);
                defaultString = range.ToString();
                Assert.IsTrue(String.Equals(longString, defaultString) || String.Equals(shortString, defaultString));
            }
        }
        [TestMethod]
        public void TimeSpanRange()
        {
            using (AmbientClock.Pause())
            {
                // first test with a single time span
                TimeSpanRange range = new(TimeSpan.FromSeconds(10));

                string longString = range.ToLongString();
                string shortString = range.ToShortString();
                Assert.IsGreaterThan(shortString.Length, longString.Length);
                string defaultString = range.ToString();
                Assert.IsTrue(String.Equals(longString, defaultString) || String.Equals(shortString, defaultString));

                // now add another time span
                range.AddSample(TimeSpan.FromHours(10));

                longString = range.ToLongString();
                shortString = range.ToShortString();
                Assert.IsGreaterThan(shortString.Length, longString.Length);
                defaultString = range.ToString();
                Assert.IsTrue(String.Equals(longString, defaultString) || String.Equals(shortString, defaultString));
            }
        }
        [TestMethod]
        public void StatusPropertyRange()
        {
            using (AmbientClock.Pause())
            {
                // first test with a single property value
                StatusPropertyRange range = new(new StatusProperty("property", "5"));

                Assert.Contains("5", range.ToString());

                // now add another property value
                range.Merge("10");

                Assert.Contains("5", range.ToString());
                Assert.Contains("10", range.ToString());

                // now add another property value in the middle
                range.Merge("7");

                Assert.Contains("5", range.ToString());
                Assert.Contains("10", range.ToString());
                Assert.DoesNotContain("7", range.ToString());

                // now add another property value
                range.Merge("2");

                Assert.Contains("2", range.ToString());
                Assert.Contains("10", range.ToString());
                Assert.DoesNotContain("7", range.ToString());
                Assert.DoesNotContain("5", range.ToString());
            }
        }
        [TestMethod]
        public void StatusResultsOrganizer()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsOrganizer organizer;
                string o;

                organizer = new StatusResultsOrganizer();
                organizer.Add(organizer, new StatusResults(null, "/Target1", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))), "Source1");
                organizer.Add(organizer, new StatusResults(null, "/Target1", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))), "Source2");
                organizer.Add(organizer, new StatusResults(null, "/SampleDisk.SystemTemp", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), null), "Source1");
                organizer.Add(organizer, new StatusResults(null, "/SampleDisk.SystemTemp", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "1.0") }, null), "Source1");
                organizer.Add(organizer, new StatusResults(null, "/SampleDisk.SystemTemp", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "1.5") }, null), "Source1");
                organizer.Add(organizer, new StatusResults("Source2", "/SampleDisk.SystemTemp", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "2.3") }, null), "Source1");
                organizer.Add(organizer, new StatusResults(null, "/SampleDisk.Temp", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "4.0") }, null), "Source1");
                organizer.Add(organizer, new StatusResults(null, "/SampleDisk.OperatingSystem", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "10.0") }, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(10), DateTime.UtcNow.AddMinutes(1))), "Source2");
                organizer.Add(organizer, new StatusResults("Source1", "Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.Add(organizer, new StatusResults("Source2", "Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.Add(organizer, new StatusResults("Source3", "Pending", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.Leaf, new StatusResults[] { StatusResults.GetPendingResults("Leaf", "PendingTarget") }));
                organizer.Add(organizer.Children.First(), new StatusResults(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.Add(organizer.Children.First(), new StatusResults(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.Add(organizer.Children.Skip(1).First(), new StatusResults(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.Add(organizer.Children.Skip(2).First(), new StatusResults(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, Array.Empty<StatusResults>()));
                organizer.ComputeOverallRatingAndSort();
                o = organizer.ToString();
                Assert.Contains("Overall:", o);
                o = organizer.Children.First().ToString();
                Assert.Contains("Source1->", o);
                Assert.Contains("/Target1:", o);

                organizer = new StatusResultsOrganizer();
                StatusResults source1Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/SampleDisk.OperatingSystem", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "1.0") }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                StatusResults source2Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/SampleDisk.OperatingSystem", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "2.3") }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.1"), new StatusProperty("Att2", "Val2.1") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.1"), new StatusProperty("Att2", "Val2.1") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                StatusResults overallRoot = new(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] {
                    source1Root,
                    source2Root,
                });
                organizer.Add(organizer, overallRoot);
                organizer.ComputeOverallRatingAndSort();
                StatusPropertyRange worstRange = organizer.Children.First().WorstPropertyRange;
                o = organizer.Children.First().ToString();
            }
        }
        [TestMethod]
        public void StatusResultsOrganizerTime()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsOrganizer organizer;
                string o;

                organizer = new StatusResultsOrganizer();
                StatusResults source1Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/Target1", AmbientClock.UtcNow.AddSeconds(2), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow.AddSeconds(2), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow.AddSeconds(3), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part2", AmbientClock.UtcNow.AddSeconds(3), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target3", AmbientClock.UtcNow.AddSeconds(1), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part3", AmbientClock.UtcNow.AddSeconds(1), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                StatusResults source2Root = new(null, null, AmbientClock.UtcNow.AddSeconds(5), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/Target1", AmbientClock.UtcNow.AddSeconds(2), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow.AddSeconds(2), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow.AddSeconds(3), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part2", AmbientClock.UtcNow.AddSeconds(3), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target3", AmbientClock.UtcNow.AddSeconds(1), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part3", AmbientClock.UtcNow.AddSeconds(1), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
                StatusResults source3Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/Target1", AmbientClock.UtcNow.AddSeconds(1), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow.AddSeconds(1), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow.AddSeconds(3), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part2", AmbientClock.UtcNow.AddSeconds(3), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target3", AmbientClock.UtcNow.AddSeconds(2), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part3", AmbientClock.UtcNow.AddSeconds(2), 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                organizer.Add(organizer, source1Root);
                organizer.Add(organizer, source2Root);
                organizer.Add(organizer, source3Root);
                organizer.ComputeOverallRatingAndSort();
                StatusPropertyRange worstRange = organizer.Children.First().WorstPropertyRange;
                o = organizer.Children.First().ToString();
            }
        }
        [TestMethod]
        public void StatusResultsOrganizerPending()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsOrganizer organizer;
                string o;

                organizer = new StatusResultsOrganizer();
                StatusResults source1Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] {
                    new StatusResults(null, "/Pending", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusAuditReport.Pending) }),
                    new StatusResults(null, "/Pending", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusAuditReport.Pending) }),
                });
                StatusResults source2Root = new(null, null, AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/Pending", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenIrrelevant, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusAuditReport.Pending) }),
                });
                StatusResults overallRoot = new(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] {
                    source1Root,
                    source2Root,
                });
                organizer.Add(organizer, overallRoot);
                organizer.ComputeOverallRatingAndSort();
                Assert.IsTrue(organizer.SomeRatingsPending);
                o = organizer.Children.First().ToString();
            }
        }
        [TestMethod]
        public void StatusResultsOrganizerPropertiesWorse()
        {
            using (AmbientClock.Pause())
            {
                StatusResultsOrganizer organizer;
                string o;

                organizer = new StatusResultsOrganizer();
                StatusResults source1Root = new(null, null, AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "1.0") }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/SampleDisk.OperatingSystem", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "2.0"), new StatusProperty("AvailablePercent", "2.1") }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                        new StatusResults(null, "/SampleDisk.Temp", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "2.2") , new StatusProperty("AvailableBytes", "1100000000.0") }, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(3), AmbientClock.UtcNow.AddSeconds(2), new StatusAuditAlert(StatusRating.Okay - .0001f, "ALERT", "Alert", "AlertDetails"))),
                        new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()),
                    }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.0"), new StatusProperty("Att2", "Val2.0") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                StatusResults source2Root = new(null, null, AmbientClock.UtcNow.AddSeconds(1), 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(null, "/SampleDisk.OperatingSystem", AmbientClock.UtcNow.AddSeconds(1), 0, new StatusProperty[] { new StatusProperty("AvailablePercent", "2.3") }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.1"), new StatusProperty("Att2", "Val2.1") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                    new StatusResults(null, "/Target2", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { new StatusResults(null, "Part1", AmbientClock.UtcNow, 0, new StatusProperty[] { new StatusProperty("Att1", "Val1.1"), new StatusProperty("Att2", "Val2.1") }, StatusNatureOfSystem.Leaf, Array.Empty<StatusResults>()) }),
                });
                StatusResults overallRoot = new(null, "/", AmbientClock.UtcNow, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] {
                    source1Root,
                    source2Root,
                });
                organizer.Add(organizer, overallRoot);
                organizer.ComputeOverallRatingAndSort();
                StatusPropertyRange worstRange = organizer.Children.First().WorstPropertyRange;
                o = organizer.Children.First().ToString();
            }
        }
    }
    /// <summary>
    /// A status node for the local disk storage.
    /// </summary>
    [DefaultPropertyThresholds("TargetLeafWithNoReport.Attr1", 1000.0f, 100.0f, 10.0f)]
    [DefaultPropertyThresholds("TargetLeafWithNoReport.Attr2", 10.0f, 100.0f, 1000.0f)]
    class ThresholdRegisterAuditor : StatusAuditor
    {
        /// <summary>
        /// Constructs a <see cref="ThresholdRegisterAuditor"/>.
        /// </summary>
        public ThresholdRegisterAuditor()
            : base("SampleDisk", TimeSpan.FromMinutes(1))
        {
        }
        /// <summary>
        /// Gets whether or not this status node is applicable and should be included in the list of statuses for this machine.
        /// </summary>
        protected internal override bool Applicable => false;
        /// <summary>
        /// Computes the current status, building a <see cref="StatusResults"/> to hold information about the status.
        /// </summary>
        /// <param name="statusBuilder">A <see cref="StatusResultsBuilder"/> that may be used to fill in audit information.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public override ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
        {
            return default;
        }
    }
}
