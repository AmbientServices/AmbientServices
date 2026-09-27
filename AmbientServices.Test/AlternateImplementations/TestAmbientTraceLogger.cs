using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Summary description for StatusTest.
/// </summary>
[TestClass]
public class TestAmbientTraceLogger
{
    [TestMethod]
    public async Task AmbientTraceLoggerBasic()
    {
        using (AmbientClock.Pause())
        {
            AmbientTraceLogger logger = AmbientTraceLogger.Instance;
            // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
            logger?.Log("test1");
            if (logger != null) await logger.Flush();
            // log the second test message (since the clock is stopped, this will *never* create another file)
            logger?.Log("test2");
            if (logger != null) await logger.Flush();
        }
    }
    [TestMethod]
    public async Task AmbientTraceLoggerDuplicateFilter()
    {
        using (AmbientClock.Pause())
        {
            AmbientTraceLogger logger = AmbientTraceLogger.Instance;
            // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
            LogEntryRenderer entryRenderer = AmbientLogger.DefaultRenderer;
            LogMessageRenderer messageRenderer = AmbientLogger.DefaultMessageRenderer;
            AmbientLogger.LogFiltered(logger, entryRenderer, messageRenderer, logger, typeof(TestAmbientTraceLogger).Name, AmbientLogLevel.Information, null, new { Action= "test1" });
            AmbientLogger.LogFiltered(logger, entryRenderer, messageRenderer, logger, typeof(TestAmbientTraceLogger).Name, AmbientLogLevel.Information, null, "test2");
            await logger.Flush();
        }
    }
    /// <summary>
    /// Pins <see cref="TraceBuffer"/>'s Pledge that buffering never blocks on trace I/O, in the case that broke it: more lines waiting than the drainer's
    /// wake-up semaphore can count (<see cref="short.MaxValue"/>), which is still well under the queue's own cap.  The buffering call then failed to
    /// release, waited for a flush that nobody had asked for, and hung for good (seen in a FileDatabase stress test on a saturated machine, 2026-09-26).
    /// </summary>
    /// <remarks>
    /// The waits here are real time on purpose: they bound a hang, and a paused clock would not.  Holding the drainer delays other tests' trace output
    /// only for as long as buffering takes, a few milliseconds when it does not block.
    /// </remarks>
    [TestMethod]
    public async Task BufferingNeverBlocks_EvenWhenTheWriterIsFarBehind()
    {
        TimeSpan hangBound = TimeSpan.FromSeconds(30);
        using DrainerGate gate = new();
        Trace.Listeners.Add(gate);
        try
        {
            // wake the drainer and hold it inside the trace write, so nothing drains while the backlog builds
            TraceBuffer.BufferLine(nameof(BufferingNeverBlocks_EvenWhenTheWriterIsFarBehind));
            Assert.IsTrue(gate.Entered.Wait(hangBound), "the drainer never reached the trace listener");
            Task buffering = Task.Run(() => { for (int line = 0; line < 40_000; ++line) TraceBuffer.BufferLine("backlog"); });
            Task finished = await Task.WhenAny(buffering, Task.Delay(hangBound));
            Assert.AreSame(buffering, finished, "buffering blocked while the drainer was behind");
        }
        finally
        {
            gate.Open();
            Trace.Listeners.Remove(gate);
        }
        using CancellationTokenSource flushBound = new(hangBound);
        await TraceBuffer.Flush(flushBound.Token);
    }

    /// <summary>
    /// Pins the flush half of the Pledge: a flush returns only after every line enqueued before it has been handed to the trace output.  The drainer used
    /// to release a flush the moment it dequeued the sentinel, before writing the lines it had already batched ahead of it.
    /// </summary>
    [TestMethod]
    public async Task FlushReturnsOnlyAfterTheLinesBeforeItAreWritten()
    {
        using LineRecorder recorder = new();
        Trace.Listeners.Add(recorder);
        try
        {
            for (int attempt = 0; attempt < 200; ++attempt)
            {
                string line = $"{nameof(FlushReturnsOnlyAfterTheLinesBeforeItAreWritten)}-{Guid.NewGuid():N}";
                TraceBuffer.BufferLine(line);
                using CancellationTokenSource flushBound = new(TimeSpan.FromSeconds(30));
                await TraceBuffer.Flush(flushBound.Token);
                Assert.IsTrue(recorder.Saw(line), $"the flush returned before the line buffered ahead of it was written (attempt {attempt})");
            }
        }
        finally
        {
            Trace.Listeners.Remove(recorder);
        }
    }

    /// <summary>A trace listener that remembers what it was given.</summary>
    private sealed class LineRecorder : TraceListener
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _written = new();

        public bool Saw(string line) => _written.Any(written => written.Contains(line, StringComparison.Ordinal));

        public override void Write(string? message) { if (message is not null) _written.Enqueue(message); }

        public override void WriteLine(string? message) => Write(message);
    }

    /// <summary>A trace listener that holds the drainer thread inside its first write until opened.</summary>
    private sealed class DrainerGate : TraceListener
    {
        private readonly ManualResetEventSlim _entered = new(false);
        private readonly ManualResetEventSlim _open = new(false);

        public ManualResetEventSlim Entered => _entered;

        public void Open() => _open.Set();

        public override void Write(string? message) => Hold();

        public override void WriteLine(string? message) => Hold();

        // A trace listener is a synchronous API, so this has to be a blocking wait; it is bounded so a broken run cannot hold the drainer forever.
        private void Hold()
        {
            if (Thread.CurrentThread.Name != "TraceBuffer.FlusherThread") return;
            _entered.Set();
            _open.Wait(TimeSpan.FromSeconds(60));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _open.Set();
                _entered.Dispose();
                _open.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
