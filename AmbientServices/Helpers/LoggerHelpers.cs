using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AmbientServices;

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
    internal static readonly AmbientService<IAmbientLogger> _AmbientLogger = Ambient.GetService<IAmbientLogger>();
    internal static readonly IAmbientSetting<string> _MessageFormatString = AmbientSettings.GetAmbientSetting(nameof(AmbientLogger) + "-Format", "A format string for log messages with arguments as follows: 0: the DateTime of the event, 1: The AmbientLogLevel, 2: The logger owner type name, 3: The category, 4: the log message.", s => s, "{0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}");

    private readonly string _typeName;
    private readonly bool _useLocalLogger;
    private readonly IAmbientLogger? _logger;
    private readonly AmbientLogFilter _logFilter;

    private IAmbientLogger? DynamicLogger => _useLocalLogger ? _AmbientLogger.Local : _logger;

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
    /// <param name="loggerSettingsSet">A <see cref="IAmbientSettingsSet"/> from which the settings should be queried.</param>
    public AmbientLogger(Type type, IAmbientLogger? logger, IAmbientSettingsSet? loggerSettingsSet = null)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null) throw new ArgumentNullException(nameof(type));
#endif
        _typeName = type!.Name;
        _logger = logger;
        _logFilter = (loggerSettingsSet == null) ? AmbientLogFilter.Default : new AmbientLogFilter(_typeName, loggerSettingsSet);
    }
    /// <summary>
    /// Checks to see if the specified level (with no category) should be filtered.  If not, returns a logger that can be used to log specific data.
    /// </summary>
    /// <param name="level">The <see cref="AmbientLogLevel"/> to check.  Defaults to <see cref="AmbientLogLevel.Information"/>.</param>
    /// <returns>An optional <see cref="AmbientFilteredLogger"/>, which can be conditionally called into for logging.  Null if the specified level, the corresponding type, or the null category are being filtered.</returns>
    public AmbientFilteredLogger? Filter(AmbientLogLevel level = AmbientLogLevel.Information)
    {
        IAmbientLogger? logger = DynamicLogger;
        if (logger == null || _logFilter.IsBlocked(level, _typeName, null)) return null;
        return new AmbientFilteredLogger(logger, level, _typeName, null);
    }
    /// <summary>
    /// Checks to see if the specified category and level should be filtered.  If not, returns a logger that can be used to log specific data.
    /// </summary>
    /// <param name="categoryName">The optional category name.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> to check.  Defaults to <see cref="AmbientLogLevel.Information"/>.</param>
    /// <returns>An optional <see cref="AmbientFilteredLogger"/>, which can be conditionally called into for logging.  Null if the specified level, the corresponding type, or the null category are being filtered.</returns>
    public AmbientFilteredLogger? Filter(string? categoryName, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        IAmbientLogger? logger = DynamicLogger;
        if (logger == null || _logFilter.IsBlocked(level, _typeName, categoryName)) return null;
        return new AmbientFilteredLogger(logger, level, _typeName, categoryName);
    }

    /// <summary>
    /// Augments <paramref name="anonymous"/> with standard structured data for an error.  This is useful for logging structured data with an error.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="anonymous">The anonymous object to convert to a dictionary and add the error information to.</param>
    /// <returns>The dictionary with the error information added.</returns>
    public static Dictionary<string, object?> AugmentStructuredDataWithErrorInformation(Exception ex, object anonymous)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(anonymous);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
        if (anonymous is null) throw new ArgumentNullException(nameof(anonymous));
#endif
        Dictionary<string, object?> dictionary = AnonymousObjectToDictionary(anonymous);
        dictionary["ErrorType"] = GetErrorType(ex);
        dictionary["ErrorMessage"] = ex.Message;
        dictionary["ErrorStack"] = ex.StackTrace;
        return dictionary;
    }
    /// <summary>
    /// Logs an exception with standard structured data.
    /// </summary>
    /// <param name="logger">The logger to log to.</param
    /// <param name="errorPrefix">A prefix to add to the log message.</param>
    /// <param name="anonymous">The anonymous object to convert to a JSON string.</param>
    /// <param name="ex">An optional <see cref="Exception"/> whose information should be added to the anonymous object before logging.  If null, calls the other logger and ignores the level if it is <see cref="AmbientLogLevel.Error"/.></param>
    public void Error(Exception ex, string? message = null, AmbientLogLevel level = AmbientLogLevel.Error)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        Dictionary<string, object?> dictionary = AugmentStructuredDataWithErrorInformation(ex, new { });
        if (message != null) dictionary["Message"] = message;
        Filter(level)?.Log(dictionary);
    }
    private const string ExceptionSuffix = "Exception";
    private static string GetErrorType(Exception error)
    {
        string errorType = error.GetType().Name;
        if (errorType.EndsWith(ExceptionSuffix, StringComparison.Ordinal)) errorType = errorType.Substring(0, errorType.Length - ExceptionSuffix.Length);
        return errorType;
    }
    private static Dictionary<string, object?> AnonymousObjectToDictionary(object anonymous)
    {
        Dictionary<string, object?> dictionary = new();
        if (anonymous is string) dictionary["Message"] = anonymous;
        else
        {
            foreach (var property in anonymous.GetType().GetProperties())
            {
                dictionary[property.Name] = property.GetValue(anonymous);
            }
        }
        return dictionary;
    }


    // deprecated functions

    internal void InnerLog(string message, string? category = null, AmbientLogLevel level = AmbientLogLevel.Information)
    {
        if (!_logFilter.IsBlocked(level, _typeName, category))
        {
            if (!string.IsNullOrEmpty(category)) category += ":";
            message = string.Format(System.Globalization.CultureInfo.InvariantCulture, _MessageFormatString.Value, DateTime.UtcNow, level, _typeName, category, message);
            DynamicLogger!.Log(message);  // the calling of this method is short-circuited when DynamicLogger is null
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
        if (DynamicLogger == null) return;
        InnerLog(message, category, level);
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
        if (DynamicLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(messageLambda);
#else
        if (messageLambda is null) throw new ArgumentNullException(nameof(messageLambda));
#endif
        InnerLog(messageLambda(), category, level);
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
        if (DynamicLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        InnerLog(ex.ToString(), category, level);
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
        if (DynamicLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        InnerLog(message + Environment.NewLine + ex.ToString(), category, level);
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
        if (DynamicLogger == null) return;
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(messageLambda);
#else
        if (messageLambda is null) throw new ArgumentNullException(nameof(messageLambda));
#endif
        InnerLog(messageLambda() + Environment.NewLine + ex.ToString(), category, level);
    }
}
/// <summary>
/// A class that allows construction of a log entry after already determining that the log entry should not be filtered.
/// Instances of this class are returned by <see cref="AmbientLogger"/> only when filtering for the specified type, category, and level is not in place.
/// </summary>
public class AmbientFilteredLogger
{
    internal static readonly IAmbientSetting<string> _MessageFormatString = AmbientSettings.GetAmbientSetting(nameof(AmbientLogger) + "-Format", "A format string for log messages with arguments as follows: 0: the DateTime of the event, 1: The AmbientLogLevel, 2: The logger owner type name, 3: The category, 4: the log message.", s => s, "{0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}");

    private readonly IAmbientLogger _logger;
    private readonly AmbientLogLevel _level;
    private readonly string _typeName;
    private readonly string? _categoryName;

    /// <summary>
    /// Constructs an AmbientFilteredLogger for a category that belongs to the specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="IAmbientLogger"/> the raw log messages should be sent to after being constructed.</param>
    /// <param name="level">The <see cref="AmbientLogLevel"/> for the logging.</param>
    /// <param name="typeName">The name of the type that owns the log messages.</param>
    /// <param name="categoryName">The optional name of the category.</param>
    internal AmbientFilteredLogger(IAmbientLogger logger, AmbientLogLevel level, string typeName, string? categoryName)
    {
        _logger = logger;
        _level = level;
        _typeName = typeName;
        _categoryName = string.IsNullOrEmpty(categoryName) ? "" : $"{categoryName}:";
    }

    private void InnerLog(object? structuredData = null, string ? message = null)
    {
        // by the time we get here, we have already determined that no filtering should be done, so we can just log the data
        message = string.Format(System.Globalization.CultureInfo.InvariantCulture, _MessageFormatString.Value, DateTime.UtcNow, _level, _typeName, _categoryName, message);
        _logger.Log(message, structuredData);  // the calling of this method is short-circuited when DynamicLogger is null
    }
    /// <summary>
    /// Logs the specified log message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void LogMessage(string message)
    {
        InnerLog(message);
    }
    /// <summary>
    /// Logs the specified structured log data.
    /// </summary>
    /// <param name="structuredData">Structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    /// <param name="message">An optional message to log.</param>
    public void Log(object structuredData, string? message = null)
    {
        if (structuredData is string) InnerLog(new { Message = structuredData }, message);
        else InnerLog(structuredData, message);
    }
    /// <summary>
    /// Logs the specified structured log data, adding the standard data from the specified exception to the structured data in the process.
    /// </summary>
    /// <param name="structuredData">Structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    /// <param name="ex">An <see cref="Exception"/> whose data should be added to the log entry.</param>
    /// <param name="message">An optional message to log.</param>
    public void Log(object structuredData, Exception ex, string? message = null)
    {
        structuredData = AmbientLogger.AugmentStructuredDataWithErrorInformation(ex, structuredData);
        InnerLog(structuredData, message);
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
    /// <param name="loggerSettingsSet">A <see cref="IAmbientSettingsSet"/> from which the settings should be queried.</param>
    public AmbientLogger(IAmbientLogger? logger, IAmbientSettingsSet? loggerSettingsSet = null)
        : base (typeof(TOWNER), logger, loggerSettingsSet)
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
        _logLevelSetting = AmbientSettings.GetSetting<AmbientLogLevel>(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-LogLevel", "The AmbientLogLevel above which events should not be logged.  The default value is AmbientLogLevel.Information.", AmbientLogLevel.Information, s => (AmbientLogLevel)Enum.Parse(typeof(AmbientLogLevel), s));
        _typeAllowSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-TypeAllow", "A regular expression indicating which logger owner types should be allowed.  Blocks takes precedence over allows.  The default value is null, which allows all types.", null, s => new Regex(s, RegexOptions.Compiled));
        _typeBlockSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-TypeBlock", "A regular expression indicating which logger owner types should be blocked.  Blocks takes precedence over allows.  The default value is null, which blocks no types.", null, s => new Regex(s, RegexOptions.Compiled));
        _categoryAllowSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-CategoryAllow", "A regular expression indicating which categories should be allowed.  Blocks takes precedence over allows.  The default value is null, which allows all categories.", null, s => new Regex(s, RegexOptions.Compiled));
        _categoryBlockSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, name + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", "A regular expression indicating which categories should be blocked.  Blocks takes precedence over allows.  The default value is null, which blocks no categories.", null, s => new Regex(s, RegexOptions.Compiled));
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
