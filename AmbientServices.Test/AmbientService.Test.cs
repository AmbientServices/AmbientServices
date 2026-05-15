using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestAmbientService
{
    private static readonly AmbientService<ITest> _Test = Ambient.GetService<ITest>();
    private static readonly AmbientService<IAmbientLogger> _Logger = Ambient.GetService<IAmbientLogger>();
    private static readonly AmbientService<IAmbientProgressService> _ProgressService = Ambient.GetService<IAmbientProgressService>();
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
    private static readonly AmbientService<IAmbientLocalCache> _Cache = Ambient.GetService<IAmbientLocalCache>();
    private static readonly AmbientService<IJunk> _Junk = Ambient.GetService<IJunk>();
    private static readonly AmbientService<ILocalTest> _LocalTest = Ambient.GetService<ILocalTest>();
    private static readonly AmbientService<ITest1> _Test1 = Ambient.GetService<ITest1>();
    private static readonly AmbientService<ITest2> _Test2 = Ambient.GetService<ITest2>();
    private static readonly AmbientService<IGlobalOverrideTest> _GlobalOverrideTest = Ambient.GetService<IGlobalOverrideTest>();
    private static readonly AmbientService<IScopedGlobalOverrideTest> _ScopedGlobalOverrideTest = Ambient.GetService<IScopedGlobalOverrideTest>();
    private static readonly AmbientService<ILocalOverrideTest> _LocalOverrideTest = Ambient.GetService<ILocalOverrideTest>();
    private static readonly AmbientService<IScopedLocalOverrideFlowTest> _ScopedLocalOverrideFlow = Ambient.GetService<IScopedLocalOverrideFlowTest>();

    private sealed class ScopedLocalOverrideFlowTestStub : IScopedLocalOverrideFlowTest
    {
    }

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        System.Threading.Tasks.ValueTask t = TraceBuffer.Flush();
        t.GetAwaiter().GetResult();
        DisposeResponsibilityMstestVerification.AfterAllTestsInAssembly();
    }

    [TestMethod]
    public void AmbientServicesBasic()
    {
        ITest test = _Test.Global;
        Assert.IsNotNull(test);
        Assert.AreEqual(_Test.GlobalReference.Service, _Test.Global);
        IAmbientLogger logger = _Logger.Global;
        Assert.IsNotNull(logger);
        AmbientLogger<TestAmbientService> serviceBrokerLogger = new(logger, null);
        IAmbientProgressService progressTracker = _ProgressService.Global;
        Assert.IsNotNull(progressTracker);
        IAmbientSettingsSet settings = _SettingsSet.Global;
        Assert.IsNotNull(settings);
        IJunk junk = _Junk.Global;
        Assert.IsNull(junk);


        ITest compareTest = test;
        Assert.IsNotNull(test);

        int changed = 0;
        ITest updatedTest = test;
        Assert.IsNotNull(updatedTest);
        void globalChanged(object o, EventArgs e) { updatedTest = _Test.Global; ++changed; }
        _Test.GlobalChanged += globalChanged;

        _Test.Global = null;
        Assert.AreEqual(1, changed);
        Assert.IsNull(updatedTest);
        compareTest = null;

        ITest disabledTest = _Test.Global;
        Assert.IsNull(disabledTest);

        _Test.Global = test;
        Assert.AreEqual(2, changed);
        compareTest = test;

        ITest reenabledTest = _Test.Global;
        Assert.IsNotNull(reenabledTest);

        _Test.GlobalChanged -= globalChanged;
    }
    [TestMethod]
    public void NonInterfaceTypeAndNoDefaultConstructor()
    {
        Exception? ex = null;
        void EventHandler(object? sender, InitializationErrorEventArgs e)
        {
            ex = e.Exception;
        }
        Ambient.InitializationError += EventHandler;
        try
        {
            AmbientService<DefaultTest> defaultTest = Ambient.GetService<DefaultTest>();
            Assert.IsInstanceOfType(ex, typeof(ArgumentException), ex?.ToString() ?? "Exception is null!");
        }
        finally
        {
            Ambient.InitializationError -= EventHandler;
        }

        // JRI: Note that this was previously a separate test, but since the InitializationError event is static,
        // when both tests ran at the same time, the errors occasionally got switched.
        // Running them serially solves that problem.
        Ambient.InitializationError += EventHandler;
        try
        {
            Ambient.GetService<INoDefaultConstructor>();
            Assert.IsInstanceOfType(ex, typeof(TargetInvocationException));
        }
        finally
        {
            Ambient.InitializationError -= EventHandler;
        }
    }
    [TestMethod]
    public void MultipleInterfaces()
    {
        DefaultTestAmbientService2.Load();
    }
    [TestMethod]
    public void GlobalServiceOverride()
    {
        IGlobalOverrideTest oldValue = _GlobalOverrideTest.Global;
        try
        {
            _GlobalOverrideTest.Global = null;
            Assert.IsNull(_GlobalOverrideTest.Global);
        }
        finally
        {
            _GlobalOverrideTest.Global = oldValue;
        }
    }
    [TestMethod]
    public void LocalServiceOverride()
    {
        ILocalOverrideTest oldGlobal = _LocalOverrideTest.Global;
        ILocalOverrideTest oldLocalOverride = _LocalOverrideTest.Override;
        using ScopedLocalServiceOverride<ILocalOverrideTest> o = new(null);
        Assert.IsNull(_LocalOverrideTest.Local);
        Assert.AreEqual(oldGlobal, o.OldGlobal);
        Assert.AreEqual(oldLocalOverride, o.OldOverride);
    }
    [TestMethod]
    [DoNotParallelize] // IScopedGlobalOverrideTest.Global is process-wide; parallel methods would race.
    public void GlobalServiceScopedOverride()
    {
        IScopedGlobalOverrideTest oldGlobal = _ScopedGlobalOverrideTest.Global;
        IScopedGlobalOverrideTest oldOverride = _ScopedGlobalOverrideTest.Override;
        using ScopedGlobalServiceOverride<IScopedGlobalOverrideTest> o = new(null);
        Assert.IsNull(_ScopedGlobalOverrideTest.Global);
        Assert.IsNull(_ScopedGlobalOverrideTest.Local);
        Assert.AreEqual(oldGlobal, o.OldGlobal);
        Assert.AreEqual(oldOverride, o.OldOverride);
    }
    [TestMethod]
    [DoNotParallelize]
    public void GlobalServiceScopedOverride_Method()
    {
        IScopedGlobalOverrideTest oldGlobal = _ScopedGlobalOverrideTest.Global;
        using (IDisposable scope = _ScopedGlobalOverrideTest.ScopedGlobalOverride(null))
        {
            Assert.IsNull(_ScopedGlobalOverrideTest.Global);
        }
        Assert.AreEqual(oldGlobal, _ScopedGlobalOverrideTest.Global);
    }
    [TestMethod]
    [DoNotParallelize]
    public void GlobalServiceScopedOverride_WithReplacement()
    {
        IScopedGlobalOverrideTest oldGlobal = _ScopedGlobalOverrideTest.Global;
        IScopedGlobalOverrideTest replacement = new ScopedGlobalOverrideTestStub();
        using ScopedGlobalServiceOverride<IScopedGlobalOverrideTest> o = new(replacement);
        Assert.AreSame(replacement, _ScopedGlobalOverrideTest.Global);
        Assert.AreSame(replacement, _ScopedGlobalOverrideTest.Local);
        Assert.AreEqual(oldGlobal, o.OldGlobal);
    }
    [TestMethod]
    [DoNotParallelize]
    public void GlobalServiceScopedOverride_Nested()
    {
        IScopedGlobalOverrideTest original = _ScopedGlobalOverrideTest.Global;
        IScopedGlobalOverrideTest outerReplacement = new ScopedGlobalOverrideTestStub();
        IScopedGlobalOverrideTest innerReplacement = new ScopedGlobalOverrideTestStub();
        using (ScopedGlobalServiceOverride<IScopedGlobalOverrideTest> outer = new(outerReplacement))
        {
            Assert.AreSame(outerReplacement, _ScopedGlobalOverrideTest.Global);
            using (ScopedGlobalServiceOverride<IScopedGlobalOverrideTest> inner = new(innerReplacement))
            {
                Assert.AreSame(innerReplacement, _ScopedGlobalOverrideTest.Global);
            }
            Assert.AreSame(outerReplacement, _ScopedGlobalOverrideTest.Global);
        }
        Assert.AreSame(original, _ScopedGlobalOverrideTest.Global);
    }
    [TestMethod]
    [DoNotParallelize]
    public void GlobalServiceScopedOverride_RestoresLocalOverride()
    {
        IScopedGlobalOverrideTest oldGlobal = _ScopedGlobalOverrideTest.Global;
        IScopedGlobalOverrideTest localOnly = new ScopedGlobalOverrideTestStub();
        _ScopedGlobalOverrideTest.Local = localOnly;
        try
        {
            using ScopedGlobalServiceOverride<IScopedGlobalOverrideTest> o = new(null);
            Assert.IsNull(_ScopedGlobalOverrideTest.Global);
            Assert.AreSame(localOnly, _ScopedGlobalOverrideTest.Local);
            Assert.AreSame(localOnly, o.OldOverride);
            Assert.AreEqual(oldGlobal, o.OldGlobal);
        }
        finally
        {
            _ScopedGlobalOverrideTest.Override = null;
        }
        Assert.AreEqual(oldGlobal, _ScopedGlobalOverrideTest.Global);
        Assert.AreEqual(oldGlobal, _ScopedGlobalOverrideTest.Local);
    }
    [TestMethod]
    public void TwoInterfacesOneInstance()
    {
        ITest1 test1 = _Test1.Global;
        ITest2 test2 = _Test2.Global;
        Assert.IsTrue(ReferenceEquals(test1, test2));
    }
    [TestMethod]
    public void Override()
    {
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest));

        _LocalTest.Global = new LocalTest2();
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest2));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest2));

        _LocalTest.Local = null;
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest2));
        Assert.IsNull(_LocalTest.Local);

        _LocalTest.Global = null;
        Assert.IsNull(_LocalTest.Global);
        Assert.IsNull(_LocalTest.Local);

        _LocalTest.Local = new LocalTest3();
        Assert.IsNull(_LocalTest.Global);
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest3));

        _LocalTest.Global = new LocalTest2();
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest2));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest3));

        _LocalTest.Local = _LocalTest.Global;
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest2));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest2));
        Assert.AreEqual(_LocalTest.Global, _LocalTest.Local);

        _LocalTest.Global = new LocalTest3();
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest3));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest2));

        _LocalTest.Override = null;
        Assert.IsInstanceOfType(_LocalTest.Global, typeof(LocalTest3));
        Assert.IsInstanceOfType(_LocalTest.Local, typeof(LocalTest3));
        Assert.AreEqual(_LocalTest.Global, _LocalTest.Local);
    }

    [TestMethod]
    public async Task ScopedLocalOverride_ReferenceType_PreservedAfterTaskYield()
    {
        ScopedLocalOverrideFlowTestStub stub = new();
        using IDisposable scope = _ScopedLocalOverrideFlow.ScopedLocalOverride(stub);
        await Task.Yield();
        Assert.AreSame(stub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
    }

    [TestMethod]
    public async Task ScopedLocalOverride_ReferenceType_PreservedAfterTaskDelayConfigureAwaitFalse()
    {
        ScopedLocalOverrideFlowTestStub stub = new();
        using IDisposable scope = _ScopedLocalOverrideFlow.ScopedLocalOverride(stub);
        await Task.Delay(1);
        Assert.AreSame(stub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
    }

    [TestMethod]
    public async Task ScopedLocalOverride_ReferenceType_PreservedInsideTaskRunWithYield()
    {
        ScopedLocalOverrideFlowTestStub stub = new();
        using IDisposable scope = _ScopedLocalOverrideFlow.ScopedLocalOverride(stub);
        await Task.Run(async () =>
        {
            await Task.Yield();
            Assert.AreSame(stub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
        });
        Assert.AreSame(stub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
    }

    [TestMethod]
    public void ScopedLocalOverride_Nested_RestoresOldOverrideAfterInnerDispose()
    {
        IScopedLocalOverrideFlowTest originalGlobal = _ScopedLocalOverrideFlow.Global;
        Assert.IsNotNull(originalGlobal);
        ScopedLocalOverrideFlowTestStub outerStub = new();
        ScopedLocalOverrideFlowTestStub innerStub = new();
        using (IDisposable outer = _ScopedLocalOverrideFlow.ScopedLocalOverride(outerStub))
        {
            Assert.AreSame(outerStub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
            using (ScopedLocalServiceOverride<IScopedLocalOverrideFlowTest> inner = new(innerStub))
            {
                Assert.AreSame(innerStub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
                Assert.AreSame(outerStub, inner.OldOverride);
            }
            Assert.AreSame(outerStub, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
        }
        Assert.AreSame(originalGlobal, Ambient.GetService<IScopedLocalOverrideFlowTest>().Local);
    }

    /// <summary>
    /// When <see cref="AmbientService{T}.Local"/> is null due to local suppression, <see cref="AmbientService{T}.RawLocalOverride"/> is
    /// <see cref="AmbientService{T}.SuppressedImplementation"/>; the scoped override dispose path must restore that value,
    /// not <see cref="AmbientService{T}.Override"/> = null (which only clears the override to follow global).
    /// </summary>
    [TestMethod]
    public void ScopedLocalOverride_RestoresLocalSuppression_WhenGlobalStillSet()
    {
        IScopedLocalOverrideFlowTest originalGlobal = _ScopedLocalOverrideFlow.Global;
        Assert.IsNotNull(originalGlobal);
        _ScopedLocalOverrideFlow.Local = null;
        try
        {
            Assert.IsNull(_ScopedLocalOverrideFlow.Local, "local suppression should hide the global implementation");
            Assert.AreSame(originalGlobal, _ScopedLocalOverrideFlow.Global);
            ScopedLocalOverrideFlowTestStub stub = new();
            using (_ScopedLocalOverrideFlow.ScopedLocalOverride(stub))
            {
                Assert.AreSame(stub, _ScopedLocalOverrideFlow.Local);
            }
            Assert.IsNull(_ScopedLocalOverrideFlow.Local, "after dispose, local suppression must be restored, not cleared to follow global");
            Assert.AreSame(originalGlobal, _ScopedLocalOverrideFlow.Global);
        }
        finally
        {
            _ScopedLocalOverrideFlow.Override = null;
        }
    }

    /// <summary>
    /// Same <see cref="AmbientService{T}.RawLocalOverride"/> / suppression-sentinel restore requirement as
    /// <see cref="ScopedLocalOverride_RestoresLocalSuppression_WhenGlobalStillSet"/> for <see cref="ScopedGlobalServiceOverride{T}"/>.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public void ScopedGlobalOverride_RestoresLocalSuppression_WhenGlobalStillSet()
    {
        IScopedGlobalOverrideTest originalGlobal = _ScopedGlobalOverrideTest.Global;
        Assert.IsNotNull(originalGlobal);
        _ScopedGlobalOverrideTest.Local = null;
        try
        {
            Assert.IsNull(_ScopedGlobalOverrideTest.Local);
            Assert.AreSame(originalGlobal, _ScopedGlobalOverrideTest.Global);
            ScopedGlobalOverrideTestStub replacement = new();
            using (new ScopedGlobalServiceOverride<IScopedGlobalOverrideTest>(replacement))
            {
                Assert.AreSame(replacement, _ScopedGlobalOverrideTest.Global);
                Assert.IsNull(_ScopedGlobalOverrideTest.Local);
            }
            Assert.IsNull(_ScopedGlobalOverrideTest.Local, "after dispose, local suppression must be restored, not cleared to follow global");
            Assert.AreSame(originalGlobal, _ScopedGlobalOverrideTest.Global);
        }
        finally
        {
            _ScopedGlobalOverrideTest.Override = null;
        }
    }
}

interface IJunk
{ }
interface ITest
{ }
[DefaultAmbientService]
public class DefaultTest : ITest
{
    static void Load()
    {
    }
}
interface INoDefaultConstructor
{ }
[DefaultAmbientService]
public class NoDefaultConstructor : INoDefaultConstructor
{
    public NoDefaultConstructor(int test)
    {
    }
    static void Load()
    {
    }
}
interface ITest1 { }
interface ITest2 { }

[DefaultAmbientService]
class MultiInterfaceTest : ITest1, ITest2
{
}

interface ILocalTest
{
}
[DefaultAmbientService]
class LocalTest : ILocalTest
{
}
interface ICallContextServiceAccessorTest
{
}
[DefaultAmbientService]
class CallContextServiceAccessorTest : ICallContextServiceAccessorTest
{
}
class CallContextServiceAccessorTest2 : ICallContextServiceAccessorTest
{
}
class LocalTest2 : ILocalTest
{
}
class LocalTest3 : ILocalTest
{
}
interface IGlobalOverrideTest
{
}
[DefaultAmbientService]
class GlobalOverrideTest : IGlobalOverrideTest
{
}
interface ILocalOverrideTest
{
}
[DefaultAmbientService]
class LocalOverrideTest : ILocalOverrideTest
{
}
interface IScopedGlobalOverrideTest
{
}
[DefaultAmbientService]
class ScopedGlobalOverrideAmbientDefault : IScopedGlobalOverrideTest
{
}
class ScopedGlobalOverrideTestStub : IScopedGlobalOverrideTest
{
}

interface IScopedLocalOverrideFlowTest
{
}

[DefaultAmbientService]
class ScopedLocalOverrideFlowTestDefault : IScopedLocalOverrideFlowTest
{
}

interface ITestAmbientService
{ }

interface ITestAmbientService2
{ }

[DefaultAmbientService(typeof(ITestAmbientService), typeof(ITestAmbientService2))]
internal class DefaultTestAmbientService2 : ITestAmbientService, ITestAmbientService2
{
    private static readonly AmbientService<ITestAmbientService> _Accessor = Ambient.GetService<ITestAmbientService>();
    private static readonly AmbientService<ITestAmbientService2> _Accessor2 = Ambient.GetService<ITestAmbientService2>();
    public static void Load()
    {
        ITestAmbientService service = _Accessor.Global;
        if (service is not DefaultTestAmbientService2) throw new InvalidOperationException();
        ITestAmbientService2 service2 = _Accessor2.Global;
        if (service2 is not DefaultTestAmbientService2) throw new InvalidOperationException();
    }
}
