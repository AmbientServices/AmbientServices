using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientLoggerProvider"/>.
    /// </summary>
    [TestClass]
    public class TestLogger
    {
        private static readonly ServiceAccessor<IAmbientLoggerProvider> _LoggerProvider = Service.GetAccessor<IAmbientLoggerProvider>(out _LoggerProvider);
        private static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsProvider = Service.GetAccessor<IAmbientSettingsProvider>(out _SettingsProvider);

        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryAllow()
        {
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider();
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", null);
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", null);
            AmbientLogFilter filter = new AmbientLogFilter(settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsFalse(filter.IsTypeBlocked("testallow"));
            Assert.IsTrue(filter.IsTypeBlocked("test"));
            Assert.IsFalse(filter.IsCategoryBlocked("testallow"));
            Assert.IsTrue(filter.IsCategoryBlocked("test"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryBlock()
        {
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider();
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeAllow", null);
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryAllow", null);
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new AmbientLogFilter(settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsFalse(filter.IsTypeBlocked("test"));
            Assert.IsTrue(filter.IsTypeBlocked("testblock"));
            Assert.IsFalse(filter.IsCategoryBlocked("test"));
            Assert.IsTrue(filter.IsCategoryBlocked("testblock"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryAllowBlock()
        {
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider();
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new AmbientLogFilter(settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsTrue(filter.IsTypeBlocked("test"));
            Assert.IsTrue(filter.IsTypeBlocked("testblock"));
            Assert.IsFalse(filter.IsTypeBlocked("testallow"));
            Assert.IsTrue(filter.IsCategoryBlocked("test"));
            Assert.IsTrue(filter.IsCategoryBlocked("testblock"));
            Assert.IsFalse(filter.IsCategoryBlocked("testallow"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterBlock()
        {
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider();
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new AmbientLogFilter(settings);
            Assert.IsFalse(filter.IsBlocked(AmbientLogLevel.Information, "test", "test"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "test", "test"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Information, "testBlock", "test"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "testBlock", "test"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Information, "test", "testBlock"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "test", "testBlock"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Information, "testBlock", "testBlock"));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "testBlock", "testBlock"));

            Assert.IsFalse(filter.IsBlocked(AmbientLogLevel.Information, "test", null));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "test", null));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Information, "testBlock", null));
            Assert.IsTrue(filter.IsBlocked(AmbientLogLevel.Trace, "testBlock", null));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerDefault()
        {
            AmbientLogger<TestLogger> logger = new AmbientLogger<TestLogger>();
            logger.Log(new ApplicationException());
            logger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
            logger.Log("test message");
            logger.Log(() => "test message");
            logger.Log("test message", "category", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException());
            logger.Log(() => "Exception during test", new ApplicationException());
            logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
            await _LoggerProvider.GlobalProvider.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LoggerNone()
        {
            using (new LocalServiceScopedOverride<IAmbientLoggerProvider>(null))
            {
                AmbientLogger<TestLogger> logger = new AmbientLogger<TestLogger>();
                logger.Log(new ApplicationException());
                logger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
                logger.Log("test message");
                logger.Log(() => "test message");
                logger.Log("test message", "category", AmbientLogLevel.Information);
                logger.Log("Exception during test", new ApplicationException());
                logger.Log(() => "Exception during test", new ApplicationException());
                logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerExplicitSettings()
        {
            BasicAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider("LoggerSettingsTest");
            settingsProvider.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Error.ToString());
            settingsProvider.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settingsProvider.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogger<AllowedLoggerType> logger = new AmbientLogger<AllowedLoggerType>(_LoggerProvider.GlobalProvider, settingsProvider);
            logger.Log(new ApplicationException());
            logger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
            logger.Log("test message");
            logger.Log(() => "test message");
            logger.Log("test message", "category", AmbientLogLevel.Information);
            logger.Log("test message", "AllowedCategory", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException());
            logger.Log(() => "Exception during test", new ApplicationException());
            logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException(), "AllowedCategory", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException(), "AllowedCategory", AmbientLogLevel.Information);
            await _LoggerProvider.GlobalProvider.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerSettings()
        {
            BasicAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider("LoggerSettingsTest");
            settingsProvider.ChangeSetting(nameof(BasicAmbientLogger) + "-LogLevel", AmbientLogLevel.Error.ToString());
            settingsProvider.ChangeSetting(nameof(BasicAmbientLogger) + "-TypeFilter", "AllowedLoggerType");
            settingsProvider.ChangeSetting(nameof(BasicAmbientLogger) + "-CategoryFilter", "AllowedCategory");
            using (LocalServiceScopedOverride<IAmbientSettingsProvider> o = new LocalServiceScopedOverride<IAmbientSettingsProvider>(settingsProvider))
            {
                AmbientLogger<AllowedLoggerType> logger = new AmbientLogger<AllowedLoggerType>();
                logger.Log(new ApplicationException());

                AmbientLogger<TestLogger> testlogger = new AmbientLogger<TestLogger>();
                testlogger.Log(new ApplicationException());
                testlogger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
                testlogger.Log("test message");
                testlogger.Log(() => "test message");
                testlogger.Log("test message", "category", AmbientLogLevel.Information);
                testlogger.Log("test message", "AllowedCategory", AmbientLogLevel.Information);
                testlogger.Log("Exception during test", new ApplicationException());
                testlogger.Log(() => "Exception during test", new ApplicationException());
                testlogger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
                testlogger.Log("Exception during test", new ApplicationException(), "AllowedCategory", AmbientLogLevel.Information);
                testlogger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
                testlogger.Log("Exception during test", new ApplicationException(), "AllowedCategory", AmbientLogLevel.Information);

                await _LoggerProvider.LocalProvider.Flush();
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLoggerProvider"/>.
        /// </summary>
        [TestMethod]
        public void LoggerArgumentExceptions()
        {
            AmbientLogger<TestLogger> logger = new AmbientLogger<TestLogger>(_LoggerProvider.GlobalProvider);
            Func<string> nullLambda = null;
            Exception nullException = null;
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullException, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log("message", nullException, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda, (Exception)null, "category", AmbientLogLevel.Information));
        }
    }
    class AllowedLoggerType { }
}
