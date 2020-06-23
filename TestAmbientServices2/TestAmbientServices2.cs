using AmbientServices;
using System;
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
            ITestAmbientService service = Registry<ITestAmbientService>.Implementation;
            if (!(service is DefaultTestAmbientService)) throw new InvalidOperationException();
        }
    }
}
