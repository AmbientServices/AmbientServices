using AmbientServices.Utilities;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A basic implementation of <see cref="IAmbientLogger"/> that writes log messages to a rotating set of files.
    /// Turn the logger off for maximum performance.
    /// </summary>
    [DefaultAmbientService]
    public class BasicAmbientLogger : IAmbientLogger, IDisposable
    {
        private readonly string _filePrefix;
        private readonly string _fileExtension;
        private readonly int _rotationPeriodMinutes;
        private readonly RotatingFileBuffer _fileBuffers;
        private int _periodNumber;                      // interlocked
        private bool _disposedValue;                    // too small to need interlocking

        /// <summary>
        /// Constructs a default ambient file logger that writes files that start with a default prefix.
        /// </summary>
        public BasicAmbientLogger() : this(null)
        {
        }
        /// <summary>
        /// Constructs an ambient file logger that writes files that start with the specified prefix.
        /// </summary>
        /// <param name="filePrefix">
        /// The path and filename prefix to use for the log files.  
        /// If not specified, the executing process's ProcessName or the application domain's friendly name will be used as the prefix.
        /// If specified without a full path, on windows the local application data folder on windows will be used as the path and on Linux /home/{Environment.UserName}/.local/share will be used as the path.
        /// </param>
        /// <param name="fileExtension">The file extension (with leading .) to use for the files.  Defaults to ".log".</param>
        /// <param name="rotationPeriodMinutes">The number of minutes after which a new file should be used.  Suffixes will roll over to zero at midnight UTC every day.  Defaults to 60 minutes.</param>
        /// <param name="autoFlushSeconds">The number of seconds between automatic flushes of the log file.  Zero or negative values will disable automatic flushing, so all log messages will be buffered until the log file is rotated or <see cref="Flush"/> is called explicitly.</param>
        public BasicAmbientLogger(string? filePrefix, string? fileExtension = null, int rotationPeriodMinutes = 60, int autoFlushSeconds = 5)
        {
            // file prefix not specified?
            if (filePrefix == null)
            {
                // use a default path that uses the executable name
                filePrefix = GetExecutableName();
            }
            // else if we have a file prefix, but it doesn't have a directory, use the program data location
            if (string.IsNullOrEmpty(Path.GetDirectoryName(filePrefix)))
            {
                filePrefix = Path.Combine(GetProgramDataFolderLocation(), filePrefix);
            }
            if (fileExtension == null) fileExtension = ".log";
            if (!fileExtension.StartsWith(".", StringComparison.Ordinal)) fileExtension = "." + fileExtension;
            _filePrefix = filePrefix;
            _fileExtension = fileExtension;
            _rotationPeriodMinutes = rotationPeriodMinutes;
            // which period number within the day are we in right now?
            _periodNumber = GetPeriodNumber(AmbientClock.UtcNow);
            // use that for the starting suffix
            string startingSuffix = PeriodString(_periodNumber);
            _fileBuffers = new RotatingFileBuffer(filePrefix, startingSuffix + _fileExtension, TimeSpan.FromSeconds(autoFlushSeconds));
        }
        private static string GetExecutableName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
                || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)
#endif
                )
            {
                return System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            }
            return System.AppDomain.CurrentDomain.FriendlyName;
        }
        private static string GetProgramDataFolderLocation()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath;
            // fixup on linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && string.IsNullOrEmpty(path))
            {
                folderPath = $"/home/{Environment.UserName}/.local/share";
                // make sure this directory exists
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                path = folderPath;
            }
            return path + Path.DirectorySeparatorChar;
        }
        /// <summary>
        /// Gets the file prefix.
        /// </summary>
        public string FilePrefix => _filePrefix;
        /// <summary>
        /// Buffers the specified message to be asynchronously logged.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            // which period number within the day are we in right now?
            TimeSpan timeOfDay = AmbientClock.UtcNow.TimeOfDay;
            int newPeriodNumber = (int)(timeOfDay.TotalMinutes / _rotationPeriodMinutes);

            int attempt = 0;
            // loop attempting to update the period number if we need to or until we win the race or timeout
            while (true)
            {
                // get the latest value
                int oldValue = _periodNumber;
                // someone beat us to it?
                if (newPeriodNumber == oldValue) break;
                // try to put in our value--did we win the race?
                if (oldValue == Interlocked.CompareExchange(ref _periodNumber, newPeriodNumber, oldValue))
                {
                    // we won the race to update the period number
                    _fileBuffers.BufferFileRotation(PeriodString(newPeriodNumber) + _fileExtension);
                    break;
                }
                // note that it's very difficult to test a miss here--you really have to pound it with multiple threads, so this next line (and the not equal condition on the "if" above are not likely to get covered
                if (!InterlockedUtilities.TryAgainAfterOptomisticMissDelay(attempt++)) break;
            }
            if (!_disposedValue) _fileBuffers.BufferLine(message);
        }
        /// <summary>
        /// Flushes everything that has been previously logged to the appropriate file on disk.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public ValueTask Flush(CancellationToken cancel = default)
        {
            return _disposedValue ? default : _fileBuffers.Flush(cancel);
        }
        private int GetPeriodNumber(DateTime dateTime)
        {
            // which period number within the day are we in right now?
            TimeSpan timeOfDay = dateTime.TimeOfDay;
            return (int)(timeOfDay.TotalMinutes / _rotationPeriodMinutes);
        }
        private static string PeriodString(int periodNumber)
        {
            return periodNumber.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }
        /// <summary>
        /// Gets the log file name for the specified time.
        /// </summary>
        /// <param name="dateTime">The time whose log filename should be constructed.</param>
        /// <returns>The filename for log messages logged at the specified time.</returns>
        internal string GetLogFileName(DateTime dateTime)
        {
            _periodNumber = GetPeriodNumber(dateTime);
            // use that for the starting suffix
            string suffix = PeriodString(_periodNumber) + _fileExtension;
            return _filePrefix + suffix;
        }
        /// <summary>
        /// Attempts to delete all log files using the specified file prefix.
        /// If they cannot be deleted, they are skipped.
        /// </summary>
        /// <param name="filePathPrefix">The file prefix (the same one that would be passed as the filePrefix parameter to the constructor).</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public static ValueTask TryDeleteAllFiles(string filePathPrefix, CancellationToken cancel = default)
        {
            string? directory = Path.GetDirectoryName(filePathPrefix);
            if (directory == null) throw new ArgumentException("The specified file path must be non-null and a non-root path!", nameof(filePathPrefix));
            string filename = Path.GetFileName(filePathPrefix)!;
            // clean up the files
            foreach (string file in Directory.GetFiles(directory, filename + "*.log"))
            {
                if (cancel.IsCancellationRequested) break;
                try
                {
                    File.Delete(file);
                }
#pragma warning disable CA1031  // we REALLY want to catch everything here--delete can throw a lot of different exceptions and we don't want ANY of them to make it through--this is a "do your best" function
                catch { }   // ignore all errors and just skip files we can't delete
#pragma warning restore CA1031
            }
            return default;
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
                    _fileBuffers.Dispose();
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

    /// <summary>
    /// A class to buffer log messages and write them asynchronously.
    /// </summary>
    internal class RotatingFileBuffer : IDisposable
    {
        private static readonly string _FlushString = Guid.NewGuid().ToString();
        private static readonly string _SwitchFilesPrefix = Guid.NewGuid().ToString() + ":";

        private readonly ConcurrentQueue<string> _queue = new();
        private readonly SemaphoreSlim _writeLock = new(1);
        private readonly AmbientEventTimer _timer;
        private readonly string _baselineFilename;
        private readonly string _startingSuffix;
        private TextWriter? _currentFileWriter;          // only accessed while the write lock is held
        private bool _disposedValue;

        /// <summary>
        /// Constructs a file buffers instance that uses the specified properties.
        /// </summary>
        /// <param name="baselineFilename">The baseline filename (full path).</param>
        /// <param name="startingSuffix">The starting suffix.</param>
        /// <param name="autoFlushFrequency">A <see cref="TimeSpan"/> indicating how often to autoflush the log files.</param>
        public RotatingFileBuffer(string baselineFilename, string startingSuffix, TimeSpan autoFlushFrequency)
        {
            _baselineFilename = baselineFilename;
            _startingSuffix = startingSuffix;
            if (autoFlushFrequency > TimeSpan.Zero)
            {
                _timer = new AmbientEventTimer(autoFlushFrequency) {
                    AutoReset = true
                };
                _timer.Elapsed += Timer_Elapsed;
                _timer.Enabled = true;
            }
            else // just create a default timer with nothing attached (so we still have something to dispose)
            {
                _timer = new AmbientEventTimer();
            }
        }

        private async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                await FlushInternal().ConfigureAwait(false);
            }
            // Coverage note: this code is pretty-much impossible to test because the time has to go off AFTER this instance is marked as disposed but before the timer is disposed (or at least before it's last invocation happens)
            catch (ObjectDisposedException)
            {
                // ignore this error (it can happen during the race to dispose)
            }
        }

        /// <summary>
        /// Buffer the specified line (this function should NOT block on I/O of any kind).
        /// </summary>
        /// <param name="line">The string to put into the log.</param>
        public void BufferLine(string line)
        {
            if (_disposedValue) throw new ObjectDisposedException(_baselineFilename);
            _queue.Enqueue(line);
        }
        /// <summary>
        /// Buffers an instruction to rotate files.
        /// </summary>
        /// <param name="newSuffix">The new filename suffix.</param>
        public void BufferFileRotation(string newSuffix)
        {
            if (_disposedValue) throw new ObjectDisposedException(_baselineFilename);
            _queue.Enqueue(_SwitchFilesPrefix + newSuffix);
        }
        /// <summary>
        /// Flushes any buffered data to the appropriate file(s).
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public async ValueTask Flush(CancellationToken cancel = default)
        {
            await FlushInternal(cancel).ConfigureAwait(false);
        }
        private async ValueTask FlushInternal(CancellationToken cancel = default)
        {
            if (_disposedValue) throw new ObjectDisposedException(_baselineFilename);
            // queue up a special message so we know when we have processed up to the current spot in the queue
            _queue.Enqueue(_FlushString);
            try
            {
                // make sure only one thread at a time processes the queue
                await _writeLock.WaitAsync(cancel).ConfigureAwait(false);
                // loop through the queue processing log lines until we get to that message
                string logString;
                while (_queue.TryDequeue(out logString!))   // while TryQueue *can* put null into logString, it can only do so if it returns false, and we don't use logString in that case
                {
                    // no file yet?
                    if (_currentFileWriter == null)
                    {
                        // open the starting file
                        await SwitchFiles(_startingSuffix).ConfigureAwait(false);
                    }
                    // time to switch files?
                    else if (logString.StartsWith(_SwitchFilesPrefix, StringComparison.Ordinal))
                    {
                        await SwitchFiles(logString.Substring(_SwitchFilesPrefix.Length)).ConfigureAwait(false);
                    }
                    // reached the flush command queued above?
                    else if (logString.Equals(_FlushString, StringComparison.Ordinal))
                    {
                        // stop here even if there are more messages (they were queued AFTER we started flushing)
                        break;
                    }
                    else // this is just a regular log string
                    {
                        await _currentFileWriter.WriteLineAsync(logString).ConfigureAwait(false);
                    }
                    cancel.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
        // NOTE: This function may only be called while the write lock is held
        private ValueTask SwitchFiles(string suffix)
        {
            string filename = _baselineFilename + suffix;
            // close the old file
            _currentFileWriter?.Close();
            // open a new one
            _currentFileWriter = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
            // this really SHOULD be async--why can't windows open files asynchronously still!
            return default;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposedValue = true;

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _timer.Dispose();
                    _writeLock.Dispose();
                    _currentFileWriter?.Dispose();
                    _currentFileWriter = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FileBuffers()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
        /// <summary>
        /// Disposes of the file buffers.  No more messages should be processed.  <see cref="Flush"/> should have been called after message processing is interrupted and before disposal.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
