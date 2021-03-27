using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientClock"/>.
    /// </summary>
    [TestClass]
    public class TestClock
    {
        class AmbientPausedClockTimeChangeListener : IAmbientClockTimeChangedNotificationSink, IDisposable
        {
            AmbientClock.PausedAmbientClock _pausedClock;
            Action<IAmbientClock, long, long, DateTime, DateTime> _action;
            public AmbientPausedClockTimeChangeListener(AmbientClock.PausedAmbientClock pausedClock, Action<IAmbientClock, long, long, DateTime, DateTime> action)
            {
                _pausedClock = pausedClock;
                _action = action;
                pausedClock.RegisterTimeChangedNotificationSink(this);
            }

            public void Dispose()
            {
                _pausedClock.DeregisterTimeChangedNotificationSink(this);
            }

            public void TimeChanged(IAmbientClock clock, long oldTicks, long newTicks, DateTime oldUtcDateTime, DateTime newUtcDateTime)
            {
                _action?.Invoke(clock, oldTicks, newTicks, oldUtcDateTime, newUtcDateTime);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void PausedClock()
        {
            AmbientClock.PausedAmbientClock clock = new AmbientClock.PausedAmbientClock();
            long baselineTicks = clock.Ticks;
            IAmbientClock pausedClock = null;
            long oldTicks = 0;
            long newTicks = 0;
            DateTime oldUtcDateTime = DateTime.MinValue;
            DateTime newUtcDateTime = DateTime.MinValue;
            using (AmbientPausedClockTimeChangeListener listener = new AmbientPausedClockTimeChangeListener(clock, (c, ot, nt, od, nd) => { pausedClock = c; oldTicks = ot; newTicks = nt; oldUtcDateTime = od; newUtcDateTime = nd; }))
            {
                clock.SkipAhead(10000);
                Assert.IsNotNull(pausedClock);
                Assert.AreEqual(clock, pausedClock);
                Assert.AreEqual(baselineTicks, oldTicks);
                Assert.AreEqual(baselineTicks + 10000, newTicks);
                Assert.AreEqual(clock, pausedClock);
                Assert.IsTrue(newUtcDateTime > oldUtcDateTime);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task AmbientClockPaused()
        {
            using (AmbientClock.Pause())
            {
                DateTime start = AmbientClock.UtcNow;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                await Task.Delay(100);
                DateTime end = AmbientClock.UtcNow;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.AreEqual(endElapsed, startElapsed, $"Time elapsed ({endElapsed - startElapsed}) when clock was paused!");
                Assert.AreEqual(end, start, $"Time elapsed ({end - start}) when clock was paused!");
                Assert.IsFalse(AmbientClock.IsSystemClock);
                Assert.IsNotNull(Ambient.GetService<IAmbientClock>().Override);

                AmbientClock.ThreadSleep(100);
                end = AmbientClock.UtcNow;
                endElapsed = AmbientClock.Elapsed;

                Assert.AreEqual(endElapsed, startElapsed.Add(TimeSpan.FromMilliseconds(100)), $"Time elapsed ({endElapsed - startElapsed}).  Should be 100ms!");
                Assert.AreEqual(end, start.AddMilliseconds(100));
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task ClockSystem()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            long startTicks = AmbientClock.Ticks;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            TimeSpan startElapsed = AmbientClock.Elapsed;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            DateTime start = DateTime.UtcNow;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            await AmbientClock.TaskDelay(100);
            Assert.IsTrue(AmbientClock.IsSystemClock);
            long testTicks = AmbientClock.Ticks;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            TimeSpan testElapsed = AmbientClock.Elapsed;
            Assert.IsTrue(AmbientClock.IsSystemClock);

            DateTime test = AmbientClock.UtcNow;
            DateTime testLocal = AmbientClock.Now;
            Assert.IsTrue(Math.Abs((test.ToLocalTime() - testLocal).Ticks) < 10000000);
            Assert.IsTrue(AmbientClock.IsSystemClock);
            await AmbientClock.TaskDelay(100, default(CancellationToken));
            Assert.IsTrue(AmbientClock.IsSystemClock);

            test = AmbientClock.UtcNow;
            testLocal = AmbientClock.Now;
            Assert.IsTrue(Math.Abs((test.ToLocalTime() - testLocal).Ticks) < 10000000);
            Assert.IsTrue(AmbientClock.IsSystemClock);
            AmbientClock.ThreadSleep(100);
            Assert.IsTrue(AmbientClock.IsSystemClock);

            DateTime end = DateTime.UtcNow;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            long endTicks = AmbientClock.Ticks;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            TimeSpan endElapsed = AmbientClock.Elapsed;
            Assert.IsTrue(AmbientClock.IsSystemClock);
            Assert.IsTrue(start <= test && test <= end);
            Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks);
            Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task ClockPausedRelativeToSystem()
        {
            using (AmbientClock.Pause())
            {
                long startTicks = AmbientClock.Ticks;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                DateTime start = DateTime.UtcNow;
                await Task.Delay(100);
                await AmbientClock.TaskDelay(100);
                long testTicks = AmbientClock.Ticks;
                TimeSpan testElapsed = AmbientClock.Elapsed;
                DateTime test = AmbientClock.UtcNow;
                await Task.Delay(100, default(CancellationToken));
                await AmbientClock.TaskDelay(100, default(CancellationToken));
                DateTime end = DateTime.UtcNow;
                long endTicks = AmbientClock.Ticks;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.IsTrue(start <= test && test <= end);
                Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks);
                Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed);
                
                test = AmbientClock.UtcNow;
                DateTime testLocal = AmbientClock.Now;
                Assert.IsTrue(Math.Abs((test.ToLocalTime() - testLocal).Ticks) < 10000000);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task ClockOverrideNull()
        {
            using (new ScopedLocalServiceOverride<IAmbientClock>(null))
            {
                long startTicks = AmbientClock.Ticks;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                DateTime start = DateTime.UtcNow;
                await Task.Delay(100);
                long testTicks = AmbientClock.Ticks;
                TimeSpan testElapsed = AmbientClock.Elapsed;
                DateTime test = AmbientClock.UtcNow;
                DateTime testLocal = AmbientClock.Now;
                Assert.IsTrue(Math.Abs((test.ToLocalTime() - testLocal).Ticks) < 10000000);
                await Task.Delay(100);
                DateTime end = DateTime.UtcNow;
                long endTicks = AmbientClock.Ticks;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.IsTrue(start <= test && test <= end, $"{start}/{test}/{end}");
                Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks, $"{startTicks}/{testTicks}/{endTicks}");
                Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed, $"{startElapsed}/{testElapsed}/{endElapsed}");
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchTest()
        {
            long timestamp1 = Stopwatch.GetTimestamp();
            long timestamp2 = AmbientStopwatch.GetTimestamp();
            Assert.IsTrue(Math.Abs(timestamp1 - timestamp2) < Stopwatch.Frequency * 10);    // if this fails, it means that either there is a bug or the two lines above took 10+ seconds to run
            using (AmbientClock.Pause())
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch(true);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.ElapsedTicks == 0);
                Assert.IsTrue(stopwatch.Elapsed == TimeSpan.Zero);
                stopwatch = new AmbientStopwatch(true);
                TimeSpan delay = TimeSpan.FromMilliseconds(100);
                AmbientClock.ThreadSleep(delay);
                Assert.AreEqual((long)delay.TotalMilliseconds, stopwatch.ElapsedMilliseconds);          
                Assert.AreEqual(delay, stopwatch.Elapsed);                                              
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchSystem()
        {
            using (new ScopedLocalServiceOverride<IAmbientClock>(null))
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch(false);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.ElapsedTicks == 0);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.ElapsedTicks == 0);
                stopwatch = new AmbientStopwatch(true);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds > 0);
                Assert.IsTrue(stopwatch.ElapsedTicks > 0);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchPauseResumeReset()
        {
            using (AmbientClock.Pause())
            {
                AmbientStopwatch stopwatch = AmbientStopwatch.StartNew();
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Start();
                stopwatch.Start();
                await AmbientClock.TaskDelay(100);
                Assert.AreEqual(100, stopwatch.ElapsedMilliseconds);
                await AmbientClock.TaskDelay(100);
                Assert.AreEqual(200, stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
                stopwatch.Stop();
                await AmbientClock.TaskDelay(TimeSpan.FromMilliseconds(100), default(CancellationToken));
                Assert.AreEqual(200, stopwatch.ElapsedMilliseconds);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void StopwatchEmulation()
        {
            Assert.AreEqual(Stopwatch.Frequency, AmbientStopwatch.Frequency);
            Assert.AreEqual(Stopwatch.IsHighResolution, AmbientStopwatch.IsHighResolution);
            using (AmbientClock.Pause())
            {
                AmbientStopwatch stopwatch;
                Assert.AreEqual(AmbientStopwatch.GetTimestamp(), AmbientStopwatch.GetTimestamp());

                stopwatch = new AmbientStopwatch();
                Assert.IsFalse(stopwatch.IsRunning);
                Assert.AreEqual(0, stopwatch.ElapsedMilliseconds);
                Assert.AreEqual((long)stopwatch.Elapsed.TotalMilliseconds, stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                Assert.IsTrue(stopwatch.IsRunning);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                
                Assert.AreEqual(100, stopwatch.ElapsedMilliseconds);

                stopwatch = AmbientStopwatch.StartNew();
                Assert.IsTrue(stopwatch.IsRunning);
                Assert.AreEqual((long)stopwatch.Elapsed.TotalMilliseconds, stopwatch.ElapsedMilliseconds);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchSystemPauseResumeReset()
        {
            using (new ScopedLocalServiceOverride<IAmbientClock>(null))
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch(false);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Start();
                stopwatch.Start();
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.ElapsedTicks > 0);
                stopwatch.Stop();
                stopwatch.Stop();
                long elapsed = stopwatch.ElapsedTicks;
                await Task.Delay(100);
                Assert.AreEqual(elapsed, stopwatch.ElapsedTicks);
                stopwatch.Start();
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.Ticks < 1000);  // this could fail if there is lots of CPU contention
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task CancellationTokenSourcePaused()
        {
            using (AmbientClock.Pause())
            {
                AmbientCancellationTokenSource noTimeoutSource = new AmbientCancellationTokenSource();
                AmbientCancellationTokenSource timeoutSource1 = new AmbientCancellationTokenSource(100);
                AmbientCancellationTokenSource timeoutSource2 = new AmbientCancellationTokenSource(TimeSpan.FromMilliseconds(100));
                AmbientCancellationTokenSource timeoutSource3 = new AmbientCancellationTokenSource();
                AmbientCancellationTokenSource timeoutSource4 = new AmbientCancellationTokenSource();
                timeoutSource3.CancelAfter(100);
                timeoutSource4.CancelAfter(TimeSpan.FromMilliseconds(100));
                await AmbientClock.TaskDelay(TimeSpan.FromMilliseconds(500));
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsTrue(timeoutSource1.IsCancellationRequested);
                Assert.IsTrue(timeoutSource2.IsCancellationRequested);
                Assert.IsTrue(timeoutSource3.IsCancellationRequested);
                Assert.IsTrue(timeoutSource4.IsCancellationRequested);
                noTimeoutSource.Cancel();
                Assert.IsTrue(noTimeoutSource.IsCancellationRequested);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task CancellationTokenSourceObsolete()
        {
            using (AmbientClock.Pause())
            {
#pragma warning disable CS0618 
                CancellationTokenSource systemSource = new CancellationTokenSource();
                AmbientCancellationTokenSource noTimeoutSource = AmbientClock.CreateCancellationTokenSource();
                AmbientCancellationTokenSource timeoutSource1 = AmbientClock.CreateCancellationTokenSource(systemSource);
                AmbientCancellationTokenSource timeoutSource2 = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(100));
#pragma warning restore CS0618 
                await AmbientClock.TaskDelay(TimeSpan.FromMilliseconds(500));
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsFalse(timeoutSource1.IsCancellationRequested);
                Assert.IsTrue(timeoutSource2.IsCancellationRequested);
                noTimeoutSource.Cancel();
                systemSource.Cancel();
                Assert.IsTrue(timeoutSource1.IsCancellationRequested);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task CancellationTokenSourceRescheduleCancellation()
        {
            using (AmbientClock.Pause())
            {
                AmbientCancellationTokenSource timeoutSource = new AmbientCancellationTokenSource(100);
                Assert.IsFalse(timeoutSource.IsCancellationRequested);
                await AmbientClock.TaskDelay(50);
                Assert.IsFalse(timeoutSource.IsCancellationRequested);
                timeoutSource.CancelAfter(200);
                Assert.IsFalse(timeoutSource.IsCancellationRequested);
                await AmbientClock.TaskDelay(100);
                Assert.IsFalse(timeoutSource.IsCancellationRequested);
                await AmbientClock.TaskDelay(100);
                Assert.IsTrue(timeoutSource.IsCancellationRequested);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task CancellationTokenSourceSystem()
        {
            AmbientCancellationTokenSource noTimeoutSource = new AmbientCancellationTokenSource();
            AmbientCancellationTokenSource timeoutSource1 = new AmbientCancellationTokenSource(100);
            AmbientCancellationTokenSource timeoutSource2 = new AmbientCancellationTokenSource(TimeSpan.FromMilliseconds(100));
            AmbientCancellationTokenSource timeoutSource3 = new AmbientCancellationTokenSource();
            AmbientCancellationTokenSource timeoutSource4 = new AmbientCancellationTokenSource();
            timeoutSource3.CancelAfter(100);
            timeoutSource4.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
            Assert.IsTrue(timeoutSource1.IsCancellationRequested);
            Assert.IsTrue(timeoutSource2.IsCancellationRequested);
            Assert.IsTrue(timeoutSource3.IsCancellationRequested);
            Assert.IsTrue(timeoutSource4.IsCancellationRequested);
            noTimeoutSource.Cancel();
            Assert.IsTrue(noTimeoutSource.IsCancellationRequested);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task PausedCancellationTokenSource()
        {
            using (IDisposable dispose = AmbientClock.Pause())
            {
                AmbientCancellationTokenSource noTimeoutSource = new AmbientCancellationTokenSource();
                GC.Collect();
                AmbientCancellationTokenSource timeoutSource1 = new AmbientCancellationTokenSource(TimeSpan.FromMilliseconds(103));
                GC.Collect();
                AmbientCancellationTokenSource timeoutSource2 = new AmbientCancellationTokenSource(TimeSpan.FromMilliseconds(1151));
                GC.Collect();
                await Task.Delay(250);
                GC.Collect();
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsFalse(timeoutSource1.IsCancellationRequested);
                Assert.IsFalse(timeoutSource2.IsCancellationRequested);
                GC.Collect();
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(257));
                GC.Collect();
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsTrue(timeoutSource1.IsCancellationRequested);          // somehow this is failing, but only when run with other tests--I can't see how that could happen--everything should be local!
                Assert.IsFalse(timeoutSource2.IsCancellationRequested);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerAutoRestartAndEnabled()
        {
            using (AmbientClock.Pause())
            using (AmbientEventTimer timer = new AmbientEventTimer(1))
            {
                timer.AutoReset = true;
                Assert.IsTrue(timer.AutoReset);
                timer.Enabled = true;
                Assert.IsTrue(timer.Enabled);
                timer.AutoReset = false;
                Assert.IsFalse(timer.AutoReset);
                timer.Enabled = false;
                Assert.IsFalse(timer.Enabled);
                timer.AutoReset = true;
                Assert.IsTrue(timer.AutoReset);
                timer.Enabled = true;
                Assert.IsTrue(timer.Enabled);
                timer.AutoReset = false;
                Assert.IsFalse(timer.AutoReset);
                timer.Enabled = false;
                Assert.IsFalse(timer.Enabled);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerSystem()
        {
            bool elapsed = false;
            bool disposed = false;
            using (AmbientEventTimer timer = new AmbientEventTimer())
            {
                timer.AutoReset = true;
                timer.Interval = 10;
                timer.Enabled = true;
                // hopefully we can get the timer to elapse now (without a subscriber)
                System.Threading.Thread.Sleep(100);
                // add a subscriber
                System.Timers.ElapsedEventHandler elapsedHandler = (s, e) => { elapsed = true; };
                EventHandler disposedHandler = (s, e) => { disposed = true; };
                timer.Elapsed += elapsedHandler;
                timer.Disposed += disposedHandler;
                // now try to get it to elapse again (this time with a subscriber)
                System.Threading.Thread.Sleep(100);
                Assert.IsTrue(elapsed);
                Assert.IsFalse(disposed);
                timer.Elapsed -= elapsedHandler;
                timer.Disposed -= disposedHandler;
                // we needed to test the Disposed event removal, but we need to get notified of disposal
                timer.Disposed += disposedHandler;
            }
            Assert.IsTrue(disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerAmbientClock()
        {
            using (AmbientClock.Pause())
            {
                bool elapsed = false;
                bool disposed = false;
                using (AmbientEventTimer timer = new AmbientEventTimer())
                {
                    timer.Elapsed += (s, e) => { elapsed = true; };
                    timer.Disposed += (s, e) => { disposed = true; };
                    timer.Interval = 100;
                    timer.Enabled = true;
                    Assert.IsFalse(elapsed);
                    AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                    Assert.IsTrue(elapsed);
                    Assert.IsFalse(disposed);
                }
                Assert.IsTrue(disposed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerAmbientClockNoSubscriber()
        {
            using (AmbientClock.Pause())
            {
                bool disposed = false;
                using (AmbientEventTimer timer = new AmbientEventTimer())
                {
                    timer.Disposed += (s, e) => { disposed = true; };
                    timer.Interval = 100;
                    timer.Enabled = true;
                    AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                    Assert.IsFalse(disposed);
                }
                Assert.IsTrue(disposed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerDisposeBeforeRaised()
        {
            using (AmbientClock.Pause())
            {
                using (AmbientEventTimer timer = new AmbientEventTimer(TimeSpan.FromMilliseconds(Int32.MaxValue))) { }
            }
            using (AmbientEventTimer timer = new AmbientEventTimer(TimeSpan.FromMilliseconds(Int32.MaxValue))) { }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerExplicitClockAutoStartAutoReset()
        {
            AmbientClock.PausedAmbientClock clock = new AmbientClock.PausedAmbientClock();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(clock, TimeSpan.FromMilliseconds(100)))
            {
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Enabled = true;
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(2, elapsed);
                Assert.AreEqual(0, disposed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(3, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerAmbientClockStartPaused()
        {
            using (AmbientClock.Pause())
            {
                bool elapsed = false;
                bool disposed = false;
                using (AmbientEventTimer timer = new AmbientEventTimer(TimeSpan.FromMilliseconds(100)))
                {
                    Assert.IsFalse(timer.AutoReset);
                    Assert.AreEqual(100.0, timer.Interval);
                    Assert.IsFalse(timer.Enabled);
                    timer.Enabled = true;
                    timer.Elapsed += (s, e) => { elapsed = true; };
                    timer.Disposed += (s, e) => { disposed = true; };
                    Assert.IsFalse(elapsed);
                    AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                    Assert.IsTrue(elapsed);
                    Assert.IsFalse(disposed);
                }
                Assert.IsTrue(disposed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerExplicitClock()
        {
            AmbientClock.PausedAmbientClock clock = new AmbientClock.PausedAmbientClock();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(clock))
            {
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, s); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, s); };
                timer.Enabled = true;
                timer.Interval = 100;
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpanExtensions.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void EventTimerExplicitClockStartStop()
        {
            AmbientClock.PausedAmbientClock clock = new AmbientClock.PausedAmbientClock();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(clock))
            {
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, s); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, s); };
                timer.Interval = 100;
                timer.AutoReset = true;
                timer.Start();
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpanExtensions.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
                timer.Stop();
                clock.SkipAhead(TimeSpanExtensions.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task EventTimerSystemClock()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(null))
            {
                SemaphoreSlim ss = new SemaphoreSlim(0);
                Assert.IsTrue(timer.AutoReset);
                Assert.IsFalse(timer.Enabled);
                timer.AutoReset = false;
                timer.Elapsed += (s, e) => { ++elapsed; ss.Release(); };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Enabled = true;
                timer.Interval = 100;
                Assert.AreEqual(100.0, timer.Interval);
                Assert.AreEqual(0, elapsed);
                // wait up to 5 seconds to get raised (it should have happened 50 times by then, so if it doesn't there must be a bug, or the CPU must be horribly overloaded)
                await ss.WaitAsync(5000);
                Assert.AreEqual(1, elapsed);    // the event should *never* get raised more than once because AutoReset is false
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task EventTimerSystemClockStartStop()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(null))
            {
                SemaphoreSlim ss = new SemaphoreSlim(0);
                Assert.IsFalse(timer.Enabled);
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; ss.Release(); };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Interval = 100;
                timer.Start();
                Assert.AreEqual(100.0, timer.Interval);
                Assert.AreEqual(0, elapsed);
                // wait up to 5 seconds to get raised (it should have happened 50 times by then, so if it doesn't there must be a bug, or the CPU must be horribly overloaded)
                await ss.WaitAsync(5000);
                Assert.IsTrue(elapsed >= 1);    // this could be more than one occasionally if the event gets raised again after being released and before we can stop it here
                Assert.AreEqual(0, disposed);
                timer.Stop();   // after this, the event should *not* be raised again, though a notification may already be in progress, so this could fail on rare occasions
                int postStopElapsed = elapsed;
                await Task.Delay(300);
                Assert.AreEqual(postStopElapsed, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task EventTimerSystemClockAutoStart()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new AmbientEventTimer(null, TimeSpan.FromMilliseconds(1000)))
            {
                Assert.IsTrue(timer.AutoReset);
                Assert.IsFalse(timer.Enabled);
                Assert.AreEqual(1000, timer.Interval);
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, s); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, s); };
                Assert.AreEqual(0, elapsed);
                timer.AutoReset = true;
                timer.Enabled = true;
                await Task.Delay(3750);
                Assert.IsTrue(elapsed >= 1 && elapsed <= 4, elapsed.ToString());        // this *should* be three if we have processing power available, but it can be less than 3 when it is not (as is often the case during unit tests), and might be slightly higher if this continuation doesn't get scheduled right away, thus we are tolerant
                Assert.AreEqual(0, disposed);           // this assertion failed once, but is very intermittent, not sure how this is possible
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CallbackTimerBasic()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                Assert.AreEqual(0, invocations);
                timer.Change(100U, 77U);
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(77));
                Assert.AreEqual(2, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(154));
                Assert.AreEqual(4, invocations);
                timer.Change(33L, (long)Timeout.Infinite);
                Assert.AreEqual(4, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(33));
                Assert.AreEqual(5, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(99));
                Assert.AreEqual(5, invocations);
                timer.Change(Timeout.Infinite, 27);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(27));
                Assert.AreEqual(5, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(27));
                Assert.AreEqual(5, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(27));
                Assert.AreEqual(5, invocations);
                timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(5, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(5, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(5, invocations);
            }
            object testState = new object();
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 13, 47))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(2, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(3, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(4, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 13U, 47U))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(2, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(3, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(4, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 13L, 47L))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(2, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(3, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(4, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, TimeSpan.FromMilliseconds(13), TimeSpan.FromMilliseconds(47)))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(2, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(3, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(4, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 13, Timeout.Infinite))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(1, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(1, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, Timeout.Infinite, Timeout.Infinite))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
            }
            invocations = 0;
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, Timeout.Infinite, 47))
            {
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CallbackTimerDisposeBeforeRaised()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            {
                using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback)) { }
            }
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback)) { }
            Assert.AreEqual(0, invocations);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CallbackTimerDisposeWithWait()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                AmbientCallbackTimer timer = new AmbientCallbackTimer(callback);
                timer.Dispose();
                Assert.IsFalse(timer.Dispose(mre));

                mre = new ManualResetEvent(false);
                timer = new AmbientCallbackTimer(callback, null, 100, 100);
                Assert.IsTrue(timer.Dispose(mre));
                Assert.IsTrue(mre.WaitOne(0));
            }
        }
#if NET5_0
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CallbackTimerActiveCount()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, null, 10000, 10000)) // this assumes the test will finish in 10 seconds
            {
                Assert.IsTrue(AmbientCallbackTimer.ActiveCount > 0);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public async Task CallbackTimerAsyncDispose()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            await using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, null, 100, 100))
            {
            }
        }
#endif
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CallbackTimerArgumentExceptions()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientClock.Pause())
            {
                Assert.ThrowsException<ArgumentNullException>(() => { new AmbientCallbackTimer(null!); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, -2, 1); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, 1, -2); });
                using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback)) 
                {
                    Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(-2, 1); });
                    Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(1, -2); });
                }
            }
        }
        /*
         * 
         * 
         * 
         * Note that system callback timers and threadpool waits are *very* dependent on scheduling, concurrency, and available CPU
         * So getting called back reliably is impossible (and this is a *big* reason that ambient callbacks are better for testing)
         * As a result, I've commented out most of the asserts that count the number of callbacks when they're unreliable
         * We'll leave them in to show what is expected in an ideal scenario (plenty of CPU, test running in isolation)
         * 
         * 
         * 
         * */
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public
#if NET5_0
            async Task 
#else
            void
#endif
            SystemCallbackTimerTimeoutInfiniteInitialConstructor()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
#if NET5_0
            await
#endif
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, Timeout.Infinite, 47))
            {
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerTimeoutInfiniteTimeSpanInitialChange()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                timer.Change(Timeout.Infinite, 270);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);                            // these should be reliable because the callback should *never* be invoked even once
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInifiniteTimeoutInitialChange()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerTimeoutInfiniteInitialTimeoutInfiniteSubsequent()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, Timeout.Infinite, Timeout.Infinite))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);        // these should be okay because we should *never* get called back
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInfiniteLongSubsequentChange()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                timer.Change(330L, (long)Timeout.Infinite);
                Assert.AreEqual(0, invocations);
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(500));
                Assert.IsTrue(invocations <= 1, invocations.ToString());        // this test is somewhat more reliable than the test above because the callback should only ever be invoked once at most
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(990));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerTimeoutInfiniteSubsequentConstructor()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 370, Timeout.Infinite))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(560));
                Assert.IsTrue(invocations <= 1, invocations.ToString());        // these should be okay because we shouldn't ever be called more than once anyway
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInitialAndRepeatingAfterConstruction()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                Assert.AreEqual(0, invocations);
                timer.Change(1000U, 770U);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(1500));
//                Assert.IsTrue(invocations == 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(650));
//                Assert.IsTrue(invocations == 2, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInitialAndRepeatingOnConstruction()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 370, 530))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(560));
//                Assert.IsTrue(invocations == 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 2, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 3, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInitialAndRepeatingOnConstructionUnsigned()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 370U, 530U))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(560));
//                Assert.IsTrue(invocations == 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 2, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 3, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInitialAndRepeatingOnConstructionLong()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, 370L, 530L))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(560));
//                Assert.IsTrue(invocations == 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 2, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 3, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerInitialAndRepeatingOnConstructionTimeSpan()
        {
            Assert.IsTrue(AmbientClock.IsSystemClock);
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            object testState = new object();
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback, testState, TimeSpan.FromMilliseconds(370), TimeSpan.FromMilliseconds(530)))
            {
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(560));
//                Assert.IsTrue(invocations == 1, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 2, invocations.ToString());
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(530));
//                Assert.IsTrue(invocations == 3, invocations.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerDisposeWithWait()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            ManualResetEvent mre = new ManualResetEvent(false);
            AmbientCallbackTimer timer = new AmbientCallbackTimer(callback);
            timer.Dispose();
            Assert.IsFalse(timer.Dispose(mre));

            mre = new ManualResetEvent(false);
            timer = new AmbientCallbackTimer(callback, null, 100, 100);
            Assert.IsTrue(timer.Dispose(mre));
            Assert.IsTrue(mre.WaitOne(0));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemCallbackTimerArgumentExceptions()
        {
            int invocations = 0;
            TimerCallback callback = o => { ++invocations; };
            Assert.ThrowsException<ArgumentNullException>(() => { new AmbientCallbackTimer(null!); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, -2, 1); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, 1, -2); });
            using (AmbientCallbackTimer timer = new AmbientCallbackTimer(callback))
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(-2, 1); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(1, -2); });
                IAmbientClockTimeChangedNotificationSink sink = timer;
                Assert.ThrowsException<InvalidOperationException>(() => { sink.TimeChanged(null!, long.MinValue, long.MaxValue, DateTime.MinValue, DateTime.MaxValue); });
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void UnsafeRegisterWaitForSingleObject()
        {
            using (AmbientClock.Pause())
            {
                AutoResetEvent are = new AutoResetEvent(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, 1000L, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(2, timedOutInvocations);
                ManualResetEvent mre = new ManualResetEvent(false);
                Assert.IsTrue(registered.Unregister(mre));
                Assert.IsFalse(registered.Unregister(mre));
                Assert.IsTrue(mre.WaitOne(0));

                AmbientRegisteredWaitHandle wh = new AmbientRegisteredWaitHandle(false, mre, (s,b) => { }, null, 10000, true);
                wh.Unregister(mre);

                are = new AutoResetEvent(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, Timeout.Infinite, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10000000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10000000));
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Assert.IsTrue(registered.Unregister(null));
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SafeRegisterWaitForSingleObject()
        {
            using (AmbientClock.Pause())
            {
                AutoResetEvent are = new AutoResetEvent(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, 1000L, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(2, timedOutInvocations);
                ManualResetEvent mre = new ManualResetEvent(false);
                Assert.IsTrue(registered.Unregister(mre));
                Assert.IsFalse(registered.Unregister(mre));
                Assert.IsTrue(mre.WaitOne(0));


                are = new AutoResetEvent(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, Timeout.Infinite, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10000000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.IsTrue(signaledInvocations >= 0 && signaledInvocations <= 1, signaledInvocations.ToString());
                Assert.AreEqual(0, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10000000));
                Assert.AreEqual(2, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Assert.IsTrue(registered.Unregister(null));
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemUnsafeRegisterWaitForSingleObject()
        {
            AutoResetEvent are = new AutoResetEvent(false);
            int signaledInvocations = 0;
            int timedOutInvocations = 0;
            WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
            AmbientRegisteredWaitHandle registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, 500, false);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(0, timedOutInvocations);
            System.Threading.Thread.Sleep(750);
            Assert.AreEqual(0, signaledInvocations);
//            Assert.IsTrue(timedOutInvocations == 1);
            are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
            System.Threading.Thread.Sleep(400);
//            Assert.IsTrue(signaledInvocations == 1, signaledInvocations.ToString());
//            Assert.IsTrue(timedOutInvocations == 1);  // since the signal occured, a timeout should *not* have in the subsequent 400ms
            registered.Unregister(are);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SystemSafeRegisterWaitForSingleObject()
        {
            AutoResetEvent are = new AutoResetEvent(false);
            int signaledInvocations = 0;
            int timedOutInvocations = 0;
            WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
            AmbientRegisteredWaitHandle registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, 500, false);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(0, timedOutInvocations);
            System.Threading.Thread.Sleep(750);
            Assert.AreEqual(0, signaledInvocations);
//            Assert.IsTrue(timedOutInvocations >= 0);
            are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
            System.Threading.Thread.Sleep(400);
            Assert.AreEqual(1, signaledInvocations);
//            Assert.IsTrue(timedOutInvocations == 1); // since the signal occured, a timeout should *not* have in the subsequent 400ms
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void UnsafeRegisterWaitForSingleObjectOneShot()
        {
            using (AmbientClock.Pause())
            {
                AutoResetEvent are = new AutoResetEvent(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, 1000U, true);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should *not* signal us again because this was a one-shot
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // normally this would have been when we got signaled, but since this is a one-shot, we should *not* get signaled
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                Assert.IsFalse(registered.Unregister(null));    // returns false because it should have already been unregistered when the first signal occurred



                // now do it again, but have the one-shot be the wait handle signal instead of the timeout
                are = new AutoResetEvent(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, TimeSpan.FromMilliseconds(1000), true);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should *not* signal us again because this was a one-shot
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset, but this is a one-shot, so we should *not* be signaled
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // even after the reset, this would have been when we got signaled, but this was a one-shot, so we should *not* get signaled again
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Assert.IsFalse(registered.Unregister(null));    // returns false because it should have already been unregistered when the first signal occurred
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void SafeRegisterWaitForSingleObjectOneShot()
        {
            using (AmbientClock.Pause())
            {
                AutoResetEvent are = new AutoResetEvent(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, 1000U, true);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should *not* signal us again because this was a one-shot
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // normally this would have been when we got signaled, but since this is a one-shot, we should *not* get signaled
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(1, timedOutInvocations);
                Assert.IsFalse(registered.Unregister(null));    // returns false because it should have already been unregistered when the first signal occurred



                // now do it again, but have the one-shot be the wait handle signal instead of the timeout
                are = new AutoResetEvent(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, TimeSpan.FromMilliseconds(1000), true);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.IsTrue(signaledInvocations >= 0 && signaledInvocations <= 1, signaledInvocations.ToString());
                Assert.AreEqual(0, timedOutInvocations);
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should *not* signal us again because this was a one-shot
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset, but this is a one-shot, so we should *not* be signaled
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // even after the reset, this would have been when we got signaled, but this was a one-shot, so we should *not* get signaled again
                Assert.AreEqual(1, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Assert.IsFalse(registered.Unregister(null));    // returns false because it should have already been unregistered when the first signal occurred
            }
        }
    }
}
