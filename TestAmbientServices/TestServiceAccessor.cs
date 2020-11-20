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
        private static readonly ServiceReference<ITest> _TestProvider = Service.GetReference<ITest>();
        private static readonly ServiceReference<IAmbientLoggerProvider> _LoggerProvider = Service.GetReference<IAmbientLoggerProvider>();
        private static readonly ServiceReference<IAmbientProgressProvider> _ProgressProvider = Service.GetReference<IAmbientProgressProvider>();
        private static readonly ServiceReference<IAmbientSettingsProvider> _SettingsProvider = Service.GetReference<IAmbientSettingsProvider>();
        private static readonly ServiceReference<IAmbientCacheProvider> _CacheProvider = Service.GetReference<IAmbientCacheProvider>();
        private static readonly ServiceReference<IJunk> _JunkProvider = Service.GetReference<IJunk>();
        private static readonly ServiceReference<ILocalTest> _LocalTestProvider = Service.GetReference<ILocalTest>();
        private static readonly ServiceReference<ILateAssignmentTest> _LateAssignmentTestProvider = Service.GetReference<ILateAssignmentTest>();
        private static readonly ServiceReference<ITest1> _Test1Provider = Service.GetReference<ITest1>();
        private static readonly ServiceReference<ITest2> _Test2Provider = Service.GetReference<ITest2>();
        private static readonly ServiceReference<IGlobalOverrideTest> _GlobalOverrideTest = Service.GetReference<IGlobalOverrideTest>();
        private static readonly ServiceReference<ILocalOverrideTest> _LocalOverrideTest = Service.GetReference<ILocalOverrideTest>();

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
            EventHandler<EventArgs> providerChanged = (o,e) => { updatedTest = _TestProvider.GlobalProvider; ++changed; }; 
            _TestProvider.GlobalProviderChanged += providerChanged;

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

            _TestProvider.GlobalProviderChanged -= providerChanged;
        }

        [TestMethod]
        public void AmbientServicesLocalProviderChanged()
        {
            BasicAmbientCache cache1 = new BasicAmbientCache();
            Service.SetLocalProvider<IAmbientCacheProvider>(cache1);
            bool changeNotified = false;
            _CacheProvider.LocalReference.ProviderChanged += (s, e) => { changeNotified = true; };
            Assert.IsFalse(changeNotified);
            BasicAmbientCache cache2 = new BasicAmbientCache();
            using (LocalProviderScopedOverride<IAmbientCacheProvider> o2 = new LocalProviderScopedOverride<IAmbientCacheProvider>(cache2))
            {
                Assert.AreEqual(cache2, _CacheProvider.LocalReference.Provider);
                Assert.IsTrue(changeNotified);
            }
            Service.RevertToGlobalProvider<IAmbientCacheProvider>();
        }

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            ServiceReference<DefaultTest> DefaultTestProvider = Service.GetReference<DefaultTest>();
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
            using (LocalProviderScopedOverride<ILocalOverrideTest> o = new LocalProviderScopedOverride<ILocalOverrideTest>(null))
            {
                Assert.IsNull(_LocalOverrideTest.Provider);
                Assert.AreEqual(oldGlobal, o.OldGlobal);
                Assert.AreEqual(oldLocalOverride, o.OldOverride);
            }
        }
        [TestMethod]
        public void NoOpAssemblyOnLoad()
        {
            using (new LocalProviderScopedOverride<IAmbientLoggerProvider>(null))
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
            Assert.IsTrue(AmbientServices.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), Assembly.GetExecutingAssembly()));
        }
        [TestMethod]
        public void ReflectionTypeLoadException()
        {
            string dllPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ReflectionTypeLoadExceptionAssembly.dll");
            Type[] types = AmbientServices.AssemblyExtensions.GetLoadableTypes(Assembly.LoadFrom(dllPath)).ToArray();
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
        private static readonly ServiceReference<ITestAmbientService> _Accessor = Service.GetReference<ITestAmbientService>();
        private static readonly ServiceReference<ITestAmbientService2> _Accessor2 = Service.GetReference<ITestAmbientService2>();
        public static void Load()
        {
            ITestAmbientService service = _Accessor.GlobalProvider;
            if (!(service is DefaultTestAmbientService2)) throw new InvalidOperationException();
            ITestAmbientService2 service2 = _Accessor2.GlobalProvider;
            if (!(service2 is DefaultTestAmbientService2)) throw new InvalidOperationException();
        }
    }
}
