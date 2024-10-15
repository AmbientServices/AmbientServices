﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// A delegate that can be used to render a simple log message.
/// </summary>
/// <param name="utcNow">The timestamp for the log (in UTC), which the renderer may choose to add to the log information or not.</param>
/// <param name="level">The <see cref="AmbientLogLevel"/> of the log entry.</param>
/// <param name="structuredData">The structured data to be logged.</param>
/// <param name="ownerType">The optional name of the type that owns the log source.</param>
/// <param name="category">The optional log entry category.</param>
/// <returns>A simple log message to be given to one or more ambient <see cref="IAmbientLogger"/>s.</returns>
public delegate string LogMessageRenderer(DateTime utcNow, AmbientLogLevel level, object structuredData, string? ownerType = null, string? category = null);

/// <summary>
/// A delegate that can be used to render log data.
/// </summary>
/// <param name="utcNow">The timestamp for the log (in UTC), which the renderer may choose to add to the log information or not.</param>
/// <param name="level">The <see cref="AmbientLogLevel"/> of the log entry.</param>
/// <param name="structuredData">The structured data to be logged.</param>
/// <param name="ownerType">The optional name of the type that owns the log source.</param>
/// <param name="category">The optional log entry category.</param>
/// <returns>A structured log entry to be given to one or more ambient <see cref="IAmbientStructuredLogger"/>s.</returns>
public delegate object LogEntryRenderer(DateTime utcNow, AmbientLogLevel level, object structuredData, string? ownerType = null, string? category = null);

/// <summary>
/// An interface that can be implemented by <see cref="Exception"/> classes that provides exception-specific information that should be added to log entries for the errors related to that exception.
/// </summary>
public interface IExceptionLogInformation
{
    /// <summary>
    /// Gets an enumeration of special key-value pairs that should be added to the log entry.
    /// </summary>
    IEnumerable<(string Key, object Value)> LogInformation { get; }
}

/// <summary>
/// A type-specific logging class.  The name of the type is prepended to each log message.
/// When the log target requires I/O (as it usually will), the log messages should be buffered asynchronously so that only the flush has to wait for I/O.
/// While this isn't the most basic logging interface, using it can be as simple as just passing in a string, or as detailed as needed with anonymous structured objects.  
/// As code complexity grows over time, more and more details are usually logged, so this interface provides a way to do that.
/// Log filtering is generally done centrally, so it does not need to be abstracted or ambient and should be done by using settings or by calling into the logger service directly.
/// Categories are used primarily for filtering and may or may not be inserted as part of the message string (depending on the configured format string), so if you want them to be part of the structured data instead or as well, you will need to include it there outside of this interface.
/// To more efficiently handle filtering, this class only provides conditional access to <see cref="AmbientFilteredLogger"/> instances that can be used to actually do the message/object logging.
/// This avoids using generating complicated log data when the messages are set to be filtered anyway by returning a null <see cref="AmbientFilteredLogger"/> when the log is going to be filtered anyway.
/// For example, in C#, you simply write something like this:
/// <code>Logger.Filtered()?.Log(string.Join(",", MessageListToLog()), new { List = StructuredListToLog() });</code>
/// In this scenraio, MessageListToLog() and StructuredListToLog() will not be called at all when the logging is being filtered because the Filtered() function will return null, and the compiler automatically builds code that conditionally bypasses the construction of the parameters for Log() because of that null.
/// </summary>
public class AmbientLogger
{
    private static readonly IAmbientSetting<string> _MessageFormatString = AmbientSettings.GetAmbientSetting(nameof(AmbientLogger) + "-Format", "A format string for log messages with arguments as follows: 0: the DateTime of the event, 1: The AmbientLogLevel, 2: The logger owner type name, 3: The category, 4: the log message.", s => s, "{0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}");
    private static readonly AmbientService<IAmbientLogger> _AmbientSimpleLogger = Ambient.GetService<IAmbientLogger>();
    private static readonly AmbientService<IAmbientStructuredLogger> _AmbientLogger = Ambient.GetService<IAmbientStructuredLogger>();
    private static readonly JsonSerializerOptions DefaultSerializer = InitDefaultSerializerOptions();
    private static JsonSerializerOptions InitDefaultSerializerOptions()
    {
        JsonSerializerOptions options = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        return options;
    }

    private readonly string _typeName;
    private readonly bool _useLocalLogger;
    private readonly IAmbientLogger? _simpleLogger;
    private readonly IAmbientStructuredLogger? _logger;
    private readonly AmbientLogFilter _logFilter;

    private IAmbientLogger? DynamicSimpleLogger => _useLocalLogger ? _AmbientSimpleLogger.Local : _simpleLogger;
    private IAmbientStructuredLogger? DynamicLogger => _useLocalLogger ? _AmbientLogger.Local : _logger;

    private LogMessageRenderer? _simpleRenderer;
    private LogEntryRenderer? _renderer;

    /// <summary>
    /// Constructs an AmbientLogger using the ambient logger and ambient settings set.
    /// </summary>
    /// <param name="type">The type doing the logging.</param>
    public AmbientLogger(Type type)
        : this(type, null, null)
    {
        _useLocalLogger = true;
    }
    /// <summary>
    /// Constructs an AmbientLogger with the specified logger and settings set.
    /// </summary>
    /// <param name="type">The type doing the logging.</param>
    /// <param name="logger">The <see cref="IAmbientLogger"/> to use for the logging.</param>
    /// <param name="structuredLogger">The <see cref="IAmbientStructuredLogger"/> to use for the logging.</param>
    /// <param name="loggerSettingsSet">A <see cref="IAmbientSettingsSet"/> from which the settings should be queried.</param>
    public AmbientLogger(Type type, IAmbientLogger? logger, IAmbientStructuredLogger? structuredLogger, IAmbientSettingsSet? loggerSettingsSet = null)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null) throw new ArgumentNullException(nameof(type));
#endif
        _typeName = type!.Name;
        _simpleLogger = logger;
        _logger = structuredLogger;
        _logFilter = (loggerSettingsSet == null) ? AmbientLogFilter.Default : new AmbientLogFilter(_typeName, loggerSettingsSet);
    }
    /// <summary>
    /// Gets or sets the <see cref="LogMessageRenderer"/> which renders log message strings for <see cref="IAmbientLogger"/>s.
    /// If null, the default renderer is used.
    /// </summary>
    public LogMessageRenderer? MessageRenderer
    {
        get
        {
            return _simpleRenderer;
        }
        set
        {
            Interlocked.Exchange(ref _simpleRenderer, value);
        }
    }
    /// <summary>
    /// Gets or sets the <see cref="LogEntryRenderer"/> which is the last step to alter message and/or structure data before it is sent to the ambient <see cref="IAmbientLogger"/>.
    /// If null, the default renderer is used.
    /// </summary>
    public LogEntryRenderer? Renderer
    {
        get
        {
            return _renderer;
        }
        set
        {
            Interlocked.Exchange(ref _renderer, value);
        }
    }
    /// <summary>
    /// Checks to see if the specified level (with no category) should be filtered.  If not, returns a logger that can be used to log specific data.
    /// </summary>
    /// <param name="level">The <see cref="AmbientLogLevel"/> to check.  Defaults to <see cref="AmbientLogLevel.Information"/>.</param>
    /// <returns>An optional <see cref="AmbientFilteredLogger"/>, which can be conditionally called into for logging.  Null if the specified level, the corresponding type, or the null category are being filtered.</returns>
    public AmbientFilteredLogger? Filter(AmbientLogLevel level = AmbientLogLevel.Information)
    {
        return Filter(null, level);
    }
    /// <summary>
    /// Checks to see if the specified category and level should be filtered.  If not, returns a logger that can be used to log specific data.
    /// </summary>
    /// <param name="categoryName">The optional category name.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> to check.  Defaults to <see cref="AmbientLogLevel.Information"/>.</param>
    /// <returns>An optional <see cref="AmbientFilteredLogger"/>, which can be conditionally called into for logging.  Null if the specified level, the corresponding type, or the null category are being filtered.</returns>
    public AmbientFilteredLogger? Filter(string? categoryName, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        IAmbientLogger? simpleLogger = DynamicSimpleLogger;
        IAmbientStructuredLogger? logger = DynamicLogger;
        if ((simpleLogger == null && logger == null) || _logFilter.IsBlocked(level, _typeName, null)) return null;
        return new AmbientFilteredLogger(this, level, categoryName);
    }

    /// <summary>
    /// Augments <paramref name="anonymous"/> with standard structured data for an error.  This is useful for logging structured data with an error.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="anonymous">The anonymous object to convert to a dictionary and add the error information to.</param>
    /// <returns>The dictionary with the error information added.</returns>
    public static Dictionary<string, object?> AugmentStructuredDataWithExceptionInformation(Exception ex, object anonymous)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(anonymous);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
        if (anonymous is null) throw new ArgumentNullException(nameof(anonymous));
#endif
        Dictionary<string, object?> dictionary = StructuredDataToDictionary(anonymous);
        CopyStructuredDataToDictionary(dictionary, new ErrorLogInfo(GetErrorType(ex), ex.Message, ex.StackTrace));
        if (ex is IExceptionLogInformation exli)
        {
            // loop through key-value pairs explicitly exposed by the exception for the purpose of logging
            foreach ((string key, object value) in exli.LogInformation)
            {
                dictionary[key] = value;
            }
        }
        return dictionary;
    }
    /// <summary>
    /// Logs an exception with standard structured data.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> that caused the error and whose information should be added to the structured data before logging..</param>
    /// <param name="contextDescription">A message to identify the context of where the exception occured.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> identifiying the severity of the information.</param>
    public void Error(Exception ex, string? contextDescription = null, AmbientLogLevel level = AmbientLogLevel.Error)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        Dictionary<string, object?> dictionary = AugmentStructuredDataWithExceptionInformation(ex, new { });
        if (contextDescription != null) CopyStructuredDataToDictionary(dictionary, new LogSummaryInfo(contextDescription));
        Filter(level)?.Log(dictionary);
    }
    private const string ExceptionSuffix = "Exception";
    private static string GetErrorType(Exception error)
    {
        string errorType = error.GetType().Name;
        if (errorType.EndsWith(ExceptionSuffix, StringComparison.Ordinal)) errorType = errorType.Substring(0, errorType.Length - ExceptionSuffix.Length);
        return errorType;
    }
    internal void LogFiltered(AmbientLogLevel level, string? categoryName, object structuredData)
    {
        IAmbientLogger? simpleLogger = DynamicSimpleLogger;
        if (simpleLogger != null)
        {
            string message = ConvertStructuredDataIntoSimpleMessage(level, categoryName, structuredData);
            simpleLogger.Log(message);
        }
        IAmbientStructuredLogger? logger = DynamicLogger;
        if (logger != null)
        {
            // by the time we get here, we have already determined that no filtering should be done, so we can just log the data
            LogEntryRenderer renderer = _renderer ?? DefaultRenderer;
            structuredData = renderer(DateTime.UtcNow, level, structuredData, _typeName, categoryName);
            logger.Log(structuredData);
        }
    }
    /// <summary>
    /// Converts a structured data log entry (possibly an anonymous object) into a dictionary.
    /// </summary>
    /// <param name="structuredData">The structured object.</param>
    /// <returns>The dictionary containing the properties and values in the structured data object.</returns>
    public static Dictionary<string, object?> StructuredDataToDictionary(object structuredData)
    {
        return CopyStructuredDataToDictionary(new(), structuredData);
    }
    /// <summary>
    /// Copies the data in a structured data log entry (possibly an anonymous object) into a dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to add the properties and values to.</param>
    /// <param name="structuredData">The structured object.</param>
    /// <returns>The dictionary containing the properties and values in the structured data object.</returns>
    public static Dictionary<string, object?> CopyStructuredDataToDictionary(Dictionary<string, object?> dictionary, object structuredData)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(structuredData);
#else
        if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
        if (structuredData is null) throw new ArgumentNullException(nameof(structuredData));
#endif
        if (structuredData is string sds) CopyStructuredDataToDictionary(dictionary, new LogSummaryInfo(sds));
        else if (structuredData is Dictionary<string, object?> sdd) return sdd;
        else
        {
            foreach (PropertyInfo property in structuredData.GetType().GetProperties())
            {
                object? propertyValue = property.GetValue(structuredData);
                dictionary[property.Name] = propertyValue;
            }
        }
        return dictionary;
    }
    /// <summary>
    /// Converts the specified structured data into a simple log message.
    /// </summary>
    /// <param name="level">The <see cref="AmbientLogLevel"/> in case the renderer needs it.</param>
    /// <param name="categoryName">The optional name of the category.</param>
    /// <param name="structuredData">The structured data object.</param>
    /// <returns>The simple log message</returns>
    public string ConvertStructuredDataIntoSimpleMessage(AmbientLogLevel level, string? categoryName, object structuredData)
    {
        // by the time we get here, we have already determined that no filtering should be done, so we can just log the data
        LogMessageRenderer renderer = _simpleRenderer ?? DefaultMessageRenderer;
        string message = renderer(DateTime.UtcNow, level, structuredData, _typeName, categoryName);
        return message;
    }
    /// <summary>
    /// Renders the structured data into a simple log entry.
    /// </summary>
    /// <param name="structuredData">The structured data (often an anonymous object).</param>
    /// <param name="summaryStructuredDelimiter">The delimiter to use between the summary data and the structured data.</param>
    /// <returns>The simple log entry string.</returns>
    public static string ConvertStructuredDataIntoSimpleMessage(object structuredData, string summaryStructuredDelimiter = "|")
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(structuredData);
#else
        if (structuredData is null) throw new ArgumentNullException(nameof(structuredData));
#endif
        string summary;
        string structuredEntry;
        if (structuredData is string sds)
        {
            summary = sds;
            structuredEntry = "";
        }
        else
        {
            // look for a "Summary" property to use as a summary.  We remove this below so it's not redundant
            PropertyInfo? summaryProperty = structuredData.GetType().GetProperty(nameof(LogSummaryInfo.Summary), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            summary = (summaryProperty == null) ? "" : summaryProperty.GetValue(structuredData)?.ToString() ?? "";
            Dictionary<string, object?> dict = StructuredDataToDictionary(structuredData);
            dict.Remove(nameof(LogSummaryInfo.Summary));
            structuredData = dict;
            structuredEntry = summaryStructuredDelimiter + JsonSerializer.Serialize(structuredData, DefaultSerializer);
        }
        return summary + structuredEntry;
    }
    /// <summary>
    /// Renders a simple log message from the specified structured data.
    /// </summary>
    /// <param name="utcNow">The timestamp for the log (in UTC), which the renderer may choose to add to the log information or not.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> of the log entry.</param>
    /// <param name="structuredData">The structured data to be logged.</param>
    /// <param name="ownerType">The optional name of the type that owns the log source.</param>
    /// <param name="category">The optional log entry category.</param>
    /// <returns>The rendered message.</returns>
    public static string DefaultMessageRenderer(DateTime utcNow, AmbientLogLevel level, object structuredData, string? ownerType = null, string? category = null)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(structuredData);
#else
        if (structuredData is null) throw new ArgumentNullException(nameof(structuredData));
#endif
        string summary;
        string entry;
        if (structuredData is string sds)
        {
            summary = sds;
            entry = "";
        }
        else
        {
            // look for a "Summary" property to use as a summary.  We remove this below so it's not redundant
            PropertyInfo? summaryProperty = structuredData.GetType().GetProperty(nameof(LogSummaryInfo.Summary), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            summary = (summaryProperty == null) ? "" : summaryProperty.GetValue(structuredData)?.ToString() ?? "";
            Dictionary<string, object?> dict = StructuredDataToDictionary(structuredData);
            dict.Remove(nameof(LogSummaryInfo.Summary));
            entry = JsonSerializer.Serialize(dict, DefaultSerializer);
        }
        string ownerTypePart = string.IsNullOrEmpty(ownerType) ? "" : $":{ownerType}";
        string entryPart = string.IsNullOrEmpty(entry) ? "" : $"{Environment.NewLine}{entry}";
        string renderedMessage = $"{utcNow:yyMMdd HHmmss.fff} [{level}{ownerTypePart}] {summary}{entryPart}";
        return renderedMessage;
    }
    private static object DefaultRenderer(DateTime utcNow, AmbientLogLevel level, object structuredData, string? ownerType = null, string? category = null)
    {
        Dictionary<string, object?> dict = new();
        // add in the standard log entry properties
        CopyStructuredDataToDictionary(dict, new StandardRequestLogInfo(utcNow, level, ownerType, category));
        // look for additional context-specific data to add to the log entry (request-tracking information, for example)
        foreach ((string key, object value) in AmbientLogContext.ContextLogPairs.Reverse())
        {
            dict[key] = value;
        }
        // add in any data from the structuredData object
        CopyStructuredDataToDictionary(dict, structuredData);
        return dict;
    }


    // deprecated functions

    internal void LogDeprecated(string message, string? category = null, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        if (!_logFilter.IsBlocked(level, _typeName, category))
        {
            if (!string.IsNullOrEmpty(category)) category += ":";
            message = string.Format(System.Globalization.CultureInfo.InvariantCulture, _MessageFormatString.Value, DateTime.UtcNow, level, _typeName, category, message);
            DynamicSimpleLogger!.Log(message);  // the calling of this method is short-circuited when DynamicLogger is null
        }
    }
    /// <summary>
    /// Logs the specified message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="category">The (optional) category to attach to the message.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
    [Obsolete("Use more natrual and efficient Filter(...).Log(...) or a custom extension method now")]
    public void Log(string message, string? category = null, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        if (DynamicSimpleLogger == null) return;
        LogDeprecated(message, category, level);
    }
    /// <summary>
    /// Logs the message returned by the delegate.
    /// </summary>
    /// <param name="messageLambda">A delegate that creates a message.</param>
    /// <param name="category">The (optional) category to attach to the message.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
    [Obsolete("Use more natrual and efficient Filter(...).Log(...) or a custom extension method now")]
    public void Log(Func<string> messageLambda, string? category = null, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        if (DynamicSimpleLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(messageLambda);
#else
        if (messageLambda is null) throw new ArgumentNullException(nameof(messageLambda));
#endif
        LogDeprecated(messageLambda(), category, level);
    }
    /// <summary>
    /// Logs the specified exception.
    /// </summary>
    /// <param name="ex">An <see cref="Exception"/> to log.</param>
    /// <param name="category">The (optional) category to attach to the message.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
    [Obsolete("Use more natrual and efficient Filter(...).Log(...) or a custom extension method now")]
    public void Log(Exception ex, string? category = null, AmbientLogLevel level = AmbientLogLevel.Error)
    {
        if (DynamicSimpleLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        LogDeprecated(ex.ToString(), category, level);
    }
    /// <summary>
    /// Logs the specified message and exception.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
    /// <param name="category">The (optional) category to attach to the message.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
    [Obsolete("Use more natrual and efficient Filter(...).Log(...) or a custom extension method now")]
    public void Log(string message, Exception ex, string? category = null, AmbientLogLevel level = AmbientLogLevel.Error)
    {
        if (DynamicSimpleLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        LogDeprecated(message + Environment.NewLine + ex.ToString(), category, level);
    }
    /// <summary>
    /// Logs the specified message (returned by a delegate) and exception.
    /// </summary>
    /// <param name="messageLambda">A delegate that creates a message.</param>
    /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
    /// <param name="category">The (optional) category to attach to the message.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
    [Obsolete("Use more natrual and efficient Filter(...).Log(...) or a custom extension method now")]
    public void Log(Func<string> messageLambda, Exception ex, string? category = null, AmbientLogLevel level = AmbientLogLevel.Error)
    {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        if (DynamicSimpleLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(messageLambda);
#else
        if (messageLambda is null) throw new ArgumentNullException(nameof(messageLambda));
#endif
        LogDeprecated(messageLambda() + Environment.NewLine + ex.ToString(), category, level);
    }
}
/// <summary>
/// A class that allows construction of a log entry after already determining that the log entry should not be filtered.
/// Instances of this class are returned by <see cref="AmbientLogger"/> only when filtering for the specified type, category, and level is not in place.
/// </summary>
public class AmbientFilteredLogger
{
    private readonly AmbientLogger _logger;
    private readonly AmbientLogLevel _level;
    private readonly string? _categoryName;

    /// <summary>
    /// Constructs an AmbientFilteredLogger for a category that belongs to the specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="AmbientLogger"/> that generated this filtered logger.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the logging.</param>
    /// <param name="categoryName">The optional name of the category.</param>
    internal AmbientFilteredLogger(AmbientLogger logger, AmbientLogLevel level, string? categoryName)
    {
        _logger = logger;
        _level = level;
        _categoryName = string.IsNullOrEmpty(categoryName) ? "" : $"{categoryName}:";
    }

    /// <summary>
    /// Logs the specified structured log data.
    /// </summary>
    /// <param name="structuredData">Structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    public void Log(object structuredData)
    {
        _logger.LogFiltered(_level, _categoryName, structuredData);
    }
    /// <summary>
    /// Logs the specified structured log data, adding the standard data from the specified exception to the structured data in the process.
    /// </summary>
    /// <param name="structuredData">Structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    /// <param name="ex">An <see cref="Exception"/> whose data should be added to the log entry.</param>
    public void Log(object structuredData, Exception ex)
    {
        structuredData = AmbientLogger.AugmentStructuredDataWithExceptionInformation(ex, structuredData);
        _logger.LogFiltered(_level, _categoryName, structuredData);
    }
}
/// <summary>
/// A generic type-specific logging class.  The name of the type is prepended to each log message.
/// When the log target requires I/O (as it usually will), the log messages should be buffered asynchronously so that only the flush has to wait for I/O.
/// Note that some functions take a delegate-generating string rather than a string.  This is to be used when computation of the string is expensive so that in scenarios where the log message is getting filtered anyway, that expense is not incurred.
/// While this isn't the most basic logging interface, using it can be as simple as just passing in a string.  
/// As code complexity grows over time, more and more details are usually logged, so this interface provides a way to do that.
/// Log filtering is generally done centrally, so it does not need to be abstracted or ambient and should be done by using settings or by calling into the logger service directly.
/// </summary>
/// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
public class AmbientLogger<TOWNER> : AmbientLogger
{
    /// <summary>
    /// Constructs an AmbientLogger using the ambient logger and ambient settings set.
    /// </summary>
    public AmbientLogger()
        : base(typeof(TOWNER))
    {
    }
    /// <summary>
    /// Constructs an AmbientLogger with the specified logger and settings set.
    /// </summary>
    /// <param name="logger">The <see cref="IAmbientLogger"/> to use for the logging.</param>
    /// <param name="structuredLogger">An optional <see cref="IAmbientStructuredLogger"/> to use for the logging.</param>
    /// <param name="loggerSettingsSet">A <see cref="IAmbientSettingsSet"/> from which the settings should be queried.</param>
    public AmbientLogger(IAmbientLogger? logger, IAmbientStructuredLogger? structuredLogger = null, IAmbientSettingsSet? loggerSettingsSet = null)
        : base (typeof(TOWNER), logger, structuredLogger, loggerSettingsSet)
    {
    }
}
internal class AmbientLogFilter
{
    private static readonly AmbientLogFilter _Default = new("Default");
    /// <summary>
    /// Gets the default log filter.
    /// </summary>
    public static AmbientLogFilter Default => _Default;

    private readonly string _name;
    private readonly IAmbientSetting<AmbientLogLevel> _logLevelSetting;
    private readonly IAmbientSetting<Regex?> _typeAllowSetting;
    private readonly IAmbientSetting<Regex?> _typeBlockSetting;
    private readonly IAmbientSetting<Regex?> _categoryAllowSetting;
    private readonly IAmbientSetting<Regex?> _categoryBlockSetting;

    public AmbientLogFilter(string name)
        : this (name, null)
    {
    }
    internal AmbientLogFilter(string name, IAmbientSettingsSet? settingsSet)
    {
        _name = name;
        _logLevelSetting = AmbientSettings.GetSetting(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-LogLevel", "The AmbientLogLevel above which events should not be logged.  The default value is AmbientLogLevel.Information.", AmbientLogLevel.Information, s => (AmbientLogLevel)Enum.Parse(typeof(AmbientLogLevel), s));
        _typeAllowSetting = AmbientSettings.GetSetting(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-TypeAllow", "A regular expression indicating which logger owner types should be allowed.  Blocks takes precedence over allows.  The default value is null, which allows all types.", null, s => new Regex(s, RegexOptions.Compiled));
        _typeBlockSetting = AmbientSettings.GetSetting(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-TypeBlock", "A regular expression indicating which logger owner types should be blocked.  Blocks takes precedence over allows.  The default value is null, which blocks no types.", null, s => new Regex(s, RegexOptions.Compiled));
        _categoryAllowSetting = AmbientSettings.GetSetting(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-CategoryAllow", "A regular expression indicating which categories should be allowed.  Blocks takes precedence over allows.  The default value is null, which allows all categories.", null, s => new Regex(s, RegexOptions.Compiled));
        _categoryBlockSetting = AmbientSettings.GetSetting(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", "A regular expression indicating which categories should be blocked.  Blocks takes precedence over allows.  The default value is null, which blocks no categories.", null, s => new Regex(s, RegexOptions.Compiled));
    }
    internal string Name => _name;
    internal AmbientLogLevel LogLevel => _logLevelSetting.Value;

    internal bool IsTypeBlocked(string typeName)
    {
        System.Diagnostics.Debug.Assert(typeName != null);
        bool blocked = _typeBlockSetting.Value?.IsMatch(typeName) ?? false;
        if (blocked) return true;
        bool allowed = _typeAllowSetting.Value?.IsMatch(typeName) ?? true;
        return !allowed;
    }
    internal bool IsCategoryBlocked(string? categoryName)
    {
        categoryName ??= "";
        bool blocked = _categoryBlockSetting.Value?.IsMatch(categoryName) ?? false;
        if (blocked) return true;
        bool allowed = _categoryAllowSetting.Value?.IsMatch(categoryName) ?? true;
        return !allowed;
    }
    internal bool IsLevelBlocked(AmbientLogLevel level)
    {
        if (level > _logLevelSetting.Value) return true;
        return false;
    }
    internal bool IsBlocked(AmbientLogLevel level, string typeName, string? categoryName)
    {
        if (level > _logLevelSetting.Value) return true;
        if (IsTypeBlocked(typeName)) return true;
        if (IsCategoryBlocked(categoryName)) return true;
        return false;
    }
}
record struct StandardRequestLogInfo(DateTime Timestamp, AmbientLogLevel Level, string? OwnerType, string? Category);
record struct ErrorLogInfo(string ErrorType, string ErrorMessage, string? ErrorStackTrace);
record struct LogSummaryInfo(string? Summary);

