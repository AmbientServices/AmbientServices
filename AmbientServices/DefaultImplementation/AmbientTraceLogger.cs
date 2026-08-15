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

namespace AmbientServices;

/// <summary>
/// A very basic ambient logger that just sends log data to a high-performance asynchronous wrapper on the system debug/trace output.
/// This logger is higher performance than the default one that writes to files, but it also effectively tosses data unless running under a debugger,
/// so using the file logger by default is better for diagnosing issues that occur before the user is able to switch loggers.
/// Switch to this logger for better performance, but less persistent log data.
/// Turn the logger off for maximum performance.
/// </summary>
/// <remarks>
/// <pitch>The zero-configuration default logger: higher-performance debug/trace output that is effectively discarded unless a debugger (or trace listener) is attached.  Choose it for speed during development and testing; choose <see cref="AmbientFileLogger"/> instead when logs must survive to be read later.</pitch>
/// <pledge><see cref="IAmbientLogger"/></pledge>
/// <pledge><see cref="IAmbientStructuredLogger"/></pledge>
/// <plan>
/// A stateless singleton (<see cref="Instance"/>, private constructor) that forwards every line to the process-wide <see cref="TraceBuffer"/>, which does the actual asynchronous buffering and writes batches to <see cref="System.Diagnostics.Trace"/> from a background thread — so logging callers never block on trace I/O.  Structured data is flattened to a summary-plus-JSON line via <see cref="AmbientLogger.ConvertStructuredDataIntoSimpleMessage(object, string)"/> before buffering.
/// Trade-off profile: fastest of the built-in loggers on the logging path, but durability is entirely delegated to whatever trace listeners are attached — with none, the data is discarded.
/// </plan>
/// <priority>
/// <see cref="IAmbientLogger"/>
/// 1. Logging-path speed over durability: whether anything survives is the attached listeners' business, and with none attached the data is discarded rather than buffered up, spilled, or held.  This is the exact inverse of <see cref="AmbientFileLogger"/>'s ranking, and having both inversions available is why both types exist. (public)
/// </priority>
/// </remarks>
[DefaultAmbientService]
public class AmbientTraceLogger : IAmbientLogger, IAmbientStructuredLogger
{
    /// <summary>
    /// Gets the default instance of the ambient debug/trace logger.
    /// </summary>
    public static AmbientTraceLogger Instance { get; } = new();

    /// <summary>
    /// Constructs an ambient trace logger, and implementation of <see cref="IAmbientLogger"/> that outputs log data to the system debug/trace output.
    /// </summary>
    private AmbientTraceLogger()
    {
    }
    /// <summary>
    /// Buffers the specified structured data to be asynchronously logged.
    /// </summary>
    /// <param name="structuredData">The structured data object.</param>
#if NET5_0_OR_GREATER
    [UnsupportedOSPlatform("browser")]
#endif
    public void Log(object structuredData)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(structuredData);
#else
    if (structuredData is null) throw new ArgumentNullException(nameof(structuredData));
#endif
        string message = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(structuredData);
        Log(message);
    }
    /// <summary>
    /// Adds the specified message to the log.
    /// </summary>
    /// <param name="message">The message to log.</param>
#if NET5_0_OR_GREATER
    [UnsupportedOSPlatform("browser")]
#endif
    public void Log(string message)
    {
        TraceBuffer.BufferLine(message);
    }
    /// <summary>
    /// Asynchronously flushes log entries to the system debug/trace output.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnsupportedOSPlatform("browser")]
#endif
    public ValueTask Flush(CancellationToken cancel = default)
    {
        return TraceBuffer.Flush(cancel);
    }
}
/// <summary>
/// A class to buffer debug trace messages and display them asynchronously.
/// </summary>
/// <remarks>
/// <pitch>The process-wide asynchronous buffer between logging callers and <see cref="System.Diagnostics.Trace"/>: buffering a line is a cheap enqueue, and a single background thread does the actual trace writes.</pitch>
/// <pledge>
/// Buffering never blocks on trace I/O and may be called concurrently from any thread; buffered lines are written to the trace output in enqueue order by a single writer.  A flush returns only after every line enqueued before it has been handed to the trace output.
/// When the in-memory buffer is at capacity, additional lines spill to the ambient <see cref="IAmbientLogOverflowWriter"/> instead of growing the queue.
/// </pledge>
/// <plan>
/// A static <see cref="ConcurrentQueue{T}"/> drained by one dedicated below-normal-priority background thread that batches up to ten lines per <see cref="Trace.Write(string)"/> call and sleeps on a <see cref="SemaphoreSlim"/> when idle.  Flush works by enqueueing a GUID sentinel string and waiting on a second semaphore that the drainer releases when it dequeues the sentinel; during an explicit flush the drainer thread's priority is temporarily boosted.  Overflow beyond the buffer cap is delegated to <see cref="AmbientLogBufferLimits"/>.
/// Trade-off profile: minimal per-line cost and no lock contention on the logging path, at the price of a dedicated thread and delivery that lags by the batching latency.
/// </plan>
/// </remarks>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public static class TraceBuffer
{
    private static readonly string _FlushString = Guid.NewGuid().ToString();
    private static readonly ConcurrentQueue<string> _Queue = new();
    private static readonly SemaphoreSlim _Semaphore = new(0, short.MaxValue);
    private static readonly Thread _FlusherThread = FlusherThread();
    private static readonly SemaphoreSlim _FlusherSemaphore = new(0, short.MaxValue);

    private static Thread FlusherThread()
    {
        // fire up a background thread to flush the trace data
        Thread thread = new(new ThreadStart(TraceBufferBackgroundFlusher)) {
            IsBackground = true,
            Name = "TraceBuffer.FlusherThread",
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
        // enqueue the string given to us (or spill to the standard local overflow log when the buffer is full)
        AmbientLogBufferLimits.EnqueueOrOverflow(_Queue, s);
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
                await _FlusherSemaphore.WaitAsync(cancel);
            }
        }
        finally
        {
            // restore the thread priority
            if (flush) _FlusherThread.Priority = ThreadPriority.BelowNormal;
        }
    }
    /// <summary>
    /// Asynchronously flushes any queued trace lines.
    /// </summary>
    /// <param name="cancel">A <see cref="CancellationToken"/> that the caller can use to interrupt the operation before completion.</param>
    public static async ValueTask Flush(CancellationToken cancel = default)
    {
        // queue a flush command
        _Queue.Enqueue(_FlushString);
        // release the semaphore so the data gets processed
        await Release(true, cancel);
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
    private static void TraceBufferBackgroundFlusher()
    {
        // loop forever!
        while (true)
        {
            try
            {
                StringBuilder traceData = new();
                // get up to 10 lines of trace data
                for (int line = 0; line < 10; ++line)
                {
                    // get the oldest item on the queue
                    // is there a string to trace?
                    if (_Queue.TryDequeue(out string? s))
                    {
                        if (s == _FlushString)
                        {
                            // release the flusher that told us to flush
                            _FlusherSemaphore.Release();
                        }
                        else
                        {
                            // add this to the trace data
                            traceData.Append(s);
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
                // is there a string to trace?
                if (traceData.Length > 0)
                {
                    // trace out this string
                    Trace.Write(traceData.ToString());
                }
                else
                {
                    // wait for more work (ie. stop using CPU until there is more work to do)
                    _Semaphore.Wait(TimeSpan.FromMinutes(5));   // we shouldn't ever hang here, but just in case, exit *eventually*
                }
            }
            catch (Exception ex)
            {
                // trace out this string
                Trace.Write(ex.ToString());
            }
        }
    }
}
