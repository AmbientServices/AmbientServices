using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// Summary description for StatusTest.
    /// </summary>
    [TestClass]
    public class TestAmbientFileLogger
    {
        private static AmbientService<IAmbientLogger> Logger = Ambient.GetService<IAmbientLogger>();

        [TestMethod]
        public async Task AmbientFileLoggerBasic()
        {
            string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
            try
            {
                using (AmbientClock.Pause())
                using (AmbientFileLogger logger = new AmbientFileLogger(tempPath, ".log", 8 * 60))
                {
                    // delete any preexisting files
                    await AmbientFileLogger.TryDeleteAllFiles(logger.FilePrefix);

                    using (IDisposable over = Logger.ScopedLocalOverride(logger))
                    {
                        // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                        Logger.Local.Log("test1");
                        Assert.AreEqual(0, TempFileCount(tempPath));
                        await Logger.Local.Flush();
                        Assert.AreEqual(1, TempFileCount(tempPath));
                        // log the second test message (since the clock is stopped, this will *never* create another file)
                        Logger.Local.Log("test2");
                        await Logger.Local.Flush();
                        Assert.AreEqual(1, TempFileCount(tempPath));
                    }
                }
            }
            finally
            {
                await AmbientFileLogger.TryDeleteAllFiles(tempPath);
            }
        }

        [TestMethod]
        public async Task AmbientFileLoggerFileRotation()
        {
            string logFilePrefix = null;
            try
            {
                using (AmbientClock.Pause())
                using (AmbientFileLogger logger = new AmbientFileLogger(null, null, 8 * 60))
                {
                    logFilePrefix = logger.FilePrefix;
                    // delete any preexisting files
                    await AmbientFileLogger.TryDeleteAllFiles(logFilePrefix);

                    using (IDisposable over = Logger.ScopedLocalOverride(logger))
                    {
                        // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                        Logger.Local.Log("test1");
                        Assert.AreEqual(0, TempFileCount(logFilePrefix));
                        await Logger.Local.Flush();
                        Assert.AreEqual(1, TempFileCount(logFilePrefix));

                        // get the name of the first log file
                        string firstLogFile = logger.GetLogFileName(AmbientClock.UtcNow);

                        // skip ahead eight hours and flush another message (it should go in another file)
                        AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                        // log the second test message
                        Logger.Local.Log("test2");
                        await Logger.Local.Flush();
                        Assert.AreEqual(2, TempFileCount(logFilePrefix));

                        // skip ahead eight hours and flush another message (it should go in another file)
                        AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                        // log the third test message
                        Logger.Local.Log("test3");
                        await Logger.Local.Flush();
                        Assert.AreEqual(3, TempFileCount(logFilePrefix));

                        // skip ahead eight hours and flush another message (this should rotate around to the first file)
                        AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                        // log the fourth test message
                        Logger.Local.Log("test4");
                        await Logger.Local.Flush();
                        Assert.AreEqual(3, TempFileCount(logFilePrefix));

                        // skip ahead eight hours and flush another message (this should rotate around to the second file)
                        AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                        // log a fifth test message just to make sure the first file gets closed
                        Logger.Local.Log("test5");
                        await Logger.Local.Flush();
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
                await AmbientFileLogger.TryDeleteAllFiles(logFilePrefix);
            }
        }
        [TestMethod]
        public void AmbientFileLoggerExceptions()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new AmbientFileLogger(null, "log"));
        }

        private int TempFileCount(string filePathPrefix)
        {
            string directory = Path.GetDirectoryName(filePathPrefix);
            string filename = Path.GetFileName(filePathPrefix);
            return Directory.GetFiles(directory, filename + "*.log").Length;
        }

        [TestMethod]
        public async Task RotatingFileBuffer()
        {
            string tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
            using (AmbientClock.Pause())
            {
                using (RotatingFileBuffer buffer = new RotatingFileBuffer(tempPath, "0.log", TimeSpan.Zero))
                {
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
        }
    }
}
