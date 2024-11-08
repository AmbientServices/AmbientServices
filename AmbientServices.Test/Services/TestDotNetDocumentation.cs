using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for assembly extension methods.
/// </summary>
[TestClass]
public class TestDotNetDocumentation
{
    private static readonly AmbientService<ILateAssignmentTest> _LateAssignmentTest = Ambient.GetService<ILateAssignmentTest>();

    /// <summary>
    /// Tests docs for nullable parameters.
    /// </summary>
    /// <param name="nullableInt">A nullable integer.</param>
    /// <param name="nullableClass">A nullable class.</param>
    public ValueTask TestNullable(int? nullableInt, TestDotNetDocumentation? nullableClass)
    {
        return ValueTask.CompletedTask;
    }

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
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(TestDotNetDocumentation).Assembly);
        MethodInfo mi = typeof(TestDotNetDocumentation).GetMethod("TestNullable");
        MethodDocumentation md = docs.GetMethodDocumentation(mi);
        Assert.IsNotNull(md);
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
