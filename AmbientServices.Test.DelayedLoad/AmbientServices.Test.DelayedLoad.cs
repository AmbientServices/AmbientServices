[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace AmbientServices.Test.DelayedLoad
{
    [DefaultAmbientService]
    public class DefaultLateAssignmentTest : ILateAssignmentTest
    {
        public static void Load()
        {
        }
    }
}
