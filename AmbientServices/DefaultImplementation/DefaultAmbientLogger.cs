using AmbientServices.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A basic implementation of <see cref="IAmbientLogger"/> that writes log messages to a rotating set of files.
    /// Turn the logger off for maximum performance.
    /// </summary>
    [DefaultAmbientService]
    public class DefaultAmbientLogger : IAmbientLogger
    {
        private static readonly JsonSerializerOptions DefaultSerializer = InitDefaultSerializerOptions();
        private static JsonSerializerOptions InitDefaultSerializerOptions()
        {
            JsonSerializerOptions options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
            return options;
        }

        /// <summary>
        /// Constructs a default ambient file logger that writes logs to the system trace log.
        /// </summary>
        public DefaultAmbientLogger()
        {
        }
        /// <summary>
        /// Buffers the specified message to be asynchronously logged.
        /// </summary>
        /// <param name="message">An optional message to log.</param>
        /// <param name="structuredData">An optional structured data to log.</param>
        public void Log(string? message, object? structuredData = null)
        {
            System.Diagnostics.Trace.WriteLine(DefaultCombineLog(message, structuredData));
        }
        /// <summary>
        /// Flushes everything that has been previously logged to the appropriate file on disk.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public ValueTask Flush(CancellationToken cancel = default)
        {
            return default(ValueTask);
        }

        /// <summary>
        /// Combines a raw message and structure data together into a single string separated by a pipe character (with pipe characters in <paramref name="message"/> backslash encoded).
        /// </summary>
        /// <param name="message">An optional message to log.</param>
        /// <param name="structuredData">An optional structured data to log.</param>
        public static string DefaultCombineLog(string? message, object? structuredData = null)
        {
            bool needSeparator = true;
            string escapedMessage;
            if (message == null)
            {
                needSeparator = false;
                escapedMessage = "";
            }
            else
            {
                escapedMessage = message.ReplaceOrdinal("\\", "\\\\").ReplaceOrdinal("|", "\\|");
            }
            string structured;
            if (structuredData == null)
            {
                needSeparator = false;
                structured = "";
            }
            else
            {
                structured = JsonSerializer.Serialize(structuredData, DefaultSerializer);
            }
            return escapedMessage + (needSeparator ? "|" : "") + structured;
        }
    }
}
