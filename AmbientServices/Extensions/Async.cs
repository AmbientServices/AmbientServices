using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A static class to hold utility functions for asynchronous operations.
    /// When migrating code from sync to async, begin from the bottom off the call stack.  
    /// Use <see cref="Synchronize(Func{Task})"/> at the transition from sync to async, forcing the task to run in a synchronous ambient context.
    /// Use await <see cref="Run(Func{Task})"/> as the default asynchronous invocation, which will run synchronously in a synchronous ambient context, and asynchronously in an asynchronous ambient context.
    /// Use await <see cref="ForceAsync(Func{Task})"/> to force asynchronous execution within a synchronous ambient context (even within <see cref="ForceSync(Func{Task})"/>).
    /// Use await <see cref="ForceSync(Func{Task})"/> to force synchronous execution of an async function within an async context (even within <see cref="ForceAsync(Func{Task})"/>).
    /// As migration progresses, calls to <see cref="Synchronize(Func{Task})"/> move up the call stack, being gradually replaced by calls to <see cref="Run(Func{Task})"/>.
    /// Calls that use await without one of these as the target will run asynchonously in a newly spawned async ambient context.
    /// </summary>
    public static class Async
    {
        private static readonly System.Threading.SynchronizationContext sMultithreadedContext = new();

        /// <summary>
        /// Gets a multithreaded context to use for spawning tasks to the multithreaded context from within a synchronous context.
        /// </summary>
        public static System.Threading.SynchronizationContext MultithreadedContext { get { return sMultithreadedContext; } }
        /// <summary>
        /// Gets the single threaded context to use for spawning tasks to be run on the current thread.
        /// </summary>
        public static System.Threading.SynchronizationContext SinglethreadedContext { get { return SynchronousSynchronizationContext.Default; } }

        /// <summary>
        /// Gets whether or not the current context is using synchronous execution.
        /// </summary>
        public static bool UsingSynchronousExecution { get { return System.Threading.SynchronizationContext.Current == SynchronousSynchronizationContext.Default; } }

        private static void RunIfNeeded(Task task)
        {
            if (task.Status == TaskStatus.Created)
            {
                task.RunSynchronously(SynchronousTaskScheduler.Default);
            }
        }

        private static void ConvertAggregateException(AggregateException ex)
        {
            if (ex.InnerExceptions.Count < 2)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
            }
        }

        private static void WaitAndUnwrapException(this Task t, bool continueOnCapturedContext)
        {
            try
            {
                t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                ConvertAggregateException(ex);
                throw;
            }
        }
        private static T WaitAndUnwrapException<T>(this Task<T> t, bool continueOnCapturedContext)
        {
            try
            {
                return t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                ConvertAggregateException(ex);
                throw;
            }
        }
        // NOTE that the following are not currently needed because we can't force a ValueTask to run synchronously, and after it's done running, we can't call GetResult() on it because we would have already gotten the results
//        private static void WaitAndUnwrapException(this ValueTask t, bool continueOnCapturedContext)
//        private static T WaitAndUnwrapException<T>(this ValueTask<T> t, bool continueOnCapturedContext)

        private static void RunInTemporaryContextWithExceptionConversion(SynchronizationContext newContext, Action a)
        {
            System.Threading.SynchronizationContext? oldContext = System.Threading.SynchronizationContext.Current;
            try
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(newContext);
                a();
            }
            catch (AggregateException ex)
            {
                ConvertAggregateException(ex);
                throw;
            }
            finally
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }
        private static T RunInTemporaryContextWithExceptionConversion<T>(SynchronizationContext newContext, Func<T> a)
        {
            System.Threading.SynchronizationContext? oldContext = System.Threading.SynchronizationContext.Current;
            try
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(newContext);
                return a();
            }
            catch (AggregateException ex)
            {
                ConvertAggregateException(ex);
                throw;
            }
            finally
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }


        /// <summary>
        /// Runs the specified asynchronous action synchronously on the current thread, staying on the current thread.
        /// </summary>
        /// <param name="a">The action to run.</param>
        [DebuggerStepThrough]
        public static void Synchronize(Func<Task> a)
        {
            RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                using (Task task = a())
                {
                    RunIfNeeded(task);
                    task.WaitAndUnwrapException(true);
                }
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action using the currently ambient mode.
        /// </summary>
        /// <param name="a">The asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task Run(Func<Task> a)
        {
            if (System.Threading.SynchronizationContext.Current == SynchronousSynchronizationContext.Default)
            {
                return ForceSync(a);
            }
            else
            {
                return ForceAsync(a);
            }
        }

        /// <summary>
        /// Runs the specified asynchronous action synchronously and switches the ambient synchronization context to the synchronous one during the operation.
        /// Use this to run the action with a synchronous ambient context, even with an asynchronous one.
        /// </summary>
        /// <param name="a">The asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task ForceSync(Func<Task> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                using (Task task = a()) // the task should be created here by the delegate and should be started using the ambient synchronization context, which is now the sync one
                {
                    RunIfNeeded(task);
                    task.WaitAndUnwrapException(true);
                }
                return Task.CompletedTask;
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action asynchronously and switches the ambient synchronization context to the asynchronous one during the operation.
        /// Use this to run the action in an asynchronous ambient context, but wait on the current thread for it to finish.
        /// </summary>
        /// <param name="a">The asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task ForceAsync(Func<Task> a)
        {
            return RunInTemporaryContextWithExceptionConversion(sMultithreadedContext, () =>
            {
                Task task = Task.Run(a);       // this should run the task on another thread and should be started using the ambient synchronization context, which is now the async one
                task.WaitAndUnwrapException(false);
                return task;
            });
        }



        /// <summary>
        /// Runs the specified asynchronous action synchronously on the current thread, staying on the current thread.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static T Synchronize<T>(Func<Task<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                using (Task<T> task = a())
                {
                    RunIfNeeded(task);
                    return task.WaitAndUnwrapException(true);
                }
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action using the currently ambient mode.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task<T> Run<T>(Func<Task<T>> a)
        {
            if (System.Threading.SynchronizationContext.Current == SynchronousSynchronizationContext.Default)
            {
                return ForceSync(a);
            }
            else
            {
                return ForceAsync(a);
            }
        }
        /// <summary>
        /// Runs the specified asynchronous action synchronously and switches the ambient synchronization context to the synchronous one during the operation.
        /// Use this to run the action with a synchronous ambient context, even with an asynchronous one.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task<T> ForceSync<T>(Func<Task<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                using (Task<T> task = a()) // the task should be created here by the delegate and should be started using the ambient synchronization context, which is now the sync one
                {
                    RunIfNeeded(task);
                    T result = task.WaitAndUnwrapException(true);
                    return Task.FromResult(result);
                }
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action asynchronously and switches the ambient synchronization context to the asynchronous one during the operation.
        /// Use this to run the action in an asynchronous ambient context, but wait on the current thread for it to finish.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static Task<T> ForceAsync<T>(Func<Task<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(sMultithreadedContext, () =>
            {
                Task<T> task = Task.Run(a);       // this should run the task on another thread and should be started using the ambient synchronization context, which is now the async one
                task.WaitAndUnwrapException(false);
                return task;
            });
        }


        /// <summary>
        /// Runs the specified asynchronous action synchronously on the current thread, staying on the current thread.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static T SynchronizeValue<T>(Func<ValueTask<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                ValueTask<T> valueTask = a();
                Task<T> task = valueTask.AsTask();          // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask FWIW)
                RunIfNeeded(task);
                return task.WaitAndUnwrapException(true);
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action synchronously and switches the ambient synchronization context to the synchronous one during the operation.
        /// Use this to run the action with a synchronous ambient context, even with an asynchronous one.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask<T> ForceSync<T>(Func<ValueTask<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                ValueTask<T> valueTask = a();
                Task<T> task = valueTask.AsTask();          // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask FWIW)
                RunIfNeeded(task);
                T result = task.WaitAndUnwrapException(true);
                return new ValueTask<T>(result);
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action asynchronously and switches the ambient synchronization context to the asynchronous one during the operation.
        /// Use this to run the action in an asynchronous ambient context, but wait on the current thread for it to finish.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask<T> ForceAsync<T>(Func<ValueTask<T>> a)
        {
            return RunInTemporaryContextWithExceptionConversion(sMultithreadedContext, () =>
            {
                Task<T> task = Task.Run(() => a().AsTask());        // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask FWIW)
                T result = task.WaitAndUnwrapException(false);
                return new ValueTask<T>(result);
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action using the currently ambient mode.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask<T> Run<T>(Func<ValueTask<T>> a)
        {
            if (System.Threading.SynchronizationContext.Current == SynchronousSynchronizationContext.Default)
            {
                return ForceSync(a);
            }
            else
            {
                return ForceAsync(a);
            }
        }


        /// <summary>
        /// Runs the specified asynchronous action synchronously on the current thread, staying on the current thread.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static void SynchronizeValue(Func<ValueTask> a)
        {
            RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                ValueTask valueTask = a();
                Task task = valueTask.AsTask();          // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask)
                RunIfNeeded(task);
                task.WaitAndUnwrapException(true);
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action synchronously and switches the ambient synchronization context to the synchronous one during the operation.
        /// Use this to run the action with a synchronous ambient context, even with an asynchronous one.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask ForceSync(Func<ValueTask> a)
        {
            return RunInTemporaryContextWithExceptionConversion(SynchronousSynchronizationContext.Default, () =>
            {
                ValueTask valueTask = a();
                Task task = valueTask.AsTask();             // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask)
                RunIfNeeded(task);
                task.WaitAndUnwrapException(true);
                return new ValueTask();
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action asynchronously and switches the ambient synchronization context to the asynchronous one during the operation.
        /// Use this to run the action in an asynchronous ambient context, but wait on the current thread for it to finish.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask ForceAsync(Func<ValueTask> a)
        {
            return RunInTemporaryContextWithExceptionConversion(sMultithreadedContext, () =>
            {
                Task task = Task.Run(() => a().AsTask());           // I'm not seeing a way to do this without this conversion (which negates the optimization provided by ValueTask)
                task.WaitAndUnwrapException(false);
                return new ValueTask();
            });
        }
        /// <summary>
        /// Runs the specified asynchronous action using the currently ambient mode.
        /// </summary>
        /// <param name="a">The cancelable asynchronous action to run.</param>
        [DebuggerStepThrough]
        public static ValueTask Run(Func<ValueTask> a)
        {
            if (System.Threading.SynchronizationContext.Current == SynchronousSynchronizationContext.Default)
            {
                return ForceSync(a);
            }
            else
            {
                return ForceAsync(a);
            }
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
        /// <summary>
        /// Synchronously converts an <see cref="IAsyncEnumerable{T}"/> into an <see cref="IEnumerable{T}"/>.
        /// Works with infinite collections.
        /// </summary>
        /// <typeparam name="T">The type being enumerated.</typeparam>
        /// <param name="funcAsyncEnumerable">A delegate that returns an <see cref="IAsyncEnumerable{T}"/></param>
        /// <param name="cancel">A <see cref="System.Threading.CancellationToken"/> which the caller can use to notify the executor to cancel the operation before it finishes.</param>
        /// <returns>The <see cref="IEnumerable{T}"/>.</returns>
        [DebuggerStepThrough]
        public static IEnumerable<T> AsyncEnumerableToEnumerable<T>(Func<IAsyncEnumerable<T>> funcAsyncEnumerable, System.Threading.CancellationToken cancel = default)
        {
            if (funcAsyncEnumerable == null) throw new ArgumentNullException(nameof(funcAsyncEnumerable));
            IAsyncEnumerator<T> asyncEnum = funcAsyncEnumerable().GetAsyncEnumerator(cancel);
            try
            {
                while (SynchronizeValue(() => asyncEnum.MoveNextAsync()))
                {
                    cancel.ThrowIfCancellationRequested();
                    yield return asyncEnum.Current;
                }
            }
            finally
            {
                SynchronizeValue(() => asyncEnum.DisposeAsync());
            }
        }
#endif
    }
#if NETSTANDARD2_1 || NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// A static class to hold extensions to IAsynceEnumerable.
    /// </summary>
    public static class IAsyncEnumerableExtensions
    {
        /// <summary>
        /// Asynchronously converts an <see cref="IAsyncEnumerable{T}"/> into a list.
        /// Note that since it returns a list, this function does NOT work with inifinite (or even very large) enumerations.
        /// </summary>
        /// <typeparam name="T">The type within the list.</typeparam>
        /// <param name="ae">The <see cref="IAsyncEnumerable{T}"/>.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> the caller can use to cancel the operation before it completes.</param>
        /// <returns>A <see cref="List{T}"/> containing all the items in the async enumerator.</returns>
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> ae, CancellationToken cancel = default)
        {
            if (ae == null) throw new ArgumentNullException(nameof(ae));
            List<T> ret = new List<T>();
            await foreach (T t in ae)
            {
                cancel.ThrowIfCancellationRequested();
                ret.Add(t);
            }
            return ret;
        }
    }
#endif
    /// <summary>
    /// A <see cref="SynchronousTaskScheduler"/> that just runs each task immediately as it is queued.
    /// </summary>
    public sealed class SynchronousTaskScheduler : System.Threading.Tasks.TaskScheduler
    {
        private static readonly SynchronousTaskScheduler _Default = new();
        /// <summary>
        /// Gets the default instance for this singleton class.
        /// </summary>
        public static new SynchronousTaskScheduler Default { get { return _Default; } }

        private SynchronousTaskScheduler()
        {
        }
        /// <summary>
        /// Gets the list of scheduled tasks, which for this class, is always empty.
        /// </summary>
        /// <returns>An empty enumeration.</returns>
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] //  (no way to call externally, and I can't find a way to call it indirectly).
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Enumerable.Empty<Task>();
        }
        /// <summary>
        /// Queues the specified task, which in this case, just executes it immediately.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> which is to be executed.</param>
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] //  (no way to call externally, and I can't find a way to call it indirectly).
        protected override void QueueTask(Task task)
        {
            base.TryExecuteTask(task);
        }
        /// <summary>
        /// Attempts to execute the specified task inline, which just runs the task.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> to be executed.</param>
        /// <param name="taskWasPreviouslyQueued">Whether or not the task was previously queued.</param>
        /// <returns><b>true</b>.</returns>
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] //  (no way to call externally, and I can't find a way to call it indirectly).
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return base.TryExecuteTask(task);
        }
        /// <summary>
        /// Gets the maximum number of tasks that can be concurrently running under this scheduler, which is one.
        /// </summary>
        public override int MaximumConcurrencyLevel { get { return 1; } }
    }

    /// <summary>
    /// A <see cref="SynchronousSynchronizationContext"/> that schedules work items on the <see cref="SynchronousTaskScheduler"/>.
    /// </summary>
    public class SynchronousSynchronizationContext : System.Threading.SynchronizationContext
    {
        private static readonly SynchronousSynchronizationContext _Default = new();
        /// <summary>
        /// Gets the instance for this singleton class.
        /// </summary>
        public static SynchronousSynchronizationContext Default { get { return _Default; } }

        private SynchronousSynchronizationContext()
        {
            base.SetWaitNotificationRequired();
        }

        /// <summary>
        /// Synchronously posts a message.
        /// </summary>
        /// <param name="d">The message to post.</param>
        /// <param name="state">The state to give to the post callback.</param>
        public override void Send(System.Threading.SendOrPostCallback d, object? state)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            d(state);
        }
        /// <summary>
        /// Posts a message.  The caller intended to post it asynchronously, but the whole point of this class is to do everything synchronously, so this call is synchronous.
        /// </summary>
        /// <param name="d">The message to post.</param>
        /// <param name="state">The state to give to the post callback.</param>
        public override void Post(System.Threading.SendOrPostCallback d, object? state)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            d(state);
        }
        /// <summary>
        /// Creates a "copy" of this <see cref="SynchronousSynchronizationContext"/>, which in this case just returns the singleton instance because there is nothing held in memory anyway.
        /// </summary>
        /// <returns>The same singleton <see cref="SynchronousSynchronizationContext"/> on which we were called.</returns>
        public override System.Threading.SynchronizationContext CreateCopy()
        {
            return this;
        }
    }
}
