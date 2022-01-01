﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestSynchronous
    {
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_Basic()
        {
            Assert.AreNotEqual(Async.MultithreadedContext, Async.SinglethreadedContext);
            int result = Async.Synchronize(() =>
            {
                Task<int> task = Async_BasicAsync();
                return task;
            });
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_AlreadyRun()
        {
            Async.Synchronize(() => Task.CompletedTask);
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_NotAlreadyRun()
        {
            Async.Synchronize(() => new Task(() => { }));
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ExpectedException))]
        public void Async_AggregateExceptionUnwrap()
        {
            Async.Synchronize((Func<Task>)(() => throw new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionUnwrap)))));
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ExpectedException))]
        public void Async_AggregateExceptionUnwrapWithTask()
        {
            Async.Synchronize((Func<Task>)(async () => { await Task.Delay(10); throw new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionUnwrapWithTask))); }));
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ExpectedException))]
        public void Async_AggregateExceptionUnwrapWithReturn()
        {
            Assert.AreNotEqual(Async.MultithreadedContext, Async.SinglethreadedContext);
            int result = Async.Synchronize(async () =>
            {
                await Task.Delay(10);
                return await Async_BasicAsyncThrow(new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionUnwrapWithReturn))));
            });
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(AggregateException))]
        public void Async_AggregateExceptionCantUnwrap()
        {
            Async.Synchronize((Func<Task>)(() => throw new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionCantUnwrap)), new ExpectedException(nameof(Async_AggregateExceptionCantUnwrap)))));
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(AggregateException))]
        public void Async_AggregateExceptionCantUnwrapWithTask()
        {
            Async.Synchronize((Func<Task>)(async () => { await Task.Delay(10); throw new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionCantUnwrapWithTask)), new ExpectedException(nameof(Async_AggregateExceptionCantUnwrapWithTask))); }));
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod, ExpectedException(typeof(AggregateException))]
        public void Async_AggregateExceptionCantUnwrapWithReturn()
        {
            Assert.AreNotEqual(Async.MultithreadedContext, Async.SinglethreadedContext);
            int result = Async.Synchronize(async () =>
            {
                await Task.Delay(10);
                return await Async_BasicAsyncThrow(new AggregateException(new ExpectedException(nameof(Async_AggregateExceptionUnwrapWithReturn)), new ExpectedException(nameof(Async_AggregateExceptionUnwrapWithReturn))));
            });
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_SynchronizeVariations()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(60);
            Async.Synchronize(() =>
            {
                Task task = Async_BasicAsyncPart2();
                return task;
            });
            Async.Synchronize(() => Async_BasicAsyncPart2(default));
            int result = Async.Synchronize(Async_BasicAsyncPart3);
            result = Async.Synchronize(() => Async_BasicAsyncPart4(default));
        }

        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_Misc()
        {
            // try to create a copy
            System.Threading.SynchronizationContext context = SynchronousSynchronizationContext.Default.CreateCopy();
            // try to send a delegate (should execute synchronously)
            int set = 0;
            SynchronousSynchronizationContext.Default.Send(new SendOrPostCallback(o =>
            {
                set = 1;
            }),
                this);
            Assert.AreEqual(1, set);

            Assert.AreEqual(1, SynchronousTaskScheduler.Default.MaximumConcurrencyLevel);
        }

        private Task<int> Async_BasicAsyncThrow(Exception ex)
        {
            throw ex;
        }

        private async Task<int> Async_BasicAsync()
        {
            await Async_BasicAsyncPart1();
            await Async_BasicAsyncPart2();
            await Async_BasicAsyncWithLock();
            int result = await Async_BasicAsyncPart3();
            return result;
        }

        private async Task Async_BasicAsyncPart1()
        {
            await Task.Delay(200);
            await Task.Delay(50);
        }

        private async Task Async_BasicAsyncPart2(CancellationToken cancel = default)
        {
            System.Threading.Thread.Sleep(200);
            System.Threading.Thread.Sleep(50);
            await Task.Yield();
        }
        private async Task<int> Async_BasicAsyncPart3()
        {
            await Task.Delay(50);
            return 1378902;
        }
        private async Task<int> Async_BasicAsyncPart4(CancellationToken cancel = default)
        {
            await Task.Delay(50, cancel);
            return 1378902;
        }

        private async Task Async_BasicAsyncWithLock(CancellationToken cancel = default)
        {
            using (SemaphoreSlim lock2 = new SemaphoreSlim(1))
            {
                await lock2.WaitAsync(1000, cancel);
                try
                {
                    await Task.Delay(50);
                    await Task.Yield();
                }
                finally
                {
                    lock2.Release();
                }
            }
        }

#if NETSTANDARD2_1 || NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_Enumerable()
        {
            int count = 0;
            foreach (int i in Async.AsyncEnumerableToEnumerable(() => TestAsyncEnum(10, 100)))
            {
                ++count;
            }
            Assert.AreEqual(100, count);
        }
        private async IAsyncEnumerable<int> TestAsyncEnum(int delayMilliseconds, int limit, [EnumeratorCancellation] CancellationToken cancel = default)
        {
            for (int loop = 0; loop < limit; ++loop)
            {
                await Task.Delay(delayMilliseconds);
                yield return loop;
            }
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_Synchronize()
        {
            Assert.AreNotEqual(Async.MultithreadedContext, Async.SinglethreadedContext);
            Async.Synchronize(AsyncTest);
        }
        private async Task AsyncTest()
        {
            await Task.Delay(10);
        }
#endif


        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_SyncInAsyncInSyncVoid()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                Async.Synchronize(() => Loop1(mainThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        private async Task Loop1(int mainThreadId)
        {
            Assert.IsTrue(Async.UsingSynchronousExecution);
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                await Async.ForceSync(() => Loop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                await Async.ForceAsync(() => Loop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);    // ForceAsync, unlike a native await, keeps us on the same thread even though the action runs on another thread
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                await Loop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);    // this native await on a non-specially-created function triggers real async execution and thus a thread switch, also resulting in a switch to a non-synchronous execution context
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.IsFalse(Async.UsingSynchronousExecution);
                Assert.AreNotEqual(mainThreadId, threadId);
            }
        }
        private async Task Loop2(int mainThreadId, int mainThreadId2)
        {
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(threadId, mainThreadId2);
            for (int count = 0; count < 3; ++count)
            {
                await Async.Run(() => Task.Delay(10));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
            for (int count = 0; count < 3; ++count)
            {
                await Task.Delay(10);
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
        }


        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_SyncInAsyncInSyncWithReturn()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                int result = Async.Synchronize(() => LoopWithReturn1(mainThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        private async Task<int> LoopWithReturn1(int mainThreadId)
        {
            Assert.IsTrue(Async.UsingSynchronousExecution);
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.ForceSync(() => LoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.ForceAsync(() => LoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);    // ForceAsync, unlike a native await, keeps us on the same thread even though the action runs on another thread
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                int result = await LoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);    // this native await on a non-specially-created function triggers real async execution and thus a thread switch, also resulting in a switch to a non-synchronous execution context
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.IsFalse(Async.UsingSynchronousExecution);
                Assert.AreNotEqual(mainThreadId, threadId);
            }
            return 0;
        }
        private async Task<int> LoopWithReturn2(int mainThreadId, int mainThreadId2)
        {
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(threadId, mainThreadId2);
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.Run(() => Task.FromResult(0));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
            for (int count = 0; count < 3; ++count)
            {
                await Task.Delay(10);
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
            return 0;
        }


        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_ValueTaskSyncInAsyncInSyncVoid()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                Async.SynchronizeValue(() => ValueTaskLoop1(mainThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        private async ValueTask ValueTaskLoop1(int mainThreadId)
        {
            Assert.IsTrue(Async.UsingSynchronousExecution);
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                await Async.ForceSync(() => ValueTaskLoop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                await Async.ForceAsync(() => ValueTaskLoop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);    // ForceAsync, unlike a native await, keeps us on the same thread even though the action runs on another thread
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                await ValueTaskLoop2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);    // this native await on a non-specially-created function triggers real async execution and thus a thread switch, also resulting in a switch to a non-synchronous execution context
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.IsFalse(Async.UsingSynchronousExecution);
                Assert.AreNotEqual(mainThreadId, threadId);
            }
        }
        private async ValueTask ValueTaskLoop2(int mainThreadId, int mainThreadId2)
        {
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(threadId, mainThreadId2);
            for (int count = 0; count < 3; ++count)
            {
                await Async.Run(() => Task.Delay(10));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
            for (int count = 0; count < 3; ++count)
            {
                await Task.Delay(10);
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check be sure that we're not running on that
                }
            }
        }


        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_ValueTaskSyncInAsyncInSyncWithReturn()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                int result = Async.SynchronizeValue(() => ValueTaskLoopWithReturn1(mainThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        private async ValueTask<int> ValueTaskLoopWithReturn1(int mainThreadId)
        {
            Assert.IsTrue(Async.UsingSynchronousExecution);
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.ForceSync(() => ValueTaskLoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.ForceAsync(() => ValueTaskLoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.AreEqual(mainThreadId, threadId);    // ForceAsync, unlike a native await, keeps us on the same thread even though the action runs on another thread
            }
            Assert.IsTrue(Async.UsingSynchronousExecution);
            for (int count = 0; count < 3; ++count)
            {
                int result = await ValueTaskLoopWithReturn2(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);    // this native await on a non-specially-created function triggers real async execution and thus a thread switch, also resulting in a switch to a non-synchronous execution context
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Assert.IsFalse(Async.UsingSynchronousExecution);
                Assert.AreNotEqual(mainThreadId, threadId);
            }
            return 0;
        }
        private async ValueTask<int> ValueTaskLoopWithReturn2(int mainThreadId, int mainThreadId2)
        {
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(threadId, mainThreadId2);
            for (int count = 0; count < 3; ++count)
            {
                int result = await Async.Run(() => new ValueTask<int>(0));
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check to be sure that we're not running on that
                }
            }
            for (int count = 0; count < 3; ++count)
            {
                await Async.Run(() => TaskExtensions.CompletedValueTask);
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check to be sure that we're not running on that
                }
            }
            for (int count = 0; count < 3; ++count)
            {
                await Task.Delay(10);
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (Async.UsingSynchronousExecution)
                {
                    Assert.AreEqual(mainThreadId, threadId);
                    Assert.AreEqual(mainThreadId2, threadId);
                }
                else
                {
                    Assert.AreNotEqual(mainThreadId, threadId);
                    // when we're not using sync execution, although the main thread is unavailable to execute tasks (because we only call sync stuff from there),
                    // main thread 2 *is* available, so we could end up using that here, so we can't check to be sure that we're not running on that
                }
            }
            return 0;
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Synchronized_AsyncEnumerator()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            foreach (int ret in Async.AsyncEnumerableToEnumerable(() => EnumerateAsync(10, default)))
            {
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public async Task Async_AsyncEnumerator()
        {
            int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            await foreach (int ret in EnumerateAsync(10, default))
            {
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
            foreach (int ret in await EnumerateAsync(10, default).ToListAsync())
            {
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
            foreach (int ret in Async.AsyncEnumerableToEnumerable(() => EnumerateAsync(10, default)))
            {
                Assert.AreEqual(mainThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
        }
        private async IAsyncEnumerable<int> EnumerateAsync(int limit, [EnumeratorCancellation] CancellationToken cancel = default)
        {
            for (int ret = 0; ret < limit; ++ret)
            {
                await Async.Run(() => Task.Delay(10));
                yield return ret;
            }
        }

        /// <summary>
        /// Performs basic tests on the <see cref="Async"/> class.
        /// </summary>
        [TestMethod]
        public void Async_InfiniteEnumeratorAsyncToSync()
        {
            foreach (int ret in Async.AsyncEnumerableToEnumerable(() => InfiniteEnumerateAsync(default)))
            {
                if (ret >= 10) break;
            }
        }
        private async IAsyncEnumerable<int> InfiniteEnumerateAsync([EnumeratorCancellation] CancellationToken cancel = default)
        {
            int ret = 0;
            while (true)
            {
                await Async.Run(() => Task.Delay(10));
                yield return ++ret;
            }
        }
#endif
    }
}
