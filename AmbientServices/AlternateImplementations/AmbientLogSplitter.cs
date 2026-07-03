using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic implementation of <see cref="IAmbientLogger"/> that writes log messages to a rotating set of files.
/// Turn the logger off for maximum performance.
/// </summary>
/// <remarks>
/// <pitch>Fan-out: register it as the one ambient logger and it forwards every entry to any number of underlying loggers — for example, files for durability <em>and</em> console for visibility — without the log sources knowing there is more than one target.</pitch>
/// <pledge><see cref="IAmbientLogger"/></pledge>
/// <pledge><see cref="IAmbientStructuredLogger"/></pledge>
/// <pledge>
/// Every simple log entry is forwarded to each registered simple logger and every structured entry to each registered structured logger, synchronously and in registration order; the splitter adds no buffering, filtering, or rendering of its own.  A registered logger that realizes both interfaces is enrolled for both kinds of entries by a single registration and receives each entry exactly once.
/// Registration and removal are <em>not</em> thread-safe and must complete during application initialization, before concurrent logging begins; logging itself is as thread-safe as the underlying loggers.  A flush completes only after every registered logger has been flushed.
/// </pledge>
/// <plan>
/// Two plain <see cref="List{T}"/>s (simple and structured); each add/remove cross-enrolls the logger in the other list when it realizes the other interface, and each log call is a straight loop over the matching list.  No locking anywhere — the initialization-only registration term of the Pledge is what makes the lock-free read path safe.  Flush awaits each simple logger then each structured logger sequentially, so a dual-interface logger is flushed twice (harmless — the second flush finds nothing left to deliver).
/// Trade-off profile: per-entry cost is one virtual call per target with zero added allocation; latency and durability are simply those of the slowest/weakest registered logger.
/// </plan>
/// </remarks>
public class AmbientLogSplitter : IAmbientLogger, IAmbientStructuredLogger
{
    private readonly List<IAmbientLogger> _ambientLoggers = new();
    private readonly List<IAmbientStructuredLogger> _ambientStructuredLoggers = new();

    /// <summary>
    /// Constructs a default log splitter.
    /// </summary>
    public AmbientLogSplitter()
    {
    }
    /// <summary>
    /// Adds the specified logger to the ambient loggers.
    /// This function is *not* thread-safe, so it should only be called during application initialization.
    /// </summary>
    /// <param name="logger">The <see cref="IAmbientLogger"/> to start logging to.</param>
    public void AddSimpleLogger(IAmbientLogger logger)
    {
        _ambientLoggers.Add(logger);
        if (logger is IAmbientStructuredLogger structuredLogger)
        {
            _ambientStructuredLoggers.Add(structuredLogger);
        }
    }
    /// <summary>
    /// Removes the specified logger from the ambient loggers.
    /// This function is *not* thread-safe, so it should only be called during application initialization.
    /// </summary>
    /// <param name="logger">The <see cref="IAmbientLogger"/> to stop logging to.</param>
    public void RemoveSimpleLogger(IAmbientLogger logger)
    {
        _ambientLoggers.Remove(logger);
        if (logger is IAmbientStructuredLogger structuredLogger)
        {
            _ambientStructuredLoggers.Remove(structuredLogger);
        }
    }
    /// <summary>
    /// Adds the specified structured logger to the ambient structured loggers.
    /// This function is *not* thread-safe, so it should only be called during application initialization.
    /// </summary>
    /// <param name="structuredLogger">The <see cref="IAmbientStructuredLogger"/> to start logging to.</param>
    public void AddLogger(IAmbientStructuredLogger structuredLogger)
    {
        _ambientStructuredLoggers.Add(structuredLogger);
        if (structuredLogger is IAmbientLogger logger)
        {
            _ambientLoggers.Add(logger);
        }
    }
    /// <summary>
    /// Removes the specified structured logger from the ambient structured loggers.
    /// This function is *not* thread-safe, so it should only be called during application initialization.
    /// </summary>
    /// <param name="structuredLogger">The <see cref="IAmbientStructuredLogger"/> to stop logging to.</param>
    public void RemoveLogger(IAmbientStructuredLogger structuredLogger)
    {
        _ambientStructuredLoggers.Remove(structuredLogger);
        if (structuredLogger is IAmbientLogger logger)
        {
            _ambientLoggers.Remove(logger);
        }
    }
    /// <summary>
    /// Buffers the specified structured data to be asynchronously logged.
    /// </summary>
    /// <param name="structuredData">The structured data object.</param>
    public void Log(object structuredData)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(structuredData);
#else
    if (structuredData is null) throw new ArgumentNullException(nameof(structuredData));
#endif
        foreach (IAmbientStructuredLogger structuredLogger in _ambientStructuredLoggers)
        {
            structuredLogger.Log(structuredData);
        }
    }
    /// <summary>
    /// Buffers the specified message to be asynchronously logged.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Log(string message)
    {
        foreach (IAmbientLogger logger in _ambientLoggers)
        {
            logger.Log(message);
        }
    }
    /// <summary>
    /// Flushes everything that has been previously logged to the appropriate file on disk.
    /// </summary>
    /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
    public async ValueTask Flush(CancellationToken cancel = default)
    {
        foreach (IAmbientLogger logger in _ambientLoggers)
        {
            await logger.Flush(cancel);
        }
        foreach (IAmbientStructuredLogger structuredLogger in _ambientStructuredLoggers)
        {
            await structuredLogger.Flush(cancel);
        }
    }
    /// <summary>
    /// Gets the string representation of the ambient log splitter.
    /// </summary>
    /// <returns>The string representation of the ambient log splitter.</returns>
    public override string ToString()
    {
        return string.Join(",", _ambientLoggers.Select(l => l.ToString())) + "/" + string.Join(",", _ambientStructuredLoggers.Select(l => l.ToString()));
    }
}
