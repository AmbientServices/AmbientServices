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
        CpuSample differentSample = CpuSample.GetSample();
        float recentUsage;
        float pendingUsage;
        using IDisposable pause = AmbientClock.Pause();

        using CpuMonitor monitor = new(TimeSpan.FromMilliseconds(1000));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsLessThanOrEqualTo(recentUsage, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, recentUsage);
        Assert.IsLessThanOrEqualTo(pendingUsage, 0.0f);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsLessThanOrEqualTo(recentUsage, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, recentUsage);
        Assert.IsLessThanOrEqualTo(pendingUsage, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, pendingUsage);

        await using CpuMonitor monitor2 = new(1000);
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsLessThanOrEqualTo(recentUsage, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, recentUsage);
        Assert.IsLessThanOrEqualTo(pendingUsage, 0.0f);
        AmbientClock.SkipAhead(TimeSpan.FromSeconds(2));
        recentUsage = monitor.RecentUsage;
        pendingUsage = monitor.PendingUsage;
        Assert.IsLessThanOrEqualTo(recentUsage, 0.0f);
        Assert.IsLessThanOrEqualTo(1.0f, recentUsage);
        Assert.IsLessThanOrEqualTo(pendingUsage, 0.0f);

        CpuSample sample = CpuSample.GetSample();
        CpuSample sample2 = sample;
        Assert.IsTrue(sample.Equals((object)sample2));
        Assert.IsTrue(sample == sample2);
        Assert.IsFalse(sample != sample2);

        Assert.IsFalse(sample.Equals((object)differentSample));
        Assert.IsFalse(sample == differentSample);
        Assert.IsTrue(sample != differentSample);

        int hashCode = sample.GetHashCode();
    }
}
