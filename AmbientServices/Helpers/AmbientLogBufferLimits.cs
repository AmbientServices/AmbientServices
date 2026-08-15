using System.Collections.Concurrent;

namespace AmbientServices;

/// <summary>
/// Limits in-memory buffering for ambient log queues. When a queue is full, additional lines are written via the ambient <see cref="IAmbientLogOverflowWriter"/> service.
/// </summary>
/// <remarks>
/// <pitch>The shared guardrail that keeps a logging burst from exhausting process memory: every in-memory log queue in the library enqueues through this, and lines beyond the cap divert to the overflow writer instead of growing the queue.</pitch>
/// <pledge>Below the cap (100,000 lines by default) the line is enqueued normally; at or above it, the line goes to the ambient <see cref="IAmbientLogOverflowWriter"/> (local override preferred over global) and is never enqueued.  The call never throws, even when the overflow writer is missing or fails.  The cap is approximate under concurrency — simultaneous writers near the boundary may briefly exceed it.</pledge>
/// <plan>A count check against the <see cref="ConcurrentQueue{T}"/> followed by either an enqueue or a swallow-all-exceptions overflow write; the overflow service is resolved through a cached <see cref="AmbientService{T}"/> accessor.  Deliberately lock-free: an exact cap is not worth serializing every log call.</plan>
/// <priority>
/// 1. Bounded process memory over never losing a line: past the cap, lines divert to the overflow writer instead of growing the queue, and with no overflow writer registered they are simply gone.  A logging burst must never be able to exhaust the process it is describing. (public)
/// 2. An approximate cap over an exact one: the check is lock-free, so concurrent writers near the boundary may briefly exceed it.  Serializing every log call to make the cap exact would cost far more than the overshoot it prevents. (public)
/// 3. Never throwing over reporting an overflow failure: the call swallows everything, a missing or failing overflow writer included, because a logging call that throws turns a diagnostic into an outage. (public)
/// </priority>
/// </remarks>
internal static class AmbientLogBufferLimits
{
    private static readonly AmbientService<IAmbientLogOverflowWriter> _OverflowWriter = Ambient.GetService<IAmbientLogOverflowWriter>();

    /// <summary>
    /// Maximum number of lines held in an in-memory log buffer before additional lines overflow to disk.
    /// </summary>
    public const int DefaultMaxBufferedLines = 100_000;

    /// <summary>
    /// Enqueues <paramref name="line"/> when the queue is below the limit; otherwise writes the line via <see cref="IAmbientLogOverflowWriter"/>.
    /// </summary>
    public static void EnqueueOrOverflow(ConcurrentQueue<string> queue, string line, int maxBufferedLines = DefaultMaxBufferedLines)
    {
        if (queue.Count < maxBufferedLines)
        {
            queue.Enqueue(line);
        }
        else
        {
            try
            {
                IAmbientLogOverflowWriter? writer = _OverflowWriter.Local ?? _OverflowWriter.Global;
                writer?.WriteOverflowLine(line);
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
            }
        }
    }
}
