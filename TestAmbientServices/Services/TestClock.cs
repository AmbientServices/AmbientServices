using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientClockProvider"/>.
    /// </summary>
    [TestClass]
    public class TestClock
    {
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void PausedClock()
        {
            DateTime now = DateTime.UtcNow;
            AmbientClock.PausedAmbientClockProvider clock = new AmbientClock.PausedAmbientClockProvider();
            AmbientClockProviderTimeChangedEventArgs eventArgs = null;
            clock.OnTimeChanged += (s,e) => { Assert.AreEqual(s, clock); eventArgs = e; };
            clock.SkipAhead(10000);
            Assert.IsNotNull(eventArgs);
            Assert.AreEqual(clock, eventArgs.Clock);
            Assert.AreEqual(0, eventArgs.OldTicks);
            Assert.AreEqual(10000, eventArgs.NewTicks);
            Assert.AreEqual(clock, eventArgs.Clock);
            Assert.IsTrue(eventArgs.NewUtcDateTime > eventArgs.OldUtcDateTime);
        }

        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task Clock()
        {
            long startTicks = AmbientClock.Ticks;
            TimeSpan startElapsed = AmbientClock.Elapsed;
            DateTime start = DateTime.UtcNow;
            await Task.Delay(100);
            long testTicks = AmbientClock.Ticks;
            TimeSpan testElapsed = AmbientClock.Elapsed;
            DateTime test = AmbientClock.UtcNow;
            await Task.Delay(100);
            DateTime end = DateTime.UtcNow;
            long endTicks = AmbientClock.Ticks;
            TimeSpan endElapsed = AmbientClock.Elapsed;
            Assert.IsTrue(start <= test && test <= end);
            Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks);
            Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task ClockPaused()
        {
            using (AmbientClock.Pause())
            {
                long startTicks = AmbientClock.Ticks;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                DateTime start = DateTime.UtcNow;
                await Task.Delay(100);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                long testTicks = AmbientClock.Ticks;
                TimeSpan testElapsed = AmbientClock.Elapsed;
                DateTime test = AmbientClock.UtcNow;
                await Task.Delay(100);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                DateTime end = DateTime.UtcNow;
                long endTicks = AmbientClock.Ticks;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.IsTrue(start <= test && test <= end);
                Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks);
                Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task ClockSystem()
        {
            using (new LocalServiceScopedOverride<IAmbientClockProvider>(null))
            {
                long startTicks = AmbientClock.Ticks;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                DateTime start = DateTime.UtcNow;
                await Task.Delay(100);
                long testTicks = AmbientClock.Ticks;
                TimeSpan testElapsed = AmbientClock.Elapsed;
                DateTime test = AmbientClock.UtcNow;
                await Task.Delay(100);
                DateTime end = DateTime.UtcNow;
                long endTicks = AmbientClock.Ticks;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.IsTrue(start <= test && test <= end);
                Assert.IsTrue(startTicks <= testTicks && testTicks <= endTicks);
                Assert.IsTrue(startElapsed <= testElapsed && testElapsed <= endElapsed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task Pause()
        {
            using (AmbientClock.Pause())
            {
                DateTime start = AmbientClock.UtcNow;
                TimeSpan startElapsed = AmbientClock.Elapsed;
                await Task.Delay(100);
                DateTime end = AmbientClock.UtcNow;
                TimeSpan endElapsed = AmbientClock.Elapsed;
                Assert.AreEqual(endElapsed, startElapsed);
                Assert.AreEqual(end, start);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                end = AmbientClock.UtcNow;
                endElapsed = AmbientClock.Elapsed;
                Assert.AreEqual(endElapsed, startElapsed.Add(TimeSpan.FromMilliseconds(100)));
                Assert.AreEqual(end, start.AddMilliseconds(100));
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task Stopwatch()
        {
            using (AmbientClock.Pause())
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch();
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.TicksElapsed == 0);
                stopwatch = new AmbientStopwatch(true);
                TimeSpan delay = TimeSpan.FromMilliseconds(100);
                AmbientClock.SkipAhead(delay);
                Assert.AreEqual(delay, stopwatch.Elapsed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchSystem()
        {
            using (new LocalServiceScopedOverride<IAmbientClockProvider>(null))
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch(false);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.TicksElapsed == 0);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                Assert.IsTrue(stopwatch.TicksElapsed == 0);
                stopwatch = new AmbientStopwatch(true);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds > 0);
                Assert.IsTrue(stopwatch.TicksElapsed > 0);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchPauseResumeReset()
        {
            using (AmbientClock.Pause())
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch();
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Resume();
                stopwatch.Resume();
                AmbientClock.SkipAhead(TimeSpan.FromTicks(100 * TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency));
                Assert.AreEqual(100, stopwatch.Elapsed.Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                AmbientClock.SkipAhead(TimeSpan.FromTicks(100 * TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency));
                Assert.AreEqual(200, stopwatch.Elapsed.Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                stopwatch.Pause();
                stopwatch.Pause();
                AmbientClock.SkipAhead(TimeSpan.FromTicks(100 * TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency));
                Assert.AreEqual(200, stopwatch.Elapsed.Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task StopwatchSystemPauseResumeReset()
        {
            using (new LocalServiceScopedOverride<IAmbientClockProvider>(null))
            {
                AmbientStopwatch stopwatch = new AmbientStopwatch(false);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds == 0);
                stopwatch.Resume();
                stopwatch.Resume();
                await Task.Delay(100);
                Assert.IsTrue(stopwatch.TicksElapsed > 0);
                stopwatch.Pause();
                stopwatch.Pause();
                long elapsed = stopwatch.TicksElapsed;
                await Task.Delay(100);
                Assert.AreEqual(elapsed, stopwatch.TicksElapsed);
                stopwatch.Resume();
                stopwatch.Reset();
                Assert.IsTrue(stopwatch.Elapsed.Ticks < 1000);  // this could fail if there is lots of CPU contention
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void TimerAmbientClock()
        {
            using (AmbientClock.Pause())
            {
                bool elapsed = false;
                bool disposed = false;
                using (AmbientTimer timer = new AmbientTimer())
                {
                    timer.Elapsed += (s, e) => { elapsed = true; };
                    timer.Disposed += (s, e) => { disposed = true; };
                    timer.Period = TimeSpan.FromMilliseconds(100);
                    timer.Enabled = true;
                    Assert.IsFalse(elapsed);
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                    Assert.IsTrue(elapsed);
                    Assert.IsFalse(disposed);
                }
                Assert.IsTrue(disposed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void TimerExplicitClockAutoStartAutoReset()
        {
            AmbientClock.PausedAmbientClockProvider clock = new AmbientClock.PausedAmbientClockProvider();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(clock, TimeSpan.FromMilliseconds(100)))
            {
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Enabled = true;
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(2, elapsed);
                Assert.AreEqual(0, disposed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(3, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void TimerAmbientClockAutoStart()
        {
            using (AmbientClock.Pause())
            {
                bool elapsed = false;
                bool disposed = false;
                using (AmbientTimer timer = new AmbientTimer(TimeSpan.FromMilliseconds(100)))
                {
                    Assert.IsFalse(timer.AutoReset);
                    Assert.AreEqual(TimeSpan.FromMilliseconds(100), timer.Period);
                    Assert.IsTrue(timer.Enabled);
                    timer.Elapsed += (s, e) => { elapsed = true; };
                    timer.Disposed += (s, e) => { disposed = true; };
                    Assert.IsFalse(elapsed);
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                    Assert.IsTrue(elapsed);
                    Assert.IsFalse(disposed);
                }
                Assert.IsTrue(disposed);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void TimerExplicitClock()
        {
            AmbientClock.PausedAmbientClockProvider clock = new AmbientClock.PausedAmbientClockProvider();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(clock))
            {
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, e.Timer); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, e.Timer); };
                timer.Enabled = true;
                timer.Period = TimeSpan.FromMilliseconds(100);
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public void TimerExplicitClockStartStop()
        {
            AmbientClock.PausedAmbientClockProvider clock = new AmbientClock.PausedAmbientClockProvider();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(clock))
            {
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, e.Timer); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, e.Timer); };
                timer.Period = TimeSpan.FromMilliseconds(100);
                timer.AutoReset = true;
                timer.Start();
                Assert.AreEqual(0, elapsed);
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
                timer.Stop();
                clock.SkipAhead(TimeSpan.FromMilliseconds(100).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond);
                Assert.AreEqual(1, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task TimerSystemClock()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(null))
            {
                SemaphoreSlim ss = new SemaphoreSlim(0);
                Assert.IsFalse(timer.AutoReset);
                Assert.IsFalse(timer.Enabled);
                timer.Elapsed += (s, e) => { ++elapsed; ss.Release(); };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Enabled = true;
                timer.Period = TimeSpan.FromMilliseconds(100);
                Assert.AreEqual(TimeSpan.FromMilliseconds(100), timer.Period);
                Assert.AreEqual(0, elapsed);
                // wait up to 5 seconds to get triggered (it should have happened 50 times by then, so if it doesn't there must be a bug, or the CPU must be horribly overloaded)
                await ss.WaitAsync(5000);
                Assert.AreEqual(1, elapsed);    // the event should *never* get triggered more than once because AutoReset is false
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task TimerSystemClockStartStop()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(null))
            {
                SemaphoreSlim ss = new SemaphoreSlim(0);
                Assert.IsFalse(timer.Enabled);
                timer.AutoReset = true;
                timer.Elapsed += (s, e) => { ++elapsed; ss.Release(); };
                timer.Disposed += (s, e) => { ++disposed; };
                timer.Period = TimeSpan.FromMilliseconds(100);
                timer.Start();
                Assert.AreEqual(TimeSpan.FromMilliseconds(100), timer.Period);
                Assert.AreEqual(0, elapsed);
                // wait up to 5 seconds to get triggered (it should have happened 50 times by then, so if it doesn't there must be a bug, or the CPU must be horribly overloaded)
                await ss.WaitAsync(5000);
                Assert.IsTrue(elapsed >= 1);    // this could be more than one occasionally if the event gets triggered again after being released and before we can stop it here
                Assert.AreEqual(0, disposed);
                timer.Stop();   // after this, the event should *not* trigger again, though a trigger may already be in progress, so this could fail on rare occasions
                int postStopElapsed = elapsed;
                await Task.Delay(300);
                Assert.AreEqual(postStopElapsed, elapsed);
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task TimerSystemClockAutoStart()
        {
            int elapsed = 0;
            int disposed = 0;
            using (AmbientTimer timer = new AmbientTimer(null, TimeSpan.FromMilliseconds(100)))
            {
                Assert.IsFalse(timer.AutoReset);
                Assert.IsTrue(timer.Enabled);
                timer.AutoReset = true;
                Assert.AreEqual(TimeSpan.FromMilliseconds(100), timer.Period);
                timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, e.Timer); };
                timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, e.Timer); };
                Assert.AreEqual(0, elapsed);
                await Task.Delay(375);
                Assert.IsTrue(elapsed == 2 || elapsed == 3, elapsed.ToString());        // this is sometimes two when the tests run slowly
                Assert.AreEqual(0, disposed);
            }
            Assert.AreEqual(1, disposed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task CancellationTokenSource()
        {
            AmbientCancellationTokenSource noTimeoutSource = AmbientClock.CreateCancellationTokenSource();
            AmbientCancellationTokenSource timeoutSource = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Task.Delay(250);
            Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
            Assert.IsTrue(timeoutSource.IsCancellationRequested);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClockProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task PausedCancellationTokenSource()
        {
            using (IDisposable dispose = AmbientClock.Pause())
            {
                AmbientCancellationTokenSource noTimeoutSource = AmbientClock.CreateCancellationTokenSource();
                GC.Collect();
                AmbientCancellationTokenSource timeoutSource1 = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(103));
                GC.Collect();
                AmbientCancellationTokenSource timeoutSource2 = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(1151));
                GC.Collect();
                await Task.Delay(250);
                GC.Collect();
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsFalse(timeoutSource1.IsCancellationRequested);
                Assert.IsFalse(timeoutSource2.IsCancellationRequested);
                GC.Collect();
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(257));
                GC.Collect();
                Assert.IsFalse(noTimeoutSource.IsCancellationRequested);
                Assert.IsTrue(timeoutSource1.IsCancellationRequested);          // somehow this is failing, but only when run with other tests--I can't see how that could happen--everything should be local!
                Assert.IsFalse(timeoutSource2.IsCancellationRequested);
            }
        }
    }
}
