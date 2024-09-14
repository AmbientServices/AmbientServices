using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A very basic ambient logger that just sends log data to a high-performance asynchronous wrapper on the system console output.
    /// This logger is higher performance than the default one that writes to files, but it also effectively tosses data unless running under a debugger,
    /// so using the file logger by default is better for diagnosing issues that occur before the user is able to switch loggers.
    /// Switch to this logger for better performance, but less persistent log data.
    /// Turn the logger off for maximum performance.
    /// </summary>
    public class AmbientConsoleLogger : IAmbientLogger
    {
        /// <summary>
        /// Constructs an ambient console logger, and implementation of <see cref="IAmbientLogger"/> that outputs log data to the system console output.
        /// </summary>
        public AmbientConsoleLogger()
        {
        }
        /// <summary>
        /// Adds the specified message to the log.
        /// </summary>
        /// <param name="message"></param>
#if NET5_0_OR_GREATER
        [UnsupportedOSPlatform("browser")]
#endif
        public void Log(string message)
        {
            ConsoleBuffer.BufferLine(message);
        }
        /// <summary>
        /// Asynchronously flushes log entries to the system console output.
        /// </summary>
#if NET5_0_OR_GREATER
        [UnsupportedOSPlatform("browser")]
#endif
        public ValueTask Flush(CancellationToken cancel = default)
        {
            return ConsoleBuffer.Flush(cancel);
        }
    }
    /// <summary>
    /// A class to buffer debug console messages and display them asynchronously.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnsupportedOSPlatform("browser")]
#endif
    public static class ConsoleBuffer
    {
        private static readonly string _FlushString = Guid.NewGuid().ToString();
        private static readonly ConcurrentQueue<string> _Queue = new();
        private static readonly SemaphoreSlim _Semaphore = new(0, short.MaxValue);
        private static readonly Thread _FlusherThread = FlusherThread();
        private static readonly SemaphoreSlim _FlusherSemaphore = new(0, short.MaxValue);

        private static Thread FlusherThread()
        {
            // fire up a background thread to flush the console data
            Thread thread = new(new ThreadStart(ConsoleBufferBackgoundFlusher)) {
                IsBackground = true,
                Name = "ConsoleBuffer.FlusherThread",
                Priority = ThreadPriority.BelowNormal,
            };
            thread.Start();
            return thread;
        }
        /// <summary>
        /// Buffers the specified line to the concurrent buffer.
        /// </summary>
        /// <param name="s">The string to buffer.</param>
        public static void BufferLine(string s)
        {
            Buffer(s + Environment.NewLine);
        }
        private static void Buffer(string s)
        {
            // enqueue the string given to us
            _Queue.Enqueue(s);
            // release the semaphore so the data gets processed
            Release(false).Wait();
        }
        [DebuggerStepThrough]
        private static bool Release()
        {
            try
            {
                _Semaphore.Release();
                return true;
            }
            catch (SemaphoreFullException)
            {
                // failure!
                return false;
            }
        }
        private static async Task Release(bool flush, CancellationToken cancel = default)
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
                    // wait for the flush to happen
                    await _FlusherSemaphore.WaitAsync(cancel).ConfigureAwait(false);
                }
            }
            finally
            {
                // restore the thread priority
                if (flush) _FlusherThread.Priority = ThreadPriority.BelowNormal;
            }
        }
        /// <summary>
        /// Asynchronously flushes any queued console lines.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> that the caller can use to interrupt the operation before completion.</param>
        public static async ValueTask Flush(CancellationToken cancel = default)
        {
            // queue a flush command
            _Queue.Enqueue(_FlushString);
            // release the semaphore so the data gets processed
            await Release(true, cancel).ConfigureAwait(false);
        }
        /// <summary>
        /// Peeks at all unflushed messages synchronously (for diagnostic purposes only).
        /// </summary>
        [ExcludeFromCoverage]
        [ExcludeFromCodeCoverage, Obsolete("This property should not be used directly--it's only for debugging!")]
        public static string PeekUnflushed
        {
            get
            {
                StringBuilder ret = new();
                foreach (string s in _Queue)
                {
                    // add this to the result
                    ret.Append(s);
                }
                // return the data
                return ret.ToString();
            }
        }
        private static void ConsoleBufferBackgoundFlusher()
        {
            // loop forever!
            while (true)
            {
                try
                {
                    StringBuilder consoleData = new();
                    // get up to 10 lines of console data
                    for (int line = 0; line < 10; ++line)
                    {
                        // get the oldest item on the queue
                        string? s;
                        // is there a string to console?
                        if (_Queue.TryDequeue(out s))
                        {
                            if (s == _FlushString)
                            {
                                // release the flusher that told us to flush
                                _FlusherSemaphore.Release();
                            }
                            else
                            {
                                // add this to the console data
                                consoleData.Append(s);
                            }
                            // is there more data? (don't wait if there isn't)
                            if (_Semaphore.Wait(0))
                            {
                                // try to get some more data (up to ten lines)
                                continue;
                            }
                            // else no data left in queue--no point in waiting before we flush to the output
                        }
                        // else nothing left in the queue
                        else break;
                    }
                    // is there a string to console?
                    if (consoleData.Length > 0)
                    {
                        // console out this string
                        Console.Write(consoleData.ToString());
                    }
                    else
                    {
                        // wait for more work (ie. stop using CPU until there is more work to do)
                        _Semaphore.Wait(TimeSpan.FromMinutes(5));   // we shouldn't ever hang here, but just in case, exit *eventually*
                    }
                }
                catch (Exception ex)
                {
                    // console out this string
                    Console.Write(ex.ToString());
                }
            }
        }
    }
}
