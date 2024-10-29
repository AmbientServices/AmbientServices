using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Assert.IsTrue(0.0f <= internalPressure);
        Assert.IsTrue(internalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= externalPressure);
        Assert.IsTrue(externalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= overallPressure);
        Assert.IsTrue(overallPressure <= 1.0f);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsTrue(0.0f <= internalPressure);
        Assert.IsTrue(internalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= externalPressure);
        Assert.IsTrue(externalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= overallPressure);
        Assert.IsTrue(overallPressure <= 1.0f);

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
        Assert.IsTrue(0.0f <= internalPressure);
        Assert.IsTrue(internalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= externalPressure);
        Assert.IsTrue(externalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= overallPressure);
        Assert.IsTrue(overallPressure <= 1.0f);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        internalPressure = monitor.InternalPressure;
        externalPressure = monitor.ExternalPressure;
        overallPressure = monitor.OverallPressure;
        Assert.IsTrue(0.0f <= internalPressure);
        Assert.IsTrue(internalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= externalPressure);
        Assert.IsTrue(externalPressure <= 1.0f);
        Assert.IsTrue(0.0f <= overallPressure);
        Assert.IsTrue(overallPressure <= 1.0f);

        PressureMonitor defaultMonitor = PressureMonitor.Default;
    }
    [TestMethod]
    public void PressureMonitorError()
    {
        Assert.ThrowsException<ArgumentNullException>(() => PressureMonitor.Max((IEnumerable<float>)null!));
    }
    [TestMethod]
    public void LinearPressureToMemoryPressure()
    {
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(-1)    <= 0.0f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0)     <= 0.0f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0)     >= 0.0f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.10f)  < 0.10f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.50f) <= 0.10f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.75f) <= 0.50f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.95f)  < 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.00f) <= 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.01f) <= 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.1f) <= 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.5f) <= 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(100f) <= 1.00f);
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.0f)  >= MemoryPressurePoint.LinearPressureToMemoryPressure(-0.1f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.10f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.0f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.50f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.10f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.75f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.50f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.95f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.75f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.99f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.95f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.999f) > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.99f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(0.9999f)> MemoryPressurePoint.LinearPressureToMemoryPressure( 0.999f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.00f)  > MemoryPressurePoint.LinearPressureToMemoryPressure( 0.9999f));
        Assert.IsTrue(MemoryPressurePoint.LinearPressureToMemoryPressure(1.01f) >= MemoryPressurePoint.LinearPressureToMemoryPressure( 1.00f));
    }
    class TestExternalPressurePoint : IPressurePoint
    {
        public string Name => "TestExternal";

        public float Pressure => 1.0f;
    }
}
