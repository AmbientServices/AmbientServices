using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic implementation of <see cref="IAmbientLogger"/> that writes log messages to a rotating set of files.
/// Turn the logger off for maximum performance.
/// </summary>
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
            await logger.Flush(cancel).ConfigureAwait(true);
        }
        foreach (IAmbientStructuredLogger structuredLogger in _ambientStructuredLoggers)
        {
            await structuredLogger.Flush(cancel).ConfigureAwait(true);
        }
    }
}
