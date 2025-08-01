﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#pragma warning disable CS0618

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for <see cref="IAmbientLogger"/>.
/// </summary>
[TestClass]
public class TestLogger
{
    private static readonly AmbientService<IAmbientLogger> _Logger = Ambient.GetService(out _Logger);
    private static readonly AmbientService<IAmbientStructuredLogger> _StructuredLogger = Ambient.GetService(out _StructuredLogger);
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService(out _SettingsSet);

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

        logger.Error(new ApplicationException());
        logger.Error(new ApplicationException(), "test message");
        logger.Error(new ApplicationException(), "test message", AmbientLogLevel.Information);
        logger.Filter()?.Log(new { }, new ApplicationException());
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
        logger.Filter()?.Log(new { Summary = "test message" });
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
        logger.Filter()?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        logger.Error(new ApplicationException(), "Exception during test");
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        if (_Logger.Global != null) await _Logger.Global.Flush();

        // we should filter one or the other based on the level to test that branch
        logger.Error(new ApplicationException(), level: AmbientLogLevel.Verbose);
        logger.Error(new ApplicationException(), level: AmbientLogLevel.Critical);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void LoggerStructuredConsole()
    {
        AmbientConsoleLogger console = AmbientConsoleLogger.Instance;
        console.Log(new { Message = "this is a test" });
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public async Task LoggerBasic()
    {
        using AmbientFileLogger bl = new();
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

            logger.Error(new ApplicationException());
            logger.Error(new ApplicationException(), "test message");
            logger.Error(new ApplicationException(), "test message", AmbientLogLevel.Information);
            logger.Filter()?.Log(new { Error = new ApplicationException() });
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
            logger.Filter()?.Log(new { Summary = "test message" });
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
            logger.Filter()?.Log(new { Summary = "Exception during test" }, new ApplicationException());
            logger.Error(new ApplicationException(), "Exception during test");
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
            if (_Logger.Global != null) await _Logger.Global.Flush();
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientStructuredLogger"/>.
    /// </summary>
    [TestMethod]
    public async Task LoggerStructured()
    {
        using AmbientFileLogger bl = new();
        using (new ScopedLocalServiceOverride<IAmbientStructuredLogger>(bl))
        {
            AmbientLogger logger = new(typeof(TestLogger));
            Dictionary<string, object?> inner = new();
            inner["test"] = DateTime.UtcNow;
            Dictionary<string, object?> outer = new();
            inner["inner"] = inner;
            logger.Filter()?.Log(new { Structured = outer });
            logger.Filter()?.Log("Testing unstructured as structured");
            bl.Log("direct log");
            bl.Log((object)"direct log");
            bl.Log(new { Structured = outer });
            if (_Logger.Global != null) await _Logger.Global.Flush();
            Assert.AreEqual("AmbientTraceLogger/AmbientFileLogger", logger.LoggerType);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public async Task LoggerStructuredError()
    {
        using AmbientFileLogger bl = new();
        using (new ScopedLocalServiceOverride<IAmbientStructuredLogger>(bl))
        {
            AmbientLogger logger = new(typeof(TestLogger));
            Dictionary<string, object?> outer = new();
            Dictionary<string, object?> inner = new();
            inner["test"] = IPAddress.None;     // there is no standard JsonSerialization for IPAddress, so this should throw an exception and be handled specially
            outer["inner"] = inner;
            logger.Filter()?.Log(new { Structured = outer });
            if (_Logger.Global != null) await _Logger.Global.Flush();
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public async Task LoggerException()
    {
        using AmbientFileLogger bl = new();
        using (new ScopedLocalServiceOverride<IAmbientStructuredLogger>(bl))
        {
            AmbientLogger logger = new(typeof(TestLogger));
            logger.Error(new ExpectedException(nameof(LoggerException)), nameof(LoggerException));
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

            logger.Error(new ApplicationException());
            logger.Error(new ApplicationException(), "test message");
            logger.Error(new ApplicationException(), "test message", AmbientLogLevel.Information);
            logger.Filter()?.Log(new { Error = new ApplicationException() });
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
            logger.Filter()?.Log(new { Summary = "test message" });
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
            logger.Filter()?.Log(new { Summary = "Exception during test" }, new ApplicationException());
            logger.Error(new ApplicationException(), "Exception during test");
            logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
            Assert.AreEqual("/AmbientTraceLogger", logger.LoggerType);
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
        AmbientLogger<AllowedLoggerType> logger = new(_Logger.Global, null, settingsSet);
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

        logger.Error(new ApplicationException());
        logger.Error(new ApplicationException(), "test message");
        logger.Error(new ApplicationException(), "test message", AmbientLogLevel.Information);
        logger.Filter()?.Log(new { Error = new ApplicationException() });
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
        logger.Filter()?.Log(new { Summary = "test message" });
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
        logger.Filter()?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        logger.Error(new ApplicationException(), "Exception during test");
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
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
        logger.Error(new ApplicationException());

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

        logger.Error(new ApplicationException());
        logger.Error(new ApplicationException(), "test message");
        logger.Error(new ApplicationException(), "test message", AmbientLogLevel.Information);
        logger.Filter()?.Log(new { Error = new ApplicationException() });
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { }, new ApplicationException());
        logger.Filter()?.Log(new { Summary = "test message" });
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { Summary = "test message" });
        logger.Filter()?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        logger.Error(new ApplicationException(), "Exception during test");
        logger.Filter("category", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());
        logger.Filter("AllowedCategory", AmbientLogLevel.Information)?.Log(new { Summary = "Exception during test" }, new ApplicationException());

        if (_Logger.Local != null) await _Logger.Local.Flush();
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void LoggerArgumentExceptions()
    {
        AmbientLogger<TestLogger> logger = new(_Logger.Global, null);
        Func<string> nullLambda = null;
        Exception nullException = null;
        Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda!, "category", AmbientLogLevel.Information));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullException!, "category", AmbientLogLevel.Information));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Log("message", nullException!, "category", AmbientLogLevel.Information));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Log(() => "message", nullException!, "category", AmbientLogLevel.Information));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Log(nullLambda!, new ApplicationException(), "category", AmbientLogLevel.Information));

        Assert.ThrowsException<ArgumentNullException>(() => new AmbientLogger(null!));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Error(nullException!, level: AmbientLogLevel.Warning));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Error(nullException!));
        Assert.ThrowsException<ArgumentNullException>(() => logger.Error(nullException!, level: AmbientLogLevel.Critical));
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void ConvertStructuredDataIntoSimpleMessage()
    {
        Dictionary<string, object?> dictionary = new()
        {
            { "Level", AmbientLogLevel.Information },
            { "baselineKey", "baselineValue" },
            { "Summary", "Summary of the Error"},
            { "key2", null },
        };

        string logEntry = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(dictionary);

        logEntry = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(new { Summary = "Summary of the Error", Other = 1 });
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void IPAddressSerializer()
    {
        JsonSerializerOptions options = AmbientLogger.DefaultSerializer;
        string none = JsonSerializer.Serialize(IPAddress.None, options);
        Assert.AreEqual(IPAddress.None, JsonSerializer.Deserialize<IPAddress>(none, options));
        string any = JsonSerializer.Serialize(IPAddress.Any, options);
        Assert.AreEqual(IPAddress.Any, JsonSerializer.Deserialize<IPAddress>(any, options));
        string nullableNotNull = JsonSerializer.Serialize(IPAddress.Any, options);
        Assert.AreEqual(IPAddress.Any, JsonSerializer.Deserialize<IPAddress>(nullableNotNull, options));
        string nullableNull = JsonSerializer.Serialize<IPAddress?>(null, options);
        Assert.IsNull(JsonSerializer.Deserialize<IPAddress?>(nullableNull, options));


        Dictionary<string, object?> dictionary = new()
        {
            { "Level", AmbientLogLevel.Information },
            { "baselineKey", "baselineValue" },
            { "key1", IPAddress.Parse("0.0.0.0") },
            { "key2", null },
        };

        string json = JsonSerializer.Serialize(dictionary, options);

        string logEntry = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(dictionary);

        dictionary["Key"] = IPAddress.Any;

        json = JsonSerializer.Serialize(dictionary, options);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void IPEndPointSerializer()
    {
        IPEndPoint min = IPEndPoint.Parse("0");

        JsonSerializerOptions options = AmbientLogger.DefaultSerializer;
        string none = JsonSerializer.Serialize(min, options);
        Assert.AreEqual(min, JsonSerializer.Deserialize<IPEndPoint>(none, options));
        string nullableNull = JsonSerializer.Serialize<IPEndPoint?>(null, options);
        Assert.IsNull(JsonSerializer.Deserialize<IPEndPoint?>(nullableNull, options));

        Dictionary<string, object?> dictionary = new()
        {
            { "Level", AmbientLogLevel.Information },
            { "baselineKey", "baselineValue" },
            { "key1", IPEndPoint.Parse("0.0.0.0") },
            { "key2", null },
        };

        string json = JsonSerializer.Serialize(dictionary, options);

        string logEntry = AmbientLogger.ConvertStructuredDataIntoSimpleMessage(dictionary);

        dictionary["Key"] = min;

        json = JsonSerializer.Serialize(dictionary, options);
    }
    [TestMethod]
    public void AmbientLoggerTest()
    {
        AmbientLogger<TestLogger> logger = new();
        string result;
        object entry;

        logger.MessageRenderer = (time, level, owner, category, message) => "test";
        result = logger.MessageRenderer(DateTime.UtcNow, AmbientLogLevel.Error, new { IPAddress.None }, "owner", "category");
        logger.MessageRenderer = AmbientLogger.DefaultMessageRenderer;
        result = logger.MessageRenderer(DateTime.UtcNow, AmbientLogLevel.Error, "this is a plain old message", "owner", "category");
        result = logger.MessageRenderer(DateTime.UtcNow, AmbientLogLevel.Error, new { IPAddress.None }, "owner", "category");
        result = logger.MessageRenderer(DateTime.UtcNow, AmbientLogLevel.Error, new { Summary = "Summary of the Error", IPAddress.None }, "owner", "category");
        result = logger.MessageRenderer(DateTime.UtcNow, AmbientLogLevel.Error, new { Summary = "Summary of the Error", IPAddress.None });

        logger.Renderer = (time, level, owner, category, message) => "test";
        entry = logger.Renderer(DateTime.UtcNow, AmbientLogLevel.Error, new { IPAddress.None }, "owner", "category");
        logger.Renderer = AmbientLogger.DefaultRenderer;
        entry = logger.Renderer(DateTime.UtcNow, AmbientLogLevel.Error, "this is a plain old message", "owner", "category");
        entry = logger.Renderer(DateTime.UtcNow, AmbientLogLevel.Error, new { IPAddress.None }, "owner", "category");
        entry = logger.Renderer(DateTime.UtcNow, AmbientLogLevel.Error, new { Summary = "Summary of the Error", IPAddress.None }, "owner", "category");
        entry = logger.Renderer(DateTime.UtcNow, AmbientLogLevel.Error, new { Summary = "Summary of the Error", IPAddress.None });
    }
    [TestMethod]
    public void HandleFallbackException()
    {
        StringBuilder sb = new();
        AmbientLogger.HandleFallbackException(sb, new ExpectedException(nameof(HandleFallbackException)));
        AmbientLogger.HandleFallbackException(sb, new TargetInvocationException(new ExpectedException(nameof(HandleFallbackException))));
        Assert.IsTrue(sb.ToString().Length > 0);
        Assert.IsTrue(sb.ToString().Contains(nameof(HandleFallbackException)));
    }
    [TestMethod]
    public void HandleUnserializable()
    {
        string s;
        s = AmbientLogger.HandleUnserializable(new { IntPtr = System.IntPtr.Zero }, new ExpectedException(nameof(HandleUnserializable)));
        s = AmbientLogger.HandleUnserializable(new { Inner = new { Nint = System.IntPtr.Zero } }, new ExpectedException(nameof(HandleUnserializable)));
    }
}
class AllowedLoggerType { }
