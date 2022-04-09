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

            thresholds = new StatusPropertyThresholds(10.0f, 20.0f, 30.0f);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(30.0f, 20.0f, 10.0f);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0f);
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




            thresholds = new StatusPropertyThresholds(null, 20.0f, 30.0f);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(null, 20.0f, 10.0f);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0f);
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




            thresholds = new StatusPropertyThresholds(null, null, 30.0f, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Rating < StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(null, null, 10.0f, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 5.0f);
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




            thresholds = new StatusPropertyThresholds(10.0f, null, null, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));


            thresholds = new StatusPropertyThresholds(30.0f, null, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 5.0f);
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




            thresholds = new StatusPropertyThresholds(10.0f, 20.0f, null, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));


            thresholds = new StatusPropertyThresholds(30.0f, 20.0f, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Alert);
            Assert.IsTrue(alert.Rating < StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Okay) + " range"));

            alert = thresholds.Rate("Property", 5.0f);
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

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));


            thresholds = new StatusPropertyThresholds(null, null, null, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));




            thresholds = new StatusPropertyThresholds(10.0f, null, 30.0f, StatusThresholdNature.HighIsGood);
            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Catastrophic);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating < StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("at or below"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains("<="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating >= StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));


            thresholds = new StatusPropertyThresholds(30.0f, null, 10.0f, StatusThresholdNature.LowIsGood);
            alert = thresholds.Rate("Property", 35.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating <= StatusRating.Fail);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("at or above"));

            alert = thresholds.Rate("Property", 25.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 15.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Fail);
            Assert.IsTrue(alert.Rating < StatusRating.Alert);
            Assert.IsTrue(alert.Terse.Contains(">="));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Alert) + " range"));

            alert = thresholds.Rate("Property", 5.0f);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating > StatusRating.Okay);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            alert = thresholds.Rate("Property", 0);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains("<"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));




            thresholds = new StatusPropertyThresholds(10.0f, 20.0f, 30.0f);
            alert = thresholds.Rate("Property", Single.MaxValue);
            Assert.AreEqual("Property.Threshold", alert.AuditAlertCode);
            Assert.IsTrue(alert.Rating == StatusRating.Superlative);
            Assert.IsTrue(alert.Terse.Contains(">"));
            Assert.IsTrue(alert.Details.Contains("in the " + nameof(StatusRating.Superlative) + " range"));

            thresholds = new StatusPropertyThresholds(10.0f, 20.0f, 30.0f);
            alert = thresholds.Rate("Property", Single.PositiveInfinity);
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

            thresholds = new StatusPropertyThresholds(10.0f, 20.0f, 30.0f);
            for (float testLowValue = -5.0f; testLowValue < 35.0f; testLowValue += 5.0f)
            {
                for (float testHighValue = testLowValue; testHighValue < 35.0f; testHighValue += 5.0f)
                {
                    float significantValue = testLowValue;
                    StatusRatingRange expectedRatingRange;
                    if (significantValue <= 10.0f) expectedRatingRange = StatusRatingRange.Fail;
                    else if (significantValue <= 20.0f) expectedRatingRange = StatusRatingRange.Alert;
                    else if (significantValue <= 30.0f) expectedRatingRange = StatusRatingRange.Okay;
                    else expectedRatingRange = StatusRatingRange.Superlative;
                    alert = thresholds.Rate("Property", testLowValue, testHighValue);
                    Assert.AreEqual(expectedRatingRange, StatusRating.FindRange(alert.Rating));
                }
            }

            thresholds = new StatusPropertyThresholds(30.0f, 20.0f, 10.0f);
            for (float testLowValue = -5.0f; testLowValue < 35.0f; testLowValue += 5.0f)
            {
                for (float testHighValue = testLowValue; testHighValue < 35.0f; testHighValue += 5.0f)
                {
                    float significantValue = testHighValue;
                    StatusRatingRange expectedRatingRange;
                    if (significantValue >= 30.0f) expectedRatingRange = StatusRatingRange.Fail;
                    else if (significantValue >= 20.0f) expectedRatingRange = StatusRatingRange.Alert;
                    else if (significantValue >= 10.0f) expectedRatingRange = StatusRatingRange.Okay;
                    else expectedRatingRange = StatusRatingRange.Superlative;
                    alert = thresholds.Rate("Property", testLowValue, testHighValue);
                    Assert.AreEqual(expectedRatingRange, StatusRating.FindRange(alert.Rating));
                }
            }
        }
        [TestMethod]
        public void StatusThresholdsExceptions()
        {
            Assert.ThrowsException<ArgumentException>(() => { StatusPropertyThresholds s = new(2.0f, 3.0f, 1.0f); });
            //StatusThresholds thresholds;
            //StatusAuditAlert alert;
//            thresholds = new StatusThresholds(1.0f, 2.0f, 3.0f);
//            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { alert = thresholds.Rate("Property", -0.0f001); });
        }
        [TestMethod]
        public void DefaultPropertyThresholdsProperty()
        {
            DefaultPropertyThresholdsAttribute property;
            property = (typeof(DefaultPropertyThresholdsAttributeDeferSource).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathDeferSource", property.PropertyPath);
            Assert.AreEqual(typeof(DefaultPropertyThresholdsAttributeTestDeferTarget), property.DeferToType);
            Assert.IsNull(property.Thresholds);

            property = (typeof(DefaultPropertyThresholdsAttributeTestDeferTarget).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathDeferTarget", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(1.0f, property.Thresholds?.FailVsAlertThreshold);
            Assert.AreEqual(2.0f, property.Thresholds?.AlertVsOkayThreshold);
            Assert.AreEqual(3.0f, property.Thresholds?.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.HighIsGood, property.Thresholds?.Nature);
            Assert.AreEqual(1.0f, property.FailVsAlertThreshold);
            Assert.AreEqual(2.0f, property.AlertVsOkayThreshold);
            Assert.AreEqual(3.0f, property.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.HighIsGood, property.ThresholdNature);

            property = (typeof(DefaultPropertyThresholdsAttributeTestNoFailure).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathNoFailure", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.IsNull(property.Thresholds?.FailVsAlertThreshold);
            Assert.AreEqual(2.0f, property.Thresholds?.AlertVsOkayThreshold);
            Assert.AreEqual(1.0f, property.Thresholds?.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds?.Nature);
            Assert.IsTrue(float.IsNaN(property.FailVsAlertThreshold));
            Assert.AreEqual(2.0f, property.AlertVsOkayThreshold);
            Assert.AreEqual(1.0f, property.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = (typeof(DefaultPropertyThresholdsAttributeTestNoAlert).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathNoAlert", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(3.0f, property.Thresholds?.FailVsAlertThreshold);
            Assert.IsNull(property.Thresholds?.AlertVsOkayThreshold);
            Assert.AreEqual(1.0f, property.Thresholds?.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds?.Nature);
            Assert.AreEqual(3.0f, property.FailVsAlertThreshold);
            Assert.IsTrue(float.IsNaN(property.AlertVsOkayThreshold));
            Assert.AreEqual(1.0f, property.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = (typeof(DefaultPropertyThresholdsAttributeTestNoOkay).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathNoOkay", property.PropertyPath);
            Assert.IsNull(property.DeferToType);
            Assert.AreEqual(3.0f, property.Thresholds?.FailVsAlertThreshold);
            Assert.AreEqual(2.0f, property.Thresholds?.AlertVsOkayThreshold);
            Assert.IsNull(property.Thresholds?.OkayVsSuperlativeThreshold);
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.Thresholds?.Nature);
            Assert.AreEqual(3.0f, property.FailVsAlertThreshold);
            Assert.AreEqual(2.0f, property.AlertVsOkayThreshold);
            Assert.IsTrue(float.IsNaN(property.OkayVsSuperlativeThreshold));
            Assert.AreEqual(StatusThresholdNature.LowIsGood, property.ThresholdNature);

            property = (typeof(DefaultPropertyThresholdsAttributeTestNoThreshold).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathNoThreshold", property.PropertyPath);
            Assert.IsNull(property.Thresholds?.FailVsAlertThreshold);
            Assert.IsNull(property.Thresholds?.AlertVsOkayThreshold);
            Assert.IsNull(property.Thresholds?.OkayVsSuperlativeThreshold);

            property = (typeof(DefaultPropertyThresholdsAttributeTestNullThresholds).GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), false)[0] as DefaultPropertyThresholdsAttribute)!;
            Assert.AreEqual("PropertyPathNullThresholds", property.PropertyPath);
            Assert.IsTrue(float.IsNaN(property.FailVsAlertThreshold));
            Assert.IsTrue(float.IsNaN(property.AlertVsOkayThreshold));
            Assert.IsTrue(float.IsNaN(property.OkayVsSuperlativeThreshold));
            Assert.AreEqual(StatusThresholdNature.HighIsGood, property.ThresholdNature);
        }
        [TestMethod]
        public void DefaultStatusThresholdsClass()
        {
            ConcurrentDictionary<string, StatusPropertyThresholds> thresholds = new();
            AmbientServices.StatusPropertyThresholds thresholds1 = new(null, null, 50000);
            thresholds.TryAdd("KEY1", thresholds1);
            AmbientServices.StatusPropertyThresholds thresholds2 = new(40, 100, 5000);
            thresholds.TryAdd("KEY2", thresholds2);
            AmbientServices.StatusPropertyThresholds thresholds3 = new(1000, 0, -1000);
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
            //[DefaultPropertyThresholds("AvailableBytes", 1000000000.0f, 10000000000.0f, 100000000000.0f, StatusThresholdNature.HighIsGood)]
            //[DefaultPropertyThresholds("AvailablePercent", 1.0f, 2.5f, 5.0f, StatusThresholdNature.HighIsGood)]
            //class SampleVolumeAuditor

            IStatusThresholdsRegistry defaultThresholds = StatusPropertyThresholds.DefaultPropertyThresholds;
            Assert.AreEqual(1.0f, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent")?.FailVsAlertThreshold);
            Assert.AreEqual(2.5f, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent")?.AlertVsOkayThreshold);
            Assert.AreEqual(5.0f, defaultThresholds.GetThresholds("SampleDisk.Temp.AvailablePercent")?.OkayVsSuperlativeThreshold);
        }
    }
    [DefaultPropertyThresholds("PropertyPathDeferSource", typeof(DefaultPropertyThresholdsAttributeTestDeferTarget))]
    class DefaultPropertyThresholdsAttributeDeferSource
    {
    }
    [DefaultPropertyThresholds("PropertyPathDeferTarget", 1.0f, 2.0f, 3.0f, StatusThresholdNature.HighIsGood)]
    class DefaultPropertyThresholdsAttributeTestDeferTarget
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoFailure", Single.NaN, 2.0f, 1.0f, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoFailure
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoAlert", 3.0f, Single.NaN, 1.0f, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoAlert
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoOkay", 3.0f, 2.0f, Single.NaN, StatusThresholdNature.LowIsGood)]
    class DefaultPropertyThresholdsAttributeTestNoOkay
    {
    }
    [DefaultPropertyThresholds("PropertyPathNoThreshold")]
    class DefaultPropertyThresholdsAttributeTestNoThreshold
    {
    }
    [DefaultPropertyThresholds("PropertyPathNullThresholds", typeof(int))]
    class DefaultPropertyThresholdsAttributeTestNullThresholds
    {
    }
}
