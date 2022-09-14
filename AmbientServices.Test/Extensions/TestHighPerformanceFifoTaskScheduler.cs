using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace AmbientServices.Test
{
    [TestClass]
    public class TestHighPerformanceFifoTaskScheduler
    {
        private static readonly AmbientService<IAmbientLogger> LoggerBackend = Ambient.GetService<IAmbientLogger>();
        private static readonly AmbientService<IAmbientStatistics> StatisticsBackend = Ambient.GetService<IAmbientStatistics>();

        [TestMethod]
        public void StartNew()
        {
            //using IDisposable d = LoggerBackend.ScopedLocalOverride(new AmbientTraceLogger());
            List<Task> tasks = new();
            for (int i = 0; i < 1000; ++i)
            {
                FakeWork w = new(i, true);
                tasks.Add(HighPerformanceFifoTaskFactory.Default.StartNew(() => w.DoWorkAsync(CancellationToken.None).AsTask()));
            }
            Task.WaitAll(tasks.ToArray());
            HighPerformanceFifoTaskScheduler.Default.Reset();
        }
        [TestMethod]
        public void StartNewNoStats()
        {
            using IDisposable d = StatisticsBackend.ScopedLocalOverride(null);
            using HighPerformanceFifoTaskScheduler scheduler = HighPerformanceFifoTaskScheduler.Start(nameof(StartNewNoStats), ThreadPriority.Highest);
            HighPerformanceFifoTaskFactory testFactory = new(scheduler);
            List<Task> tasks = new();
            for (int i = 0; i < 100; ++i)
            {
                FakeWork w = new(i, true);
                tasks.Add(testFactory.StartNew(() => w.DoWorkAsync(CancellationToken.None).AsTask()));
            }
            Task.WaitAll(tasks.ToArray());
            scheduler.Invoke(() => { });
            scheduler.Reset();
        }
        [TestMethod]
        public void ExecuteWithCatchAndLog()
        {
            using HighPerformanceFifoTaskScheduler scheduler = HighPerformanceFifoTaskScheduler.Start(nameof(StartNewNoStats));
            scheduler.ExecuteWithCatchAndLog(() => throw new ExpectedException());
            scheduler.ExecuteWithCatchAndLog(() => throw new TaskCanceledException());
            //scheduler.ExecuteWithCatchAndLog(() => throw new ThreadAbortException()); // can't construct this, so can't test it
        }
        [TestMethod]
        public void Constructors()
        {
            HighPerformanceFifoTaskFactory testFactory;
            testFactory = new(CancellationToken.None);
            testFactory = new(HighPerformanceFifoTaskScheduler.Default);
            testFactory = new(TaskCreationOptions.None, TaskContinuationOptions.None);
        }
        [TestMethod]
        public void RunInHighPerformanceFifoSynchronizationContext()
        {
            //using IDisposable d = LoggerBackend.ScopedLocalOverride(new AmbientTraceLogger());
            List<Task> tasks = new();
            for (int i = 0; i < 250; ++i)
            {
                FakeWork w = new(i, true);
                tasks.Add(RunInHPContext(() => w.DoWorkAsync(CancellationToken.None)));
            }
            Task.WaitAll(tasks.ToArray());
            HighPerformanceFifoTaskScheduler.Default.Reset();
        }
        [TestMethod, ExpectedException(typeof(ExpectedException))]
        public async Task StartNewException()
        {
            //using IDisposable d = LoggerBackend.ScopedLocalOverride(new AmbientTraceLogger());
            await HighPerformanceFifoTaskFactory.Default.StartNew(() => ThrowExpectedException());
        }
        [TestMethod]
        public void DisposedException()
        {
            HighPerformanceFifoTaskFactory test;
            using (HighPerformanceFifoTaskScheduler testScheduler = HighPerformanceFifoTaskScheduler.Start(nameof(DisposedException)))
            {
                test = new(testScheduler);
            }
            Assert.ThrowsException<TaskSchedulerException>(() => test.StartNew(() => { }));     // our code throws an ObjectDisposedException but TaskScheduler converts it
        }
        private static int UnobservedExceptions;
        [TestMethod]
        public async Task UnobservedTaskException()
        {
            //using IDisposable d = LoggerBackend.ScopedLocalOverride(new AmbientTraceLogger());
            try
            {
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                _ = HighPerformanceFifoTaskFactory.Default.StartNew(() => ThrowExpectedException());
                for (int loop = 0; loop < 100; ++loop)
                {
                    if (UnobservedExceptions > 0) break;
                    await Task.Delay(100);
                }
                Assert.AreEqual(1, UnobservedExceptions);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Assert.AreEqual(typeof(AggregateException), e.Exception?.GetType());
            Assert.AreEqual(typeof(ExpectedException), e.Exception?.InnerExceptions[0].GetType());
            Interlocked.Increment(ref UnobservedExceptions);
        }

        private static void ThrowExpectedException()
        {
            throw new ExpectedException();
        }
        private static async Task RunInHPContext(Func<ValueTask> f)
        {
            System.Threading.SynchronizationContext? oldContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(HighPerformanceFifoSynchronizationContext.Default);
                await f();
            }
            catch (AggregateException ex)
            {
                Async.ConvertAggregateException(ex);
                throw;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }
        [TestMethod]
        public void InvokeException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => HighPerformanceFifoTaskScheduler.Default.Invoke(null!));
        }
        [TestMethod]
        public void SendAndPostExceptions()
        {
            Assert.ThrowsException<ArgumentNullException>(() => HighPerformanceFifoSynchronizationContext.Default.Post(null!, null));
            Assert.ThrowsException<ArgumentNullException>(() => HighPerformanceFifoSynchronizationContext.Default.Send(null!, null));
        }
        [TestMethod]
        public void CreateCopy()
        {
            SynchronizationContext copy = HighPerformanceFifoSynchronizationContext.Default.CreateCopy();
            Assert.IsNotNull(copy);
            Assert.IsInstanceOfType(copy, typeof(HighPerformanceFifoSynchronizationContext));
        }
        [TestMethod]
        public async Task Send()
        {
            bool called = false;
            HighPerformanceFifoSynchronizationContext.Default.Send(state =>
            {
                Assert.IsNull(state);
                called = true;
            }, null);
            for (int loop = 0; loop < 100; ++loop)
            {
                if (called) break;
                await Task.Delay(100);
            }
            Assert.IsTrue(called);
        }
        [TestMethod]
        public void Properties()
        {
            Assert.IsTrue(HighPerformanceFifoTaskScheduler.Default.Workers >= 0);
            Assert.IsTrue(HighPerformanceFifoTaskScheduler.Default.BusyWorkers >= 0);
            Assert.IsTrue(HighPerformanceFifoTaskScheduler.Default.MaximumConcurrencyLevel >= Environment.ProcessorCount);
        }
        [TestMethod]
        public void IntrusiveSinglyLinkedList()
        {
            InterlockedSinglyLinkedList<NodeTest> list1 = new();
            InterlockedSinglyLinkedList<NodeTest> list2 = new();
            NodeTest node = new() { Value = 1 };
            list1.Push(node);
            Assert.ThrowsException<ArgumentNullException>(() => list2.Push(null!));
            Assert.ThrowsException<InvalidOperationException>(() => list2.Push(node));
            list1.Validate();
            Assert.AreEqual(1, list1.Count);
            list1.Clear();
            Assert.AreEqual(0, list1.Count);
            list2.Push(node);
            NodeTest? popped = list2.Pop();
            Assert.AreEqual(node, popped);
        }
        class NodeTest : IntrusiveSinglyLinkedListNode
        {
            public int Value { get; set; }
        }
        [TestMethod]
        public void Worker()
        {
            using HighPerformanceFifoTaskScheduler scheduler = HighPerformanceFifoTaskScheduler.Start(nameof(Worker));
            HighPerformanceFifoWorker worker = HighPerformanceFifoWorker.Start(scheduler, "1", ThreadPriority.Normal);  // the worker disposes of itself
            worker.Invoke(LongWait);
            Assert.IsTrue(worker.IsBusy);
            Assert.ThrowsException<InvalidOperationException>(() => worker.Invoke(LongWait));
            Assert.IsFalse(HighPerformanceFifoWorker.IsWorkerInternalMethod(null));
            Assert.IsFalse(HighPerformanceFifoWorker.IsWorkerInternalMethod(typeof(TestHighPerformanceFifoTaskScheduler).GetMethod(nameof(Worker))));
            Assert.IsTrue(HighPerformanceFifoWorker.IsWorkerInternalMethod(typeof(HighPerformanceFifoWorker).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)));
            Assert.IsTrue(HighPerformanceFifoWorker.IsWorkerInternalMethod(typeof(HighPerformanceFifoWorker).GetMethod("WorkerFunc", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)));
            worker.Stop();
            Assert.ThrowsException<InvalidOperationException>(() => worker.Invoke(null!));
        }
        public void LongWait()
        {
            Thread.Sleep(5000);
        }
        [TestMethod]
        public void WorkerNullWork()
        {
            using HighPerformanceFifoTaskScheduler scheduler = HighPerformanceFifoTaskScheduler.Start(nameof(WorkerNullWork));
            HighPerformanceFifoWorker worker = scheduler.CreateWorker();    // the worker disposes of itself
            worker.Invoke(null!);
        }
    }
    public class FakeWork
    {
        private readonly bool _fast;
        private readonly long _id;

        public FakeWork(long id, bool fast)
        {
            _fast = fast;
            _id = id;
        }

        public async ValueTask DoWorkAsync(CancellationToken cancel = default)
        {
            ulong hash = GetHash(_id);
            await Task.Yield();
            //string? threadName = Thread.CurrentThread.Name;

            Assert.AreEqual(typeof(HighPerformanceFifoSynchronizationContext), SynchronizationContext.Current?.GetType());
            Assert.ThrowsException<ArgumentNullException>(() => HighPerformanceFifoTaskScheduler.IsIdleHighPerformanceFifoTaskShcedulerThread(null!));
            Assert.IsFalse(HighPerformanceFifoTaskScheduler.IsIdleHighPerformanceFifoTaskShcedulerThread(new StackTrace()));
            Stopwatch s = Stopwatch.StartNew();
            for (int outer = 0; outer < (int)(hash % 256) && !cancel.IsCancellationRequested; ++outer)
            {
                Stopwatch cpu = Stopwatch.StartNew();
                // use some CPU
                for (int spin = 0; spin < (int)((hash >> 6) % (_fast ? 16UL : 256UL)); ++spin)
                {
                    double d1 = 0.0000000000000001;
                    double d2 = 0.0000000000000001;
                    for (int inner = 0; inner < (_fast ? 100 : 1000000); ++inner) { d2 = d1 * d2; }
                }
                cpu.Stop();
                Assert.AreEqual(typeof(HighPerformanceFifoSynchronizationContext), SynchronizationContext.Current?.GetType());
                Stopwatch mem = Stopwatch.StartNew();
                // use some memory
                int bytesPerLoop = (int)((hash >> 12) % (_fast ? 10UL : 1024UL));
                int loops = (int)((hash >> 22) % 1024);
                for (int memory = 0; memory < loops; ++memory)
                {
                    byte[] bytes = new byte[bytesPerLoop];
                }
                mem.Stop();
                Assert.AreEqual(typeof(HighPerformanceFifoSynchronizationContext), SynchronizationContext.Current?.GetType());
                Stopwatch io = Stopwatch.StartNew();
                // simulate I/O by blocking
                await Task.Delay((int)((hash >> 32) % (_fast ? 5UL : 500UL)), cancel);
                io.Stop();
                Assert.AreEqual(typeof(HighPerformanceFifoSynchronizationContext), SynchronizationContext.Current?.GetType());
            }
            Assert.AreEqual(typeof(HighPerformanceFifoSynchronizationContext), SynchronizationContext.Current?.GetType());
            //Debug.WriteLine($"Ran work {_id} on {threadName}!", "Work");
        }
        private static ulong GetHash(long id)
        {
            unchecked
            {
                ulong x = (ulong)id * 1_111_111_111_111_111_111UL;        // note that this is a prime number (but not a mersenne prime)
                x = (((x & 0xaaaaaaaaaaaaaaaa) >> 1) | ((x & 0x5555555555555555) << 1));
                x = (((x & 0xcccccccccccccccc) >> 2) | ((x & 0x3333333333333333) << 2));
                x = (((x & 0xf0f0f0f0f0f0f0f0) >> 4) | ((x & 0x0f0f0f0f0f0f0f0f) << 4));
                x = (((x & 0xff00ff00ff00ff00) >> 8) | ((x & 0x00ff00ff00ff00ff) << 8));
                x = (((x & 0xffff0000ffff0000) >> 16) | ((x & 0x0000ffff0000ffff) << 16));
                return ((x >> 32) | (x << 32));
            }
        }

    }
}
