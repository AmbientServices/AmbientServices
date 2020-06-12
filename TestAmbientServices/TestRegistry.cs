using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAmbientServices
{
    interface IJunk
    { }

    [TestClass]
    public class TestRegistry
    {
        [TestMethod]
        public void AmbientServicesRegistry()
        {
            ICache cache = Registry<ICache>.Implementation;
            Assert.IsNotNull(cache);
            ILogger logger = Registry<ILogger>.Implementation;
            Assert.IsNotNull(logger);
            ILogger<TestRegistry> registryLogger = logger.GetLogger<TestRegistry>();
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            Assert.IsNotNull(progressTracker);
            ISettings settings = Registry<ISettings>.Implementation;
            Assert.IsNotNull(settings);
            IJunk junk = Registry<IJunk>.Implementation;
            Assert.IsNull(junk);
        }
        [TestMethod]
        public void DisableService()
        {
            ICache cache = Registry<ICache>.Implementation;
            Assert.IsNotNull(cache);

            Registry<ICache>.Implementation = null;

            ICache disabledCache = Registry<ICache>.Implementation;
            Assert.IsNull(disabledCache);

            Registry<ICache>.Implementation = cache;

            ICache reenabledCache = Registry<ICache>.Implementation;
            Assert.IsNotNull(reenabledCache);
        }
    }
}
