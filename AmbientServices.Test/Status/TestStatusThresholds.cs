using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestStatusThresholds
    {
        [TestMethod]
        public void StatusThresholdsClass()
        {
            StatusPropertyThresholds thresholds;
            StatusAuditAlert alert;

            thresholds = new StatusPropertyThresholds(10.0, 20.0, 30.0);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(30.0, 20.0, 10.0);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));




            thresholds = new StatusPropertyThresholds(null, 20.0, 30.0);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(null, 20.0, 10.0);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));




            thresholds = new StatusPropertyThresholds(null, null, 30.0, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(null, null, 10.0, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));




            thresholds = new StatusPropertyThresholds(10.0, null, null, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));


            thresholds = new StatusPropertyThresholds(30.0, null, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));




            thresholds = new StatusPropertyThresholds(10.0, 20.0, null, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));


            thresholds = new StatusPropertyThresholds(30.0, 20.0, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));




            thresholds = new StatusPropertyThresholds(null, null, null, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));


            thresholds = new StatusPropertyThresholds(null, null, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));




            thresholds = new StatusPropertyThresholds(10.0, null, 30.0, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(30.0, null, 10.0, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 5.0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));




            thresholds = new StatusPropertyThresholds(10.0, 20.0, 30.0);
            alert = thresholds.Rate("Property", Double.MaxValue);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            thresholds = new StatusPropertyThresholds(10.0, 20.0, 30.0);
            alert = thresholds.Rate("Property", Double.PositiveInfinity);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));
        }
        [TestMethod]
        public void StatusThresholdsRange()
        {
            StatusPropertyThresholds thresholds;
            StatusAuditAlert alert;

            thresholds = new StatusPropertyThresholds(10.0, 20.0, 30.0);
            for (double testLowValue = -5.0; testLowValue < 35.0; testLowValue += 5.0)
            {
                for (double testHighValue = testLowValue; testHighValue < 35.0; testHighValue += 5.0)
                {
                    double significantValue = testLowValue;
                    StatusRatingRange expectedRatingRange;
                    if (significantValue <= 10.0) expectedRatingRange = StatusRatingRange.Fail;
                    else if (significantValue <= 20.0) expectedRatingRange = StatusRatingRange.Alert;
                    else if (significantValue <= 30.0) expectedRatingRange = StatusRatingRange.Okay;
                    else expectedRatingRange = StatusRatingRange.Superlative;
                    alert = thresholds.Rate("Property", testLowValue, testHighValue);
                    Assert.AreEqual(expectedRatingRange, StatusRating.FindRange(alert.Rating));
                }
            }

            thresholds = new StatusPropertyThresholds(30.0, 20.0, 10.0);
            for (double testLowValue = -5.0; testLowValue < 35.0; testLowValue += 5.0)
            {
                for (double testHighValue = testLowValue; testHighValue < 35.0; testHighValue += 5.0)
                {
                    double significantValue = testHighValue;
                    StatusRatingRange expectedRatingRange;
                    if (significantValue >= 30.0) expectedRatingRange = StatusRatingRange.Fail;
                    else if (significantValue >= 20.0) expectedRatingRange = StatusRatingRange.Alert;
                    else if (significantValue >= 10.0) expectedRatingRange = StatusRatingRange.Okay;
                    else expectedRatingRange = StatusRatingRange.Superlative;
                    alert = thresholds.Rate("Property", testLowValue, testHighValue);
                    Assert.AreEqual(expectedRatingRange, StatusRating.FindRange(alert.Rating));
                }
            }
        }
        [TestMethod]
        public void StatusThresholdsExceptions()
        {
            Assert.ThrowsException<ArgumentException>(() => { StatusPropertyThresholds s = new StatusPropertyThresholds(2.0, 3.0, 1.0); });
            //StatusThresholds thresholds;
            //StatusAuditAlert alert;
//            thresholds = new StatusThresholds(1.0, 2.0, 3.0);
//            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { alert = thresholds.Rate("Property", -0.0001); });
        }
        [TestMethod]
        public void DefaultPropertyThresholdsProperty()
        {
            DefaultPropertyThresholdsAttribute property;
            property = typeof(DefaultPropertyThresholdsAttributeDeferSource).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathDeferSource", property.PropertyPath);
            Assert.AreEqual(typeof(DefaultPropertyThresholdsAttributeTestDeferTarget), property.DeferToType);
            Assert.IsNull(property.Thresholds);

            property = typeof(DefaultPropertyThresholdsAttributeTestDeferTarget).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathDeferTarget", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(1.0, property.Thresholds.FailVsAlertThreshold);
            Assert.AreEqual(2.0, property.Thresholds.AlertVsOkayThreshold);
            Assert.AreEqual(3.0, property.Thresholds.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.HighIsGood, property.Thresholds.Nature);
            Assert.AreEqual(1.0, property.FailureThreshold);
            Assert.AreEqual(2.0, property.AlertThreshold);
            Assert.AreEqual(3.0, property.OkayThreshold);
            Assert.AreEqual(StatusThresholdNature.HighIsGood, property.ThresholdNature);

            property = typeof(DefaultPropertyThresholdsAttributeTestNoFailure).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathNoFailure", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.IsNull(property.Thresholds.FailVsAlertThreshold);
            Assert.AreEqual(2.0, property.Thresholds.AlertVsOkayThreshold);
            Assert.AreEqual(1.0, property.Thresholds.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds.Nature);
            Assert.IsTrue(double.IsNaN(property.FailureThreshold));
            Assert.AreEqual(2.0, property.AlertThreshold);
            Assert.AreEqual(1.0, property.OkayThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = typeof(DefaultPropertyThresholdsAttributeTestNoAlert).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathNoAlert", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(3.0, property.Thresholds.FailVsAlertThreshold);
            Assert.IsNull(property.Thresholds.AlertVsOkayThreshold);
            Assert.AreEqual(1.0, property.Thresholds.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds.Nature);
            Assert.AreEqual(3.0, property.FailureThreshold);
            Assert.IsTrue(double.IsNaN(property.AlertThreshold));
            Assert.AreEqual(1.0, property.OkayThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = typeof(DefaultPropertyThresholdsAttributeTestNoOkay).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathNoOkay", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(3.0, property.Thresholds.FailVsAlertThreshold);
            Assert.AreEqual(2.0, property.Thresholds.AlertVsOkayThreshold);
            Assert.IsNull(property.Thresholds.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds.Nature);
            Assert.AreEqual(3.0, property.FailureThreshold);
            Assert.AreEqual(2.0, property.AlertThreshold);
            Assert.IsTrue(double.IsNaN(property.OkayThreshold));
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = typeof(DefaultPropertyThresholdsAttributeTestNoThreshold).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute;
            Assert.AreEqual("PropertyPathNoThreshold", property.PropertyPath);
            Assert.IsNull(property.Thresholds.FailVsAlertThreshold);
            Assert.IsNull(property.Thresholds.AlertVsOkayThreshold);
            Assert.IsNull(property.Thresholds.OkayVsSuperlativeThreshold);
        }
        [TestMethod]
        public void DefaultStatusThresholdsClass()
        {
            ConcurrentDictionary<string, StatusPropertyThresholds> thresholds = new ConcurrentDictionary<string, StatusPropertyThresholds>();
            AmbientServices.StatusPropertyThresholds thresholds1 = new AmbientServices.StatusPropertyThresholds(null, null, 50000);
            thresholds.TryAdd("KEY1", thresholds1);
            AmbientServices.StatusPropertyThresholds thresholds2 = new AmbientServices.StatusPropertyThresholds(40, 100, 5000);
            thresholds.TryAdd("KEY2", thresholds2);
            AmbientServices.StatusPropertyThresholds thresholds3 = new AmbientServices.StatusPropertyThresholds(1000, 0, -1000);
            thresholds.TryAdd("KEY3", thresholds3);
            IStatusThresholdsRegistry defaultThresholds = new DefaultStatusThresholds(thresholds);
            Assert.AreEqual(thresholds1, defaultThresholds.GetThresholds("key1"));
            Assert.AreEqual(thresholds2, defaultThresholds.GetThresholds("key2"));
            Assert.AreEqual(thresholds3, defaultThresholds.GetThresholds("key3"));
            Assert.IsNull(defaultThresholds.GetThresholds("key4"));
        }
        [TestMethod]
        public void StatusThresholdsDefaultPropertyThresholds()
        {
            //[DefaultPropertyThresholds("AvailableBytes", 1000000000.0, 10000000000.0, 100000000000.0, StatusThresholdNature.HighIsGood)]
            //[DefaultPropertyThresholds("AvailablePercent", 1.0, 2.5, 5.0, StatusThresholdNature.HighIsGood)]
            //class SampleVolumeAuditor

            IStatusThresholdsRegistry defaultThresholds = StatusPropertyThresholds.DefaultPropertyThresholds;
            Assert.AreEqual(1.0, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent").FailVsAlertThreshold);
            Assert.AreEqual(2.5, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent").AlertVsOkayThreshold);
            Assert.AreEqual(5.0, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent").OkayVsSuperlativeThreshold);
        }
    }
    [DefaultPropertyThresholds("PropertyPathDeferSource", typeof(DefaultPropertyThresholdsAttributeTestDeferTarget))]
    class DefaultPropertyThresholdsAttributeDeferSource
    {
    }
    [DefaultPropertyThresholds("PropertyPathDeferTarget", 1.0, 2.0, 3.0, StatusThresholdNature.HighIsGood)]
    class DefaultPropertyThresholdsAttributeTestDeferTarget
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoFailure", Double.NaN, 2.0, 1.0, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoFailure
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoAlert", 3.0, Double.NaN, 1.0, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoAlert
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoOkay", 3.0, 2.0, Double.NaN, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoOkay
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoThreshold")]
    class DefaultPropertyThresholdsAttributeTestNoThreshold
    {
    }
}
