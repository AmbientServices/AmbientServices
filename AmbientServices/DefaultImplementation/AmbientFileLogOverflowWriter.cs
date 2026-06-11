using System;
using System.IO;
using System.Text;

namespace AmbientServices;

/// <summary>
/// Default <see cref="IAmbientLogOverflowWriter"/> that appends overflow lines to a file under local application data
/// (same folder convention as <see cref="AmbientFileLogger"/>).
/// Uses a single long-lived <see cref="StreamWriter"/> per instance to avoid recursion through ambient loggers and per-line open/close cost.
/// </summary>
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
