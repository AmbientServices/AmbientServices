using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Summary description for StatusTest.
/// </summary>
[TestClass]
public class TestAmbientConsoleLogger
{
    [TestMethod]
    public async Task AmbientConsoleLoggerBasic()
    {
        using (AmbientClock.Pause())
        {
            AmbientConsoleLogger logger = AmbientConsoleLogger.Instance;
            // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
            logger?.Log("test1");
            if (logger != null) await logger.Flush();
            // log the second test message (since the clock is stopped, this will *never* create another file)
            logger?.Log("test2");
            if (logger != null) await logger.Flush();
        }
    }

    [TestMethod]
    public async Task AmbientConsoleLoggerFileRotation()
    {
        using (AmbientClock.Pause())
        {
            AmbientConsoleLogger logger = AmbientConsoleLogger.Instance;
            // log the first test message (this will cause the file to be created, but only *after* this message gets flushed
            logger.Log("test1");
            await logger.Flush();

            // skip ahead eight hours and flush another message (it should go in another file)
            AmbientClock.SkipAhead(TimeSpan.FromHours(8));

            // log the second test message
            logger.Log("test2");
            await logger.Flush();

            // skip ahead eight hours and flush another message (it should go in another file)
            AmbientClock.SkipAhead(TimeSpan.FromHours(8));

            // log the third test message
            logger.Log("test3");
            await logger.Flush();

            // skip ahead eight hours and flush another message (this should rotate around to the first file)
            AmbientClock.SkipAhead(TimeSpan.FromHours(8));

            // log the fourth test message
            logger.Log("test4");
            await logger.Flush();

            // skip ahead eight hours and flush another message (this should rotate around to the second file)
            AmbientClock.SkipAhead(TimeSpan.FromHours(8));

            // log a fifth test message just to make sure the first file gets closed
            logger.Log("test5");
            await logger.Flush();
            await logger.Flush();   // this should just be ignored after disposal
        }
    }
}
