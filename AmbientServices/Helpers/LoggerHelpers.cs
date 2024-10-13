using System;
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
    /// Logs the specified warning exception unless it (or the previosly-specified type) is set to be filtered.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> that occured.</param>
    /// <param name="prefixMessage">An optional string to be prefixed to the error message.</param>
    public void Warning(Exception ex, string? prefixMessage = null)
    {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        IAmbientLogger? logger = DynamicLogger;
        if (logger == null || _logFilter.IsBlocked(AmbientLogLevel.Warning, _typeName, null)) return;
        string messagePrefix = string.IsNullOrEmpty(prefixMessage) ? "" : prefixMessage + Environment.NewLine;
        new AmbientFilteredLogger(logger, AmbientLogLevel.Warning, _typeName, null).Log(messagePrefix + ex.ToString());
    }
    /// <summary>
    /// Logs the specified exception unless it (or the previosly-specified type) is set to be filtered.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> that occured.</param>
    /// <param name="prefixMessage">An optional string to be prefixed to the error message.</param>
    public void Error(Exception ex, string? prefixMessage = null)
    {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        IAmbientLogger? logger = DynamicLogger;
        if (logger == null || _logFilter.IsBlocked(AmbientLogLevel.Error, _typeName, null)) return;
        string messagePrefix = string.IsNullOrEmpty(prefixMessage) ? "" : prefixMessage + Environment.NewLine;
        new AmbientFilteredLogger(logger, AmbientLogLevel.Error, _typeName, null).Log(messagePrefix + ex.ToString());
    }
    /// <summary>
    /// Logs the specified critical exception unless it (or the previosly-specified type) is set to be filtered.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> that occured.</param>
    /// <param name="prefixMessage">An optional string to be prefixed to the error message.</param>
    public void Critical(Exception ex, string? prefixMessage = null)
    {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(ex);
#else
        if (ex is null) throw new ArgumentNullException(nameof(ex));
#endif
        IAmbientLogger? logger = DynamicLogger;
        if (logger == null || _logFilter.IsBlocked(AmbientLogLevel.Error, _typeName, null)) return;
        string messagePrefix = string.IsNullOrEmpty(prefixMessage) ? "" : prefixMessage + Environment.NewLine;
        new AmbientFilteredLogger(logger, AmbientLogLevel.Error, _typeName, null).Log(messagePrefix + ex.ToString());
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

    private void InnerLog(string? message = null, object? structuredData = null)
    {
        // by the time we get here, we have already determined that no filtering should be done, so we can just log the data
        message = string.Format(System.Globalization.CultureInfo.InvariantCulture, _MessageFormatString.Value, DateTime.UtcNow, _level, _typeName, _categoryName, message);
        _logger.Log(message, structuredData);  // the calling of this method is short-circuited when DynamicLogger is null
    }
    /// <summary>
    /// Logs the specified log message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Log(string message)
    {
        InnerLog(message);
    }
    /// <summary>
    /// Logs the specified structured log data.
    /// </summary>
    /// <param name="structuredData">Structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    public void LogStructured(object structuredData)
    {
        InnerLog(null, structuredData);
    }
    /// <summary>
    /// Logs the specified log message and structured log data.
    /// </summary>
    /// <param name="message">An optional message to log.</param>
    /// <param name="structuredData">Optional structured data to log, usually either an anonymous type or a dictionary of name-value pairs to log.</param>
    public void Log(string? message, object? structuredData)
    {
        InnerLog(message, structuredData);
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
