using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for <see cref="IAmbientClock"/>.
/// </summary>
[TestClass]
public class TestClock
{
    class AmbientPausedClockTimeChangeListener : IAmbientClockTimeChangedNotificationSink, IDisposable
    {
        readonly AmbientClock.PausedAmbientClock _pausedClock;
        readonly Action<IAmbientClock, long, long, DateTime, DateTime> _action;
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
        AmbientClock.PausedAmbientClock clock = new();
        long baselineTicks = clock.Ticks;
        IAmbientClock pausedClock = null;
        long oldTicks = 0;
        long newTicks = 0;
        DateTime oldUtcDateTime = DateTime.MinValue;
        DateTime newUtcDateTime = DateTime.MinValue;
        using AmbientPausedClockTimeChangeListener listener = new(clock, (c, ot, nt, od, nd) => { pausedClock = c; oldTicks = ot; newTicks = nt; oldUtcDateTime = od; newUtcDateTime = nd; });
        clock.SkipAhead(10000);
        Assert.IsNotNull(pausedClock);
        Assert.AreEqual(clock, pausedClock);
        Assert.AreEqual(baselineTicks, oldTicks);
        Assert.AreEqual(baselineTicks + 10000, newTicks);
        Assert.AreEqual(clock, pausedClock);
        Assert.IsTrue(newUtcDateTime > oldUtcDateTime);
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
        await AmbientClock.TaskDelay(100, default);
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
            await Task.Delay(100, default);
            await AmbientClock.TaskDelay(100, default);
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
            AmbientStopwatch stopwatch = new(true);
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
            AmbientStopwatch stopwatch = new(false);
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
            await AmbientClock.TaskDelay(TimeSpan.FromMilliseconds(100), default);
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
            AmbientStopwatch stopwatch = new(false);
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
            using AmbientCancellationTokenSource noTimeoutSource = new();
            using AmbientCancellationTokenSource timeoutSource1 = new(100);
            using AmbientCancellationTokenSource timeoutSource2 = new(TimeSpan.FromMilliseconds(100));
            using AmbientCancellationTokenSource timeoutSource3 = new();
            using AmbientCancellationTokenSource timeoutSource4 = new();
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
            using CancellationTokenSource systemSource = new();
            using AmbientCancellationTokenSource noTimeoutSource = AmbientClock.CreateCancellationTokenSource();
            using AmbientCancellationTokenSource timeoutSource1 = AmbientClock.CreateCancellationTokenSource(systemSource);
            using AmbientCancellationTokenSource timeoutSource2 = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(100));
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
            using AmbientCancellationTokenSource timeoutSource = new(100);
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
        int firstFailure = -1;
         for (int attempt = 0; attempt < 10; ++attempt)
        {
            firstFailure = -1;
            using AmbientCancellationTokenSource noTimeoutSource = new();
            using AmbientCancellationTokenSource timeoutSource1 = new(100);
            using AmbientCancellationTokenSource timeoutSource2 = new(TimeSpan.FromMilliseconds(100));
            using AmbientCancellationTokenSource timeoutSource3 = new();
            using AmbientCancellationTokenSource timeoutSource4 = new();
            timeoutSource3.CancelAfter(100);
            timeoutSource4.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
             if (noTimeoutSource.IsCancellationRequested)
            {
                firstFailure = 1;
                continue;
            }
             if (!timeoutSource1.IsCancellationRequested)
            {
                firstFailure = 2;
                continue;
            }
             if (!timeoutSource2.IsCancellationRequested)
            {
                firstFailure = 3;
                continue;
            }
             if (!timeoutSource3.IsCancellationRequested)
            {
                firstFailure = 4;
                continue;
            }
             if (!timeoutSource4.IsCancellationRequested)
            {
                firstFailure = 5;
                continue;
            }
            noTimeoutSource.Cancel();
             if (!noTimeoutSource.IsCancellationRequested)
            {
                firstFailure = 6;
                continue;
            }
            break;
        }
        Assert.AreEqual(-1, firstFailure);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public async Task PausedCancellationTokenSource()
    {
        using IDisposable dispose = AmbientClock.Pause();
        using AmbientCancellationTokenSource noTimeoutSource = new();
        GC.Collect();
        using AmbientCancellationTokenSource timeoutSource1 = new(TimeSpan.FromMilliseconds(103));
        GC.Collect();
        using AmbientCancellationTokenSource timeoutSource2 = new(TimeSpan.FromMilliseconds(1151));
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
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void EventTimerAutoRestartAndEnabled()
    {
        using (AmbientClock.Pause())
        using (AmbientEventTimer timer = new(1))
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
        StringBuilder errorInfo = new();
        // this test is testing against system objects which are timing-dependent to make sure they behave the same way the ambient versions do
        // so they fail occasionally, so we'll retry several times to be sure they're really misbehaving
        for (int attempt = 0; attempt < 5; ++attempt)
        {
            errorInfo.Clear();

            bool elapsed = false;
            bool disposed = false;
            using (AmbientEventTimer timer = new())
            {
                timer.AutoReset = true;
                timer.Interval = 10;
                timer.Enabled = true;
                // hopefully we can get the timer to elapse now (without a subscriber)
                Thread.Sleep(100);
                // add a subscriber
                void elapsedHandler(object s, System.Timers.ElapsedEventArgs e) { elapsed = true; }
                void disposedHandler(object s, EventArgs e) { disposed = true; }
                timer.Elapsed += elapsedHandler;
                timer.Disposed += disposedHandler;
                // now try to get it to elapse again (this time with a subscriber)
                Thread.Sleep(100);
                if (!elapsed) errorInfo.AppendLine($"elapsed was false after waiting for ten intervals (100ms)!");
                if (disposed) errorInfo.AppendLine($"dispose was set prematurely!");
                timer.Elapsed -= elapsedHandler;
                timer.Disposed -= disposedHandler;
                // we needed to test the Disposed event removal (above), but we need to get notified of disposal to test that the handler gets called, so re-add it
                timer.Disposed += disposedHandler;
            }
            if (!disposed) errorInfo.AppendLine($"dispose handler was not called!");
            Assert.IsTrue(disposed);

            if (errorInfo.Length == 0) break;
            // wait a random time to break any kind of resonant failure
            Thread.Sleep(Pseudorandom.Next.NextInt32Ranged(1000));
        }
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
            using (AmbientEventTimer timer = new())
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
            using (AmbientEventTimer timer = new())
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
            using AmbientEventTimer timer = new(TimeSpan.FromMilliseconds(Int32.MaxValue));
        }
        using (AmbientEventTimer timer = new(TimeSpan.FromMilliseconds(Int32.MaxValue))) { }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void EventTimerExplicitClockAutoStartAutoReset()
    {
        AmbientClock.PausedAmbientClock clock = new();
        int elapsed = 0;
        int disposed = 0;
        using (AmbientEventTimer timer = new(clock, TimeSpan.FromMilliseconds(100)))
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
            using (AmbientEventTimer timer = new(TimeSpan.FromMilliseconds(100)))
            {
                Assert.IsTrue(timer.AutoReset);
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
    public void EventTimerDefaults()
    {
        using System.Timers.Timer systemTimer = new(100.0);
        using AmbientEventTimer ambientTimer = new(100.0);
        Assert.AreEqual(systemTimer.Enabled, ambientTimer.Enabled);
        Assert.AreEqual(systemTimer.AutoReset, ambientTimer.AutoReset);
        Assert.AreEqual(systemTimer.Interval, ambientTimer.Interval);
        Assert.AreEqual(systemTimer.Container, ambientTimer.Container);
        Assert.AreEqual(systemTimer.Site, ambientTimer.Site);
        Assert.AreEqual(systemTimer.SynchronizingObject, ambientTimer.SynchronizingObject);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void EventTimerExplicitClock()
    {
        AmbientClock.PausedAmbientClock clock = new();
        int elapsed = 0;
        int disposed = 0;
        using (AmbientEventTimer timer = new(clock))
        {
            timer.AutoReset = true;
            timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, s); };
            timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, s); };
            timer.Enabled = true;
            timer.Interval = 100;
            Assert.AreEqual(0, elapsed);
            clock.SkipAhead(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
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
        AmbientClock.PausedAmbientClock clock = new();
        int elapsed = 0;
        int disposed = 0;
        using (AmbientEventTimer timer = new(clock))
        {
            timer.Elapsed += (s, e) => { ++elapsed; Assert.AreEqual(timer, s); };
            timer.Disposed += (s, e) => { ++disposed; Assert.AreEqual(timer, s); };
            timer.Interval = 100;
            timer.AutoReset = true;
            timer.Start();
            Assert.AreEqual(0, elapsed);
            clock.SkipAhead(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
            Assert.AreEqual(1, elapsed);
            Assert.AreEqual(0, disposed);
            timer.Stop();
            clock.SkipAhead(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(100).Ticks));
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
        using (AmbientEventTimer timer = new(null))
        {
            using SemaphoreSlim ss = new(0);
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
        using (AmbientEventTimer timer = new(null))
        {
            using SemaphoreSlim ss = new(0);
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
        StringBuilder errorInfo = new();
        // this test is testing against system objects which are timing-dependent to make sure they behave the same way the ambient versions do
        // so they fail occasionally, so we'll retry several times to be sure they're really misbehaving
        for (int attempt = 0; attempt < 5; ++attempt)
        {
            errorInfo.Clear();
            int elapsed = 0;
            int disposed = 0;
            using (AmbientEventTimer timer = new(null, TimeSpan.FromMilliseconds(1000)))
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
                if (elapsed != 3) errorInfo.AppendLine($"elapsed is {elapsed} but should be 3!");
                Assert.AreEqual(0, disposed);           // this assertion failed once, but is very intermittent, not sure how this is possible
            }
            Assert.AreEqual(1, disposed);
            if (errorInfo.Length == 0) break;
            // wait a random time to break any kind of resonant failure
            Thread.Sleep(Pseudorandom.Next.NextInt32Ranged(1000));
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void CallbackTimerBasic()
    {
        int invocations = 0;
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        using (AmbientCallbackTimer timer = new(callback))
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
            timer.Change(33L, Timeout.Infinite);
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
        object testState = new();
        invocations = 0;
        using (AmbientClock.Pause())
        using (AmbientCallbackTimer timer = new(callback, testState, 13, 47))
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
        using (AmbientCallbackTimer timer = new(callback, testState, 13U, 47U))
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
        using (AmbientCallbackTimer timer = new(callback, testState, 13L, 47L))
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
        using (AmbientCallbackTimer timer = new(callback, testState, TimeSpan.FromMilliseconds(13), TimeSpan.FromMilliseconds(47)))
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
        using (AmbientCallbackTimer timer = new(callback, testState, 13, Timeout.Infinite))
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
        using (AmbientCallbackTimer timer = new(callback, testState, Timeout.Infinite, Timeout.Infinite))
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
        using (AmbientCallbackTimer timer = new(callback, testState, Timeout.Infinite, 47))
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
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        {
            using AmbientCallbackTimer timer = new(callback);
        }
        using (AmbientCallbackTimer timer = new(callback)) { }
        Assert.AreEqual(0, invocations);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void CallbackTimerDisposeWithWait()
    {
        int invocations = 0;
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        {
            using ManualResetEvent mre = new(false);
            using AmbientCallbackTimer timer = new(callback);
            timer.Dispose();
            Assert.IsFalse(timer.Dispose(mre));

            using ManualResetEvent mre2 = new(false);
            using AmbientCallbackTimer timer2 = new(callback, null, 100, 100);
            Assert.IsTrue(timer2.Dispose(mre2));
            Assert.IsTrue(mre2.WaitOne(0));
        }
    }
#if NET5_0_OR_GREATER
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void CallbackTimerActiveCount()
    {
        int invocations = 0;
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        {
            StringBuilder errorInfo = new();
            // this test is testing against system objects which are timing-dependent to make sure they behave the same way the ambient versions do
            // so they fail occasionally, so we'll retry several times to be sure they're really misbehaving
            for (int attempt = 0; attempt < 5; ++attempt)
            {
                errorInfo.Clear();
                using (AmbientCallbackTimer timer = new(callback, null, 10000, 10000)) // this assumes the test will finish in 10 seconds
                {
                    if (AmbientCallbackTimer.ActiveCount <= 0) errorInfo.AppendLine($"AmbientCallbackTimer.ActiveCount is {AmbientCallbackTimer.ActiveCount} but should be at least one!");
                }
                if (errorInfo.Length == 0) break;
                // wait a random time to break any kind of resonant failure
                Thread.Sleep(Pseudorandom.Next.NextInt32Ranged(1000));
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public async Task CallbackTimerAsyncDispose()
    {
        int invocations = 0;
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        await using (AmbientCallbackTimer timer = new(callback, null, 100, 100))
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
        void callback(object o) { ++invocations; }
        using (AmbientClock.Pause())
        {
            Assert.ThrowsException<ArgumentNullException>(() => { new AmbientCallbackTimer(null!); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, -2, 1); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, 1, -2); });
            using AmbientCallbackTimer timer = new(callback);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(-2, 1); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(1, -2); });
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
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
#if NET5_0
        await
#endif
                using AmbientCallbackTimer timer = new(callback, testState, Timeout.Infinite, 47);
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerTimeoutInfiniteTimeSpanInitialChange()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                using AmbientCallbackTimer timer = new(callback);
                timer.Change(Timeout.Infinite, 270);
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);                            // these should be reliable because the callback should *never* be invoked even once
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInifiniteTimeoutInitialChange()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                using AmbientCallbackTimer timer = new(callback);
                timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(0, invocations);
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerTimeoutInfiniteInitialTimeoutInfiniteSubsequent()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, Timeout.Infinite, Timeout.Infinite);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(13));
                Assert.AreEqual(0, invocations);        // these should be okay because we should *never* get called back
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(47));
                Assert.AreEqual(0, invocations);
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInfiniteLongSubsequentChange()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                using AmbientCallbackTimer timer = new(callback);
                timer.Change(330L, Timeout.Infinite);
                Assert.AreEqual(0, invocations);
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                Assert.IsTrue(invocations <= 1, invocations.ToString());        // this test is somewhat more reliable than the test above because the callback should only ever be invoked once at most
                Thread.Sleep(TimeSpan.FromMilliseconds(990));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerTimeoutInfiniteSubsequentConstructor()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, 370, Timeout.Infinite);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(560));
                Assert.IsTrue(invocations <= 1, invocations.ToString());        // these should be okay because we shouldn't ever be called more than once anyway
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                Assert.IsTrue(invocations <= 1, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInitialAndRepeatingAfterConstruction()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                using AmbientCallbackTimer timer = new(callback);
                Assert.AreEqual(0, invocations);
                timer.Change(1000U, 770U);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(1500));
                //                Assert.IsTrue(invocations == 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(650));
                //                Assert.IsTrue(invocations == 2, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInitialAndRepeatingOnConstruction()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, 370, 530);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(560));
                //                Assert.IsTrue(invocations == 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 2, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 3, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInitialAndRepeatingOnConstructionUnsigned()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, 370U, 530U);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(560));
                //                Assert.IsTrue(invocations == 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 2, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 3, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInitialAndRepeatingOnConstructionLong()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, 370L, 530L);
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(560));
                //                Assert.IsTrue(invocations == 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 2, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 3, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerInitialAndRepeatingOnConstructionTimeSpan()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                Assert.IsTrue(AmbientClock.IsSystemClock);
                int invocations = 0;
                void callback(object o) { ++invocations; }
                object testState = new();
                using AmbientCallbackTimer timer = new(callback, testState, TimeSpan.FromMilliseconds(370), TimeSpan.FromMilliseconds(530));
                Assert.AreEqual(0, invocations);    // this could theoretically fail, but only if the system paused for more than one second after the previous line, but then scheduled the callback before resuming here
                                                    // if it becomes unreliable, we shall comment out this assert
                Thread.Sleep(TimeSpan.FromMilliseconds(560));
                //                Assert.IsTrue(invocations == 1, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 2, invocations.ToString());
                Thread.Sleep(TimeSpan.FromMilliseconds(530));
                //                Assert.IsTrue(invocations == 3, invocations.ToString());
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
    public void SystemCallbackTimerDisposeWithWait()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                int invocations = 0;
                void callback(object o) { ++invocations; }
                using ManualResetEvent mre = new(false);
                using AmbientCallbackTimer timer = new(callback);
                timer.Dispose();
                Assert.IsFalse(timer.Dispose(mre));

                using ManualResetEvent mre2 = new(false);
                using AmbientCallbackTimer timer2 = new(callback, null, 100, 100);
                Assert.IsTrue(timer2.Dispose(mre2));
                Assert.IsTrue(mre2.WaitOne(0));
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemCallbackTimerArgumentExceptions()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                int invocations = 0;
                void callback(object o) { ++invocations; }
                Assert.ThrowsException<ArgumentNullException>(() => { new AmbientCallbackTimer(null!); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, -2, 1); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { new AmbientCallbackTimer(callback, null, 1, -2); });
                using AmbientCallbackTimer timer = new(callback);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(-2, 1); });
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => { timer.Change(1, -2); });
                IAmbientClockTimeChangedNotificationSink sink = timer;
                Assert.ThrowsException<InvalidOperationException>(() => { sink.TimeChanged(null!, long.MinValue, long.MaxValue, DateTime.MinValue, DateTime.MaxValue); });
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
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
            StringBuilder errorInfo = new();
            // this test is testing against system objects which are timing-dependent to make sure they behave the same way the ambient versions do
            // so they fail occasionally, so we'll retry several times to be sure they're really misbehaving
            for (int attempt = 0; attempt < 5; ++attempt)
            {
                errorInfo.Clear();
                using AutoResetEvent are = new(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, 1000L, false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when no time has passed!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 0 when no time has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when 1000ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1000ms has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 1 when 1500ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1500ms has passed!");
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 500ms!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting and 500ms!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 1000ms!");
                if (2 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 2 after resetting and setting and 1000ms!");
                using ManualResetEvent mre = new(false);
                Assert.IsTrue(registered.Unregister(mre));
                Assert.IsFalse(registered.Unregister(mre));
                if (!mre.WaitOne(0)) errorInfo.AppendLine($"mre.WaitOne(0) should have indicated that a signal was received!!");

                AmbientRegisteredWaitHandle wh = new(false, mre, (s, b) => { }, null, 10000, true);
                wh.Unregister(mre);

                using AutoResetEvent are2 = new AutoResetEvent(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are2, callback, null, Timeout.Infinite, false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 0 after no time!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after no time!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10_000_000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 0 after 10s!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s!");
                are2.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 1 after 10s and Set!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s and Set!");
                are2.Reset();    // now that we've been signaled, we can reset the event
                are2.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 2 after 10s and Set,Reset,Set!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s and Set,Reset,Set!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10_000_000));
                if (2 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 2 after 10s,Set,Reset,Set, and 10s!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s,Set,Reset,Set, and 10s!");
                Assert.IsTrue(registered.Unregister(null));

                using AutoResetEvent are3 = new(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are3, callback, null, (uint)1000, false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when no time has passed!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 0 when no time has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when 1000ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1000ms has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are3.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 1 when 1500ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1500ms has passed!");
                are3.Reset();    // now that we've been signaled, we can reset the event
                are3.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 500ms!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting and 500ms!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 1000ms!");
                if (2 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 2 after resetting and setting and 1000ms!");
                using ManualResetEvent mre3 = new(false);
                Assert.IsTrue(registered.Unregister(mre3));
                Assert.IsFalse(registered.Unregister(mre3));
                if (!mre3.WaitOne(0)) errorInfo.AppendLine($"mre3.WaitOne(0) should have indicated that a signal was received!!");

                using AutoResetEvent are4 = new(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are4, callback, null, TimeSpan.FromSeconds(1), false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when no time has passed!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 0 when no time has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 when 1000ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1000ms has passed!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are4.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 1 when 1500ms has passed!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 when 1500ms has passed!");
                are4.Reset();    // now that we've been signaled, we can reset the event
                are4.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 500ms!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after resetting and setting and 500ms!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after resetting and setting and 1000ms!");
                if (2 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 2 after resetting and setting and 1000ms!");
                using ManualResetEvent mre4 = new(false);
                Assert.IsTrue(registered.Unregister(mre4));
                Assert.IsFalse(registered.Unregister(mre4));
                if (!mre4.WaitOne(0)) errorInfo.AppendLine($"mre4.WaitOne(0) should have indicated that a signal was received!!");

                // wait a random time to break any kind of resonant failure
                Thread.Sleep(Pseudorandom.Next.NextInt32Ranged(1000));

                if (errorInfo.Length == 0) break;
            }
            Assert.IsTrue(errorInfo.Length == 0, errorInfo.ToString());
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
            StringBuilder errorInfo = new();
            // this test is testing against system objects which are timing-dependent to make sure they behave the same way the ambient versions do
            // so they fail occasionally, so we'll retry several times to be sure they're really misbehaving
            for (int attempt = 0; attempt < 5; ++attempt)
            {
                errorInfo.Clear();
                using AutoResetEvent are = new(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                WaitOrTimerCallback callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, 1000L, false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 after no time!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 0 after no time!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(1000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 0 after 1000ms!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after 1000ms!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 1 after 1000ms,Set!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after 1000ms,Set!");
                are.Reset();    // now that we've been signaled, we can reset the event
                are.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after 1000ms,Set,Reset,Set!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after 1000ms,Set,Reset,Set!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500)); // this would have been when we got timed out again, but since we got signaled in the middle, the period should have been reset
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after 1000ms,Set,Reset,Set,500ms!");
                if (1 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 1 after 1000ms,Set,Reset,Set,500ms!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
                if (2 != signaledInvocations) errorInfo.AppendLine($"signaledInvocations is {signaledInvocations} but should be 2 after 1000ms,Set,Reset,Set,1000ms!");
                if (2 != timedOutInvocations) errorInfo.AppendLine($"timedOutInvocations is {timedOutInvocations} but should be 2 after 1000ms,Set,Reset,Set,1000ms!");
                using ManualResetEvent mre = new(false);
                Assert.IsTrue(registered.Unregister(mre));
                Assert.IsFalse(registered.Unregister(mre));
                if (!mre.WaitOne(0)) errorInfo.AppendLine($"mre.WaitOne(0) should have indicated that a signal was received!!");


                using AutoResetEvent are2 = new(false);
                signaledInvocations = 0;
                timedOutInvocations = 0;
                callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
                registered = AmbientThreadPool.RegisterWaitForSingleObject(are2, callback, null, Timeout.Infinite, false);
                if (0 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 0 after no time!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after no time!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10_000_000));
                if (0 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 0 after 10s!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s!");
                are2.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (1 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 1 after 10s,Set!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s,Set!");
                are2.Reset();    // now that we've been signaled, we can reset the event
                are2.Set();  // this should signal us again even though no virtual time has passed, but we need to sleep again, which will hopefully cause the signaler thread to run
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
                if (2 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 2 after 10s,Set,Reset,Set!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s,Set,Reset,Set!");
                AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(10_00_0000));
                if (2 != signaledInvocations) errorInfo.AppendLine($"Part 2: signaledInvocations is {signaledInvocations} but should be 2 after 10s,Set,Reset,Set,10s!");
                if (0 != timedOutInvocations) errorInfo.AppendLine($"Part 2: timedOutInvocations is {timedOutInvocations} but should be 0 after 10s,Set,Reset,Set,10s!");
                Assert.IsTrue(registered.Unregister(null));
                if (errorInfo.Length == 0) break;
                // wait a random time to break any kind of resonant failure
                Thread.Sleep(Pseudorandom.Next.NextInt32Ranged(1000));
            }
            Assert.IsTrue(errorInfo.Length == 0, errorInfo.ToString());
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemUnsafeRegisterWaitForSingleObject()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                using AutoResetEvent are = new(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                void callback(object state, bool timedOut) { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; }
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are, callback, null, 500, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Thread.Sleep(750);
                Assert.AreEqual(0, signaledInvocations);
                //            Assert.IsTrue(timedOutInvocations == 1);
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(400);
                //            Assert.IsTrue(signaledInvocations == 1, signaledInvocations.ToString());
                //            Assert.IsTrue(timedOutInvocations == 1);  // since the signal occured, a timeout should *not* have in the subsequent 400ms
                registered.Unregister(are);
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
#if NET6_0_OR_GREATER   // only do this in the code coverage framework version because it's flaky and running it more than once is just more likely to cause the build to fail

    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SystemSafeRegisterWaitForSingleObject()
    {
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                using AutoResetEvent are = new(false);
                int signaledInvocations = 0;
                int timedOutInvocations = 0;
                void callback(object state, bool timedOut) { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; }
                AmbientRegisteredWaitHandle registered = AmbientThreadPool.RegisterWaitForSingleObject(are, callback, null, 500, false);
                Assert.AreEqual(0, signaledInvocations);
                Assert.AreEqual(0, timedOutInvocations);
                Thread.Sleep(750);
                Assert.AreEqual(0, signaledInvocations);
                //            Assert.IsTrue(timedOutInvocations >= 0);
                are.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
                Thread.Sleep(400);
                Assert.AreEqual(1, signaledInvocations);
                //            Assert.IsTrue(timedOutInvocations == 1); // since the signal occured, a timeout should *not* have in the subsequent 400ms
                // if we get here, we succeeded
                break;
            }
            catch (Exception)
            {
                if (attempt > 10) throw;
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void UnsafeRegisterWaitForSingleObjectOneShot()
    {
        using (AmbientClock.Pause())
        {
            using AutoResetEvent are = new(false);
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
            for (int loops = 0; (signaledInvocations != 0 || timedOutInvocations != 1) && loops < 100; ++loops) Thread.Sleep(100);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(1, timedOutInvocations);
            are.Reset();    // now that we've been signaled, we can reset the event
            are.Set();  // this should *not* signal us again because this was a one-shot
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
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
            using AutoResetEvent are2 = new(false);
            signaledInvocations = 0;
            timedOutInvocations = 0;
            callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
            registered = AmbientThreadPool.UnsafeRegisterWaitForSingleObject(are2, callback, null, TimeSpan.FromMilliseconds(1000), true);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(0, timedOutInvocations);
            AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
            are2.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
            for (int loops = 0; (signaledInvocations != 1 || timedOutInvocations != 0) && loops < 100; ++loops) Thread.Sleep(100);
            Assert.AreEqual(1, signaledInvocations);
            Assert.AreEqual(0, timedOutInvocations);
            are2.Reset();    // now that we've been signaled, we can reset the event
            are2.Set();  // this should *not* signal us again because this was a one-shot
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
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
#endif
    /// <summary>
    /// Performs tests on <see cref="IAmbientClock"/>.
    /// </summary>
    [TestMethod]
    public void SafeRegisterWaitForSingleObjectOneShot()
    {
        using (AmbientClock.Pause())
        {
            using AutoResetEvent are = new(false);
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
            for (int loops = 0; (signaledInvocations != 0 || timedOutInvocations != 1) && loops < 100; ++loops) Thread.Sleep(100);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(1, timedOutInvocations);
            are.Reset();    // now that we've been signaled, we can reset the event
            are.Set();  // this should *not* signal us again because this was a one-shot
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
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
            using AutoResetEvent are2 = new(false);
            signaledInvocations = 0;
            timedOutInvocations = 0;
            callback = (state, timedOut) => { if (timedOut) ++timedOutInvocations; else ++signaledInvocations; };
            registered = AmbientThreadPool.RegisterWaitForSingleObject(are2, callback, null, TimeSpan.FromMilliseconds(1000), true);
            Assert.AreEqual(0, signaledInvocations);
            Assert.AreEqual(0, timedOutInvocations);
            AmbientClock.ThreadSleep(TimeSpan.FromMilliseconds(500));
            are2.Set();  // this should signal us ONCE but probably asynchronously, and we can't control when an asynchronous signal happens so we'll sleep several times in the hope that one of them will cause the signaling thread to execute
            for (int loops = 0; signaledInvocations != 1 && loops < 100; ++loops) Thread.Sleep(100);
            Assert.IsTrue(signaledInvocations >= 0 && signaledInvocations <= 1, signaledInvocations.ToString());
            Assert.AreEqual(0, timedOutInvocations);
            are2.Reset();    // now that we've been signaled, we can reset the event
            are2.Set();  // this should *not* signal us again because this was a one-shot
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
            Thread.Sleep(100);
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
