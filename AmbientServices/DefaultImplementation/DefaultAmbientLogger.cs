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
    public class DefaultAmbientLogger : IAmbientLogger, IDisposable
    {
        private bool _disposedValue;                    // too small to need interlocking

        /// <summary>
        /// Constructs a default ambient file logger that writes logs to the system trace log.
        /// </summary>
        public DefaultAmbientLogger()
        {
        }
        /// <summary>
        /// Buffers the specified message to be asynchronously logged.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            System.Diagnostics.Trace.Write(message);
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
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing">Whether or not the instance is being disposed (as opposed to finalized).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~AmbientFileLogger()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change or override this method. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
