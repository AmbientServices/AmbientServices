using System;
using System.IO;
using System.Text;

namespace AmbientServices;

/// <summary>
/// Writes log lines that exceed in-memory buffer limits directly to a file at the standard local application data path.
/// Uses a single long-lived <see cref="StreamWriter"/> (not ambient loggers) to avoid recursion and per-line open/close cost.
/// </summary>
internal static class AmbientLogOverflowWriter
{
    private static readonly object _writeLock = new();
    private static string? _overflowFilePath;
    private static StreamWriter? _writer;

    /// <summary>
    /// Gets the overflow log file path (same folder convention as <see cref="AmbientFileLogger"/>).
    /// </summary>
    internal static string GetOverflowLogFilePath()
    {
        string prefix = AmbientFileLogger.CombineRelativeFilePrefixWithProgramData(
            AmbientFileLogger.GetExecutableName() + "_AmbientLogBufferOverflow",
            AmbientFileLogger.GetProgramDataFolderLocationInternal,
            AmbientFileLogger.GetExecutableName);
        return prefix + ".log";
    }

    /// <summary>
    /// Appends a single line to the overflow log file.
    /// </summary>
    public static void WriteLine(string line)
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
            // best-effort overflow sink; never throw back to logging callers
        }
    }

    private static StreamWriter GetOrCreateWriter()
    {
        if (_writer != null) return _writer;

        string path = _overflowFilePath ??= GetOverflowLogFilePath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        return _writer;
    }

    private static void CloseWriter()
    {
        lock (_writeLock)
        {
            if (_writer == null) return;
            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
            }
            _writer = null;
        }
    }

    /// <summary>
    /// Resets the cached overflow path and closes any open writer (for unit tests).
    /// </summary>
    internal static void ResetCachedPathForTesting()
    {
        CloseWriter();
        _overflowFilePath = null;
    }

    /// <summary>
    /// Overrides the overflow log file path and closes any open writer (for unit tests).
    /// </summary>
    internal static void SetOverflowFilePathForTesting(string path)
    {
        CloseWriter();
        _overflowFilePath = path;
    }
}
