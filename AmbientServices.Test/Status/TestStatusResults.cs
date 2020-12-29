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
    public class TestStatusResults
    {
        [TestMethod]
        public void StatusPropertyClass()
        {
            StatusProperty property = StatusProperty.Create("Property", "Value");
            string prop = property.ToString();
            Assert.IsTrue(prop.Contains("Property"));
            Assert.IsTrue(prop.Contains("Value"));
        }
        [TestMethod]
        public void StatusResults()
        {
            StatusResults results;

            results = new StatusResults("Source", "StatusTarget", AmbientClock.UtcNow, 1, new StatusProperty[] { StatusProperty.Create("Property", "Value") }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults("Source", "Target", Array.Empty<StatusResults>()) });
            Assert.AreEqual("Source", results.SourceSystem);
            Assert.AreEqual("Source", results.SourceSystemDisplayName);
            Assert.AreEqual("StatusTarget", results.TargetSystem);
            Assert.AreEqual("StatusTarget", results.TargetSystemDisplayName);
            Assert.IsTrue(results.ToString().Contains("Source"));
            Assert.IsTrue(results.ToString().Contains("StatusTarget"));

            results = new StatusResults("Source", "/", AmbientClock.UtcNow, 1, new StatusProperty[] { StatusProperty.Create("Property", "Value") }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults("Source", "Target", Array.Empty<StatusResults>()) });
            Assert.AreEqual("Source", results.SourceSystem);
            Assert.AreEqual("Source", results.SourceSystemDisplayName);
            Assert.AreEqual("/", results.TargetSystem);
            Assert.AreEqual("Overall", results.TargetSystemDisplayName);
            Assert.IsTrue(results.ToString().Contains("Source"));
            Assert.IsTrue(results.ToString().Contains("Overall"));

            results = new StatusResults(null, null, AmbientClock.UtcNow, 1, new StatusProperty[] { StatusProperty.Create("Property", "Value") }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { new StatusResults("Source", "Target", Array.Empty<StatusResults>()) });
            Assert.IsNull(results.SourceSystem);
            Assert.AreEqual("Localhost", results.SourceSystemDisplayName);
            Assert.IsNull(results.TargetSystem);
            Assert.AreEqual("Unknown Target", results.TargetSystemDisplayName);
            Assert.IsTrue(results.ToString().Length == 0);

            results = new StatusResults(null, "/SampleDisk.Temp", AmbientClock.UtcNow, 0, new StatusProperty[] { StatusProperty.Create("AvailablePercent", "2.2"), StatusProperty.Create("AvailableBytes", "1100000000.0") }, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromSeconds(3), AmbientClock.UtcNow.AddSeconds(2), new StatusAuditAlert(StatusRating.Okay - .0001f, "ALERT", "Alert", "AlertDetails")));
            Assert.IsTrue(results.ToString().Length != 0);
        }
        [TestMethod]
        public void StatusResultsGetSummaryAlerts()
        {
            StatusResults results;

            results = new StatusResults(null, null, AmbientClock.UtcNow, 1, new StatusProperty[] { StatusProperty.Create("Property", "Value") }, StatusNatureOfSystem.ChildrenHomogenous,
                new StatusResults[]
                {
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                        new StatusResults("Source1", "Target1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.Leaf, new StatusResults[] {
                            new StatusResults("Source1.1", "Target1.1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.Leaf, new StatusResults[0]),
                            }),
                        new StatusResults("Source2", "Target2", AmbientClock.UtcNow, 1, new StatusProperty[] { }, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5))),
                        new StatusResults("Source3", "Target3", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))
                            ),
                        new StatusResults("Source4", "Target4", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Okay, "Okay", "Terse", "Details"))
                            ),
                        new StatusResults("Source5", "Target5", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))
                            ),
                        new StatusResults("Source6", "Target6", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] {
                            new StatusResults("Source6.1", "Target6.1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.Leaf, new StatusResults[0]),
                            new StatusResults("Source6.2", "Target6.2", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))
                                ),
                            new StatusResults("Source6.3", "Target6.3", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Okay, "Okay", "Terse", "Details"))
                                ),
                            new StatusResults("Source6.4", "Target6.4", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))
                                ),
                            }),
                        new StatusResults("Source7", "Target7", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                        new StatusResults("Source8", "Target8", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenIrrelevant, new StatusResults[] { }),
                    }),
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                        new StatusResults("Source1", "Target1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.Leaf, new StatusResults[] {
                            new StatusResults("Source1.1", "Target1.1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.Leaf, new StatusResults[0]),
                            }),
                        new StatusResults("Source2", "Target2", AmbientClock.UtcNow, 1, new StatusProperty[] { }, new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5))),
                        new StatusResults("Source3", "Target3", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), null, new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))
                            ),
                        new StatusResults("Source4", "Target4", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), null, new StatusAuditAlert(StatusRating.Okay, "Okay", "Terse", "Details"))
                            ),
                        new StatusResults("Source5", "Target5", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                            new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), null, new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))
                            ),
                        new StatusResults("Source6", "Target6", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[] { }),
                        new StatusResults("Source7", "Target7", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                        new StatusResults("Source8", "Target8", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenIrrelevant, new StatusResults[] {
                            new StatusResults("Source8.1", "Target8.1", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHomogenous, new StatusResults[0]),
                            new StatusResults("Source8.2", "Target8.2", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Fail, "Fail", "Terse", "Details"))
                                ),
                            new StatusResults("Source8.3", "Target8.3", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Okay, "Okay", "Terse", "Details"))
                                ),
                            new StatusResults("Source8.4", "Target8.4", AmbientClock.UtcNow, 1, new StatusProperty[] { },
                                new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.FromMilliseconds(5), AmbientClock.UtcNow.AddMinutes(1), new StatusAuditAlert(StatusRating.Alert, "Alert", "Terse", "Details"))
                                ),
                            }),
                    }),
                });
            StatusAuditAlert alert;
            alert = results.GetSummaryAlerts(true, float.MaxValue, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Okay, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Alert, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Fail, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Catastrophic, false);
            alert = results.GetSummaryAlerts(true, float.MinValue, true);
            alert = results.GetSummaryAlerts(true, float.MaxValue, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Okay, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Alert, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Fail, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Catastrophic, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, float.MinValue, true, TimeZoneInfo.Local);
        }
        [TestMethod]
        public void StatusResultsGetSummaryAlertsNoTime()
        {
            StatusResults results;

            results = new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                }),
                new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] {
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                    new StatusResults(nameof(StatusResultsGetSummaryAlertsNoTime), "/", AmbientClock.UtcNow, 1, new StatusProperty[] { }, StatusNatureOfSystem.ChildrenHeterogenous, new StatusResults[] { }),
                }),
            });
            StatusAuditAlert alert;
            alert = results.GetSummaryAlerts(true, float.MaxValue, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Okay, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Alert, false);
            alert = results.GetSummaryAlerts(true, StatusRating.Fail, false);
            alert = results.GetSummaryAlerts(true, float.MaxValue, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Okay, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Alert, false, TimeZoneInfo.Local);
            alert = results.GetSummaryAlerts(true, StatusRating.Fail, false, TimeZoneInfo.Local);
        }
        [TestMethod]
        public void StatusResultsGetSummaryAlertsNotificationTime()
        {
            using (AmbientClock.Pause())
            {
                DateTime now = AmbientClock.UtcNow;
                string shortTime = now.ToShortTimeString();
                string longTime = now.ToLongTimeString();
                StatusResults results;
                StatusAuditAlert alert;

                shortTime = now.ToShortTimeString();
                longTime = now.ToLongTimeString();
                results = new StatusResults("Source", "StatusTarget", new StatusResults[0]);
                alert = results.GetSummaryAlerts(true, StatusRating.Okay, false);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));
                alert = results.GetSummaryAlerts(true, StatusRating.Alert, false);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));
                alert = results.GetSummaryAlerts(true, StatusRating.Fail, true);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));

                now = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
                shortTime = now.ToShortTimeString();
                longTime = now.ToLongTimeString();
                alert = results.GetSummaryAlerts(true, StatusRating.Okay, false, TimeZoneInfo.Local);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));
                alert = results.GetSummaryAlerts(true, StatusRating.Alert, false, TimeZoneInfo.Local);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));
                alert = results.GetSummaryAlerts(true, StatusRating.Fail, false, TimeZoneInfo.Local);
                Assert.IsTrue(alert.Details.Contains("Okay"));
                Assert.IsTrue(alert.Details.Contains(longTime));
                Assert.IsTrue(alert.Terse.Contains("OKAY"));
                Assert.IsTrue(alert.Terse.Contains(shortTime));
            }
        }
    }
}
