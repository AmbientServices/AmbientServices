using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    [DefaultAmbientService]
    class BasicAmbientLogger : IAmbientLogger
    {
        static readonly IAmbientSettings _Settings = AmbientServices.Registry<IAmbientSettings>.Implementation;
        static readonly ISetting<LogLevel> LogLevelSetting = _Settings.GetSetting<LogLevel>(nameof(BasicAmbientLogger) + "-LogLevel", s => (LogLevel)Enum.Parse(typeof(LogLevel), s), LogLevel.Information);
        static readonly ISetting<Regex> TypeFilterSetting = _Settings.GetSetting<Regex>(nameof(BasicAmbientLogger) + "-TypeFilter", s => new Regex(s, RegexOptions.Compiled));
        static readonly ISetting<Regex> CategoryFilterSetting = _Settings.GetSetting<Regex>(nameof(BasicAmbientLogger) + "-CategoruFilter", s => new Regex(s, RegexOptions.Compiled));

        class TypeLogger<T> : ILogger<T>
        {
            private static readonly string TypeName = typeof(T).Name;

            private void InnerLog(string message, string category = null, LogLevel level = LogLevel.Information)
            {
                if (level >= LogLevelSetting.Value &&
                    (TypeFilterSetting.Value == null || TypeFilterSetting.Value.IsMatch(TypeName)) &&
                    (CategoryFilterSetting.Value == null || category == null || CategoryFilterSetting.Value.IsMatch(category))
                    )
                {
                    category = (category != null) ? category + ": " : "";
                    message = "[" + level.ToString() + ":" + TypeName + "] " + category + message;
                    TraceBuffer.BufferLine(message);
                }
            }
            public void Log(string message, string category = null, LogLevel level = LogLevel.Information)
            {
                InnerLog(message, category, level);
            }
            public void Log(Func<string> message, string category = null, LogLevel level = LogLevel.Information)
            {
                InnerLog(message(), category, level);
            }
            public void Log(Exception ex, string category = null, LogLevel level = LogLevel.Error)
            {
                InnerLog(ex.ToString(), category, level);
            }
            public void Log(string message, Exception ex, string category = null, LogLevel level = LogLevel.Error)
            {
                InnerLog(message + Environment.NewLine + ex.ToString(), category, level);
            }
            public void Log(Func<string> message, Exception ex, string category = null, LogLevel level = LogLevel.Error)
            {
                InnerLog(message() + Environment.NewLine + ex.ToString(), category, level);
            }
        }

        public ILogger<T> GetLogger<T>()
        {
            return new TypeLogger<T>();
        }
        public Task Flush(CancellationToken cancel = default(CancellationToken))
        {
            return TraceBuffer.Flush(cancel);
        }
    }
    /// <summary>
    /// A class to buffer debug trace messages and display them asynchronously.
    /// </summary>
    static class TraceBuffer
    {
        static private readonly ConcurrentQueue<string> _Queue;
        static private readonly SemaphoreSlim _Semaphore;
        static private readonly Thread _FlusherThread;
        static private TaskCompletionSource<object> _FlushedEvent;

        static TraceBuffer()
        {
            _Queue = new ConcurrentQueue<string>();
            _Semaphore = new SemaphoreSlim(0, Int16.MaxValue);
            _FlushedEvent = new TaskCompletionSource<object>(null);
            _FlusherThread = new Thread(new ThreadStart(_BackgroundThread));
            _FlusherThread.Name = "TraceBuffer";
            _FlusherThread.Priority = ThreadPriority.BelowNormal;
            _FlusherThread.IsBackground = true;
            // fire up a background thread to trace the trace data
            _FlusherThread.Start();
        }

        public static void BufferLine(string s)
        {
            Buffer("{0}" + Environment.NewLine, s);
        }
        public static void WriteLine(string s, params object[] o)
        {
            Buffer(s + Environment.NewLine, o);
        }
        public static void Write(string s)
        {
            Buffer("{0}", s);
        }
        public static void Write(string s, params object[] o)
        {
            Buffer(s, o);
        }
        private static void Buffer(string s, params object[] o)
        {
            // enqueue the string given to us
            _Queue.Enqueue(String.Format(s, o));
            // we've queued data, so create a new flushed event
            System.Threading.Interlocked.Exchange(ref _FlushedEvent, new TaskCompletionSource<object>(null));
            // release the semaphore so the data gets processed
            Release(false).Wait();
        }
        [DebuggerStepThrough]
        private static bool Release()
        {
            try
            {
                // trigger the semaphore
                _Semaphore.Release();
                return true;
            }
            catch (SemaphoreFullException)
            {
                // failure!
                return false;
            }
        }
        private static async Task Release(bool flush, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                // if the release fails, flush the queue
                if (!Release()) flush = true;
                cancel.ThrowIfCancellationRequested();
                // are we flushing?
                if (flush)
                {
                    // boost the priority of the flusher thread for a bit
                    _FlusherThread.Priority = ThreadPriority.AboveNormal;
                    cancel.ThrowIfCancellationRequested();
                    // wait until we get flushed
                    await _FlushedEvent.Task;
                }
            }
            finally
            {
                // restore the thread priority
                if (flush) _FlusherThread.Priority = ThreadPriority.BelowNormal;
            }
        }
        public static Task Flush(CancellationToken cancel = default(CancellationToken))
        {
            return Release(true, cancel);
        }
        /// <summary>
        /// Peeks at all unflushed messages synchronously (for diagnostic purposes only).
        /// </summary>
        [ExcludeFromCodeCoverage, Obsolete("This property should not be used directly--it's only for debugging!")]
        public static string PeekUnflushed
        {
            get
            {
                StringBuilder ret = new StringBuilder();
                foreach (string s in _Queue)
                {
                    // add this to the result
                    ret.Append(s);
                }
                // return the data
                return ret.ToString();
            }
        }
        private static void _BackgroundThread()
        {
            // loop forever!
            while (true)
            {
                try
                {
                    StringBuilder traceData = new StringBuilder();
                    // get up to 10 lines of trace data
                    for (int line = 0; line < 10; ++line)
                    {
                        // get the oldest item on the queue
                        string s;
                        // is there a string to trace?
                        if (_Queue.TryDequeue(out s))
                        {
                            System.Diagnostics.Debug.Assert(s != null);
                            // add this to the trace data
                            traceData.Append(s);
                            // is there more data? (timeout immediately if there isn't)
                            if (_Semaphore.Wait(0))
                            {
                                // try to get some more data (up to ten lines)
                                continue;
                            }
                            // else no data left in queue--no point in waiting before we flush to the output
                        }
                        // else nothing left in the queue
                        // signal any waiters that we've done what we can to flush and we appear to be done
                        _FlushedEvent.TrySetResult(null);
                        break;
                    }
                    // is there a string to trace?
                    if (traceData.Length > 0)
                    {
                        // trace out this string
                        System.Diagnostics.Trace.Write(traceData.ToString());
                    }
                    else
                    {
                        // wait for more work (ie. stop using CPU until there is more work to do)
                        _Semaphore.Wait();
                    }
                }
                catch (Exception ex)
                {
                    // trace out this string
                    System.Diagnostics.Trace.Write(ex.ToString());
                }
            }
        }
    }
}
