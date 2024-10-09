using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AmbientServices.Test;

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
        long startTime = AmbientStatistics.Statistics["ExecutionTime"].CurrentValue;

        IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter", "counter test");
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
        counter.SetValue(10.0f);
        counter.SetValue(10.0);
        Assert.AreEqual(10, counter.CurrentValue);
        Assert.AreEqual(null, counter.ExpectedMin);
        Assert.AreEqual(null, counter.ExpectedMax);
        Assert.AreEqual(1.0, counter.FixedFloatingPointAdjustment);
        Assert.AreEqual(0, counter.FixedFloatingPointDigits);
        IAmbientStatistic sameCounter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter", "counter test");
        Assert.AreEqual(counter, sameCounter);
        IAmbientStatistic replacedCounter = AmbientStatistics.GetOrAddStatistic(false, "counter", "counter", "counter test", true);
        Assert.AreNotEqual(counter, replacedCounter);

        IAmbientStatistic timeBasedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based", "time-based test");
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
            Assert.AreEqual(null, counter.ExpectedMin);
            Assert.AreEqual(null, counter.ExpectedMax);
            Assert.AreEqual(1.0, counter.FixedFloatingPointAdjustment);
            Assert.AreEqual(0, counter.FixedFloatingPointDigits);
            timeBasedCounter.SetValue(10);
            Assert.AreEqual(10, timeBasedCounter.CurrentValue);
            sameCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based", "time-based test");
            Assert.AreEqual(timeBasedCounter, sameCounter);
            using (replacedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based", "time-based", "time-based test", true))
            {
                Assert.AreNotEqual(timeBasedCounter, replacedCounter);
            }

            using IAmbientStatistic unreplacedCounter = AmbientStatistics.GetOrAddStatistic(true, "time-based-2", "time-based-2", "time-based-2 test", false);
            Assert.AreNotEqual(unreplacedCounter, replacedCounter);
        }

        IAmbientStatisticReader executionTime = AmbientStatistics.Statistics["ExecutionTime"];
        long endTime = executionTime.CurrentValue;
        replacedCounter.SetValue(endTime - startTime);
        Assert.IsTrue(endTime >= startTime);
        Assert.AreEqual(0, executionTime.ExpectedMin);
        Assert.AreEqual(null, executionTime.ExpectedMax);
        Assert.AreEqual(1.0, executionTime.FixedFloatingPointAdjustment);
        Assert.AreEqual(0, executionTime.FixedFloatingPointDigits);

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
        Assert.AreEqual(AggregationTypes.MostRecent, runTime.PreferredTemporalAggregationType);
        Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max, runTime.SpatialAggregationTypes);
        Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max, runTime.TemporalAggregationTypes);
        IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(false, nameof(AmbientPerformanceMetricsProperties), nameof(AmbientPerformanceMetricsProperties) + " Test", nameof(AmbientPerformanceMetricsProperties), true, null
            , 0, null, null, 0
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
                Assert.ThrowsException<InvalidOperationException>(() => AmbientStatistics.GetOrAddStatistic(true, kvp.Key, kvp.Key, "", false));
                break;
            }
        }
    }
    [TestMethod]
    public void StatisticsMissingSamples()
    {
        long?[] samples;

        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { });
        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { null, 10, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10 });
        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { null, 10, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20 });
        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { 10, null, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20, 30, 40 });
        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { 10, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20, 30, 40 });

        samples = MissingSampleHandling.Zero.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 0, 0, 0, 0, 0 });
        samples = MissingSampleHandling.Zero.HandleMissingSamples(new long?[] { null, 10, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 0, 10, 0, 0, 0 });
        samples = MissingSampleHandling.Zero.HandleMissingSamples(new long?[] { null, 10, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 0, 10, 0, 20, 0 });
        samples = MissingSampleHandling.Zero.HandleMissingSamples(new long?[] { 10, null, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 0, 20, 30, 40 });
        samples = MissingSampleHandling.Zero.HandleMissingSamples(new long?[] { 10, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20, 30, 40 });

        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { null, null, null, null, null });
        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 10, 10, 10, 10 });
        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { null, 10, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 5, 10, 15, 20, 25 });
        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 7, 10, 13, 17, 20, 23 });
        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { 10, null, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 15, 20, 30, 40 });
        samples = MissingSampleHandling.LinearEstimation.HandleMissingSamples(new long?[] { 10, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20, 30, 40 });

        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { null, null, null, null, null });
        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 10, 10, 10, 10 });
        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { null, 10, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 7, 10, 14, 20, 28 });
        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, 20, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 8, 10, 13, 16, 20, 25 });
        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { 10, null, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 14, 20, 30, 40 });
        samples = MissingSampleHandling.ExponentialEstimation.HandleMissingSamples(new long?[] { 10, 20, 30, 40 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 20, 30, 40 });

        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { null, null, null, null, null });
        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 10, 10, 10, 10 });
        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { null, 10, null, 11, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 8, 10, 11, 11, 11 });
        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { null, 10, null, null, 11, null }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 9, 10, 10, 11, 11, 11 });
        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { 10, null, 11, 13, 14 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 11, 11, 13, 14 });
        samples = MissingSampleHandling.LogarithmicEstimation.HandleMissingSamples(new long?[] { 10, 11, 13, 14 }).ToArray();
        AssertArraysEqualByValue(samples, new long?[] { 10, 11, 13, 14 });
    }
    private static void AssertArraysEqualByValue(long?[] a, long?[] b)
    {
        Assert.IsTrue(a.SequenceEqual(b));
    }
}
