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
    public class TestAmbientTraceLogger
    {
        private static AmbientService<IAmbientLogger> Logger = Ambient.GetService<IAmbientLogger>();
        [TestMethod]
        public async Task AmbientTraceLoggerBasic()
        {
            using (AmbientClock.Pause())
            {
                AmbientTraceLogger loggerImp = new AmbientTraceLogger();

                using (IDisposable over = Logger.ScopedLocalOverride(loggerImp))
                {
                    IAmbientLogger logger = Logger.Local;
                    // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                    logger?.Log("test1");
                    if (logger != null) await logger.Flush();
                    // log the second test message (since the clock is stopped, this will *never* create another file)
                    logger?.Log("test2");
                    if (logger != null) await logger.Flush();
                }
            }
        }

        [TestMethod]
        public async Task AmbientTraceLoggerFileRotation()
        {
            using (AmbientClock.Pause())
            {
                AmbientTraceLogger logger = new AmbientTraceLogger();
                using (IDisposable over = Logger.ScopedLocalOverride(logger))
                {
                    // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                    Logger.Local?.Log("test1");
                    if (Logger.Local != null) await Logger.Local.Flush();

                    // skip ahead eight hours and flush another message (it should go in another file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the second test message
                    Logger.Local?.Log("test2");
                    if (Logger.Local != null) await Logger.Local.Flush();

                    // skip ahead eight hours and flush another message (it should go in another file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the third test message
                    Logger.Local?.Log("test3");
                    if (Logger.Local != null) await Logger.Local.Flush();

                    // skip ahead eight hours and flush another message (this should rotate around to the first file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log the fourth test message
                    Logger.Local?.Log("test4");
                    if (Logger.Local != null) await Logger.Local.Flush();

                    // skip ahead eight hours and flush another message (this should rotate around to the second file)
                    AmbientClock.SkipAhead(TimeSpan.FromHours(8));

                    // log a fifth test message just to make sure the first file gets closed
                    Logger.Local?.Log("test5");
                    if (Logger.Local != null) await Logger.Local.Flush();
                }
                await logger.Flush();   // this should just be ignored after disposal
            }
        }
    }
}
