using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds test cases for the ambient bottleneck detector.
    /// </summary>
    [TestClass]
    public class TestCostTracker
    {
        private static readonly AmbientService<IAmbientCostTracker> _CostTracker = Ambient.GetService<IAmbientCostTracker>();

        private static readonly AsyncLocal<string> _localContextId = new();
        private static readonly AsyncLocal<string> _local = new(c => System.Diagnostics.Debug.WriteLine($"Thread:{Thread.CurrentThread.ManagedThreadId},Context:{_localContextId.Value ?? Guid.Empty.ToString("N")},Previous:{c.PreviousValue ?? "<null>"},Current:{c.CurrentValue ?? "<null>"},ThreadContextChanged:{c.ThreadContextChanged}"));
        [TestMethod]
        public async Task AsyncLocalTest1()
        {
            _localContextId.Value = Guid.NewGuid().ToString("N");
            DumpState(1, 0);
            await Task.Delay(100);
            DumpState(1, 1);
            _local.Value = "AsyncLocalTest1";
            DumpState(1, 2);
            await Task.Delay(100);
            DumpState(1, 3);
            await AsyncLocalTest2();
            DumpState(1, 4);
            await Task.Delay(100);
            DumpState(1, 5);
        }

        private async Task AsyncLocalTest2()
        {
            DumpState(2, 0);
            await Task.Delay(100);
            DumpState(2, 1);
            _local.Value = "AsyncLocalTest2";
            DumpState(2, 2);
            await Task.Delay(100);
            DumpState(2, 3);
            await AsyncLocalTest3();
            DumpState(2, 4);
            await Task.Delay(100);
            DumpState(2, 5);
        }

        private async Task AsyncLocalTest3()
        {
            DumpState(3, 0);
            await Task.Delay(100);
            DumpState(3, 1);
            _local.Value = "AsyncLocalTest3";
            DumpState(3, 2);
            await Task.Delay(100);
            DumpState(3, 3);
        }

        private void DumpState(int major, int minor)
        {
            System.Diagnostics.Debug.WriteLine($"Thread:{Thread.CurrentThread.ManagedThreadId},AsyncLocalTest{major}.{minor}: " + _local.Value ?? "<null>");
        }
        /*


         */
        [TestMethod]
        public void CostTrackerBasic()
        {
            IAmbientAccruedCharges timeWindowProfile = null;
            using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(new BasicAmbientCostTracker()))
            using (AmbientClock.Pause())
            using (AmbientCostTrackerCoordinator coordinator = new())
            using (IAmbientAccruedCharges processProfile = coordinator.CreateProcessProfiler(nameof(CostTrackerBasic)))
            using (IDisposable timeWindowProfiler = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerBasic), TimeSpan.FromMilliseconds(10000), p => { timeWindowProfile = p; return Task.CompletedTask; }))
            using (IAmbientAccruedCharges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
            {
                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic1", "1", 1);
                Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
                if (processProfile != null)
                {
                    Assert.AreEqual(1, processProfile.ChargeCount);
                    Assert.AreEqual(1, processProfile.AccumulatedChargeSum);
                }
                Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
                if (scopeProfile != null)
                {
                    Assert.AreEqual(1, scopeProfile.ChargeCount);
                    Assert.AreEqual(1, scopeProfile.AccumulatedChargeSum);
                }

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic2", "2", 0);

                Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
                if (processProfile != null)
                {
                    Assert.AreEqual(2, processProfile.ChargeCount);
                    Assert.AreEqual(1, processProfile.AccumulatedChargeSum);
                }
                Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
                if (scopeProfile != null)
                {
                    Assert.AreEqual(2, scopeProfile.ChargeCount);
                    Assert.AreEqual(1, scopeProfile.AccumulatedChargeSum);
                }

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic3", "3", 5);

                Assert.AreEqual(nameof(CostTrackerBasic), processProfile?.ScopeName);
                if (processProfile != null)
                {
                    Assert.AreEqual(3, processProfile.ChargeCount);
                    Assert.AreEqual(6, processProfile.AccumulatedChargeSum);
                }
                Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
                if (scopeProfile != null)
                {
                    Assert.AreEqual(3, scopeProfile.ChargeCount);
                    Assert.AreEqual(6, scopeProfile.AccumulatedChargeSum);
                }

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic4", "4", 0);

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic5", "4", 21);

                if (processProfile != null)
                {
                    Assert.AreEqual(5, processProfile.ChargeCount);
                    Assert.AreEqual(27, processProfile.AccumulatedChargeSum);
                }
                Assert.AreEqual(nameof(CostTrackerBasic), scopeProfile?.ScopeName);
                if (scopeProfile != null)
                {
                    Assert.AreEqual(5, scopeProfile.ChargeCount);
                    Assert.AreEqual(27, scopeProfile.AccumulatedChargeSum);
                }
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10000));

                _CostTracker.Local?.OnChargesAccrued("CostTrackerBasic6", "8", 58904);

                if (timeWindowProfile != null)
                {
                    Assert.AreEqual(5, timeWindowProfile.ChargeCount);
                    Assert.AreEqual(27, timeWindowProfile.AccumulatedChargeSum);
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

                _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
                _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 0);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
                _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "2", "2", 3);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(80)); // this should trigger the second window, which should close with an augmentation of the first CostTrackerCloseSampleWithRepeat1 entry
                _CostTracker.Local?.OnChargesAccrued(nameof(CostTrackerCloseSampleWithRepeat) + "1", "1", 0);
            }
        }
        [TestMethod]
        public void CostTrackerNull()
        {
            using (ScopedLocalServiceOverride<IAmbientCostTracker> o = new(null))
            using (AmbientCostTrackerCoordinator coordinator = new())
            using (IAmbientAccruedCharges processProfile = coordinator.CreateProcessProfiler(nameof(CostTrackerNull)))
            using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(CostTrackerNull), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
            using (IAmbientAccruedCharges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerNull)))
            using (AmbientClock.Pause())
            {
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
            }
        }
        [TestMethod]
        public void AmbientCostTrackerCoordinatorSettings()
        {
            string serviceId1 = "S3/Read:my-bucket-1";
            string serviceId2 = "S3/Read:my-bucket-2";
            string serviceId3 = "DynamoDB/Write:my-database";
            BasicAmbientSettingsSet settingsSet = new(nameof(AmbientCostTrackerCoordinatorSettings));
            settingsSet.ChangeSetting(nameof(AmbientCostTrackerCoordinator) + "-DefaultSystemGroupTransform", "(?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)");
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> o = new(settingsSet))
            using (AmbientClock.Pause())
            using (AmbientCostTrackerCoordinator coordinator = new())
            using (IAmbientAccruedCharges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
            {
                _CostTracker.Local?.OnChargesAccrued(serviceId1, "1", 0);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _CostTracker.Local?.OnChargesAccrued(serviceId2, "2", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

                _CostTracker.Local?.OnChargesAccrued(serviceId3, "3", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

                _CostTracker.Local?.OnChargesAccrued(serviceId1, "3", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                if (scopeProfile != null)
                {
                    Assert.AreEqual(4, scopeProfile.ChargeCount);
                    Assert.AreEqual(3, scopeProfile.AccumulatedChargeSum);
                }
            }
        }
        [TestMethod]
        public void AmbientCostTrackerCoordinatorSettingsNonCurrent()
        {
            string serviceId1 = "S3/Read:my-bucket-1";
            string serviceId2 = "S3/Read:my-bucket-2";
            string serviceId3 = "DynamoDB/Write:my-database";
            BasicAmbientSettingsSet settingsSet = new(nameof(AmbientCostTrackerCoordinatorSettingsNonCurrent));
            settingsSet.ChangeSetting(nameof(AmbientCostTrackerCoordinator) + "-DefaultSystemGroupTransform", "(?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)");
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> o = new(settingsSet))
            using (ScopedLocalServiceOverride<IAmbientCostTracker> p = new(new BasicAmbientCostTracker()))
            using (AmbientClock.Pause())
            using (AmbientCostTrackerCoordinator coordinator = new())
            using (IAmbientAccruedCharges scopeProfile = coordinator.CreateCallContextProfiler(nameof(CostTrackerBasic)))
            {
                _CostTracker.Local?.OnChargesAccrued(serviceId1, "1", 0);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _CostTracker.Local?.OnChargesAccrued(serviceId2, "2", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

                _CostTracker.Local?.OnChargesAccrued(serviceId3, "3", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

                _CostTracker.Local?.OnChargesAccrued(serviceId1, "3", 1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _CostTracker.Local?.OnChargesAccrued("", "1", 7);

                if (scopeProfile != null)
                {
                    Assert.AreEqual(5, scopeProfile.ChargeCount);
                    Assert.AreEqual(10, scopeProfile.AccumulatedChargeSum);
                }
            }
        }
    }
}
