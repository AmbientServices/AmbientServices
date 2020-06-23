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
    public class DefaultTest : ITest
    {
        static void Load()
        {
        }
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



            IAmbientCache compareCache = cache;
            Assert.IsNotNull(cache);

            int changed = 0;
            Registry<IAmbientCache>.ImplementationChanged += (s, e) => { Assert.AreEqual(e.OldImplementation, compareCache); ++changed; };

            Registry<IAmbientCache>.Implementation = null;
            Assert.AreEqual(1, changed);
            compareCache = null;

            IAmbientCache disabledCache = Registry<IAmbientCache>.Implementation;
            Assert.IsNull(disabledCache);

            Registry<IAmbientCache>.Implementation = cache;
            Assert.AreEqual(2, changed);
            compareCache = cache;

            IAmbientCache reenabledCache = Registry<IAmbientCache>.Implementation;
            Assert.IsNotNull(reenabledCache);
        }

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            DefaultTest test = Registry<DefaultTest>.Implementation;
        }
        [TestMethod]
        public void AssemblyLoad()
        {
            TestAmbientServices2.DefaultTestAmbientService.Load();
        }
    }
}
