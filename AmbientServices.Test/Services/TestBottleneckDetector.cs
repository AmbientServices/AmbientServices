using AmbientServices;
using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds test cases for the ambient bottleneck detector.
/// </summary>
[TestClass]
public class TestBottleneckDetector
{
    private static readonly AmbientService<IAmbientBottleneckDetector> _BottleneckDetector = Ambient.GetService<IAmbientBottleneckDetector>(out _BottleneckDetector);
    private static readonly AmbientBottleneck _ZeroAutoBottleneck = new("TestBottleneckDetector-ZeroAutoTest1", AmbientBottleneckUtilizationAlgorithm.Zero, true, "Zero Auto Test");
    private static readonly AmbientBottleneck _LinearAutoBottleneck = new("TestBottleneckDetector-LinearAutoTest1", AmbientBottleneckUtilizationAlgorithm.Linear, true, "Linear Auto Test");
    private static readonly AmbientBottleneck _ExponentialAutoBottleneck = new("TestBottleneckDetector-ExponentialAutoTest1", AmbientBottleneckUtilizationAlgorithm.ExponentialLimitApproach, true, "Exponential Auto Test");
    private static readonly AmbientBottleneck _ZeroManualBottleneck = new("TestBottleneckDetector-ZeroManualTest1", AmbientBottleneckUtilizationAlgorithm.Zero, false, "Zero Manual Test");
    private static readonly AmbientBottleneck _LinearManualBottleneck = new("TestBottleneckDetector-LinearManualTest1", AmbientBottleneckUtilizationAlgorithm.Linear, false, "Linear Manual Test");
    private static readonly AmbientBottleneck _ExponentialManualBottleneck = new("TestBottleneckDetector-ExponentialManualTest1", AmbientBottleneckUtilizationAlgorithm.ExponentialLimitApproach, false, "Exponential Manual Test");

    [TestMethod]
    public void BottleneckDetectorNoop()
    {
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(null))
        using (AmbientClock.Pause())
        {
            AmbientBottleneck noop = new("BottleneckDetectorNoop", AmbientBottleneckUtilizationAlgorithm.Zero, true, "BottleneckDetectorNoop Test");
            using AmbientBottleneckAccessor access = noop.EnterBottleneck();
            // this should be ignored
        }
    }
    [TestMethod]
    public void BottleneckDetectorNoListeners()
    {
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector()))
        using (AmbientClock.Pause())
        {
            AmbientBottleneck noop = new(nameof(BottleneckDetectorNoListeners), AmbientBottleneckUtilizationAlgorithm.Zero, true, nameof(BottleneckDetectorNoListeners) + " Test");
            using AmbientBottleneckAccessor access = noop.EnterBottleneck();
            // this should be ignored
        }
    }
    [TestMethod]
    public void BottleneckDetectorZero()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new("Zero");
        using (AmbientBottleneckAccessor access = _ZeroAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Zero Auto Test", _ZeroAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-ZeroAutoTest1", _ZeroAutoBottleneck.ToString());
        }
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);

        Assert.AreEqual(_ZeroAutoBottleneck, collector.ProcessAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ThreadAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ScopeAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.GetMostUtilizedBottlenecks(10).First().Bottleneck);

        Assert.StartsWith("Zero", collector.ProcessAnalyzer.ScopeName);
        Assert.StartsWith("Zero", collector.ThreadAnalyzer.ScopeName);
        Assert.StartsWith("Zero", collector.ScopeAnalyzer.ScopeName);
        Assert.StartsWith("TimeWindow", collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.ScopeName);
    }
    [TestMethod]
    public void BottleneckDetectorExceptions()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using AmbientBottleneckSurveyorCoordinator surveyorCoordinator = new();
        using (ProcessBottleneckSurveyor surveyor = new(null, _BottleneckDetector.Local, null, null))
        {
            Assert.Throws<ArgumentNullException>(() => surveyor.BottleneckExited(null!));
        }
        using (ThreadSurveyManager threadManager = new(_BottleneckDetector.Local))
        {
            using ThreadBottleneckSurveyor surveyor = threadManager.CreateThreadSurveyor(null, null, null);
            Assert.Throws<ArgumentNullException>(() => surveyor.BottleneckExited(null!));
        }
        using (ThreadSurveyManager threadManager = new(null))
        {
        }
        using (ScopedBottleneckSurveyor surveyor = new(null, _BottleneckDetector.Local, null, null))
        {
            Assert.Throws<ArgumentNullException>(() => surveyor.BottleneckExited(null));
        }
        using (TimeWindowSurveyManager timeWindowManager = new(TimeSpan.FromSeconds(1), a => Task.CompletedTask, _BottleneckDetector.Local, null, null))
        {
            Assert.Throws<ArgumentNullException>(() => timeWindowManager.BottleneckEntered(null!));
            Assert.Throws<ArgumentNullException>(() => timeWindowManager.BottleneckExited(null!));

            TimeWindowBottleneckSurvey surveyor = new(null, null, 0, TimeSpan.FromSeconds(1));
            Assert.Throws<ArgumentNullException>(() => surveyor.BottleneckEntered(null!));
            Assert.Throws<ArgumentNullException>(() => surveyor.BottleneckExited(null!));
        }

        BasicAmbientBottleneckDetector bd = new();
        AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck1 = new("BottleneckDetectorAccessRecordCombine-LinearTest1", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordCombine Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
        AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck2 = new("BottleneckDetectorAccessRecordCombine-LinearTest2", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordCombine Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
        DateTime start = AmbientClock.UtcNow;
        using AmbientBottleneckAccessor a1 = new(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
        using AmbientBottleneckAccessor a2 = new(bd, BottleneckDetectorAccessRecordPropertiesBottleneck2, start);
        Assert.Throws<ArgumentException>(() => a1.Combine(a2));
        Assert.Throws<ArgumentNullException>(
            () =>
            {
                using (surveyorCoordinator.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), null!))
                {
                }
            });
    }
    [TestMethod]
    public void TimeWindowSurveyManagerNoDetector()
    {
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(null))
        using (AmbientClock.Pause())
        {
            using TimeWindowSurveyManager timeWindowManager = new(TimeSpan.FromSeconds(1), a => Task.CompletedTask, null, null, null);
        }
    }
    [TestMethod]
    public void BottleneckDetectorAccessRecordExceptions()
    {
        using (AmbientClock.Pause())
        {
            BasicAmbientBottleneckDetector bd = new();
            AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck = new("BottleneckDetectorAccessRecordProperties-LinearTest1", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordProperties Test");
            AmbientBottleneckAccessor a1 = null;
            try
            {
                DateTime start = AmbientClock.UtcNow;
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(null!, BottleneckDetectorAccessRecordPropertiesBottleneck, start));
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(bd, null!, start));
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(null!, BottleneckDetectorAccessRecordPropertiesBottleneck, 0));
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(bd, null!, 0));
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(null!, BottleneckDetectorAccessRecordPropertiesBottleneck, 0, 0, 0, 0.0));
                Assert.Throws<ArgumentNullException>(() => new AmbientBottleneckAccessor(bd, null!, 0, 0, 0, 0.0));
                using AmbientBottleneckAccessor r = new(bd, BottleneckDetectorAccessRecordPropertiesBottleneck, start);
                Assert.Throws<InvalidOperationException>(() => r.SetUsage(0, 0));
                Assert.Throws<InvalidOperationException>(() => r.AddUsage(0, 0));
            }
            finally
            {
                a1?.Dispose();
            }
        }
    }
    [TestMethod]
    public void BottleneckDetectorBadLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AmbientBottleneck("BottleneckDetectorBadLimits", AmbientBottleneckUtilizationAlgorithm.Zero, true, "BottleneckDetectorBadLimits Test", -.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AmbientBottleneck("BottleneckDetectorBadLimits", AmbientBottleneckUtilizationAlgorithm.Zero, true, "BottleneckDetectorBadLimits Test", null, TimeSpan.FromMilliseconds(-1)));
    }
    [TestMethod]
    public void BottleneckDetectorDefaultScopeNames()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new();
        using (AmbientBottleneckAccessor access = _ZeroAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Zero Auto Test", _ZeroAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-ZeroAutoTest1", _ZeroAutoBottleneck.ToString());
        }
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);

        Assert.AreEqual(_ZeroAutoBottleneck, collector.ProcessAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ThreadAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ScopeAnalyzer.GetMostUtilizedBottlenecks(10).First().Bottleneck);

        Assert.StartsWith("Process", collector.ProcessAnalyzer.ScopeName);
#if NET6_0_OR_GREATER   // starting in net6.0, the thread's name is automatically assigned
        Assert.IsTrue(collector.ThreadAnalyzer.ScopeName.StartsWith(".NET TP Worker") || collector.ThreadAnalyzer.ScopeName.StartsWith(".NET ThreadPool Worker") || collector.ThreadAnalyzer.ScopeName.StartsWith(".NET Long Running Task"));
#else
        Assert.IsTrue(collector.ThreadAnalyzer.ScopeName.StartsWith("Thread"));
#endif
        Assert.StartsWith(".ctor", collector.ScopeAnalyzer.ScopeName);
        Assert.StartsWith("TimeWindow", collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.ScopeName);
    }
    [TestMethod]
    public void BottleneckDetectorLinear()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new("Linear");
        using (AmbientBottleneckAccessor access = _LinearAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Linear Auto Test", _LinearAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-LinearAutoTest1", _LinearAutoBottleneck.ToString());
        }
        Assert.AreEqual(_LinearAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);
    }
    [TestMethod]
    public void BottleneckDetectorFilter()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new(nameof(BottleneckDetectorFilter), ".*(Zero|Linear).*", ".*Linear.*");
        using (AmbientBottleneckAccessor access = _ZeroAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
        }
        using (AmbientBottleneckAccessor access = _LinearAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
        }
        using (AmbientBottleneckAccessor access = _ExponentialAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
        }
        // zero should be the only one that gets through the filters
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ZeroAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(1, collector.ProcessAnalyzer.GetMostUtilizedBottlenecks(10).Count());
        Assert.AreEqual(1, collector.ThreadAnalyzer.GetMostUtilizedBottlenecks(10).Count());
        Assert.AreEqual(1, collector.ScopeAnalyzer.GetMostUtilizedBottlenecks(10).Count());
        Assert.AreEqual(1, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.GetMostUtilizedBottlenecks(10).Count());
    }
    [TestMethod]
    public void BottleneckDetectorExponential()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new("Exponential");
        using (AmbientBottleneckAccessor access = _ExponentialAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Exponential Auto Test", _ExponentialAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-ExponentialAutoTest1", _ExponentialAutoBottleneck.ToString());
        }
        Assert.AreEqual(_ExponentialAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ExponentialAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ExponentialAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_ExponentialAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);
    }
    [TestMethod]
    public void BottleneckDetectorCombine()
    {
        using ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector());
        using Collector collector = new("Linear");
        using (AmbientBottleneckAccessor access = _LinearAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Linear Auto Test", _LinearAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-LinearAutoTest1", _LinearAutoBottleneck.ToString());
        }
        using (AmbientBottleneckAccessor access = _LinearAutoBottleneck.EnterBottleneck())
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            // even though we're zero, we better be marked as in-progress in the top ten of each of the analyzers, because there are only two other bottlenecks to compete with
            Assert.AreEqual("Linear Auto Test", _LinearAutoBottleneck.Description);
            Assert.AreEqual("TestBottleneckDetector-LinearAutoTest1", _LinearAutoBottleneck.ToString());
        }
        Assert.AreEqual(_LinearAutoBottleneck, collector.ProcessAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.ThreadAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.ScopeAnalyzer.MostUtilizedBottleneck?.Bottleneck);
        Assert.AreEqual(_LinearAutoBottleneck, collector.Analyses.FirstOrDefault(kvp => kvp.Value.MostUtilizedBottleneck != null).Value.MostUtilizedBottleneck?.Bottleneck);
    }
    [TestMethod]
    public void BottleneckDetectorAccessRecordCompare()
    {
        BasicAmbientBottleneckDetector bd = new();
        using (AmbientClock.Pause())
        {
            using AmbientBottleneckAccessor a1 = new(bd, _LinearManualBottleneck, AmbientClock.UtcNow);
            a1.SetUsage(1, 77);
            using AmbientBottleneckAccessor a2 = new(bd, _LinearManualBottleneck, AmbientClock.UtcNow);
            a2.SetUsage(1, 77);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(77));
            using AmbientBottleneckAccessor b1 = new(bd, _LinearManualBottleneck, AmbientClock.UtcNow);
            b1.SetUsage(1, 99);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(22));
            Assert.IsTrue(a1 == a2);
            Assert.IsFalse(a1 == null);
            Assert.IsFalse(null == a2);
            Assert.IsFalse(a1 != a2);
            Assert.IsTrue(a1 != null);
            Assert.IsTrue(null != a2);
            Assert.IsFalse(a1 == b1);
            Assert.IsTrue(a1 != b1);
            Assert.AreEqual(0, a1!.CompareTo(a2));
            Assert.AreEqual(1, a1!.CompareTo(null));
            Assert.AreEqual(0, a2!.CompareTo(a1));
            Assert.IsTrue(b1 > a2);
            Assert.IsTrue(b1 > null);
            Assert.IsFalse(null > b1);
            Assert.IsTrue(a1 >= a2);
            Assert.IsTrue(a1 >= null);
            Assert.IsFalse(null >= a1);
            Assert.IsTrue(b1 >= a2);
            Assert.IsTrue(a2 < b1);
            Assert.IsFalse(a2 < null);
            Assert.IsTrue(null < a2);
            Assert.IsTrue(a2 <= a1);
            Assert.IsFalse(a2 <= null);
            Assert.IsTrue(null <= a2);
            Assert.IsTrue(a2 <= b1);
            Assert.IsGreaterThan(0, b1.CompareTo(a2));
            Assert.IsLessThan(0, a2.CompareTo(b1));
        }
    }
    [TestMethod]
    public void BottleneckDetectorAccessRecordProperties()
    {
        using (AmbientClock.Pause())
        {
            BasicAmbientBottleneckDetector bd = new();
            long limitStopwatchTicks = TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(1000).Ticks);
            long useStopwatchTicks = TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(300).Ticks);
            TimeSpan limitPeriod = TimeSpan.FromSeconds(7);
            long limitPeriodStopwatchTicks = TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(limitPeriod.Ticks);
            AmbientBottleneck bottleneckDetectorAccessRecordPropertiesBottleneck = new("BottleneckDetectorAccessRecordProperties-LinearTest", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordProperties Test", limitStopwatchTicks, limitPeriod);
            AmbientBottleneckAccessor a1 = null;
            try
            {
                DateTime start = AmbientClock.UtcNow;
                long startTicks = AmbientClock.Ticks;
                a1 = new AmbientBottleneckAccessor(bd, bottleneckDetectorAccessRecordPropertiesBottleneck, startTicks);
                // time should be paused here, so we should always get zero back when we ask how long it's been
                Assert.AreEqual(0, a1.AccessCount);
                Assert.AreEqual(0, a1.AccessDurationStopwatchTicks, $"Begin: {a1.AccessBegin}({a1.AccessBeginStopwatchTimestamp}), End: {a1.AccessEnd}({a1.AccessEndStopwatchTimestamp}), Count: {a1.AccessCount}, Utilization: {a1.Utilization}, LimitUsed: {a1.LimitUsed}, AmbientTicks: {AmbientClock.Ticks}, startTicks: {startTicks}");
                Assert.AreEqual(start, a1.AccessBegin);
                Assert.IsNull(a1.AccessEnd);
                Assert.AreEqual(0, a1.LimitUsed);
                Assert.AreEqual(0, a1.Utilization);
                // now we'll unpause time, skip ahead, and pause again
                AmbientClock.SkipAhead(useStopwatchTicks);
                a1.Dispose();
                Assert.AreEqual(1, a1.AccessCount);
                Assert.AreEqual(useStopwatchTicks, a1.AccessDurationStopwatchTicks);
                Assert.AreEqual(start, a1.AccessBegin);
                Assert.AreEqual(AmbientClock.UtcNow, a1.AccessEnd);
                DateTime accessEnd = a1.AccessEnd ?? AmbientClock.UtcNow;
                Assert.AreEqual(useStopwatchTicks, TimeSpanUtilities.TimeSpanTicksToStopwatchTicks((accessEnd - a1.AccessBegin).Ticks));
                Assert.AreEqual(useStopwatchTicks, (long)a1.LimitUsed);
                Assert.AreEqual((1.0 * useStopwatchTicks / useStopwatchTicks) / (1.0 * limitStopwatchTicks / limitPeriodStopwatchTicks), a1.Utilization);
                Assert.AreNotEqual(0, a1.GetHashCode());
            }
            finally
            {
                a1?.Dispose();
            }
        }
    }
    [TestMethod]
    public void BottleneckDetectorAccessRecordSecondarySortKeyCompare()
    {
        using (AmbientClock.Pause())
        {
            BasicAmbientBottleneckDetector bd = new();
            AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck1 = new("BottleneckDetectorAccessRecordSecondarySortKeyCompare-LinearTest1", AmbientBottleneckUtilizationAlgorithm.Linear, false, "BottleneckDetectorAccessRecordProperties Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck2 = new("BottleneckDetectorAccessRecordSecondarySortKeyCompare-LinearTest2", AmbientBottleneckUtilizationAlgorithm.Linear, false, "BottleneckDetectorAccessRecordProperties Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneckAccessor a1 = null;
            AmbientBottleneckAccessor a2 = null;
            try
            {
                DateTime start = AmbientClock.UtcNow;
                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck2, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a1.SetUsage(1, 10);
                a2.SetUsage(1, 20);
                a1.AddUsage(1, 10);
                a2.AddUsage(1, 20);

                Assert.AreEqual(a1.Utilization, a2.Utilization);
                // a2 should be greater even though the utilizations are the same because it had more usage
                Assert.IsLessThan(0, a1.CompareTo(a2));
            }
            finally
            {
                a1?.Dispose();
                a2?.Dispose();
            }
        }
    }
    [TestMethod]
    public void BottleneckDetectorAccessRecordCombine()
    {
        using (AmbientClock.Pause())
        {
            BasicAmbientBottleneckDetector bd = new();
            AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck1 = new("BottleneckDetectorAccessRecordCombine-LinearTest1", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordCombine Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck BottleneckDetectorAccessRecordPropertiesBottleneck2 = new("BottleneckDetectorAccessRecordCombine-LinearTest2", AmbientBottleneckUtilizationAlgorithm.Linear, true, "BottleneckDetectorAccessRecordCombine Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneckAccessor a1 = null;
            AmbientBottleneckAccessor a2 = null;
            try
            {
                DateTime start = AmbientClock.UtcNow;
                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.IsNull(combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a1.Dispose();
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.IsNull(combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a2.Dispose();

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.IsNull(combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a1.Dispose();
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                a2.Dispose();

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.AreEqual(start.AddMilliseconds(800), combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

                // now test combining records that are frozen in time
                start = AmbientClock.UtcNow;
                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                a1.Dispose();
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                a2.Dispose();

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.AreEqual(start, combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

                // now test records that go *backwards* in time (this shouldn't ever happen, but we don't want even that to crash)
                // note that the results here aren't symmetric with the forwards-time results because the computation of the window end ends up using the most recent end, which is the one nearer to the beginning in this case, whereas in the forwards case, it's the one that is farther away
                start = AmbientClock.UtcNow;
                a1 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(-100));
                a1.Dispose();
                a2 = new AmbientBottleneckAccessor(bd, BottleneckDetectorAccessRecordPropertiesBottleneck1, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(-100));
                a2.Dispose();

                using (AmbientBottleneckAccessor combined = a1.Combine(a2))
                {
                    Assert.AreEqual(start, combined.AccessBegin);
                    Assert.AreEqual(start.AddMilliseconds(-100), combined.AccessEnd);
                }

                a1.Dispose();
                a2.Dispose();

            }
            finally
            {
                a1?.Dispose();
                a2?.Dispose();
            }
        }
    }
    [TestMethod]
    public void AmbientBottleneckEventCollectorManagerNotifyAndDisposeEmpty()
    {
        AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckEventCollectorManagerNotifyAndDisposeEmpty) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckEventCollectorManagerNotifyAndDisposeEmpty) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
        using (AmbientBottleneckSurveyorCoordinator manager = new())
        {
            using IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(nameof(AmbientBottleneckEventCollectorManagerNotifyAndDisposeEmpty));
            using (bottleneck1.EnterBottleneck())
            {
            }
        }
        using (AmbientBottleneckSurveyorCoordinator manager = new())
        {
            using IAmbientBottleneckSurveyor surveyor = manager.CreateProcessSurveyor(nameof(AmbientBottleneckEventCollectorManagerNotifyAndDisposeEmpty));
            using (bottleneck1.EnterBottleneck())
            {
            }
        }
    }
    [TestMethod]
    public void AmbientBottleneckCallContextSurveyorNullName()
    {
        AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckCallContextSurveyorNullName) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckCallContextSurveyorNullName) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
        using AmbientBottleneckSurveyorCoordinator manager = new();
        using IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(null, null, null);
        Assert.AreEqual("", surveyor.ScopeName);
    }
    [TestMethod]
    public void AmbientBottleneckSurveyorsEmpty()
    {
        using (CallContextSurveyManager surveymanager = new(null))
        {
        }
        using (ProcessBottleneckSurveyor surveymanager = new(nameof(AmbientBottleneckSurveyorsEmpty), null, null, null))
        {
        }
    }
    [TestMethod]
    public void AmbientBottleneckEventCollectorManagerSettings()
    {
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector()))
        using (AmbientClock.Pause())
        {
            AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckEventCollectorManagerSettings) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckEventCollectorManagerSettings) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck2 = new(nameof(AmbientBottleneckEventCollectorManagerSettings) + "-Bottleneck2", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckEventCollectorManagerSettings) + " Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck3 = new(nameof(AmbientBottleneckEventCollectorManagerSettings) + "-Bottleneck3", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckEventCollectorManagerSettings) + " Test3", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            BasicAmbientSettingsSet settingsSet = new(nameof(AmbientBottleneckEventCollectorManagerSettings));
            settingsSet.ChangeSetting(nameof(AmbientBottleneckSurveyorCoordinator) + "-DefaultAllow", ".*[12]");
            settingsSet.ChangeSetting(nameof(AmbientBottleneckSurveyorCoordinator) + "-DefaultBlock", ".*3");
            using ScopedLocalServiceOverride<IAmbientSettingsSet> s = new(settingsSet);
            using AmbientBottleneckSurveyorCoordinator manager = new();
            using (IAmbientBottleneckSurveyor processAnalyzer = manager.CreateProcessSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings)))
            using (IAmbientBottleneckSurveyor threadAnalyzer = manager.CreateThreadSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings)))
            using (manager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), w => Task.CompletedTask))
            using (IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings)))
            {
                using (bottleneck1.EnterBottleneck())
                {
                }
                using (bottleneck2.EnterBottleneck())
                {
                }
                using (bottleneck3.EnterBottleneck())
                {
                }
                Assert.IsNotNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck1));
                Assert.IsNotNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck2));
                Assert.IsNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck3));
            }
            using (IAmbientBottleneckSurveyor processAnalyzer = manager.CreateProcessSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings)))
            using (IAmbientBottleneckSurveyor threadAnalyzer = manager.CreateThreadSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings)))
            using (manager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), w => Task.CompletedTask))
            using (IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(nameof(AmbientBottleneckEventCollectorManagerSettings), ".*[13]", ".*2"))
            {
                using (bottleneck1.EnterBottleneck())
                {
                }
                using (bottleneck2.EnterBottleneck())
                {
                }
                using (bottleneck3.EnterBottleneck())
                {
                }
                Assert.IsNotNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck1));
                Assert.IsNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck2));
                Assert.IsNotNull(surveyor.GetMostUtilizedBottlenecks(100).FirstOrDefault(b => b.Bottleneck == bottleneck3));
            }
        }
    }
    [TestMethod]
    public void AmbientBottleneckDanglingAutomaticBottleneck()
    {
        IAmbientBottleneckSurvey analysis;
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector()))
        using (AmbientClock.Pause())
        {
            AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckDanglingAutomaticBottleneck) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckDanglingAutomaticBottleneck) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck2 = new(nameof(AmbientBottleneckDanglingAutomaticBottleneck) + "-Bottleneck2", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckDanglingAutomaticBottleneck) + " Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck3 = new(nameof(AmbientBottleneckDanglingAutomaticBottleneck) + "-Bottleneck3", AmbientBottleneckUtilizationAlgorithm.Linear, true, nameof(AmbientBottleneckDanglingAutomaticBottleneck) + " Test3", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            using AmbientBottleneckSurveyorCoordinator manager = new();
            List<IAmbientBottleneckSurvey> timeWindowResults = new();
            using (IAmbientBottleneckSurveyor processAnalyzer = manager.CreateProcessSurveyor(nameof(AmbientBottleneckDanglingAutomaticBottleneck)))
            using (IAmbientBottleneckSurveyor threadAnalyzer = manager.CreateThreadSurveyor(nameof(AmbientBottleneckDanglingAutomaticBottleneck)))
            using (manager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), a => { timeWindowResults.Add(a); return Task.CompletedTask; }))
            using (IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(nameof(AmbientBottleneckDanglingAutomaticBottleneck)))
            {
                using (bottleneck1.EnterBottleneck())
                {
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                    using (bottleneck2.EnterBottleneck())
                    {
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(25));
                    }
                    using (bottleneck2.EnterBottleneck())
                    {
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(25));
                    }
                    using (bottleneck3.EnterBottleneck())
                    {
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                    }
                    Assert.HasCount(1, timeWindowResults);
                    analysis = timeWindowResults[0];
                    Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                    timeWindowResults.Clear();
                }
                // trigger another time window (all the bottleneck accessors are closed now)
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                // in the second window, we should have 50ms for 1, 0ms for 2, and 50ms for 3, but 3 has a higher limit, so 1 should be the most used
                Assert.HasCount(1, timeWindowResults);
                analysis = timeWindowResults[0];
                Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                timeWindowResults.Clear();
            }
        }
    }
    private readonly AmbientLogger<TestBottleneckDetector> Logger = new();
    [TestMethod]
    public void AmbientBottleneckDanglingManualBottleneck()
    {
        IAmbientBottleneckSurvey analysis;
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector()))
        using (AmbientClock.Pause())
        {
            StringBuilder log = new();
            AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck2 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck2", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck3 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck3", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test3", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            using AmbientBottleneckSurveyorCoordinator manager = new();
            List<IAmbientBottleneckSurvey> timeWindowResults = new();
            using (IAmbientBottleneckSurveyor processAnalyzer = manager.CreateProcessSurveyor(nameof(AmbientBottleneckDanglingManualBottleneck)))
            using (IAmbientBottleneckSurveyor threadAnalyzer = manager.CreateThreadSurveyor(nameof(AmbientBottleneckDanglingManualBottleneck)))
            using (manager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), a =>
            {
                log.AppendLine($"TimeWindow {a.ScopeName} End: {AmbientClock.UtcNow.ToString()}");
                timeWindowResults.Add(a);
                return Task.CompletedTask;
            }))
            using (IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor(nameof(AmbientBottleneckDanglingManualBottleneck)))
            {
                using (AmbientBottleneckAccessor access1 = bottleneck1.EnterBottleneck())
                {
                    access1?.SetUsage(1, 50);
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(51));  // add one extra ms to avoid rounding errors during conversion
                    using (AmbientBottleneckAccessor access2 = bottleneck2.EnterBottleneck())
                    {
                        access1?.SetUsage(1, 50);
                        access2?.SetUsage(1, 50);
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(51));  // this should end the initial window and add it to the list
                        if (timeWindowResults.Count != 1) Assert.Fail("str:" + log.ToString());
                        Assert.HasCount(1, timeWindowResults, log.ToString());
                    }
                    using (AmbientBottleneckAccessor access3 = bottleneck3.EnterBottleneck())
                    {
                        access1?.SetUsage(1, 50);
                        access3?.SetUsage(1, 50);
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(51));  // this puts us in the middle of the second window
                    }
                    Assert.HasCount(1, timeWindowResults, log.ToString());
                    analysis = timeWindowResults[0];
                    Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                    timeWindowResults.Clear();
                }
                // trigger another time window (all the bottleneck accessors are closed now)
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(51));      // this should end the second window and add it to the list
                                                                            // in the second window, we should have 50 units for 1, 0 units for 2, and 50 units for 3, but 3 has a higher limit, so 1 should be the most used
                Assert.HasCount(1, timeWindowResults, log.ToString());
                analysis = timeWindowResults[0];
                Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                timeWindowResults.Clear();
            }
        }
    }
    [TestMethod]
    public void AmbientBottleneckThreadSurveyorNamedThread()
    {
        IAmbientBottleneckSurvey analysis;
        using (ScopedLocalServiceOverride<IAmbientBottleneckDetector> o = new(new BasicAmbientBottleneckDetector()))
        using (AmbientClock.Pause())
        {
            AmbientBottleneck bottleneck1 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck1", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test1", AmbientStopwatch.Frequency / 10000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck2 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck2", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test2", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            AmbientBottleneck bottleneck3 = new(nameof(AmbientBottleneckDanglingManualBottleneck) + "-Bottleneck3", AmbientBottleneckUtilizationAlgorithm.Linear, false, nameof(AmbientBottleneckDanglingManualBottleneck) + " Test3", AmbientStopwatch.Frequency / 5000, TimeSpan.FromSeconds(1));
            using AmbientBottleneckSurveyorCoordinator manager = new();
            List<IAmbientBottleneckSurvey> timeWindowResults = new();
            Thread t = new(new ThreadStart(
            () =>
            {
                Thread.CurrentThread.Name = nameof(AmbientBottleneckDanglingManualBottleneck);
                using (IAmbientBottleneckSurveyor processAnalyzer = manager.CreateProcessSurveyor())
                using (IAmbientBottleneckSurveyor threadAnalyzer = manager.CreateThreadSurveyor())
                using (manager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(100), a => { timeWindowResults.Add(a); return Task.CompletedTask; }))
                using (IAmbientBottleneckSurveyor surveyor = manager.CreateCallContextSurveyor())
                {
                    using (AmbientBottleneckAccessor access1 = bottleneck1.EnterBottleneck())
                    {
                        access1?.SetUsage(1, 50);
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                        using (AmbientBottleneckAccessor access2 = bottleneck2.EnterBottleneck())
                        {
                            access1?.SetUsage(1, 50);
                            access2?.SetUsage(1, 50);
                            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                        }
                        using (AmbientBottleneckAccessor access3 = bottleneck3.EnterBottleneck())
                        {
                            access1?.SetUsage(1, 50);
                            access3?.SetUsage(1, 50);
                            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                        }
                        Assert.HasCount(1, timeWindowResults);
                        analysis = timeWindowResults[0];
                        Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                        timeWindowResults.Clear();
                    }
                        // trigger another time window (all the bottleneck accessors are closed now)
                        AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                        // in the second window, we should have 50 units for 1, 0 units for 2, and 50 units for 3, but 3 has a higher limit, so 1 should be the most used
                        Assert.HasCount(1, timeWindowResults);
                    analysis = timeWindowResults[0];
                    Assert.AreEqual(bottleneck1, analysis.MostUtilizedBottleneck?.Bottleneck);
                    timeWindowResults.Clear();
                }
            }));
            t.Start();
            t.Join();
        }
    }
}
class Collector : IDisposable
{
    private readonly AmbientBottleneckSurveyorCoordinator _collectorManager;
    private readonly IDisposable _timeWindowAnalyzer;
    private readonly IDisposable _clock;
    private readonly ScopedLocalServiceOverride<IAmbientBottleneckDetector> _override;
    private bool _disposedValue;

    public Collector(string scopeName = null, string overrideAllowRegex = null, string overrideBlockRegex = null)
    {
        _clock = AmbientClock.Pause();
        _override = new ScopedLocalServiceOverride<IAmbientBottleneckDetector>(new BasicAmbientBottleneckDetector());
        _collectorManager = new AmbientBottleneckSurveyorCoordinator();
        Analyses = new ConcurrentDictionary<string, IAmbientBottleneckSurvey>();
        ProcessAnalyzer = _collectorManager.CreateProcessSurveyor(scopeName, overrideAllowRegex, overrideBlockRegex);
        ThreadAnalyzer = _collectorManager.CreateThreadSurveyor(scopeName, overrideAllowRegex, overrideBlockRegex);
        ScopeAnalyzer = (scopeName == null) ? _collectorManager.CreateCallContextSurveyor() : _collectorManager.CreateCallContextSurveyor(scopeName, overrideAllowRegex, overrideBlockRegex);
        _timeWindowAnalyzer = _collectorManager.CreateTimeWindowSurveyor(TimeSpan.FromMilliseconds(77), CollectAnalyses, overrideAllowRegex, overrideBlockRegex);
    }

    private Task CollectAnalyses(IAmbientBottleneckSurvey analysis)
    {
        Assert.IsTrue(Analyses.TryAdd(analysis.ScopeName, analysis));
        return Task.CompletedTask;
    }

    public IAmbientBottleneckSurveyor ProcessAnalyzer { get; }
    public IAmbientBottleneckSurveyor ThreadAnalyzer { get; }
    public IAmbientBottleneckSurveyor ScopeAnalyzer { get; }
    public TimeWindowBottleneckSurvey TimeWindowAnalyzer => (TimeWindowBottleneckSurvey)_timeWindowAnalyzer;
    public ConcurrentDictionary<string, IAmbientBottleneckSurvey> Analyses { get; }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                // double-dispose everything to make sure that doesn't crash
                _timeWindowAnalyzer.Dispose();
                _timeWindowAnalyzer.Dispose();
                ScopeAnalyzer.Dispose();
                ScopeAnalyzer.Dispose();
                ThreadAnalyzer.Dispose();
                ThreadAnalyzer.Dispose();
                ProcessAnalyzer.Dispose();
                ProcessAnalyzer.Dispose();
                _collectorManager.Dispose();
                _collectorManager.Dispose();
                _override.Dispose();
                _override.Dispose();
                _clock.Dispose();
                _clock.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~Collector()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
