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
        long startTime = _AmbientStatistics.Global?.ExecutionTime.CurrentRawValue ?? 0;

        IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Raw, "counter", "counter", "counter test");
        Assert.AreEqual(1, counter.IncrementRaw());
        Assert.AreEqual(3, counter.AddRaw(2));
        Assert.AreEqual(2, counter.DecrementRaw());
        Assert.AreEqual("counter", counter.Id);
        Assert.AreEqual("counter test", counter.Description);
        Assert.AreEqual(5, counter.SetRawMax(5));
        Assert.AreEqual(5, counter.SetRawMax(3));
        Assert.AreEqual(3, counter.SetRawMin(3));
        Assert.AreEqual(3, counter.SetRawMin(5));
        counter.SetValue(10.0f);
        counter.SetValue(10.0);
        counter.SetValue(10L);
        Assert.AreEqual(10, counter.CurrentRawValue);
        Assert.AreEqual(null, counter.ExpectedMinimumRawValue);
        Assert.AreEqual(null, counter.ExpectedMaximumRawValue);
        Assert.AreEqual(1.0, counter.FixedFloatingPointAdjustment);
        IAmbientStatistic sameCounter = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Max, "counter", "counter", "counter test");
        Assert.AreEqual(counter, sameCounter);
        IAmbientStatistic replacedCounter = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Raw, "counter", "counter", "counter test", true);
        Assert.AreNotEqual(counter, replacedCounter);

        IAmbientStatistic timeBasedCounter = AmbientStatistics.GetOrAddTimeBasedStatistic(AmbientStatisicType.Raw, "time-based", "time-based", "time-based test");
        using ((IDisposable)timeBasedCounter)
        {
            Assert.AreEqual(1, timeBasedCounter.IncrementRaw());
            Assert.AreEqual(3, timeBasedCounter.AddRaw(2));
            Assert.AreEqual(2, timeBasedCounter.DecrementRaw());
            Assert.AreEqual("time-based", timeBasedCounter.Id);
            Assert.AreEqual("time-based test", timeBasedCounter.Description);
            Assert.AreEqual(5, timeBasedCounter.SetRawMax(5));
            Assert.AreEqual(5, timeBasedCounter.SetRawMax(3));
            Assert.AreEqual(3, timeBasedCounter.SetRawMin(3));
            Assert.AreEqual(3, timeBasedCounter.SetRawMin(5));
            Assert.AreEqual(null, counter.ExpectedMinimumRawValue);
            Assert.AreEqual(null, counter.ExpectedMaximumRawValue);
            Assert.AreEqual(1.0, counter.FixedFloatingPointAdjustment);
            timeBasedCounter.SetRawValue(10);
            Assert.AreEqual(10, timeBasedCounter.CurrentRawValue);
            sameCounter = AmbientStatistics.GetOrAddTimeBasedStatistic(AmbientStatisicType.Cumulative, "time-based", "time-based", "time-based test");
            Assert.AreEqual(timeBasedCounter, sameCounter);
            using (replacedCounter = AmbientStatistics.GetOrAddTimeBasedStatistic(AmbientStatisicType.Cumulative, "time-based", "time-based", "time-based test", true))
            {
                Assert.AreNotEqual(timeBasedCounter, replacedCounter);
            }

            using IAmbientStatistic unreplacedCounter = AmbientStatistics.GetOrAddTimeBasedStatistic(AmbientStatisicType.Cumulative, "time-based-2", "time-based-2", "time-based-2 test", false);
            Assert.AreNotEqual(unreplacedCounter, replacedCounter);
        }

        IAmbientStatisticReader executionTime = AmbientStatistics.Statistics["ExecutionTime"];
        long endTime = executionTime.CurrentRawValue;
        replacedCounter.SetRawValue(endTime - startTime);
        Assert.IsTrue(endTime >= startTime);
        Assert.AreEqual(0, executionTime.ExpectedMinimumRawValue);
        Assert.AreEqual(null, executionTime.ExpectedMaximumRawValue);
        Assert.AreEqual(Stopwatch.Frequency, executionTime.FixedFloatingPointAdjustment);

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
        Assert.IsTrue(runTime.Description.Contains(" seconds "));
        Assert.IsTrue(runTime.AdjustedUnits.Equals("seconds"));

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
        Assert.AreEqual(AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, runTime.TemporalAggregationTypes);
        IAmbientStatistic counter = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Min, nameof(AmbientPerformanceMetricsProperties), nameof(AmbientPerformanceMetricsProperties) + " Test", nameof(AmbientPerformanceMetricsProperties), true
            , 0, null, null, null, 1.0
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
        using IAmbientStatistic stat = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(AmbientPerformanceMetricsException), "exception", "exception test");
        foreach (KeyValuePair<string, IAmbientStatisticReader> kvp in AmbientStatistics.Statistics)
        {
            // is this one readonly?
            if (kvp.Value is not IAmbientStatistic)
            {
                Assert.ThrowsException<InvalidOperationException>(() => AmbientStatistics.GetOrAddTimeBasedStatistic(AmbientStatisicType.Raw, kvp.Key, kvp.Key, "", false));
                break;
            }
        }
    }
    [TestMethod]
    public void PreferredSpatialAggregation()
    {
        IAmbientStatistics stats = new BasicAmbientStatistics();
        using IAmbientStatistic statAverage = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statAverage), nameof(statAverage), $"test {nameof(statAverage)}", preferredSpatialAggregationType: AggregationTypes.Average, preferredTemporalAggregationType: AggregationTypes.Average);
        using IAmbientStatistic statMin = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMin), nameof(statMin), $"test {nameof(statMin)}", preferredSpatialAggregationType: AggregationTypes.Min, preferredTemporalAggregationType: AggregationTypes.Min);
        using IAmbientStatistic statMax = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMax), nameof(statMax), $"test {nameof(statMax)}", preferredSpatialAggregationType: AggregationTypes.Max, preferredTemporalAggregationType: AggregationTypes.Max);
        using IAmbientStatistic statMostRecent = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMostRecent), nameof(statMostRecent), $"test {nameof(statMostRecent)}", preferredSpatialAggregationType: AggregationTypes.MostRecent, preferredTemporalAggregationType: AggregationTypes.MostRecent);
        using IAmbientStatistic statOther = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statOther), nameof(statOther), $"test {nameof(statOther)}", preferredSpatialAggregationType: AggregationTypes.Sum, preferredTemporalAggregationType: AggregationTypes.Sum);
        long?[] samples1 = new long?[] { 10, null, 11, 12, 15, 14, 13 };

        Assert.AreEqual(12, statAverage.PreferredSpatialAggregation(samples1));
        Assert.AreEqual(10, statMin.PreferredSpatialAggregation(samples1));
        Assert.AreEqual(15, statMax.PreferredSpatialAggregation(samples1));
        Assert.AreEqual(13, statMostRecent.PreferredSpatialAggregation(samples1));
        Assert.AreEqual(75, statOther.PreferredSpatialAggregation(samples1));

        long?[] samples2 = new long?[] { null };
        Assert.IsNull(statAverage.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMin.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMax.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMostRecent.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statOther.PreferredSpatialAggregationType.Aggregate(samples2));
    }
    [TestMethod]
    public void PreferredTemporalAggregation()
    {
        IAmbientStatistics stats = new BasicAmbientStatistics();
        using IAmbientStatistic statAverage = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statAverage), nameof(statAverage), $"test {nameof(statAverage)}", preferredSpatialAggregationType: AggregationTypes.Average, preferredTemporalAggregationType: AggregationTypes.Average);
        using IAmbientStatistic statMin = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMin), nameof(statMin), $"test {nameof(statMin)}", preferredSpatialAggregationType: AggregationTypes.Min, preferredTemporalAggregationType: AggregationTypes.Min);
        using IAmbientStatistic statMax = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMax), nameof(statMax), $"test {nameof(statMax)}", preferredSpatialAggregationType: AggregationTypes.Max, preferredTemporalAggregationType: AggregationTypes.Max);
        using IAmbientStatistic statMostRecent = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statMostRecent), nameof(statMostRecent), $"test {nameof(statMostRecent)}", preferredSpatialAggregationType: AggregationTypes.MostRecent, preferredTemporalAggregationType: AggregationTypes.MostRecent);
        using IAmbientStatistic statOther = stats.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(statOther), nameof(statOther), $"test {nameof(statOther)}", preferredSpatialAggregationType: AggregationTypes.Sum, preferredTemporalAggregationType: AggregationTypes.Sum);
        long?[] samples1 = new long?[] { 10, null, 11, 12, 15, 14, 13 };

        Assert.AreEqual(12, statAverage.PreferredTemporalAggregation(samples1));
        Assert.AreEqual(10, statMin.PreferredTemporalAggregation(samples1));
        Assert.AreEqual(15, statMax.PreferredTemporalAggregation(samples1));
        Assert.AreEqual(13, statMostRecent.PreferredTemporalAggregation(samples1));
        Assert.AreEqual(75, statOther.PreferredTemporalAggregation(samples1));

        long?[] samples2 = new long?[] { null };
        Assert.IsNull(statAverage.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMin.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMax.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statMostRecent.PreferredSpatialAggregationType.Aggregate(samples2));
        Assert.IsNull(statOther.PreferredSpatialAggregationType.Aggregate(samples2));
    }
    [TestMethod]
    public void NullReferenceExceptions()
    {
        IAmbientStatistic nullStat = null!;
        Assert.ThrowsException<ArgumentNullException>(() => nullStat.SetValue(0L));
        Assert.ThrowsException<ArgumentNullException>(() => nullStat.SetValue(0f));
        Assert.ThrowsException<ArgumentNullException>(() => nullStat.SetValue(0.0));
    }
    [TestMethod]
    public void DefaultAggregations()
    {
        Assert.AreEqual(AggregationTypes.Average, AmbientStatisicType.Raw.DefaultTemporalAggregation());
        Assert.AreEqual(AggregationTypes.MostRecent, AmbientStatisicType.Cumulative.DefaultTemporalAggregation());
        Assert.AreEqual(AggregationTypes.Min, AmbientStatisicType.Min.DefaultTemporalAggregation());
        Assert.AreEqual(AggregationTypes.Max, AmbientStatisicType.Max.DefaultTemporalAggregation());
        Assert.AreEqual(AggregationTypes.Average, ((AmbientStatisicType)(-1)).DefaultTemporalAggregation());

        Assert.AreEqual(AggregationTypes.Average, AmbientStatisicType.Raw.DefaultSpatialAggregation());
        Assert.AreEqual(AggregationTypes.Average, AmbientStatisicType.Cumulative.DefaultSpatialAggregation());
        Assert.AreEqual(AggregationTypes.Min, AmbientStatisicType.Min.DefaultSpatialAggregation());
        Assert.AreEqual(AggregationTypes.Max, AmbientStatisicType.Max.DefaultSpatialAggregation());
        Assert.AreEqual(AggregationTypes.Average, ((AmbientStatisicType)(-1)).DefaultSpatialAggregation());
    }
    [TestMethod]
    public void StatisticsMissingSamples()
    {
        long?[] samples;

        samples = MissingSampleHandling.Skip.HandleMissingSamples(new long?[] { null, null, null, null, null }).ToArray();
        AssertArraysEqualByValue(samples, Array.Empty<long?>());
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
    [TestMethod]
    public void AmbientRatioStatistics()
    {
        using IAmbientStatistic requests = AmbientStatistics.GetOrAddStatistic(AmbientStatisicType.Raw, "total_requests", "Total Requests", "The total number of requests");
        IAmbientStatisticReader executionTime = AmbientStatistics.Statistics["ExecutionTime"];
        using IAmbientRatioStatistic requestsPerSecond1 = AmbientStatistics.GetOrAddRatioStatistic("requestsPerSecond1", "requestsPerSecond", "requests per second", false, "r/s", requests.Id, true, executionTime.Id, true);
        using IAmbientRatioStatistic requestsPerSecond2 = AmbientStatistics.GetOrAddRatioStatistic("requestsPerSecond2", "requestsPerSecond", "requests per second", false, null, requests.Id, true, executionTime.Id, true);
        {
            using IAmbientRatioStatistic requestsPerSecond3 = AmbientStatistics.GetOrAddRatioStatistic("requests", "requests", "requests", true, null, requests.Id, true, null, true);
        }
        IAmbientRatioStatistic stat = AmbientStatistics.RatioStatistics["requestsPerSecond1"];
        Assert.AreEqual(_AmbientStatistics.Local, stat.StatisticsSet);
        Assert.AreEqual(requestsPerSecond1, stat);
        Assert.AreEqual(requests.Id, stat.NumeratorStatisticId);
        Assert.IsTrue(stat.NumeratorDelta);
        Assert.AreEqual(executionTime.Id, stat.DenominatorStatisticId);
        Assert.IsTrue(stat.DenominatorDelta);
        Assert.AreEqual("r/s", stat.AdjustedUnits);
        Assert.AreEqual("requestsPerSecond1", stat.Id);
        Assert.AreEqual("requestsPerSecond", stat.Name);
        Assert.AreEqual("requests per second", stat.Description);
    }
}
