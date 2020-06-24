using AmbientServices;
using System;
using System.Diagnostics.CodeAnalysis;

namespace TestAmbientServices2
{
    interface ITestAmbientService
    { }
    [ExcludeFromCoverage]
    [DefaultAmbientService]
    public class DefaultTestAmbientService : ITestAmbientService
    {
        [ExcludeFromCoverage]
        public static void Load()
        {
            ITestAmbientService service = ServiceBroker<ITestAmbientService>.Implementation;
            if (!(service is DefaultTestAmbientService)) throw new InvalidOperationException();
        }
    }
    class ExcludeFromCoverageAttribute : Attribute
    {
    }
}
