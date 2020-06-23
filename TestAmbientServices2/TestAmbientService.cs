using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;

namespace TestAmbientServices2
{
    interface ITestAmbientService
    { }
    [ExcludeFromCodeCoverage]
    [DefaultAmbientService]
    public class DefaultTestAmbientService : ITestAmbientService
    {
        [ExcludeFromCodeCoverage]
        public static void Load()
        {
        }
    }
}
