using System;
using System.IO;
using System.Text;

namespace AmbientServices;

/// <summary>
/// Default <see cref="IAmbientLogOverflowWriter"/> that appends overflow lines to a file under local application data
/// (same folder convention as <see cref="AmbientFileLogger"/>).
/// Uses a single long-lived <see cref="StreamWriter"/> per instance to avoid recursion through ambient loggers and per-line open/close cost.
/// </summary>
/// <remarks>
/// <pitch>The default place overflowed log lines land: an append-only file under local application data, so a logging burst that overruns the in-memory buffers leaves a durable trace instead of vanishing.</pitch>
/// <pledge><see cref="IAmbientLogOverflowWriter"/></pledge>
/// <pledge><see cref="IDisposable"/></pledge>
/// <pledge>
/// All lines from one instance are appended to a single fixed file — the path given at construction, or the default overflow path (executable name plus an overflow suffix, in the same folder <see cref="AmbientFileLogger"/> uses).  Existing file content is preserved across instances and process restarts.
/// Flushing (or disposing) closes the open writer so the file can be read externally; a later write transparently reopens it.
/// </pledge>
/// <plan>
/// A single lazily-created <see cref="StreamWriter"/> (UTF-8, auto-flush) over a <see cref="FileStream"/> opened with <see cref="FileMode.Append"/> and <see cref="FileShare.ReadWrite"/>, guarded by a private lock; the directory is created on demand.  Every write and close swallows all exceptions, honoring the never-throw term of the overflow-writer Pledge — deliberately writing directly to the file rather than through any ambient logger to avoid recursion.
/// Trade-off profile: durable and simple at the cost of a lock and a synchronous write per line; acceptable because overflow is an exceptional condition, not the hot logging path.
/// </plan>
/// </remarks>
[DefaultAmbientService(typeof(IAmbientLogOverflowWriter))]
public sealed class AmbientFileLogOverflowWriter : IAmbientLogOverflowWriter, IDisposable
{
    private readonly object _writeLock = new();
    private readonly string _overflowLogFilePath;
    private StreamWriter? _writer;

    /// <summary>
    /// Constructs a writer that uses the standard local application data overflow log path.
    /// </summary>
    public AmbientFileLogOverflowWriter()
        : this(DefaultOverflowLogFilePath)
    {
    }

    /// <summary>
    /// Constructs a writer that appends to the specified file path.
    /// </summary>
    /// <param name="overflowLogFilePath">The full path of the overflow log file.</param>
    public AmbientFileLogOverflowWriter(string overflowLogFilePath)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(overflowLogFilePath);
#else
        if (overflowLogFilePath is null) throw new ArgumentNullException(nameof(overflowLogFilePath));
#endif
        _overflowLogFilePath = overflowLogFilePath;
    }

    /// <summary>
    /// Gets the default overflow log file path (same folder convention as <see cref="AmbientFileLogger"/>).
    /// </summary>
    public static string DefaultOverflowLogFilePath
    {
        get
        {
            string prefix = AmbientFileLogger.CombineRelativeFilePrefixWithProgramData(
                AmbientFileLogger.GetExecutableName() + "_AmbientLogBufferOverflow",
                AmbientFileLogger.GetProgramDataFolderLocationInternal,
                AmbientFileLogger.GetExecutableName);
            return prefix + ".log";
        }
    }

    /// <inheritdoc />
    public void WriteOverflowLine(string line)
    {
        if (line == null) return;
        try
        {
            lock (_writeLock)
            {
                GetOrCreateWriter().WriteLine(line);
            }
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        lock (_writeLock)
        {
            CloseWriter();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Flush();

    private StreamWriter GetOrCreateWriter()
    {
        if (_writer != null)
        {
            return _writer;
        }

        string? directory = Path.GetDirectoryName(_overflowLogFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FileStream stream = new(_overflowLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        return _writer;
    }

    private void CloseWriter()
    {
        if (_writer == null)
        {
            return;
        }

        StreamWriter writer = _writer;
        _writer = null;
        try
        {
            writer.Flush();
            writer.Dispose();
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
        }
    }
}
