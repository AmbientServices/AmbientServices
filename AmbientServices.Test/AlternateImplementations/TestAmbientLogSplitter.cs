using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Summary description for StatusTest.
/// </summary>
[TestClass]
public class TestAmbientLogSplitter
{
    [TestMethod]
    public async Task AmbientLogSplitter()
    {
        using (AmbientClock.Pause())
        {
            AmbientLogSplitter logger = new();
            // log a message that will go to zero loggers
            logger.Log("test1");
            await logger.Flush();
            logger.AddLogger(AmbientConsoleLogger.Instance);
            // log a message that will go to one logger
            logger.Log("test2");
            await logger.Flush();
            logger.AddLogger(AmbientTraceLogger.Instance);
            // log a message that will go to three loggers
            logger.Log("test3");
            await logger.Flush();

            logger.RemoveLogger(AmbientConsoleLogger.Instance);
            logger.RemoveLogger(AmbientTraceLogger.Instance);
        }
    }
}
