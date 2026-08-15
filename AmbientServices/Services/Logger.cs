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
/// <remarks>
/// <pitch>The minimal line-oriented logging abstraction: hand it fully-rendered message strings and flush when you need them delivered.  Filtering, leveling, and formatting are deliberately out of scope — helpers like <see cref="AmbientLogger"/> do those above this interface, so realizations stay trivial to write.</pitch>
/// <pledge>
/// This is the line-logging Pledge.  Each logged message is a complete, fully-rendered line; the logger applies no filtering, leveling, or formatting of its own.  Logging is expected to buffer rather than block on I/O — a message is only guaranteed to have reached the underlying target (to whatever degree the realization promises persistence at all) after a flush that began after the message was logged completes.
/// Messages may be logged concurrently from any thread or async context; realizations must be thread-safe.  A flush delivers the messages logged before it began; messages logged concurrently with or after the start of a flush may remain buffered.
/// </pledge>
/// <priority>
/// 1. Never delaying the caller over guaranteeing delivery: logging buffers and returns, and a message is only guaranteed delivered after a flush that began after it was logged.  A realization that wrote synchronously would satisfy every signature here and is still the wrong shape, because a logger is not allowed to change how long the code it observes takes to run. (public)
/// 2. Trivial realizations over a rich interface: filtering, leveling, and formatting stay above this interface in helpers such as <see cref="AmbientLogger"/>, so a new sink costs two methods to write.  The price is that every realization receives fully-rendered lines it has no cheap way to suppress. (public)
/// </priority>
/// </remarks>
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
/// <remarks>
/// <pitch>The structured counterpart to <see cref="IAmbientLogger"/>: hand it a data object (an anonymous type or a string-keyed dictionary) instead of a pre-rendered line, so realizations that understand structure (JSON sinks, log aggregators) can index the fields rather than parse a string.</pitch>
/// <pledge>
/// This is the structured-data Pledge.  Each logged entry is an arbitrary non-null object whose public properties (or dictionary entries) carry the data; the realization renders it into whatever format its target understands — line-oriented realizations typically flatten to a summary plus JSON.  The logger applies no filtering or leveling of its own.
/// Logging is expected to buffer rather than block on I/O; delivery guarantees, thread-safety, and flush semantics are the same as the line-logging Pledge: entries may be logged concurrently from any thread or async context, and an entry is only guaranteed delivered after a flush that began after it was logged completes.
/// </pledge>
/// </remarks>
public interface IAmbientStructuredLogger
{
    /// <summary>
    /// Logs the specified structured data, rendering it into whatever format is appropriate.
    /// </summary>
    /// <param name="structuredData">Structured data to log, for example an anonymous type or a dictionary with string keys and stringizable objects as entries.</param>
    void Log(object structuredData);
    /// <summary>
    /// Flushes the log messages to the logger service.
    /// </summary>
    ValueTask Flush(CancellationToken cancel = default);
}
/// <summary>
/// Writes log lines that exceeded in-memory buffer capacity.
/// Override locally with <see cref="AmbientService{T}.ScopedLocalOverride"/> (or <see cref="ScopedLocalServiceOverride{T}"/>) in unit tests, or replace the global implementation for custom sinks.
/// </summary>
/// <remarks>
/// <pitch>The last-resort sink: when an in-memory log buffer hits its capacity limit, overflowed lines go here instead of being silently dropped or ballooning memory.  Most callers never touch this directly — the buffering helpers route to it automatically.</pitch>
/// <pledge>
/// Writing an overflow line must never throw back to the logging caller — this interface is invoked from inside logging paths where an exception would recurse into logging or take down the logger, so realizations swallow their own failures.  Lines may be written concurrently from any thread.
/// A flush pushes any buffered output to the underlying target; file-based realizations additionally close their open writer so the file can be read externally.
/// </pledge>
/// </remarks>
public interface IAmbientLogOverflowWriter
{
    /// <summary>
    /// Appends a single overflow log line. Implementations must not throw back to logging callers.
    /// </summary>
    /// <param name="line">The line to append.</param>
    void WriteOverflowLine(string line);

    /// <summary>
    /// Flushes buffered output. File-based implementations also close their open writer so the file can be read.
    /// </summary>
    void Flush();
}