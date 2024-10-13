using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestClass]
    public class TestLogger
    {
        private static readonly AmbientService<IAmbientLogger> _Logger = Ambient.GetService<IAmbientLogger>(out _Logger);
        private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>(out _SettingsSet);

        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryAllow()
        {
            BasicAmbientSettingsSet settings = new(nameof(LogFilterTypeCategoryAllow));
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllow) + "-" + nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllow) + "-" + nameof(AmbientLogFilter) + "-TypeAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllow) + "-" + nameof(AmbientLogFilter) + "-TypeBlock", null);
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllow) + "-" + nameof(AmbientLogFilter) + "-CategoryAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllow) + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", null);
            AmbientLogFilter filter = new(nameof(LogFilterTypeCategoryAllow), settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsFalse(filter.IsTypeBlocked("testallow"));
            Assert.IsTrue(filter.IsTypeBlocked("test"));
            Assert.IsFalse(filter.IsCategoryBlocked("testallow"));
            Assert.IsTrue(filter.IsCategoryBlocked("test"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryBlock()
        {
            BasicAmbientSettingsSet settings = new(nameof(LogFilterTypeCategoryBlock));
            settings.ChangeSetting(nameof(LogFilterTypeCategoryBlock) + "-" + nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(LogFilterTypeCategoryBlock) + "-" + nameof(AmbientLogFilter) + "-TypeAllow", null);
            settings.ChangeSetting(nameof(LogFilterTypeCategoryBlock) + "-" + nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryBlock) + "-" + nameof(AmbientLogFilter) + "-CategoryAllow", null);
            settings.ChangeSetting(nameof(LogFilterTypeCategoryBlock) + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new(nameof(LogFilterTypeCategoryBlock), settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsFalse(filter.IsTypeBlocked("test"));
            Assert.IsTrue(filter.IsTypeBlocked("testblock"));
            Assert.IsFalse(filter.IsCategoryBlocked("test"));
            Assert.IsTrue(filter.IsCategoryBlocked("testblock"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterTypeCategoryAllowBlock()
        {
            BasicAmbientSettingsSet settings = new(nameof(LogFilterTypeCategoryAllowBlock));
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllowBlock) + "-" + nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllowBlock) + "-" + nameof(AmbientLogFilter) + "-TypeAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllowBlock) + "-" + nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllowBlock) + "-" + nameof(AmbientLogFilter) + "-CategoryAllow", ".*[Aa]llow.*");
            settings.ChangeSetting(nameof(LogFilterTypeCategoryAllowBlock) + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new(nameof(LogFilterTypeCategoryAllowBlock), settings);
            Assert.AreEqual(AmbientLogLevel.Information, filter.LogLevel);
            Assert.IsTrue(filter.IsTypeBlocked("test"));
            Assert.IsTrue(filter.IsTypeBlocked("testblock"));
            Assert.IsFalse(filter.IsTypeBlocked("testallow"));
            Assert.IsTrue(filter.IsCategoryBlocked("test"));
            Assert.IsTrue(filter.IsCategoryBlocked("testblock"));
            Assert.IsFalse(filter.IsCategoryBlocked("testallow"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LogFilterBlock()
        {
            BasicAmbientSettingsSet settings = new(nameof(LogFilterBlock));
            settings.ChangeSetting(nameof(LogFilterBlock) + "-" + nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Information.ToString());
            settings.ChangeSetting(nameof(LogFilterBlock) + "-" + nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settings.ChangeSetting(nameof(LogFilterBlock) + "-" + nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogFilter filter = new(nameof(LogFilterBlock), settings);
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
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerDefault()
        {
            AmbientLogger logger = new(typeof(TestLogger));
            logger.Error(new ApplicationException());
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new ApplicationException().ToString());
            logger.Filter()?.Log("test message");
            logger.Filter("category", AmbientLogLevel.Information)?.Log("test message");
            logger.Error(new ApplicationException(), "Exception during test");
            logger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test" + new ApplicationException().ToString());
            if (_Logger.Global != null) await _Logger.Global.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerBasic()
        {
            using BasicAmbientLogger bl = new();
            using (new ScopedLocalServiceOverride<IAmbientLogger>(bl))
            {
                AmbientLogger logger = new(typeof(TestLogger));
                logger.Error(new ApplicationException());
                logger.Filter("category", AmbientLogLevel.Information)?.Log(new ApplicationException().ToString());
                logger.Filter()?.Log("test message");
                logger.Filter("category", AmbientLogLevel.Information)?.Log("test message");
                logger.Filter()?.Log("Exception during test", new ApplicationException());
                logger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test" + new ApplicationException().ToString());
                if (_Logger.Global != null) await _Logger.Global.Flush();
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LoggerNone()
        {
            using (new ScopedLocalServiceOverride<IAmbientLogger>(null))
            {
                AmbientLogger<TestLogger> logger = new();
                logger.Error(new ApplicationException());
                logger.Filter("category", AmbientLogLevel.Information)?.Log(new ApplicationException().ToString());
                logger.Filter()?.Log("test message");
                logger.Filter("category", AmbientLogLevel.Information)?.Log("test message");
                logger.Filter()?.Log("Exception during test", new ApplicationException());
                logger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test" + new ApplicationException().ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerExplicitSettings()
        {
            BasicAmbientSettingsSet settingsSet = new("LoggerSettingsTest");
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Error.ToString());
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-TypeBlock", ".*[Bb]lock.*");
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryBlock", ".*[Bb]lock.*");
            AmbientLogger<AllowedLoggerType> logger = new(_Logger.Global, settingsSet);
            logger.Error(new ApplicationException());
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new ApplicationException().ToString());
            logger.Filter()?.Log("test message");
            logger.Filter("category", AmbientLogLevel.Information)?.Log("test message");
            logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log("test message");
            logger.Error(new ApplicationException(), "Exception during test");
            logger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            logger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            if (_Logger.Global != null) await _Logger.Global.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public async Task LoggerSettings()
        {
            BasicAmbientSettingsSet settingsSet = new("LoggerSettingsTest");
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-LogLevel", AmbientLogLevel.Error.ToString());
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-TypeFilter", "AllowedLoggerType");
            settingsSet.ChangeSetting(nameof(AmbientLogFilter) + "-CategoryFilter", "AllowedCategory");
            using ScopedLocalServiceOverride<IAmbientSettingsSet> o = new(settingsSet);
            AmbientLogger<AllowedLoggerType> logger = new();
            logger.Error(new ApplicationException());

            AmbientLogger<TestLogger> testlogger = new();
            testlogger.Error(new ApplicationException());
            testlogger.Filter("category", AmbientLogLevel.Information)?.Log(new ApplicationException().ToString());
            testlogger.Filter()?.Log("test message");
            testlogger.Filter("category", AmbientLogLevel.Information)?.Log("test message");
            testlogger.Error(new ApplicationException(), "Exception during test");
            testlogger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            testlogger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            testlogger.Filter("category", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());
            testlogger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log("Exception during test", new ApplicationException());

            if (_Logger.Local != null) await _Logger.Local.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LoggerArgumentExceptions()
        {
            AmbientLogger<TestLogger> logger = new(_Logger.Global);
            Exception nullException = null;
            Assert.ThrowsException<ArgumentNullException>(() => new AmbientLogger(null!));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Warning(nullException!));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Error(nullException!));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Critical(nullException!));
        }
    }
    class AllowedLoggerType { }
}
