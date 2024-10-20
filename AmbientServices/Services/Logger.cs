using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// An enumeration of levels for logging.
/// </summary>
public enum AmbientLogLevel
{
    /// <summary>
    /// The associated log message is about a critical (show-stopper) issue.
    /// </summary>
    Critical = -3,
    /// <summary>
    /// The associated log message is about an error.
    /// </summary>
    Error = -2,
    /// <summary>
    /// The associated log message is about a warning.
    /// </summary>
    Warning = -1,
    /// <summary>
    /// The associated log message is just informational.
    /// </summary>
    Information = 0,
    /// <summary>
    /// The associated log message is for detailed tracing.
    /// </summary>
    Trace = 1,
    /// <summary>
    /// The associated log message is for debugging.
    /// </summary>
    Debug = 2,
    /// <summary>
    /// The associated log message is the most detailed message possible.
    /// </summary>
    Verbose = 3,
}
/// <summary>
/// An interface that abstracts a simple logging service.
/// </summary>
public interface IAmbientLogger
{
    /// <summary>
    /// Logs the specified message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Log(string message);
    /// <summary>
    /// Flushes the log messages to the logger service.
    /// </summary>
    ValueTask Flush(CancellationToken cancel = default);
}
/// <summary>
/// An interface that abstracts a structured logging service.
/// </summary>
public interface IAmbientStructuredLogger : IAmbientLogger
{
    /// <summary>
    /// Logs the specified structured data, rendering it into whatever format is appropriate.
    /// </summary>
    /// <param name="structuredData">Structured data to log, for example an anonymous type or a dictionary with string keys and stringizable objects as entries.</param>
    void Log(object structuredData);
}
