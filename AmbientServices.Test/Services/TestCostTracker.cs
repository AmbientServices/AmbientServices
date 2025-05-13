using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds test cases for the ambient bottleneck detector.
/// </summary>
[TestClass]
public class TestCostTracker
{
    private static readonly AmbientService<IAmbientCostTracker> _CostTracker = Ambient.GetService<IAmbientCostTracker>();

    [TestMethod]
    public void CostTrackerBasic()
    {
        IAmbientAccruedChargesAndCostChanges timeWindowProfile = null;
        using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(new BasicAmbientCostTracker()))
        using (AmbientClock.Pause())
        using (AmbientCostTrackerCoordinator coordinator = new())
        using (IAmbientAccruedChargesAndCostChanges processProfile = coordinator.CreateProcessProfiler(nameof(CostTrackerBasic)))
        using (IDisposable timeWindowProfiler = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerBasic), TimeSpan.FromMilliseconds(10000), p => { timeWindowProfile = p; return Task.CompletedTask; }))
        using (IAmbientAccruedChargesAndCostChanges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
        {
            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic1", "1", 1);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic1", "1", 0);
            Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
            if (processProfile != null)
            {
                Assert.AreEqual(1, processProfile.ChargeCount);
                Assert.AreEqual(1, processProfile.AccumulatedChargeSum);
                Assert.AreEqual(1, processProfile.CostChangeCount);
                Assert.AreEqual(0, processProfile.AccumulatedCostChangeSum);
            }
            Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
            if (scopeProfile != null)
            {
                Assert.AreEqual(1, scopeProfile.ChargeCount);
                Assert.AreEqual(1, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(1, scopeProfile.CostChangeCount);
                Assert.AreEqual(0, scopeProfile.AccumulatedCostChangeSum);
            }

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic2", "2", 0);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic2", "2", 17);

            Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
            if (processProfile != null)
            {
                Assert.AreEqual(2, processProfile.ChargeCount);
                Assert.AreEqual(1, processProfile.AccumulatedChargeSum);
                Assert.AreEqual(2, processProfile.CostChangeCount);
                Assert.AreEqual(17, processProfile.AccumulatedCostChangeSum);
            }
            Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
            if (scopeProfile != null)
            {
                Assert.AreEqual(2, scopeProfile.ChargeCount);
                Assert.AreEqual(1, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(2, scopeProfile.CostChangeCount);
                Assert.AreEqual(17, scopeProfile.AccumulatedCostChangeSum);
            }

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic3", "3", 5);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic3", "3", -9);

            Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
            if (processProfile != null)
            {
                Assert.AreEqual(3, processProfile.ChargeCount);
                Assert.AreEqual(6, processProfile.AccumulatedChargeSum);
                Assert.AreEqual(3, processProfile.CostChangeCount);
                Assert.AreEqual(8, processProfile.AccumulatedCostChangeSum);
            }
            Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
            if (scopeProfile != null)
            {
                Assert.AreEqual(3, scopeProfile.ChargeCount);
                Assert.AreEqual(6, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(3, scopeProfile.CostChangeCount);
                Assert.AreEqual(8, scopeProfile.AccumulatedCostChangeSum);
            }

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic4", "4", 0);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic4", "4", 13);

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic5", "4", 21);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic5", "4", -3);

            if (processProfile != null)
            {
                Assert.AreEqual(5, processProfile.ChargeCount);
                Assert.AreEqual(27, processProfile.AccumulatedChargeSum);
                Assert.AreEqual(5, processProfile.CostChangeCount);
                Assert.AreEqual(18, processProfile.AccumulatedCostChangeSum);
            }
            Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
            if (scopeProfile != null)
            {
                Assert.AreEqual(5, scopeProfile.ChargeCount);
                Assert.AreEqual(27, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(5, scopeProfile.CostChangeCount);
                Assert.AreEqual(18, scopeProfile.AccumulatedCostChangeSum);
            }
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10000));

            _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic6", "8", 58904);
            _CostTracker.Local?.OnOngoingCostChanged("CostTrackerBasic6", "8", 27893);

            if (timeWindowProfile != null)
            {
                Assert.AreEqual(5, timeWindowProfile.ChargeCount);
                Assert.AreEqual(27, timeWindowProfile.AccumulatedChargeSum);
                Assert.AreEqual(5, timeWindowProfile.CostChangeCount);
                Assert.AreEqual(18, timeWindowProfile.AccumulatedCostChangeSum);
            }

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10000));

            if (timeWindowProfile != null)
            {
                Assert.AreEqual(1, timeWindowProfile.ChargeCount);
                Assert.AreEqual(58904, timeWindowProfile.AccumulatedChargeSum);
                Assert.AreEqual(1, timeWindowProfile.CostChangeCount);
                Assert.AreEqual(27893, timeWindowProfile.AccumulatedCostChangeSum);
            }

        }
    }
    [TestMethod]
    public void CostTrackerCloseSampleWithRepeat()
    {
        using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(new BasicAmbientCostTracker()))
        using (AmbientClock.Pause())
        using (AmbientCostTrackerCoordinator coordinator = new())
        using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerCloseSampleWithRepeat), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
        {
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100)); // this should trigger the first window and it should have only the default "" entry

            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 1);
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", -1);
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 0);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerCloseSampleWithRepeat) + "2", "2", 0);
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "2", "2", 3);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(80)); // this should trigger the second window, which should close with an augmentation of the first CostTrackerCloseSampleWithRepeat1 entry
            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 4);
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 0);
        }
    }
    [TestMethod]
    public void CostTrackerNull()
    {
        using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(null))
        using (AmbientCostTrackerCoordinator coordinator = new())
        using (IAmbientAccruedChargesAndCostChanges processProfile = coordinator.CreateProcessProfiler(nameof(CostTrackerNull)))
        using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerNull), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
        using (IAmbientAccruedChargesAndCostChanges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerNull)))
        using (AmbientClock.Pause())
        {
            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerNull) + "1", "1", 0);
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerNull) + "1", "1", 0);
        }
    }
    [TestMethod]
    public void CostTrackerNullOnWindowComplete()
    {
        using ScopedLocalServiceOverride<IAmbientCostTracker> o = new(new BasicAmbientCostTracker());
        using AmbientCostTrackerCoordinator coordinator = new();
        Assert.ThrowsException<ArgumentNullException>(
            () =>
            {
                using IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerNullOnWindowComplete), TimeSpan.FromMilliseconds(100), null!);
            });
    }
    [TestMethod]
    public void CostTrackerNoListener()
    {
        using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(new BasicAmbientCostTracker()))
        using (AmbientClock.Pause())
        {
            _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerNoListener) + "1", "1", 0);
            _CostTracker.Local?.OnOngoingCostChanged(nameof(CostTrackerNoListener) + "1", "1", 0);
        }
    }
#if false
    [TestMethod]
    public void AmbientCostTrackerCoordinatorSettings()
    {
        string serviceId1 = "S3/Read:my-bucket-1";
        string serviceId2 = "S3/Read:my-bucket-2";
        string serviceId3 = "DynamoDB/Write:my-database";
        using (AmbientClock.Pause())
        using (AmbientCostTrackerCoordinator coordinator = new())
        using (IAmbientAccruedChargesAndCostChanges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
        {
            _CostTracker.Local?.OnChargesAccrued(serviceId1, "1", 0);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId1, "1", 0);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

            _CostTracker.Local?.OnChargesAccrued(serviceId2, "2", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId2, "2", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

            _CostTracker.Local?.OnChargesAccrued(serviceId3, "3", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId3, "3", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

            _CostTracker.Local?.OnChargesAccrued(serviceId1, "3", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId1, "3", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

            if (scopeProfile != null)
            {
                Assert.AreEqual(4, scopeProfile.ChargeCount);
                Assert.AreEqual(3, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(4, scopeProfile.CostChangeCount);
                Assert.AreEqual(3, scopeProfile.AccumulatedCostChangeSum);
            }
        }
    }
#endif
    [TestMethod]
    public void AmbientCostTrackerCoordinatorSettingsNonCurrent()
    {
        string serviceId1 = "S3/Read:my-bucket-1";
        string serviceId2 = "S3/Read:my-bucket-2";
        string serviceId3 = "DynamoDB/Write:my-database";
        using (ScopedLocalServiceOverride<IAmbientCostTracker> p = new(new BasicAmbientCostTracker()))
        using (AmbientClock.Pause())
        using (AmbientCostTrackerCoordinator coordinator = new())
        using (IAmbientAccruedChargesAndCostChanges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
        {
            _CostTracker.Local?.OnChargesAccrued(serviceId1, "1", 0);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId1, "1", 0);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

            _CostTracker.Local?.OnChargesAccrued(serviceId2, "2", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId2, "2", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

            _CostTracker.Local?.OnChargesAccrued(serviceId3, "3", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId3, "3", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

            _CostTracker.Local?.OnChargesAccrued(serviceId1, "3", 1);
            _CostTracker.Local?.OnOngoingCostChanged(serviceId1, "3", 1);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

            _CostTracker.Local?.OnChargesAccrued("", "1", 7);
            _CostTracker.Local?.OnOngoingCostChanged("", "1", 7);

            if (scopeProfile != null)
            {
                Assert.AreEqual(5, scopeProfile.ChargeCount);
                Assert.AreEqual(10, scopeProfile.AccumulatedChargeSum);
                Assert.AreEqual(5, scopeProfile.CostChangeCount);
                Assert.AreEqual(10, scopeProfile.AccumulatedCostChangeSum);
            }
        }
    }
}
