using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;

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
    [TestMethod]
    public void NullableTypeDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(LogContextEntry).Assembly);
        TypeDocumentation ceDocs = docs.GetTypeDocumentation(typeof(LogContextEntry));
    }
    [TestMethod]
    public void StandardTypes()
    {
        Assert.IsTrue(DotNetDocumentation.StandardTypes.Any());
    }
    [TestMethod]
    public void ProxyType()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(IPAddressConverter).Assembly);
        TypeDocumentation docsDocs = docs.GetTypeDocumentation(typeof(IPAddress));
    }
    [TestMethod]
    public void NoDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(TestDotNetDocumentation).Assembly);
        Assert.IsFalse(docs.PublicTypes.Any());
    }
}
