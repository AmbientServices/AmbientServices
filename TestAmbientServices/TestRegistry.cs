using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAmbientServices
{
    [TestClass]
    public class TestRegistry
    {
        [TestMethod]
        public void AmbientServicesRegistry()
        {
            ICache cache = Registry<ICache>.Implementation;
            ILogger logger = Registry<ILogger>.Implementation;
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            ISettings settings = Registry<ISettings>.Implementation;
        }
    }
}
