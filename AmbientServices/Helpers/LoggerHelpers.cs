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
    /// Log filtering is generally done centrally, so it does not need to be abstracted and ambient and should be done by using settings or by calling into the provider directly.
    /// </summary>
    /// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
    public class AmbientLogger<TOWNER>
    {
        private static readonly string TypeName = typeof(TOWNER).Name;

        private static readonly IAmbientSetting<string> _MessageFormatString = AmbientSettings.GetAmbientSetting("AmbientLogger-Format", null, "{0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}");
        private static readonly ServiceAccessor<IAmbientLoggerProvider> _LoggerProvider = Service.GetAccessor<IAmbientLoggerProvider>();
        private static readonly AmbientLogFilter _DefaultLogFilter = new AmbientLogFilter();

        private IAmbientLoggerProvider _provider;
        private AmbientLogFilter _logFilter;

        public AmbientLogger()
            : this (_LoggerProvider.LocalProvider, null)
        {
        }

        public AmbientLogger(IAmbientLoggerProvider logger, IAmbientSettingsProvider loggerSettingsProvider = null)
        {
            _provider = logger;
            _logFilter = (loggerSettingsProvider == null) ? _DefaultLogFilter : new AmbientLogFilter(loggerSettingsProvider);
        }

        private void InnerLog(string message, string category = null, AmbientLogLevel level = AmbientLogLevel.Information)
        {
            if (!_logFilter.IsBlocked(level, TypeName, category))
            {
                message = String.Format(System.Globalization.CultureInfo.InvariantCulture, _MessageFormatString.Value, DateTime.UtcNow, level, TypeName, category, message);
                _provider.Log(message);
            }
        }
        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
        public void Log(string message, string category = null, AmbientLogLevel level = AmbientLogLevel.Information)
        {
            if (_provider == null) return;
            InnerLog(message, category, level);
        }
        /// <summary>
        /// Logs the message returned by the delegate.
        /// </summary>
        /// <param name="messageLambda">A delegate that creates a message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
        public void Log(Func<string> messageLambda, string category = null, AmbientLogLevel level = AmbientLogLevel.Information)
        {
            if (_provider == null) return;
            if (messageLambda == null) throw new ArgumentNullException(nameof(messageLambda));
            InnerLog(messageLambda(), category, level);
        }
        /// <summary>
        /// Logs the specified exception.
        /// </summary>
        /// <param name="ex">An <see cref="Exception"/> to log.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
        public void Log(Exception ex, string category = null, AmbientLogLevel level = AmbientLogLevel.Error)
        {
            if (_provider == null) return;
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            InnerLog(ex.ToString(), category, level);
        }
        /// <summary>
        /// Logs the specified message and exception.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
        public void Log(string message, Exception ex, string category = null, AmbientLogLevel level = AmbientLogLevel.Error)
        {
            if (_provider == null) return;
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            InnerLog(message + Environment.NewLine + ex.ToString(), category, level);
        }
        /// <summary>
        /// Logs the specified message (returned by a delegate) and exception.
        /// </summary>
        /// <param name="messageLambda">A delegate that creates a message.</param>
        /// <param name="ex">An <see cref="Exception"/> to log.  The exception will be appended after the message.</param>
        /// <param name="category">The (optional) category to attach to the message.</param>
        /// <param name="level">The <see cref="AmbientLogLevel"/> for the message.</param>
        public void Log(Func<string> messageLambda, Exception ex, string category = null, AmbientLogLevel level = AmbientLogLevel.Error)
        {
            if (_provider == null) return;
            if (messageLambda == null) throw new ArgumentNullException(nameof(messageLambda));
            InnerLog(messageLambda() + Environment.NewLine + ex.ToString(), category, level);
        }
    }
    internal class AmbientLogFilter
    {
        private readonly IAmbientSetting<AmbientLogLevel> _logLevelSetting;
        private readonly IAmbientSetting<Regex> _typeAllowSetting;
        private readonly IAmbientSetting<Regex> _typeBlockSetting;
        private readonly IAmbientSetting<Regex> _categoryAllowSetting;
        private readonly IAmbientSetting<Regex> _categoryBlockSetting;

        public AmbientLogFilter()
            : this (null)
        {
        }
        internal AmbientLogFilter(IAmbientSettingsProvider settingsProvider)
        {
            _logLevelSetting = AmbientSettings.GetSetting<AmbientLogLevel>(settingsProvider, nameof(AmbientLogFilter) + "-LogLevel", s => (AmbientLogLevel)Enum.Parse(typeof(AmbientLogLevel), s), AmbientLogLevel.Information);
            _typeAllowSetting = AmbientSettings.GetSetting<Regex>(settingsProvider, nameof(AmbientLogFilter) + "-TypeAllow", s => new Regex(s, RegexOptions.Compiled));
            _typeBlockSetting = AmbientSettings.GetSetting<Regex>(settingsProvider, nameof(AmbientLogFilter) + "-TypeBlock", s => new Regex(s, RegexOptions.Compiled));
            _categoryAllowSetting = AmbientSettings.GetSetting<Regex>(settingsProvider, nameof(AmbientLogFilter) + "-CategoryAllow", s => new Regex(s, RegexOptions.Compiled));
            _categoryBlockSetting = AmbientSettings.GetSetting<Regex>(settingsProvider, nameof(AmbientLogFilter) + "-CategoryBlock", s => new Regex(s, RegexOptions.Compiled));
        }
        internal AmbientLogLevel LogLevel {  get { return _logLevelSetting.Value; } }

        internal bool IsTypeBlocked(string typeName)
        {
            System.Diagnostics.Debug.Assert(typeName != null);
            bool blocked = _typeBlockSetting.Value?.IsMatch(typeName) ?? false;
            if (blocked) return true;
            bool allowed = _typeAllowSetting.Value?.IsMatch(typeName) ?? true;
            return !allowed;
        }
        internal bool IsCategoryBlocked(string categoryName)
        {
            categoryName = categoryName ?? "";
            bool blocked = _categoryBlockSetting.Value?.IsMatch(categoryName) ?? false;
            if (blocked) return true;
            bool allowed = _categoryAllowSetting.Value?.IsMatch(categoryName) ?? true;
            return !allowed;
        }
        internal bool IsBlocked(AmbientLogLevel level, string typeName, string categoryName)
        {
            if (level > _logLevelSetting.Value) return true;
            if (IsTypeBlocked(typeName)) return true;
            if (IsCategoryBlocked(categoryName)) return true;
            return false;
        }
    }
}
