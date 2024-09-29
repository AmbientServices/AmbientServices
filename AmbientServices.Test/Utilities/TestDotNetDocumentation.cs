using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for assembly extension methods.
/// </summary>
[TestClass]
public class TestDotNetDocumentation
{
    private static readonly AmbientService<ILateAssignmentTest> _LateAssignmentTest = Ambient.GetService<ILateAssignmentTest>();

    [TestMethod]
    public void Documentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(DotNetDocumentation).Assembly);
        TypeDocumentation docsDocs = docs.GetTypeDocumentation(typeof(DotNetDocumentation));
        MethodDocumentation methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetMethodDocumentation)));
    }
}
