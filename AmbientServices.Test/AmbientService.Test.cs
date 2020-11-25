using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace AmbientServices.Test
{
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
    [TestClass]
    public class TestAmbientService
    {
        private static readonly AmbientService<ITest> _Test = Ambient.GetService<ITest>();
        private static readonly AmbientService<IAmbientLogger> _Logger = Ambient.GetService<IAmbientLogger>();
        private static readonly AmbientService<IAmbientProgressService> _ProgressService = Ambient.GetService<IAmbientProgressService>();
        private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
        private static readonly AmbientService<IAmbientCache> _Cache = Ambient.GetService<IAmbientCache>();
        private static readonly AmbientService<IJunk> _Junk = Ambient.GetService<IJunk>();
        private static readonly AmbientService<ILocalTest> _LocalTest = Ambient.GetService<ILocalTest>();
        private static readonly AmbientService<ILateAssignmentTest> _LateAssignmentTest = Ambient.GetService<ILateAssignmentTest>();
        private static readonly AmbientService<ITest1> _Test1 = Ambient.GetService<ITest1>();
        private static readonly AmbientService<ITest2> _Test2 = Ambient.GetService<ITest2>();
        private static readonly AmbientService<IGlobalOverrideTest> _GlobalOverrideTest = Ambient.GetService<IGlobalOverrideTest>();
        private static readonly AmbientService<ILocalOverrideTest> _LocalOverrideTest = Ambient.GetService<ILocalOverrideTest>();

        [TestMethod]
        public void AmbientServicesBasic()
        {
            ITest test = _Test.Global;
            Assert.IsNotNull(test);
            IAmbientLogger logger = _Logger.Global;
            Assert.IsNotNull(logger);
            AmbientLogger<TestAmbientService> serviceBrokerLogger = new AmbientLogger<TestAmbientService>(logger);
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
            EventHandler<EventArgs> globalChanged = (o,e) => { updatedTest = _Test.Global; ++changed; }; 
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

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            AmbientService<DefaultTest> defaultTest = Ambient.GetService<DefaultTest>();
            DefaultTest test = defaultTest.Global;
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
            using (ScopedLocalServiceOverride<ILocalOverrideTest> o = new ScopedLocalServiceOverride<ILocalOverrideTest>(null))
            {
                Assert.IsNull(_LocalOverrideTest.Local);
                Assert.AreEqual(oldGlobal, o.OldGlobal);
                Assert.AreEqual(oldLocalOverride, o.OldOverride);
            }
        }
        [TestMethod]
        public void NoOpAssemblyOnLoad()
        {
            using (new ScopedLocalServiceOverride<IAmbientLogger>(null))
            {
                AssemblyLoader.OnLoad(Assembly.GetExecutingAssembly());
            }
        }
        [TestMethod]
        public void AssemblyLoadAndLateAssignment()
        {
            // try to get this one now
            ILateAssignmentTest test = _LateAssignmentTest.Global;
            Assert.IsNull(test, test?.ToString());

            LateAssignment();

            // NOW this should be available
            test = _LateAssignmentTest.Global;
            Assert.IsNotNull(test);
        }
        private void LateAssignment()
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // NOW load the assembly (this should register the default implementation)
            Assembly assembly = Assembly.LoadFile(path + "\\AmbientServices.Test.DelayedLoad.dll");
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
            Assert.IsTrue(AmbientServices.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), typeof(IAmbientCache).Assembly));
            Assert.IsTrue(AmbientServices.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), Assembly.GetExecutingAssembly()));
        }
        [TestMethod]
        public void ReflectionTypeLoadException()
        {
            string dllPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ReflectionTypeLoadException.Assembly.dll");
            Type[] types = AmbientServices.AssemblyExtensions.GetLoadableTypes(Assembly.LoadFrom(dllPath)).ToArray();
        }
        [TestMethod]
        public void TwoInterfacesOneInstance()
        {
            ITest1 test1 = _Test1.Global;
            ITest2 test2 = _Test2.Global;
            Assert.IsTrue(Object.ReferenceEquals(test1, test2));
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
            if (!(service is DefaultTestAmbientService2)) throw new InvalidOperationException();
            ITestAmbientService2 service2 = _Accessor2.Global;
            if (!(service2 is DefaultTestAmbientService2)) throw new InvalidOperationException();
        }
    }
}
