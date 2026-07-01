using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
            using AmbientFileLogOverflowWriter writer = new(overflowPath);
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
            {
                System.Collections.Concurrent.ConcurrentQueue<string> queue = new();
                AmbientLogBufferLimits.EnqueueOrOverflow(queue, "overflow-line-1", maxBufferedLines: 0);
                writer.Flush();
                Assert.IsTrue(File.Exists(overflowPath));
                Assert.Contains("overflow-line-1", File.ReadAllText(overflowPath));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void DefaultOverflowLogFilePath_ReturnsLogFileUnderProgramData()
    {
        string path = AmbientFileLogOverflowWriter.DefaultOverflowLogFilePath;
        Assert.IsTrue(path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(path, "_AmbientLogBufferOverflow");
        string programData = AmbientFileLogger.GetProgramDataFolderLocationInternal().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        StringAssert.StartsWith(path, programData);
        Assert.IsInstanceOfType<AmbientFileLogOverflowWriter>(Ambient.GetService<IAmbientLogOverflowWriter>().Global);
    }

    [TestMethod]
    public void WriteOverflowLine_NullLine_IsIgnored()
    {
        OverflowLine(null!);
    }

    [TestMethod]
    public void WriteLine_AppendsMultipleLinesAndReusesWriter()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowMulti_" + Guid.NewGuid().ToString("N"));
        string overflowPath = Path.Combine(tempDir, "overflow.log");
        try
        {
            using AmbientFileLogOverflowWriter writer = new(overflowPath);
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
            {
                OverflowLine("line-one");
                OverflowLine("line-two");
                writer.Flush();
                string text = File.ReadAllText(overflowPath);
                Assert.Contains("line-one", text);
                Assert.Contains("line-two", text);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void Flush_WhenWriterNotOpen_IsNoOp()
    {
        using AmbientFileLogOverflowWriter writer = new(Path.Combine(Path.GetTempPath(), "AmbientOverflowNoWriter_" + Guid.NewGuid().ToString("N")));
        using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
        {
            writer.Flush();
        }
    }

    [TestMethod]
    public void Flush_SecondCallWhenWriterAlreadyClosed_IsNoOp()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowDoubleClose_" + Guid.NewGuid().ToString("N"));
        string overflowPath = Path.Combine(tempDir, "overflow.log");
        try
        {
            using AmbientFileLogOverflowWriter writer = new(overflowPath);
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
            {
                OverflowLine("line");
                writer.Flush();
                writer.Flush();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void WriteLine_ConcurrentWritesToSamePath_ReusesCachedWriter()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowConcurrent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string overflowPath = Path.Combine(tempDir, "overflow.log");
        try
        {
            using AmbientFileLogOverflowWriter writer = new(overflowPath);
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
            {
                using Barrier barrier = new(2);
                Task writeA = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    OverflowLine("thread-a");
                });
                Task writeB = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    OverflowLine("thread-b");
                });
                Task.WaitAll(writeA, writeB);
                writer.Flush();
                string text = File.ReadAllText(overflowPath);
                Assert.Contains("thread-a", text);
                Assert.Contains("thread-b", text);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void WriteLine_CreatesOverflowDirectoryWhenMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowNested_" + Guid.NewGuid().ToString("N"));
        string nestedDir = Path.Combine(tempDir, "nested", "deep");
        string overflowPath = Path.Combine(nestedDir, "overflow.log");
        try
        {
            using AmbientFileLogOverflowWriter writer = new(overflowPath);
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
            {
                OverflowLine("nested-line");
                writer.Flush();
                Assert.IsTrue(Directory.Exists(nestedDir));
                Assert.Contains("nested-line", File.ReadAllText(overflowPath));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void WriteLine_InvalidPath_DoesNotThrow()
    {
        using AmbientFileLogOverflowWriter writer = new("<<invalid>>");
        using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
        {
            OverflowLine("best-effort");
        }
    }

    [TestMethod]
    public void Flush_SwallowsDisposeExceptions()
    {
        using AmbientFileLogOverflowWriter writer = new(Path.Combine(Path.GetTempPath(), "AmbientOverflowDispose_" + Guid.NewGuid().ToString("N")));
        using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(writer))
        {
            FieldInfo writerField = typeof(AmbientFileLogOverflowWriter).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)!;
#pragma warning disable CA2000 // ownership transferred to AmbientFileLogOverflowWriter; Flush disposes it
            StreamWriter badWriter = new(new ThrowingDisposeStream(), Encoding.UTF8) { AutoFlush = true };
#pragma warning restore CA2000
            writerField.SetValue(writer, badWriter);
            writer.Flush();
            Assert.IsNull(writerField.GetValue(writer));
        }
    }

    [TestMethod]
    public void ScopedLocalOverflowWriter_ClosesExistingWriter()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmbientOverflowSwitch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string firstPath = Path.Combine(tempDir, "first.log");
        string secondPath = Path.Combine(tempDir, "second.log");
        try
        {
            using (AmbientFileLogOverflowWriter firstWriter = new(firstPath))
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(firstWriter))
            {
                OverflowLine("first");
                firstWriter.Flush();
            }
            using (AmbientFileLogOverflowWriter secondWriter = new(secondPath))
            using (new ScopedLocalServiceOverride<IAmbientLogOverflowWriter>(secondWriter))
            {
                OverflowLine("second");
                secondWriter.Flush();
            }
            Assert.Contains("first", File.ReadAllText(firstPath));
            Assert.Contains("second", File.ReadAllText(secondPath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static void OverflowLine(string line)
    {
        System.Collections.Concurrent.ConcurrentQueue<string> queue = new();
        AmbientLogBufferLimits.EnqueueOrOverflow(queue, line, maxBufferedLines: 0);
    }

    private sealed class ThrowingDisposeStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new IOException("simulated dispose failure");
            }
            base.Dispose(disposing);
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
