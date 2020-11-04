using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace TestAmbientServices
{
    interface IJunk
    { }
    interface ITest
    { }
    [DefaultAmbientServiceProvider]
    public class DefaultTest : ITest
    {
        static void Load()
        {
        }
    }
    [TestClass]
    public class TestServiceAccessor
    {
        private static readonly ServiceAccessor<ITest> _TestProvider = Service.GetAccessor<ITest>();
        private static readonly ServiceAccessor<IAmbientLoggerProvider> _LoggerProvider = Service.GetAccessor<IAmbientLoggerProvider>();
        private static readonly ServiceAccessor<IAmbientProgressProvider> _ProgressProvider = Service.GetAccessor<IAmbientProgressProvider>();
        private static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsProvider = Service.GetAccessor<IAmbientSettingsProvider>();
        private static readonly ServiceAccessor<IJunk> _JunkProvider = Service.GetAccessor<IJunk>();
        private static readonly ServiceAccessor<ILocalTest> _LocalTestProvider = Service.GetAccessor<ILocalTest>();
        private static readonly ServiceAccessor<ILateAssignmentTest> _LateAssignmentTestProvider = Service.GetAccessor<ILateAssignmentTest>();
        private static readonly ServiceAccessor<ITest1> _Test1Provider = Service.GetAccessor<ITest1>();
        private static readonly ServiceAccessor<ITest2> _Test2Provider = Service.GetAccessor<ITest2>();
        private static readonly ServiceAccessor<IGlobalOverrideTest> _GlobalOverrideTest = Service.GetAccessor<IGlobalOverrideTest>();
        private static readonly ServiceAccessor<ILocalOverrideTest> _LocalOverrideTest = Service.GetAccessor<ILocalOverrideTest>();

        [TestMethod]
        public void AmbientServicesProviders()
        {
            ITest test = _TestProvider.GlobalProvider;
            Assert.IsNotNull(test);
            IAmbientLoggerProvider logger = _LoggerProvider.GlobalProvider;
            Assert.IsNotNull(logger);
            AmbientLogger<TestServiceAccessor> serviceBrokerLogger = new AmbientLogger<TestServiceAccessor>(logger);
            IAmbientProgressProvider progressTracker = _ProgressProvider.GlobalProvider;
            Assert.IsNotNull(progressTracker);
            IAmbientSettingsProvider settings = _SettingsProvider.GlobalProvider;
            Assert.IsNotNull(settings);
            IJunk junk = _JunkProvider.GlobalProvider;
            Assert.IsNull(junk);



            ITest compareTest = test;
            Assert.IsNotNull(test);

            int changed = 0;
            ITest updatedTest = test;
            Assert.IsNotNull(updatedTest);
            _TestProvider.GlobalProviderChanged += (s, e) => { updatedTest = _TestProvider.GlobalProvider; ++changed; };

            _TestProvider.GlobalProvider = null;
            Assert.AreEqual(1, changed);
            Assert.IsNull(updatedTest);
            compareTest = null;

            ITest disabledTest = _TestProvider.GlobalProvider;
            Assert.IsNull(disabledTest);

            _TestProvider.GlobalProvider = test;
            Assert.AreEqual(2, changed);
            compareTest = test;

            ITest reenabledTest = _TestProvider.GlobalProvider;
            Assert.IsNotNull(reenabledTest);
        }

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            ServiceAccessor<DefaultTest> DefaultTestProvider = Service.GetAccessor<DefaultTest>();
            DefaultTest test = DefaultTestProvider.GlobalProvider;
        }

        [TestMethod]
        public void MultipleInterfaces()
        {
            DefaultTestAmbientService2.Load();
        }
        [TestMethod]
        public void GlobalServiceOverride()
        {
            IGlobalOverrideTest oldValue = _GlobalOverrideTest.GlobalProvider;
            try
            {
                _GlobalOverrideTest.GlobalProvider = null;
                Assert.IsNull(_GlobalOverrideTest.GlobalProvider);
            }
            finally
            {
                _GlobalOverrideTest.GlobalProvider = oldValue;
            }
        }
        [TestMethod]
        public void LocalServiceOverride()
        {
            ILocalOverrideTest oldGlobal = _LocalOverrideTest.GlobalProvider;
            ILocalOverrideTest oldLocalOverride = _LocalOverrideTest.ProviderOverride;
            using (LocalServiceScopedOverride<ILocalOverrideTest> o = new LocalServiceScopedOverride<ILocalOverrideTest>(null))
            {
                Assert.IsNull(_LocalOverrideTest.Provider);
                Assert.AreEqual(oldGlobal, o.OldGlobal);
                Assert.AreEqual(oldLocalOverride, o.OldOverride);
            }
        }
        [TestMethod]
        public void NoOpAssemblyOnLoad()
        {
            using (new LocalServiceScopedOverride<IAmbientLoggerProvider>(null))
            {
                AssemblyLoader.OnLoad(Assembly.GetExecutingAssembly());
            }
        }
        [TestMethod]
        public void AssemblyLoadAndLateAssignment()
        {
            // try to get this one now
            ILateAssignmentTest test = _LateAssignmentTestProvider.GlobalProvider;
            Assert.IsNull(test, test?.ToString());

            LateAssignment();

            // NOW this should be available
            test = _LateAssignmentTestProvider.GlobalProvider;
            Assert.IsNotNull(test);
        }
        private void LateAssignment()
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // NOW load the assembly (this should register the default implementation)
            Assembly assembly = Assembly.LoadFile(path + "\\TestAmbientServicesDelayedLoad.dll");
        }
        [TestMethod]
        public void TypesFromException()
        {
            ReflectionTypeLoadException ex = new ReflectionTypeLoadException(new Type[] { typeof(string) }, new Exception[0]);
            Assert.AreEqual(1, AmbientServices.AssemblyExtensions.TypesFromException(ex).Count());
            ex = new ReflectionTypeLoadException(new Type[] { typeof(string), null }, new Exception[0]);
            Assert.AreEqual(1, AmbientServices.AssemblyExtensions.TypesFromException(ex).Count());
        }
        [TestMethod]
        public void DoesAssemblyReferToAssembly()
        {
            Assert.IsFalse(AmbientServices.AssemblyExtensions.DoesAssemblyReferToAssembly(typeof(System.ValueTuple).Assembly, Assembly.GetExecutingAssembly()));
            Assert.IsTrue(AmbientServices.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), typeof(IAmbientCacheProvider).Assembly));
        }
        [TestMethod]
        public void TwoInterfacesOneInstance()
        {
            ITest1 test1 = _Test1Provider.GlobalProvider;
            ITest2 test2 = _Test2Provider.GlobalProvider;
            Assert.IsTrue(Object.ReferenceEquals(test1, test2));
        }
        [TestMethod]
        public void Override()
        {
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest));

            _LocalTestProvider.GlobalProvider = new LocalTest2();
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest2));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest2));

            _LocalTestProvider.Provider = null;
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest2));
            Assert.IsNull(_LocalTestProvider.Provider);

            _LocalTestProvider.GlobalProvider = null;
            Assert.IsNull(_LocalTestProvider.GlobalProvider);
            Assert.IsNull(_LocalTestProvider.Provider);

            _LocalTestProvider.Provider = new LocalTest3();
            Assert.IsNull(_LocalTestProvider.GlobalProvider);
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest3));

            _LocalTestProvider.GlobalProvider = new LocalTest2();
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest2));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest3));

            _LocalTestProvider.Provider = _LocalTestProvider.GlobalProvider;
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest2));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest2));
            Assert.AreEqual(_LocalTestProvider.GlobalProvider, _LocalTestProvider.Provider);

            _LocalTestProvider.GlobalProvider = new LocalTest3();
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest3));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest2));

            _LocalTestProvider.ProviderOverride = null;
            Assert.IsInstanceOfType(_LocalTestProvider.GlobalProvider, typeof(LocalTest3));
            Assert.IsInstanceOfType(_LocalTestProvider.Provider, typeof(LocalTest3));
            Assert.AreEqual(_LocalTestProvider.GlobalProvider, _LocalTestProvider.Provider);
        }
    }

    interface ITest1 { }
    interface ITest2 { }

    [DefaultAmbientServiceProvider]
    class MultiInterfaceTest : ITest1, ITest2
    {
    }

    interface ILocalTest
    {
    }
    [DefaultAmbientServiceProvider]
    class LocalTest : ILocalTest
    {
    }
    interface ICallContextServiceAccessorTest
    {
    }
    [DefaultAmbientServiceProvider]
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
    [DefaultAmbientServiceProvider]
    class GlobalOverrideTest : IGlobalOverrideTest
    {
    }
    interface ILocalOverrideTest
    {
    }
    [DefaultAmbientServiceProvider]
    class LocalOverrideTest : ILocalOverrideTest
    {
    }

    interface ITestAmbientService
    { }

    interface ITestAmbientService2
    { }

    [DefaultAmbientServiceProvider(typeof(ITestAmbientService), typeof(ITestAmbientService2))]
    internal class DefaultTestAmbientService2 : ITestAmbientService, ITestAmbientService2
    {
        private static readonly ServiceAccessor<ITestAmbientService> _Accessor = Service.GetAccessor<ITestAmbientService>();
        private static readonly ServiceAccessor<ITestAmbientService2> _Accessor2 = Service.GetAccessor<ITestAmbientService2>();
        public static void Load()
        {
            ITestAmbientService service = _Accessor.GlobalProvider;
            if (!(service is DefaultTestAmbientService2)) throw new InvalidOperationException();
            ITestAmbientService2 service2 = _Accessor2.GlobalProvider;
            if (!(service2 is DefaultTestAmbientService2)) throw new InvalidOperationException();
        }
    }
}
