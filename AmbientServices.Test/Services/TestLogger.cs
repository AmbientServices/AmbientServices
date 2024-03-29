﻿using AmbientServices;
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
            logger.Log(new ApplicationException());
            logger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
            logger.Log("test message");
            logger.Log(() => "test message");
            logger.Log("test message", "category", AmbientLogLevel.Information);
            logger.Log("Exception during test", new ApplicationException());
            logger.Log(() => "Exception during test", new ApplicationException());
            logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
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
                logger.Log(new ApplicationException());
                logger.Log(new ApplicationException(), "category", AmbientLogLevel.Information);
                logger.Log("test message");
                logger.Log(() => "test message");
                logger.Log("test message", "category", AmbientLogLevel.Information);
                logger.Log("Exception during test", new ApplicationException());
                logger.Log(() => "Exception during test", new ApplicationException());
                logger.Log("Exception during test", new ApplicationException(), "category", AmbientLogLevel.Information);
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
            logger.Log(new ApplicationException());

            AmbientLogger<TestLogger> testlogger = new();
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

            if (_Logger.Local != null) await _Logger.Local.Flush();
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public void LoggerArgumentExceptions()
        {
            AmbientLogger<TestLogger> logger = new(_Logger.Global);
            Func<string> nullLambda = null;
            Exception nullException = null;
            Assert.ThrowsException<ArgumentNullException>(() => new AmbientLogger(null!));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda!, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullException!, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log("message", nullException!, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(() => "message", nullException!, "category", AmbientLogLevel.Information));
            Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda!, new ApplicationException(), "category", AmbientLogLevel.Information));
        }
    }
    class AllowedLoggerType { }
}
