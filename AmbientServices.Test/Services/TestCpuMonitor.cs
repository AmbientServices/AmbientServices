using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestCpuMonitor
{
    [TestMethod]
    public async Task CpuMonitor()
    {
        CpuUsageSample differentSample = CpuUsageSample.GetSample();
        float recentUsage;
        float pendingUsage;
        using IDisposable pause = AmbientClock.Pause();

        using CpuMonitor monitor = new(TimeSpan.FromMilliseconds(1000));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsTrue(0.0f <= recentUsage);
        Assert.IsTrue(recentUsage <= 1.0f);
        Assert.IsTrue(0.0f <= pendingUsage);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsTrue(0.0f <= recentUsage);
        Assert.IsTrue(recentUsage <= 1.0f);
        Assert.IsTrue(0.0f <= pendingUsage);
        Assert.IsTrue(pendingUsage <= 1.0f);

        await using CpuMonitor monitor2 = new(1000);
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsTrue(0.0f <= recentUsage);
        Assert.IsTrue(recentUsage <= 1.0f);
        Assert.IsTrue(0.0f <= pendingUsage);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsTrue(0.0f <= recentUsage);
        Assert.IsTrue(recentUsage <= 1.0f);
        Assert.IsTrue(0.0f <= pendingUsage);

        CpuUsageSample sample = CpuUsageSample.GetSample();
        CpuUsageSample sample2 = sample;
        Assert.IsTrue(sample.Equals((object)sample2));
        Assert.IsTrue(sample == sample2);
        Assert.IsFalse(sample != sample2);

        Assert.IsFalse(sample.Equals((object)differentSample));
        Assert.IsFalse(sample == differentSample);
        Assert.IsTrue(sample != differentSample);

        int hashCode = sample.GetHashCode();
    }
}
