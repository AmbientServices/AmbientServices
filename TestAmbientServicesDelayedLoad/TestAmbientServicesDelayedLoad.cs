using AmbientServices;
using System;
using System.Diagnostics.CodeAnalysis;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace TestAmbientServicesDelayedLoad
{
    [DefaultAmbientServiceProvider]
    public class DefaultLateAssignmentTest : ILateAssignmentTest
    {
        static public void Load()
        {
        }
    }
}
