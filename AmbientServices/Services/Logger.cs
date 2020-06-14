using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An enumeration of levels for logging.
    /// </summary>
    public enum LogLevel
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
    /// An interface that abstracts a type-specific logging service.
    /// When the log target requires I/O (as it usually will), the log messages should be buffered asynchronously so that only the flush has to wait for I/O.
    /// Note that some functions take a delegate-generating string rather than a string.  This is to be used when computation of the string is expensive so that in scenarios where the log message is getting filtered anyway, that expense is not incurred.
    /// While this isn't the most basic logging interface, using it can be as simple as just passing in a string.  
    /// As code complexity grows over time, more and more details are usually logged, so this interface provides a way to do that.
    /// Log filtering is generally done centrally, so it does not need to be abstracted and ambient and should be done by using settings or by calling into the implementation directly.
    /// </summary>
    public interface ILogger<T>
    {
        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="LogLevel"/> for the message.</param>
        void Log(string message, string category = null, LogLevel level = LogLevel.Information);
        /// <summary>
        /// Logs the message returned by the delegate.
        /// </summary>
        /// <param name="message">A delegate that creates a message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="LogLevel"/> for the message.</param>
        void Log(Func<string> message, string category = null, LogLevel level = LogLevel.Information);
        /// <summary>
        /// Logs the specified exception.
        /// </summary>
        /// <param name="ex">An <see cref="Exception"/> to log.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="LogLevel"/> for the message.</param>
        void Log(Exception ex, string category = null, LogLevel level = LogLevel.Error);
        /// <summary>
        /// Logs the specified message and exception.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="LogLevel"/> for the message.</param>
        void Log(string message, Exception ex, string category = null, LogLevel level = LogLevel.Error);
        /// <summary>
        /// Logs the specified message (returned by a delegate) and exception.
        /// </summary>
        /// <param name="message">A delegate that creates a message.</param>
        /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="LogLevel"/> for the message.</param>
        void Log(Func<string> message, Exception ex, string category = null, LogLevel level = LogLevel.Error);
    }
    /// <summary>
    /// An interface that abstracts an ambient logging service.
    /// Logging should be buffered in performance-sensitive scenarios so that async processing is not necessary.
    /// </summary>
    public interface IAmbientLogger
    {
        /// <summary>
        /// Gets a <see cref="ILogger{T}"/> that may be used to log messages generated by the type.
        /// </summary>
        /// <typeparam name="T">The type doing the logging.</typeparam>
        /// <returns>A <see cref="ILogger{T}"/> implementation.</returns>
        ILogger<T> GetLogger<T>();
        /// <summary>
        /// Flushes the log messages to the target (wherever that may be).
        /// </summary>
        Task Flush(CancellationToken cancel = default(CancellationToken));
    }
}
