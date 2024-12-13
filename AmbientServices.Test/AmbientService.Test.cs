using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace AmbientServices.Test
{
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
        private static readonly AmbientService<ILocalOverrideTest> _LocalOverrideTest = Ambient.GetService<ILocalOverrideTest>();

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            System.Threading.Tasks.ValueTask t = TraceBuffer.Flush();
            t.ConfigureAwait(false).GetAwaiter().GetResult();
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
}
