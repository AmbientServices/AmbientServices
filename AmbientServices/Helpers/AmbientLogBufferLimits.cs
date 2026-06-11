using System.Collections.Concurrent;

namespace AmbientServices;

/// <summary>
/// Limits in-memory buffering for ambient log queues. When a queue is full, additional lines are written via the ambient <see cref="IAmbientLogOverflowWriter"/> service.
/// </summary>
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
