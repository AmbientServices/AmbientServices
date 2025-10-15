using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace AmbientServices.Test;

[TestClass]
public class TestPressureMonitor
{
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    [TestMethod]
    public void PressureMonitorWithStats()
    {
        using IDisposable pause = AmbientClock.Pause();
        float internalPressure;
        float externalPressure;
        float overallPressure;
        CpuPressurePoint cpuPressure = new();
        InternalPressurePoints.Register(cpuPressure);
        ThreadPoolPressurePoint threadPoolPressure = new();
        InternalPressurePoints.Register(threadPoolPressure);
        MemoryPressurePoint memoryPressure = new();
        InternalPressurePoints.Register(memoryPressure);
        ExternalPressurePoints.Register(new TestExternalPressurePoint());
        using PressureMonitor monitor = new(1000);
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsLessThanOrEqualTo(internalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, internalPressure);
        Assert.IsLessThanOrEqualTo(externalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, externalPressure);
        Assert.IsLessThanOrEqualTo(overallPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, overallPressure);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsLessThanOrEqualTo(internalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, internalPressure);
        Assert.IsLessThanOrEqualTo(externalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, externalPressure);
        Assert.IsLessThanOrEqualTo(overallPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, overallPressure);

        PressureMonitor defaultMonitor = PressureMonitor.Default;
    }
    [TestMethod]
    public void PressureMonitorNoStats()
    {
        using IDisposable noStatistics = AmbientStatistics.ScopedLocalOverride(null);

        using IDisposable pause = AmbientClock.Pause();
        float internalPressure;
        float externalPressure;
        float overallPressure;
        CpuPressurePoint cpuPressure = new();
        InternalPressurePoints.Register(cpuPressure);
        ThreadPoolPressurePoint threadPoolPressure = new();
        InternalPressurePoints.Register(threadPoolPressure);
        MemoryPressurePoint memoryPressure = new();
        InternalPressurePoints.Register(memoryPressure);
        ExternalPressurePoints.Register(new TestExternalPressurePoint());
        using PressureMonitor monitor = new(1000);
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsLessThanOrEqualTo(internalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, internalPressure);
        Assert.IsLessThanOrEqualTo(externalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, externalPressure);
        Assert.IsLessThanOrEqualTo(overallPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, overallPressure);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsLessThanOrEqualTo(internalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, internalPressure);
        Assert.IsLessThanOrEqualTo(externalPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, externalPressure);
        Assert.IsLessThanOrEqualTo(overallPressure, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, overallPressure);

        PressureMonitor defaultMonitor = PressureMonitor.Default;
    }
    [TestMethod]
    public void PressureMonitorError()
    {
        Assert.Throws<ArgumentNullException>(() => PressureMonitor.Max((IEnumerable<float>)null!));
    }
    [TestMethod]
    public void LinearPressureToMemoryPressure()
    {
        Assert.IsLessThanOrEqualTo(0.0f, MemoryPressurePoint.LinearPressureToMemoryPressure(-1));
        Assert.IsLessThanOrEqualTo(0.0f, MemoryPressurePoint.LinearPressureToMemoryPressure(0));
        Assert.IsGreaterThanOrEqualTo(0.0f, MemoryPressurePoint.LinearPressureToMemoryPressure(0));
        Assert.IsLessThan(0.10f, MemoryPressurePoint.LinearPressureToMemoryPressure(0.10f));
        Assert.IsLessThanOrEqualTo(0.10f, MemoryPressurePoint.LinearPressureToMemoryPressure(0.50f));
        Assert.IsLessThanOrEqualTo(0.50f, MemoryPressurePoint.LinearPressureToMemoryPressure(0.75f));
        Assert.IsLessThan(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(0.95f));
        Assert.IsLessThanOrEqualTo(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(1.00f));
        Assert.IsLessThanOrEqualTo(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(1.01f));
        Assert.IsLessThanOrEqualTo(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(1.1f));
        Assert.IsLessThanOrEqualTo(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(1.5f));
        Assert.IsLessThanOrEqualTo(1.00f, MemoryPressurePoint.LinearPressureToMemoryPressure(100f));
        Assert.IsGreaterThanOrEqualTo(MemoryPressurePoint.LinearPressureToMemoryPressure(-0.1f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.0f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.0f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.10f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.10f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.50f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.50f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.75f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.75f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.95f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.95f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.99f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.99f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.999f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.999f), MemoryPressurePoint.LinearPressureToMemoryPressure(0.9999f));
        Assert.IsGreaterThan(MemoryPressurePoint.LinearPressureToMemoryPressure(0.9999f), MemoryPressurePoint.LinearPressureToMemoryPressure(1.00f));
        Assert.IsGreaterThanOrEqualTo(MemoryPressurePoint.LinearPressureToMemoryPressure(1.00f), MemoryPressurePoint.LinearPressureToMemoryPressure(1.01f));
    }
    class TestExternalPressurePoint : IPressurePoint
    {
        public string Name => "TestExternal";

        public float Pressure => 1.0f;
    }
}
