using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// Summary description for StatusTest.
    /// </summary>
    [TestClass]
    public class TestAmbientTraceLogger
    {
        [TestMethod]
        public async Task AmbientTraceLoggerBasic()
        {
            using (AmbientClock.Pause())
            {
                AmbientTraceLogger logger = AmbientTraceLogger.Instance;
                // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                logger?.Log("test1");
                if (logger != null) await logger.Flush();
                // log the second test message (since the clock is stopped, this will *never* create another file)
                logger?.Log("test2");
                if (logger != null) await logger.Flush();
            }
        }
        [TestMethod]
        public async Task AmbientTraceLoggerDuplicateFilter()
        {
            using (AmbientClock.Pause())
            {
                AmbientTraceLogger logger = AmbientTraceLogger.Instance;
                // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
                LogEntryRenderer entryRenderer = AmbientLogger.DefaultRenderer;
                LogMessageRenderer messageRenderer = AmbientLogger.DefaultMessageRenderer;
                AmbientLogger.LogFiltered(logger, entryRenderer, messageRenderer, logger, typeof(TestAmbientTraceLogger).Name, AmbientLogLevel.Information, null, new { Action= "test1" });
                await logger.Flush();
            }
        }
    }
}
