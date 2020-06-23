using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace TestAmbientServices
{
    interface IJunk
    { }
    interface ITest
    { }
    [DefaultAmbientService]
    class DefaultTest : ITest
    {
    }
    [TestClass]
    public class TestRegistry
    {
        [TestMethod]
        public void AmbientServicesRegistry()
        {
            IAmbientCache cache = Registry<IAmbientCache>.Implementation;
            Assert.IsNotNull(cache);
            IAmbientLogger logger = Registry<IAmbientLogger>.Implementation;
            Assert.IsNotNull(logger);
            ILogger<TestRegistry> registryLogger = logger.GetLogger<TestRegistry>();
            IAmbientProgress progressTracker = Registry<IAmbientProgress>.Implementation;
            Assert.IsNotNull(progressTracker);
            IAmbientSettings settings = Registry<IAmbientSettings>.Implementation;
            Assert.IsNotNull(settings);
            IJunk junk = Registry<IJunk>.Implementation;
            Assert.IsNull(junk);
        }
        [TestMethod]
        public void DisableService()
        {
            IAmbientCache cache = Registry<IAmbientCache>.Implementation;
            Assert.IsNotNull(cache);

            Registry<IAmbientCache>.Implementation = null;

            IAmbientCache disabledCache = Registry<IAmbientCache>.Implementation;
            Assert.IsNull(disabledCache);

            Registry<IAmbientCache>.Implementation = cache;

            IAmbientCache reenabledCache = Registry<IAmbientCache>.Implementation;
            Assert.IsNotNull(reenabledCache);
        }
        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            DefaultTest test = Registry<DefaultTest>.Implementation;
        }
    }
}
