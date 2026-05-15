using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        foreach (MethodInfo mi in typeof(AmbientSharedCache<>).GetMethods(BindingFlags.Public))
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

    [TestMethod]
    public void LoadNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DotNetDocumentation.Load((Type)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => DotNetDocumentation.Load((Assembly)null!));
    }

    [TestMethod]
    public void LoadTypeOverloadAndCacheUsesSameInstance()
    {
        Assembly asm = typeof(DotNetDocumentation).Assembly;
        DotNetDocumentation a = DotNetDocumentation.Load(asm);
        DotNetDocumentation b = DotNetDocumentation.Load(typeof(DotNetDocumentation));
        Assert.AreSame(a, b);
    }

    [TestMethod]
    public void DynamicAssemblyUsesEmptyDocumentation()
    {
        AssemblyName an = new("DotNetDocDynamic" + Guid.NewGuid().ToString("N"));
        AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
        ModuleBuilder mod = ab.DefineDynamicModule("main");
        TypeBuilder tb = mod.DefineType("DynamicDocType", TypeAttributes.Public);
        Type t = tb.CreateType();
        DotNetDocumentation docs = DotNetDocumentation.Load(t.Assembly);
        Assert.IsFalse(docs.PublicTypes.Any());
    }

    [TestMethod]
    public void GetTypeOrProxy_ReturnsProxyWhenAttributePresent()
    {
        Assert.AreSame(typeof(string), DotNetDocumentation.GetTypeOrProxy(typeof(IPAddressConverter)));
        Assert.AreSame(typeof(AmbientConsoleLogger), DotNetDocumentation.GetTypeOrProxy(typeof(AmbientConsoleLogger)));
    }

    [TestMethod]
    public void PropertyFieldAndMemberDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientFileLogger).Assembly);
        PropertyInfo? filePrefix = typeof(AmbientFileLogger).GetProperty(nameof(AmbientFileLogger.FilePrefix));
        Assert.IsNotNull(filePrefix);
        MemberDocumentation? propDoc = docs.GetPropertyDocumentation(filePrefix);
        Assert.IsNotNull(propDoc);
        Assert.IsFalse(string.IsNullOrEmpty(propDoc.Name));
        Assert.IsNotNull(propDoc.Summary);

        FieldInfo? kilo = typeof(SI).GetField(nameof(SI.Kilo));
        Assert.IsNotNull(kilo);
        MemberDocumentation? fieldDoc = docs.GetFieldDocumentation(kilo);
        Assert.IsNotNull(fieldDoc);
        Assert.IsFalse(string.IsNullOrEmpty(fieldDoc.Name));

        Assert.IsNotNull(docs.GetMemberDocumentation(filePrefix));
        Assert.IsNotNull(docs.GetMemberDocumentation(kilo));
        Assert.IsNull(docs.GetMemberDocumentation(typeof(AmbientFileLogger).GetMethod(nameof(AmbientFileLogger.Log), new[] { typeof(string) })!));
    }

    [TestMethod]
    public void NullableTypeDocumentationAndOpenGenericHumanReadableNames()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientBottleneckUtilizationAlgorithm).Assembly);
        TypeDocumentation? nullableDoc = docs.GetNullableTypeDocumentation(typeof(AmbientBottleneckUtilizationAlgorithm));
        Assert.IsNotNull(nullableDoc);
        StringAssert.StartsWith(nullableDoc.Name, "Nullable<");

        TypeDocumentation? twoStageOpen = docs.GetTypeDocumentation(typeof(AmbientTwoStageCache<>));
        Assert.IsNotNull(twoStageOpen);
        Assert.IsTrue(twoStageOpen.Name.Contains('<', StringComparison.Ordinal));
    }

    [TestMethod]
    public void GenericTypeDefinitionHasTypeParameterDocumentation()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientService<>).Assembly);
        TypeDocumentation? openGeneric = docs.GetTypeDocumentation(typeof(AmbientService<>));
        Assert.IsNotNull(openGeneric);
        Assert.IsNotNull(openGeneric.TypeParameters);
        Assert.IsTrue(openGeneric.TypeParameters!.Any(tp => tp.Name == "T"));
    }

    [TestMethod]
    public void GenericMethodDocumentation_IncludesArityInDocId()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(TaskUtilities).Assembly);
        MethodInfo def = typeof(TaskUtilities).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(TaskUtilities.ValueTaskFromResult) && m.IsGenericMethodDefinition);
        MethodDocumentation? md = docs.GetMethodDocumentation(def);
        Assert.IsNotNull(md);
        Assert.IsNotNull(md.Parameters);

        MethodInfo retrieveDef = typeof(AmbientSharedCache).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == nameof(AmbientSharedCache.Retrieve) && m.IsGenericMethodDefinition);
        MethodInfo retrieveClosed = retrieveDef.MakeGenericMethod(typeof(string));
        MethodDocumentation? closedDoc = docs.GetMethodDocumentation(retrieveClosed);
        Assert.IsNotNull(closedDoc);
    }

    [TestMethod]
    public void ParameterlessConstructorDocumentationHasNoParameterDocs()
    {
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(AmbientServiceProfilerCoordinator).Assembly);
        ConstructorInfo? defaultCtor = typeof(AmbientServiceProfilerCoordinator).GetConstructor(Type.EmptyTypes);
        Assert.IsNotNull(defaultCtor);
        MethodDocumentation? md = docs.GetMethodDocumentation(defaultCtor);
        Assert.IsNotNull(md);
        Assert.IsNull(md.Parameters);
    }

    [TestMethod]
    public void MethodWithDeclaringTypeNullReturnsNull()
    {
        DynamicMethod dyn = new("DynDoc", typeof(void), Type.EmptyTypes, typeof(TestDotNetDocumentation).Module, skipVisibility: true);
        DotNetDocumentation docs = DotNetDocumentation.Load(typeof(DotNetDocumentation).Assembly);
        Assert.IsNull(docs.GetMethodDocumentation(dyn));
    }

    [TestMethod]
    public void BuildDisambiguatingParameterList_GenericParameters()
    {
        Type inner = typeof(AmbientDocumentationGenericParameterFixture<>).GetNestedType("InnerNonGeneric", BindingFlags.Public)!;
        MethodInfo? usesOuterT = inner.GetMethod("UsesOuterGenericParam", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(usesOuterT);
        // Open generic nested type: outer type parameter is encoded as ``0 in ECMA XML doc IDs.
        const string ecmaGp0 = "\u0060\u0060" + "0";
        Assert.AreEqual($"({ecmaGp0})", DotNetDocumentation.BuildDisambiguatingParameterList(usesOuterT));

        ConstructorInfo kvpCtor = typeof(KeyValuePair<,>).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == 2);
        string kvpList = DotNetDocumentation.BuildDisambiguatingParameterList(kvpCtor);
        StringAssert.Contains(kvpList, "``0");
        StringAssert.Contains(kvpList, "``1");
    }

    [TestMethod]
    public void GetHumanReadableTypeName_CoversArraysNullableAndCompilerNestedTypes()
    {
        Assert.AreEqual("Int32[]", DotNetDocumentation.GetHumanReadableTypeName(typeof(int[])));
        Assert.AreEqual("Int32[][]", DotNetDocumentation.GetHumanReadableTypeName(typeof(int[][])));
        Assert.AreEqual("Int32?", DotNetDocumentation.GetHumanReadableTypeName(typeof(int?)));

        Type? asyncState = typeof(HumanReadableAsyncNameFixture).GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("NoOpAsync", StringComparison.Ordinal));
        Assert.IsNotNull(asyncState, "Expected compiler-generated async state machine nested type.");
        string nestedName = DotNetDocumentation.GetHumanReadableTypeName(asyncState);
        StringAssert.Contains(nestedName, ".");
        Assert.IsFalse(nestedName.Contains('<', StringComparison.Ordinal));
    }

}

/// <summary>
/// Fixture for exercising generic-parameter XML doc ID disambiguation when the parameter comes from an outer generic type.
/// </summary>
public static class AmbientDocumentationGenericParameterFixture<T>
{
    /// <summary>
    /// Non-generic nested type; method uses <typeparamref name="T"/> from the outer type.
    /// </summary>
    public sealed class InnerNonGeneric
    {
        /// <summary>Uses outer T.</summary>
        public void UsesOuterGenericParam(T value) { }
    }
}

internal static class HumanReadableAsyncNameFixture
{
    internal static async Task NoOpAsync() => await Task.Yield();
}
