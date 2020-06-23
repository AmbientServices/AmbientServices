using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAmbientServices2
{
    interface ITestAmbientService
    { }
    [DefaultAmbientService]
    public class DefaultTestAmbientService : ITestAmbientService
    {
        public static void Load()
        {
        }
    }
}
