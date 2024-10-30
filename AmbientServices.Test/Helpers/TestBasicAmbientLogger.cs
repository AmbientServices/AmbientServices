using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Summary description for StatusTest.
/// </summary>
[TestClass]
public class TestBasicAmbientLogger
{
    private static readonly AmbientService<IAmbientLogger> Logger = Ambient.GetService<IAmbientLogger>();
    [TestMethod]
    public async Task BasicAmbientLoggerBasic()
    {
        string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
        try
        {
            using (AmbientClock.Pause())
            using (AmbientFileLogger loggerImp = new(tempPath, ".log", 8 * 60))
            {
                // delete any preexisting files
                await AmbientFileLogger.TryDeleteAllFiles(loggerImp.FilePrefix);

                using IDisposable over = Logger.ScopedLocalOverride(loggerImp);
                IAmbientLogger logger = Logger.Local;
                // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                logger?.Log("test1");
                Assert.AreEqual(0, TempFileCount(tempPath));
                if (logger != null) await logger.Flush();
                Assert.AreEqual(1, TempFileCount(tempPath));
                // log the second test message (since the clock is stopped, this will *never* create another file)
                logger?.Log("test2");
                if (logger != null) await logger.Flush();
                Assert.AreEqual(1, TempFileCount(tempPath));
            }
        }
        finally
        {
            await AmbientFileLogger.TryDeleteAllFiles(tempPath);
        }
    }
    [TestMethod]
    public async Task BasicAmbientLoggerAtDefaultPath()
    {
        using (AmbientClock.Pause())
        using (AmbientFileLogger loggerImp = new())
        {
            // delete any preexisting files
            await AmbientFileLogger.TryDeleteAllFiles(loggerImp.FilePrefix);
        }
    }

    [TestMethod]
    public async Task BasicAmbientLoggerFileRotation()
    {
        string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
        string logFilePrefix = null;
        try
        {
            using (AmbientClock.Pause())
            using (AmbientFileLogger logger = new(tempPath + nameof(BasicAmbientLoggerFileRotation) + Guid.NewGuid().ToString("N"), null, 8 * 60))
            {
                logFilePrefix = logger.FilePrefix;
                // delete any preexisting files
                await AmbientFileLogger.TryDeleteAllFiles(logFilePrefix);

                using (IDisposable over = Logger.ScopedLocalOverride(logger))
                {
                    // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                    Logger.Local?.Log("test1");
                    Assert.AreEqual(0, TempFileCount(logFilePrefix));
                    if (Logger.Local != null) await Logger.Local.Flush();
                    Assert.AreEqual(1, TempFileCount(logFilePrefix));

                    // get the name of the first log file
                    string firstLogFile = logger.GetLogFileName(AmbientClock.UtcNow);

                    // skip ahead eight hours and flush another message (it should go in another file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the second test message
                    Logger.Local?.Log("test2");
                    if (Logger.Local != null) await Logger.Local.Flush();
                    Assert.AreEqual(2, TempFileCount(logFilePrefix));

                    // skip ahead eight hours and flush another message (it should go in another file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the third test message
                    Logger.Local?.Log("test3");
                    if (Logger.Local != null) await Logger.Local.Flush();
                    Assert.AreEqual(3, TempFileCount(logFilePrefix));

                    // skip ahead eight hours and flush another message (this should rotate around to the first file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the fourth test message
                    Logger.Local?.Log("test4");
                    if (Logger.Local != null) await Logger.Local.Flush();
                    Assert.AreEqual(3, TempFileCount(logFilePrefix));

                    // skip ahead eight hours and flush another message (this should rotate around to the second file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log a fifth test message just to make sure the first file gets closed
                    Logger.Local?.Log("test5");
                    if (Logger.Local != null) await Logger.Local.Flush();
                    Assert.AreEqual(3, TempFileCount(logFilePrefix));

                    // open the first log file
                    string firstLogContents = File.ReadAllText(firstLogFile);
                    Assert.IsFalse(firstLogContents.Contains("test1")); // the first log message should have been overwritten when the log rotated around to the first file again
                    Assert.IsTrue(firstLogContents.Contains("test4"));

                    // try to delete files here (this will cause an ignored exception)
                    await AmbientFileLogger.TryDeleteAllFiles(logFilePrefix);
                }
                // dispose a little early so we can test flushing after disposal
                logger.Dispose();
                await logger.Flush();   // this should just be ignored after disposal
            }
        }
        finally
        {
            await AmbientFileLogger.TryDeleteAllFiles(logFilePrefix!);
        }
    }
    [TestMethod]
    public async Task BasicAmbientLoggerExceptions()
    {
        try
        {
            await AmbientFileLogger.TryDeleteAllFiles(null!);
            Assert.Fail("No ArgumentException thrown!");
        }
        catch (ArgumentException)
        {
            // this is what we were expecting!
        }
        try
        {
            await AmbientFileLogger.TryDeleteAllFiles("C:\\");
            Assert.Fail("No ArgumentException thrown!");
        }
        catch (ArgumentException)
        {
            // this is what we were expecting!
        }
    }

    private int TempFileCount(string filePathPrefix)
    {
        string directory = Path.GetDirectoryName(filePathPrefix);
        string filename = Path.GetFileName(filePathPrefix);
        return Directory.GetFiles(directory!, filename + "*.log").Length;
    }

    [TestMethod]
    public async Task RotatingFileBuffer()
    {
        string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
        using (AmbientClock.Pause())
        {
            using RotatingFileBuffer buffer = new(tempPath, "0.log", TimeSpan.Zero);
            buffer.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => buffer.BufferLine(""));
            Assert.ThrowsException<ObjectDisposedException>(() => buffer.BufferFileRotation("1.log"));
            try
            {
                await buffer.Flush();
                Assert.Fail("ObjectDisposedException expected!");
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestMethod]
    public void TestAmbientLogContext()
    {
        AmbientLogContext.Reset("baselineKey", "baselineValue");
        IDisposable kvpScope = AmbientLogContext.AddKeyValuePair(new("key1", "value1"));
        IDisposable kvpsScope = AmbientLogContext.AddKeyValuePairs(new LogContextEntry[] { new("key2", "value2") });
    }
}
