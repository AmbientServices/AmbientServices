using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientLogger"/>.
    /// </summary>
    [TestClass]
    public class TestLogger
    {
        private static readonly IAmbientLogger _Logger = ServiceBroker<IAmbientLogger>.GlobalImplementation;
        /// <summary>
        /// Performs tests on <see cref="IAmbientLogger"/>.
        /// </summary>
        [TestMethod]
        public async Task Logger()
        {
            ILogger<TestLogger> logger = _Logger.GetLogger<TestLogger>();
            logger.Log(new ApplicationException());
            logger.Log(new ApplicationException(), "category", LogLevel.Information);
            logger.Log("test message");
            logger.Log(() => "test message");
            logger.Log("test message", "category", LogLevel.Information);
            logger.Log("Exception during test", new ApplicationException());
            logger.Log(() => "Exception during test", new ApplicationException());
            logger.Log("Exception during test", new ApplicationException(), "category", LogLevel.Information);
            await _Logger.Flush();
        }
    }
}
