using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AmbientServices
{
    /// <summary>
    /// A type-specific logging class.  The name of the type is prepended to each log message.
    /// When the log target requires I/O (as it usually will), the log messages should be buffered asynchronously so that only the flush has to wait for I/O.
    /// Note that some functions take a delegate-generating string rather than a string.  This is to be used when computation of the string is expensive so that in scenarios where the log message is getting filtered anyway, that expense is not incurred.
    /// While this isn't the most basic logging interface, using it can be as simple as just passing in a string.  
    /// As code complexity grows over time, more and more details are usually logged, so this interface provides a way to do that.
    /// Log filtering is generally done centrally, so it does not need to be abstracted or ambient and should be done by using settings or by calling into the logger service directly.
    /// </summary>
    public class AmbientLogger
    {
        internal static readonly IAmbientSetting<string> _MessageFormatString = AmbientSettings.GetAmbientSetting(nameof(AmbientLogger) + "-Format", "A format string for log messages with arguments as follows: 0: the DateTime of the event, 1: The AmbientLogLevel, 2: The logger owner type name, 3: The category, 4: the log message.", s => s, "{0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}");
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
        internal bool IsBlocked(AmbientLogLevel level, string typeName, string? categoryName)
        {
            if (level > _logLevelSetting.Value) return true;
            if (IsTypeBlocked(typeName)) return true;
            if (IsCategoryBlocked(categoryName)) return true;
            return false;
        }
    }
}
