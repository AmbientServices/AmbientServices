using AmbientServices;
using System;
using System.Diagnostics.CodeAnalysis;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace AmbientServices.Test.DelayedLoad
{
    [DefaultAmbientService]
    public class DefaultLateAssignmentTest : ILateAssignmentTest
    {
        static public void Load()
        {
        }
    }
}
