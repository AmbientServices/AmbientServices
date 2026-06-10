using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestLogSecurityAndBuffers
{
    [TestMethod]
    public void SensitiveFieldFilter_MasksMatchingPropertyNames()
    {
        string uniqueField = nameof(SensitiveFieldFilter_MasksMatchingPropertyNames) + "SecretField";
        using (AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(uniqueField)))
        {
            Dictionary<string, object?> dict = AmbientLogger.StructuredDataToDictionary(new Dictionary<string, object?> { ["User"] = "alice", [uniqueField] = "hunter2" });
            Assert.AreEqual("alice", dict["User"]);
            Assert.AreEqual(AmbientLogSensitiveFieldFilters.MaskedValue, dict[uniqueField]);
        }
        Dictionary<string, object?> afterDispose = AmbientLogger.StructuredDataToDictionary(new Dictionary<string, object?> { [uniqueField] = "still-visible" });
        Assert.AreEqual("still-visible", afterDispose[uniqueField]);
    }

    [TestMethod]
    public void SensitiveFieldFilter_MultipleIndependentRegistrations_AllApply()
    {
        string passwordField = nameof(SensitiveFieldFilter_MultipleIndependentRegistrations_AllApply) + "Password";
        string credentialField = nameof(SensitiveFieldFilter_MultipleIndependentRegistrations_AllApply) + "Credential";
        using IDisposable filterA = AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(passwordField));
        using IDisposable filterB = AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(credentialField));
        Dictionary<string, object?> dict = AmbientLogger.StructuredDataToDictionary(new Dictionary<string, object?> { [passwordField] = "p", [credentialField] = "c", ["Name"] = "n" });
        Assert.AreEqual(AmbientLogSensitiveFieldFilters.MaskedValue, dict[passwordField]);
        Assert.AreEqual(AmbientLogSensitiveFieldFilters.MaskedValue, dict[credentialField]);
        Assert.AreEqual("n", dict["Name"]);
    }

    [TestMethod]
    public void SensitiveFieldFilter_UnregisterRemovesOnlyOwnFilter()
    {
        string fieldA = nameof(SensitiveFieldFilter_UnregisterRemovesOnlyOwnFilter) + "A";
        string fieldB = nameof(SensitiveFieldFilter_UnregisterRemovesOnlyOwnFilter) + "B";
        using (AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(fieldA)))
        using (AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(fieldB)))
        {
            Assert.IsTrue(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldA));
            Assert.IsTrue(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldB));
        }
        Assert.IsFalse(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldA));
        Assert.IsFalse(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldB));
        using (AmbientLogSensitiveFieldFilters.RegisterFieldNameFilter(Regex.Escape(fieldA)))
        {
            Assert.IsTrue(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldA));
            Assert.IsFalse(AmbientLogSensitiveFieldFilters.ShouldMaskFieldName(fieldB));
        }
    }

    [TestMethod]
    public void LogBufferOverflow_WritesToOverflowFileWhenQueueFull()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string overflowPath = Path.Combine(tempDir, "overflow.log");
        try
        {
            AmbientLogOverflowWriter.SetOverflowFilePathForTesting(overflowPath);
            System.Collections.Concurrent.ConcurrentQueue<string> queue = new();
            AmbientLogBufferLimits.EnqueueOrOverflow(queue, "overflow-line-1", maxBufferedLines: 0);
            AmbientLogOverflowWriter.ResetCachedPathForTesting(); // close writer so the file can be read
            Assert.IsTrue(File.Exists(overflowPath));
            Assert.Contains("overflow-line-1", File.ReadAllText(overflowPath));
        }
        finally
        {
            AmbientLogOverflowWriter.ResetCachedPathForTesting();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public async Task TryDeleteAllFiles_UsesCustomFileExtension()
    {
        string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(tempPath);
        string prefix = Path.Combine(tempPath, "customlog");
        string rotated = prefix + "0001.txt";
        try
        {
            await File.WriteAllTextAsync(rotated, "data");
            Assert.AreEqual(1, Directory.GetFiles(tempPath, "*.txt").Length);
            await AmbientFileLogger.TryDeleteAllFiles(prefix, ".txt");
            Assert.AreEqual(0, Directory.GetFiles(tempPath, "*.txt").Length);
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    [TestMethod]
    public void SettingConversionFailed_RaisesEventOnParseFailure()
    {
        string key = nameof(SettingConversionFailed_RaisesEventOnParseFailure) + "-int";
        SettingConversionFailedEventArgs? received = null;
        void Handler(object? s, SettingConversionFailedEventArgs e) => received = e;
        AmbientSettings.ConversionFailed += Handler;
        try
        {
            AmbientSettingsOverride settings = new(new Dictionary<string, string> { { key, "not-a-number" } }, nameof(SettingConversionFailed_RaisesEventOnParseFailure));
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
            {
                IAmbientSetting<int> setting = AmbientSettings.GetAmbientSetting<int>(key, "", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "42");
                Assert.AreEqual(42, setting.Value);
            }
            Assert.IsNotNull(received);
            Assert.AreEqual(key, received!.Key);
            Assert.AreEqual("not-a-number", received.RawValue);
            Assert.AreEqual(42, received.DefaultValue);
            Assert.IsNotNull(received.Exception);
        }
        finally
        {
            AmbientSettings.ConversionFailed -= Handler;
        }
    }

    [TestMethod]
    public void EnvironmentSettings_DoesNotBulkCopyUnregisteredSecrets()
    {
        string secretKey = "AMBIENT_TEST_SECRET_" + Guid.NewGuid().ToString("N");
        string secretValue = "top-secret-value";
        Environment.SetEnvironmentVariable(secretKey, secretValue);
        try
        {
            AmbientEnvironmentSettingsSet settingsSet = new();
            System.Collections.Concurrent.ConcurrentDictionary<string, string> rawValues =
                (System.Collections.Concurrent.ConcurrentDictionary<string, string>)typeof(AmbientEnvironmentSettingsSet)
                    .GetField("_rawValues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(settingsSet)!;
            Assert.IsFalse(rawValues.ContainsKey(secretKey), "Unregistered environment variables must not be bulk-copied at construction.");
            Assert.AreEqual(secretValue, settingsSet.GetRawValue(secretKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretKey, null);
        }
    }

    [TestMethod]
    public async Task LocalCache_EjectFrequencyZero_DoesNotThrow()
    {
        Dictionary<string, string> settings = new()
        {
            { nameof(BasicAmbientLocalCache) + "-EjectFrequency", "0" },
            { nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "1000" },
            { nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "1" },
        };
        AmbientSettingsOverride localSettingsSet = new(settings, nameof(LocalCache_EjectFrequencyZero_DoesNotThrow));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            BasicAmbientLocalCache cache = new(localSettingsSet);
            using ScopedLocalServiceOverride<IAmbientLocalCache> localCache = new(cache);
            AmbientLocalCache<TestLogSecurityAndBuffers> ambientCache = new();
            await ambientCache.Store("k", this, false);
            TestLogSecurityAndBuffers? item = await ambientCache.Retrieve<TestLogSecurityAndBuffers>("k", null);
            Assert.AreEqual(this, item);
            await ambientCache.Clear();
        }
    }

}
