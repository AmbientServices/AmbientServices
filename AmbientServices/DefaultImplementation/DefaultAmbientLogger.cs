using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A basic implementation of <see cref="IAmbientLogger"/> that writes log messages to a rotating set of files.
    /// Turn the logger off for maximum performance.
    /// </summary>
    [DefaultAmbientService]
    public class DefaultAmbientLogger : IAmbientLogger, IAmbientStructuredLogger
    {
        /// <summary>
        /// Constructs a default ambient file logger that writes logs to the system trace log.
        /// </summary>
        public DefaultAmbientLogger()
        {
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
            string message = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(structuredData);
            Log(message);
        }
        /// <summary>
        /// Buffers the specified message to be asynchronously logged.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            System.Diagnostics.Trace.WriteLine(message);
        }
        /// <summary>
        /// Flushes everything that has been previously logged to the appropriate file on disk.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public ValueTask Flush(CancellationToken cancel = default)
        {
            return default(ValueTask);
        }
    }
}
