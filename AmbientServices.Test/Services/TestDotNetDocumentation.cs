using AmbientServices;
using AmbientServices.Utilities;
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
        MethodDocumentation? methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetMethodDocumentation)));
        Assert.IsNotNull(methodDocs);
        Assert.IsNotNull(methodDocs.ReturnDescription);
        Assert.IsNotNull(methodDocs.Summary);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetFieldDocumentation)));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetTypeDocumentation)));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetNullableTypeDocumentation)));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetMemberDocumentation)));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.GetTypeOrProxy)));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.Load), new System.Type[] { typeof(System.Type) }));
        Assert.IsNotNull(methodDocs);
        methodDocs = docs.GetMethodDocumentation(typeof(DotNetDocumentation).GetMethod(nameof(DotNetDocumentation.Load), new System.Type[] { typeof(Assembly) }));
        Assert.IsNotNull(methodDocs);
        foreach (MethodInfo mi in typeof(AmbientFilteredLogger).GetMethods(BindingFlags.Public))
        {
            methodDocs = docs.GetMethodDocumentation(mi);
            Assert.IsNotNull(methodDocs);
        }
        foreach (MethodInfo mi in typeof(SI).GetMethods(BindingFlags.Public))
        {
            methodDocs = docs.GetMethodDocumentation(mi);
            Assert.IsNotNull(methodDocs);
        }
        foreach (MethodInfo mi in typeof(AmbientCache<>).GetMethods(BindingFlags.Public))
        {
            methodDocs = docs.GetMethodDocumentation(mi);
            Assert.IsNotNull(methodDocs);
        }
        ConstructorInfo ci = typeof(TypeDocumentation).GetConstructor( new System.Type[] { typeof(string), typeof(string), typeof(string), typeof(System.Collections.Generic.IEnumerable<ParameterDocumentation>) });
        methodDocs = docs.GetMethodDocumentation(ci);
        Assert.IsNotNull(methodDocs);

    }
    [TestMethod]
    public void NullableTypeDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientFileLogger).Assembly);
        MethodInfo mi = typeof(AmbientFileLogger).GetMethod("Flush");
        MethodDocumentation? md = docs.GetMethodDocumentation(mi);
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
        TypeDocumentation? docsDocs = docs.GetTypeDocumentation(typeof(IPAddressConverter));
        Assert.IsNotNull(docsDocs);
    }
    [TestMethod]
    public void EnumType()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientBottleneckUtilizationAlgorithm).Assembly);
        TypeDocumentation enumDocs = docs.GetTypeDocumentation(typeof(AmbientBottleneckUtilizationAlgorithm));
        Assert.IsNotNull(enumDocs);
        Assert.IsNotNull(enumDocs.Summary);
    }
    [TestMethod]
    public void NoDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(TestDotNetDocumentation).Assembly);
        Assert.IsFalse(docs.PublicTypes.Any());
    }
}
