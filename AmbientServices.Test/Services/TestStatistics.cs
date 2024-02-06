using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestStatistics
    {
        private static readonly AmbientService<IAmbientStatistics> _AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
        private static readonly IAmbientStatistics AmbientStatistics = _AmbientStatistics.Local;

        [TestMethod]
        public void AmbientPerformanceMetricsBasic()
        {
            Assert.IsNotNull(AmbientStatistics);

            double timeUnitsPerSecond = Stopwatch.Frequency;
            long startTime = AmbientStatistics.Statistics["ExecutionTime"].SampleValue;

            IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter test");
            Assert.AreEqual(1, counter.Increment());
            Assert.AreEqual(3, counter.Add(2));
            Assert.AreEqual(2, counter.Decrement());
            Assert.AreEqual("counter", counter.Id);
            Assert.AreEqual("counter test", counter.Description);
            Assert.AreEqual(false, counter.IsTimeBased);
            Assert.AreEqual(5, counter.SetMax(5));
            Assert.AreEqual(5, counter.SetMax(3));
            Assert.AreEqual(3, counter.SetMin(3));
            Assert.AreEqual(3, counter.SetMin(5));
            counter.SetValue(10);
            Assert.AreEqual(10, counter.SampleValue);
            IAmbientStatistic sameCounter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter test");
            Assert.AreEqual(counter, sameCounter);
            IAmbientStatistic replacedCounter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter test", true);
            Assert.AreNotEqual(counter, replacedCounter);

            IAmbientStatistic timeBasedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based test");
            using ((IDisposable)timeBasedCounter)
            {
                Assert.AreEqual(1, timeBasedCounter.Increment());
                Assert.AreEqual(3, timeBasedCounter.Add(2));
                Assert.AreEqual(2, timeBasedCounter.Decrement());
                Assert.AreEqual("time-based", timeBasedCounter.Id);
                Assert.AreEqual("time-based test", timeBasedCounter.Description);
                Assert.AreEqual(true, timeBasedCounter.IsTimeBased);
                Assert.AreEqual(5, timeBasedCounter.SetMax(5));
                Assert.AreEqual(5, timeBasedCounter.SetMax(3));
                Assert.AreEqual(3, timeBasedCounter.SetMin(3));
                Assert.AreEqual(3, timeBasedCounter.SetMin(5));
                timeBasedCounter.SetValue(10);
                Assert.AreEqual(10, timeBasedCounter.SampleValue);
                sameCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based test");
                Assert.AreEqual(timeBasedCounter, sameCounter);
                using (replacedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based test", true))
                {
                    Assert.AreNotEqual(timeBasedCounter, replacedCounter);
                }

                using IAmbientStatistic unreplacedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based-2", "time-based-2 test", false);
                Assert.AreNotEqual(unreplacedCounter, replacedCounter);
            }

            long endTime = AmbientStatistics.Statistics["ExecutionTime"].SampleValue;
            replacedCounter.SetValue(endTime - startTime);
            Assert.IsTrue(endTime >= startTime);

            Assert.IsTrue(AmbientStatistics.RemoveStatistic(counter.Id));
        }
        [TestMethod]
        public void AmbientPerformanceMetricsNotFound()
        {
            IAmbientStatisticReader runTime = AmbientStatistics.ReadStatistic("vchjxoiuhsufihjkvlhjxkhusidohvuicxv");
            Assert.IsNull(runTime);
        }
        [TestMethod]
        public void AmbientPerformanceMetricsRunTime()
        {
            IAmbientStatisticReader runTime = AmbientStatistics.Statistics["ExecutionTime"];
            Assert.IsTrue(runTime.IsTimeBased);
            Assert.IsTrue(runTime.Description.Contains(" ticks "));

            Assert.AreEqual(runTime, AmbientStatistics.ReadStatistic("ExecutionTime"));

            Assert.IsFalse(AmbientStatistics.RemoveStatistic(runTime.Id));
        }
        [TestMethod]
        public void AmbientPerformanceMetricsProperties()
        {
            IAmbientStatisticReader runTime = AmbientStatistics.Statistics["ExecutionTime"];
            Assert.AreEqual(MissingSampleHandling.LinearEstimation, runTime.MissingSampleHandling);
            Assert.AreEqual(AggregationTypes.Average, runTime.PreferredSpatialAggregationType);
            Assert.AreEqual(AggregationTypes.Average, runTime.PreferredTemporalAggregationType);
            Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max, runTime.SpatialAggregationTypes);
            Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max, runTime.TemporalAggregationTypes);
            IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(false, nameof(AmbientPerformanceMetricsProperties), nameof(AmbientPerformanceMetricsProperties) + " Test", true, 0
                , AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
                , AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.Sum
                , AggregationTypes.MostRecent, AggregationTypes.MostRecent, MissingSampleHandling.ExponentialEstimation);
            Assert.AreEqual(MissingSampleHandling.ExponentialEstimation, counter.MissingSampleHandling);
            Assert.AreEqual(AggregationTypes.MostRecent, counter.PreferredSpatialAggregationType);
            Assert.AreEqual(AggregationTypes.MostRecent, counter.PreferredTemporalAggregationType);
            Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.Sum, counter.SpatialAggregationTypes);
            Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max, counter.TemporalAggregationTypes);
        }
        [TestMethod]
        public void AmbientPerformanceMetricsException()
        {
            foreach (KeyValuePair<string, IAmbientStatisticReader> kvp in AmbientStatistics.Statistics)
            {
                // is this one readonly?
                if (kvp.Value is not IAmbientStatistic)
                {
                    Assert.ThrowsException<InvalidOperationException>(() => AmbientStatistics.GetOrAddStatistic(true, kvp.Key, "", false));
                    break;
                }
            }
        }
    }
}
