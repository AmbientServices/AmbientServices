using AmbientServices;
using System;
using System.Diagnostics.CodeAnalysis;

namespace TestAmbientServices2
{
    [ExcludeFromCoverage]
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
    [ExcludeFromCoverage]
    [AttributeUsage(AttributeTargets.All)]
    class ExcludeFromCoverageAttribute : Attribute
    {
    }

    [ExcludeFromCoverage]
    [DefaultAmbientService]
    public class DefaultLateAssignmentTest : ILateAssignmentTest
    {
        static void Load()
        {
        }
    }
}
