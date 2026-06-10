using System.Collections.Concurrent;

namespace AmbientServices;

/// <summary>
/// Limits in-memory buffering for ambient log queues. When a queue is full, additional lines are written to the standard local overflow log file.
/// </summary>
internal static class AmbientLogBufferLimits
{
    /// <summary>
    /// Maximum number of lines held in an in-memory log buffer before additional lines overflow to disk.
    /// </summary>
    public const int DefaultMaxBufferedLines = 100_000;

    /// <summary>
    /// Enqueues <paramref name="line"/> when the queue is below the limit; otherwise writes the line to the overflow log file.
    /// </summary>
    public static void EnqueueOrOverflow(ConcurrentQueue<string> queue, string line, int maxBufferedLines = DefaultMaxBufferedLines)
    {
        if (queue.Count < maxBufferedLines)
        {
            queue.Enqueue(line);
        }
        else
        {
            AmbientLogOverflowWriter.WriteLine(line);
        }
    }
}
