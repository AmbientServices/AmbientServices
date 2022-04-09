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
    public class TestServiceProfiler
    {
        private static readonly AmbientService<IAmbientServiceProfiler> _ServiceProfiler = Ambient.GetService<IAmbientServiceProfiler>();

        private static readonly AsyncLocal<string> _localContextId = new();
        private static readonly AsyncLocal<string> _local = new(c => System.Diagnostics.Debug.WriteLine($"Thread:{System.Threading.Thread.CurrentThread.ManagedThreadId},Context:{_localContextId.Value ?? Guid.Empty.ToString("N")},Previous:{c.PreviousValue ?? "<null>"},Current:{c.CurrentValue ?? "<null>"},ThreadContextChanged:{c.ThreadContextChanged}"));
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
            System.Diagnostics.Debug.WriteLine($"Thread:{System.Threading.Thread.CurrentThread.ManagedThreadId},AsyncLocalTest{major}.{minor}: " + _local.Value ?? "<null>");
        }
        /*
    Thread:13,AsyncLocalTest1.0: 
    Thread:9,AsyncLocalTest1.1: 
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest1,ThreadContextChanged:False
    Thread:9,AsyncLocalTest1.2: AsyncLocalTest1
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest1,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest1,ThreadContextChanged:True
    Thread:9,AsyncLocalTest1.3: AsyncLocalTest1
    Thread:9,AsyncLocalTest2.0: AsyncLocalTest1
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest1,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest1,ThreadContextChanged:True
    Thread:9,AsyncLocalTest2.1: AsyncLocalTest1
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest1,Current:AsyncLocalTest2,ThreadContextChanged:False
    Thread:9,AsyncLocalTest2.2: AsyncLocalTest2
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest2,Current:AsyncLocalTest1,ThreadContextChanged:True
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest1,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,AsyncLocalTest2.3: AsyncLocalTest2
    Thread:9,AsyncLocalTest3.0: AsyncLocalTest2
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest2,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,AsyncLocalTest3.1: AsyncLocalTest2
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest2,Current:AsyncLocalTest3,ThreadContextChanged:False
    Thread:9,AsyncLocalTest3.2: AsyncLocalTest3
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest3,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest2,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest3,ThreadContextChanged:True
    Thread:9,AsyncLocalTest3.3: AsyncLocalTest3
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest3,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,AsyncLocalTest2.4: AsyncLocalTest2
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest2,Current:AsyncLocalTest3,ThreadContextChanged:True
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest3,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,AsyncLocalTest2.5: AsyncLocalTest2
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest2,Current:AsyncLocalTest1,ThreadContextChanged:True
    Thread:9,AsyncLocalTest1.4: AsyncLocalTest1
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:AsyncLocalTest1,Current:AsyncLocalTest2,ThreadContextChanged:True
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest2,Current:<null>,ThreadContextChanged:True
    Thread:9,Context:002d421118804fb3b6f40941df50e056,Previous:<null>,Current:AsyncLocalTest1,ThreadContextChanged:True
    Thread:9,AsyncLocalTest1.5: AsyncLocalTest1
    Thread:9,Context:00000000000000000000000000000000,Previous:AsyncLocalTest1,Current:<null>,ThreadContextChanged:True

         */
        [TestMethod]
        public void ServiceProfilerBasic()
        {
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(new BasicAmbientServiceProfiler()))
            using (AmbientClock.Pause())
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IAmbientServiceProfile processProfile = coordinator.CreateProcessProfiler(nameof(ServiceProfilerBasic)))
            using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(ServiceProfilerBasic), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
            using (IAmbientServiceProfile scopeProfile = coordinator.CreateCallContextProfiler(nameof(ServiceProfilerBasic)))
            {
                _ServiceProfiler.Local?.SwitchSystem("ServiceProfilerBasic1");
                Assert.AreEqual(nameof(ServiceProfilerBasic), processProfile?.ScopeName);
                if (processProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in processProfile.ProfilerStatistics)
                    {
                        if (string.IsNullOrEmpty(stats.Group))
                        {
                            Assert.AreEqual("", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(0, stats.TotalStopwatchTicksUsed);
                        }
                        else
                        {
                            Assert.AreEqual("ServiceProfilerBasic1", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(0, stats.TotalStopwatchTicksUsed);
                        }
                    }
                }
                Assert.AreEqual(nameof(ServiceProfilerBasic), scopeProfile?.ScopeName);
                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (string.IsNullOrEmpty(stats.Group))
                        {
                            Assert.AreEqual("", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(0, stats.TotalStopwatchTicksUsed);
                        }
                        else
                        {
                            Assert.AreEqual("ServiceProfilerBasic1", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(0, stats.TotalStopwatchTicksUsed);
                        }
                    }
                }

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                _ServiceProfiler.Local?.SwitchSystem(null);

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

                _ServiceProfiler.Local?.SwitchSystem("ServiceProfilerBasic2");

                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (string.IsNullOrEmpty(stats.Group))
                        {
                            Assert.AreEqual("", stats.Group);
                            Assert.AreEqual(2, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(113), stats.TimeUsed);
                        }
                        else if (stats.Group.EndsWith("1"))
                        {
                            Assert.AreEqual("ServiceProfilerBasic1", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(100), stats.TimeUsed);
                        }
                        else if (stats.Group.EndsWith("2"))
                        {
                            Assert.AreEqual("ServiceProfilerBasic2", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(0), stats.TimeUsed);
                        }
                    }
                }

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

                _ServiceProfiler.Local?.SwitchSystem(null);

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(113));

                _ServiceProfiler.Local?.SwitchSystem("ServiceProfilerBasic3");

                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (string.IsNullOrEmpty(stats.Group))
                        {
                            Assert.AreEqual("", stats.Group);
                            Assert.AreEqual(3, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(226), stats.TimeUsed);
                        }
                        else if (stats.Group.EndsWith("1"))
                        {
                            Assert.AreEqual("ServiceProfilerBasic1", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(100), stats.TimeUsed);
                        }
                        else if (stats.Group.EndsWith("2"))
                        {
                            Assert.AreEqual("ServiceProfilerBasic2", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(100), stats.TimeUsed);
                        }
                        else if (stats.Group.EndsWith("3"))
                        {
                            Assert.AreEqual("ServiceProfilerBasic3", stats.Group);
                            Assert.AreEqual(1, stats.ExecutionCount);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(0), stats.TimeUsed);
                        }
                    }
                }

                _ServiceProfiler.Local?.SwitchSystem(null);
            }
        }
        [TestMethod]
        public void ServiceProfilerCloseSampleWithRepeat()
        {
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(new BasicAmbientServiceProfiler()))
            using (AmbientClock.Pause())
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(ServiceProfilerCloseSampleWithRepeat), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
            {
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100)); // this should trigger the first window and it should have only the default "" entry

                _ServiceProfiler.Local?.SwitchSystem(nameof(ServiceProfilerCloseSampleWithRepeat) + "1");
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
                _ServiceProfiler.Local?.SwitchSystem(null);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10));
                _ServiceProfiler.Local?.SwitchSystem("ServiceProfilerCloseSampleWithRepeat1");
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(80)); // this should trigger the second window, which should close with an augmentation of the first ServiceProfilerCloseSampleWithRepeat1 entry
                _ServiceProfiler.Local?.SwitchSystem(null);
            }
        }
        [TestMethod]
        public void ServiceProfilerNull()
        {
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(null))
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IAmbientServiceProfile processProfile = coordinator.CreateProcessProfiler(nameof(ServiceProfilerNull)))
            using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(ServiceProfilerNull), TimeSpan.FromMilliseconds(100), p => Task.CompletedTask))
            using (IAmbientServiceProfile scopeProfile = coordinator.CreateCallContextProfiler(nameof(ServiceProfilerNull)))
            using (AmbientClock.Pause())
            {
                _ServiceProfiler.Local?.SwitchSystem(nameof(ServiceProfilerNull));
            }
        }
        [TestMethod]
        public void ServiceProfilerNullOnWindowComplete()
        {
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(new BasicAmbientServiceProfiler()))
            using (AmbientServiceProfilerCoordinator coordinator = new())
            {
                Assert.ThrowsException<ArgumentNullException>(
                    () =>
                    {
                        using (IDisposable timeWindowProfile = coordinator.CreateTimeWindowProfiler(nameof(ServiceProfilerNull), TimeSpan.FromMilliseconds(100), null!))
                        {
                        }
                    });
            }
        }
        [TestMethod]
        public void ServiceProfilerNoListener()
        {
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(new BasicAmbientServiceProfiler()))
            using (AmbientClock.Pause())
            {
                _ServiceProfiler.Local?.SwitchSystem(nameof(ServiceProfilerNull));
            }
        }
        [TestMethod]
        public void AmbientServiceProfilerCoordinatorSettings()
        {
            string system1 = "DynamoDB/Table:My-table/Partition:342644/Result:Success";
            string system2 = "S3/Bucket:My-bucket/Prefix:abcdefg/Result:Retry";
            string system3 = "SQL/Database:My-database/Table:User/Result:Failed";
            BasicAmbientSettingsSet settingsSet = new(nameof(AmbientServiceProfilerCoordinatorSettings));
            settingsSet.ChangeSetting(nameof(AmbientServiceProfilerCoordinator) + "-DefaultSystemGroupTransform", "(?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)");
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> o = new(settingsSet))
            using (AmbientClock.Pause())
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IAmbientServiceProfile scopeProfile = coordinator.CreateCallContextProfiler(nameof(ServiceProfilerBasic)))
            {
                _ServiceProfiler.Local?.SwitchSystem(system1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _ServiceProfiler.Local?.SwitchSystem(system2);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

                _ServiceProfiler.Local?.SwitchSystem(system3);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

                _ServiceProfiler.Local?.SwitchSystem(system1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (stats.Group.StartsWith("DynamoDB"))
                        {
                            Assert.AreEqual("DynamoDB/Result:Success", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.TimeUsed);
                            Assert.AreEqual(2, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("S3"))
                        {
                            Assert.AreEqual("S3/Bucket:My-bucket/Result:Retry", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(200), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("SQL"))
                        {
                            Assert.AreEqual("SQL/Database:My-database/Result:Failed", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(3000), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                    }
                }
            }
        }
        [TestMethod]
        public void AmbientServiceProfilerCoordinatorSettingsNonCurrent()
        {
            string system1 = "DynamoDB/Table:My-table/Partition:342644/Result:Success";
            string system2 = "S3/Bucket:My-bucket/Prefix:abcdefg/Result:Retry";
            string system3 = "SQL/Database:My-database/Table:User/Result:Failed";
            BasicAmbientSettingsSet settingsSet = new(nameof(AmbientServiceProfilerCoordinatorSettingsNonCurrent));
            settingsSet.ChangeSetting(nameof(AmbientServiceProfilerCoordinator) + "-DefaultSystemGroupTransform", "(?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)");
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> o = new(settingsSet))
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> p = new(new BasicAmbientServiceProfiler()))
            using (AmbientClock.Pause())
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IAmbientServiceProfile scopeProfile = coordinator.CreateCallContextProfiler(nameof(ServiceProfilerBasic)))
            {
                _ServiceProfiler.Local?.SwitchSystem(system1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _ServiceProfiler.Local?.SwitchSystem(system2);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

                _ServiceProfiler.Local?.SwitchSystem(system3);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

                _ServiceProfiler.Local?.SwitchSystem(system1);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _ServiceProfiler.Local?.SwitchSystem("noreport");

                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (stats.Group.StartsWith("DynamoDB"))
                        {
                            Assert.AreEqual("DynamoDB/Result:Success", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.TimeUsed);
                            Assert.AreEqual(2, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("S3"))
                        {
                            Assert.AreEqual("S3/Bucket:My-bucket/Result:Retry", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(200), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("SQL"))
                        {
                            Assert.AreEqual("SQL/Database:My-database/Result:Failed", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(3000), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                    }
                }
            }
        }
        [TestMethod]
        public void AmbientServiceProfilerCoordinatorOverrideGroupTransform()
        {
            string system1Start = "DynamoDB/Table:My-table/Partition:342644";
            string system1End = "DynamoDB/Table:My-table/Partition:342644/Result:Success";
            string system2 = "S3/Bucket:My-bucket/Prefix:abcdefg/Result:Retry";
            string system3 = "SQL/Database:My-database/Table:User/Result:Failed";
            string groupTransform = "(?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)";
            IAmbientServiceProfile timeWindowProfile = null;
            using (AmbientClock.Pause())
            using (ScopedLocalServiceOverride<IAmbientServiceProfiler> o = new(new BasicAmbientServiceProfiler()))
            using (AmbientServiceProfilerCoordinator coordinator = new())
            using (IAmbientServiceProfile processProfile = coordinator.CreateProcessProfiler(nameof(AmbientServiceProfilerCoordinatorOverrideGroupTransform), groupTransform))
            using (IDisposable timeWindowProfiler = coordinator.CreateTimeWindowProfiler(nameof(AmbientServiceProfilerCoordinatorOverrideGroupTransform), TimeSpan.FromMilliseconds(10000), p => { timeWindowProfile = p; return Task.CompletedTask; }, groupTransform))
            using (IAmbientServiceProfile scopeProfile = coordinator.CreateCallContextProfiler(nameof(AmbientServiceProfilerCoordinatorOverrideGroupTransform), groupTransform))
            {
                _ServiceProfiler.Local?.SwitchSystem(system1Start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _ServiceProfiler.Local?.SwitchSystem(system2, system1End);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));

                _ServiceProfiler.Local?.SwitchSystem(system3);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(3000));

                _ServiceProfiler.Local?.SwitchSystem(system1End);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5));

                _ServiceProfiler.Local?.SwitchSystem("noreport");

                if (scopeProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in scopeProfile.ProfilerStatistics)
                    {
                        if (stats.Group.StartsWith("DynamoDB"))
                        {
                            Assert.AreEqual("DynamoDB/Result:Success", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.TimeUsed);
                            Assert.AreEqual(2, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("S3"))
                        {
                            Assert.AreEqual("S3/Bucket:My-bucket/Result:Retry", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(200), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("SQL"))
                        {
                            Assert.AreEqual("SQL/Database:My-database/Result:Failed", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(3000), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                    }
                }
                if (processProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in processProfile.ProfilerStatistics)
                    {
                        if (stats.Group.StartsWith("DynamoDB"))
                        {
                            Assert.AreEqual("DynamoDB/Result:Success", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.TimeUsed);
                            Assert.AreEqual(2, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("S3"))
                        {
                            Assert.AreEqual("S3/Bucket:My-bucket/Result:Retry", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(200), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("SQL"))
                        {
                            Assert.AreEqual("SQL/Database:My-database/Result:Failed", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(3000), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                    }
                }
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(10000));
                if (timeWindowProfile != null)
                {
                    foreach (AmbientServiceProfilerAccumulator stats in timeWindowProfile.ProfilerStatistics)
                    {
                        if (stats.Group.StartsWith("DynamoDB"))
                        {
                            Assert.AreEqual("DynamoDB/Result:Success", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.TimeUsed);
                            Assert.AreEqual(2, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("S3"))
                        {
                            Assert.AreEqual("S3/Bucket:My-bucket/Result:Retry", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(200), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                        else if (stats.Group.StartsWith("SQL"))
                        {
                            Assert.AreEqual("SQL/Database:My-database/Result:Failed", stats.Group);
                            Assert.AreEqual(TimeSpan.FromMilliseconds(3000), stats.TimeUsed);
                            Assert.AreEqual(1, stats.ExecutionCount);
                        }
                    }
                }
            }
        }
    }
}
