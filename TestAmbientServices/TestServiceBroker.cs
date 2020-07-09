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
    [DefaultAmbientService]
    public class DefaultTest : ITest
    {
        static void Load()
        {
        }
    }
    [TestClass]
    public class TestServiceBroker
    {
        [TestMethod]
        public void AmbientServicesBroker()
        {
            ITest test = ServiceBroker<ITest>.GlobalImplementation;
            Assert.IsNotNull(test);
            IAmbientLogger logger = ServiceBroker<IAmbientLogger>.GlobalImplementation;
            Assert.IsNotNull(logger);
            ILogger<TestServiceBroker> serviceBrokerLogger = logger.GetLogger<TestServiceBroker>();
            IAmbientProgress progressTracker = ServiceBroker<IAmbientProgress>.GlobalImplementation;
            Assert.IsNotNull(progressTracker);
            IAmbientSettings settings = ServiceBroker<IAmbientSettings>.GlobalImplementation;
            Assert.IsNotNull(settings);
            IJunk junk = ServiceBroker<IJunk>.GlobalImplementation;
            Assert.IsNull(junk);



            ITest compareTest = test;
            Assert.IsNotNull(test);

            int changed = 0;
            ServiceBroker<ITest>.GlobalImplementationChanged += (s, e) => { Assert.AreEqual(e.OldImplementation, compareTest); ++changed; };

            ServiceBroker<ITest>.GlobalImplementation = null;
            Assert.AreEqual(1, changed);
            compareTest = null;

            ITest disabledTest = ServiceBroker<ITest>.GlobalImplementation;
            Assert.IsNull(disabledTest);

            ServiceBroker<ITest>.GlobalImplementation = test;
            Assert.AreEqual(2, changed);
            compareTest = test;

            ITest reenabledTest = ServiceBroker<ITest>.GlobalImplementation;
            Assert.IsNotNull(reenabledTest);
        }

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            DefaultTest test = ServiceBroker<DefaultTest>.GlobalImplementation;
        }
        [TestMethod]
        public void AssemblyLoadAndLateAssignment()
        {
            // try to get this one now
            ILateAssignmentTest test = ServiceBroker<ILateAssignmentTest>.GlobalImplementation;
            Assert.IsNull(test);

            LateAssignment();

            // NOW this should be available
            test = ServiceBroker<ILateAssignmentTest>.GlobalImplementation;
            Assert.IsNotNull(test);
        }
        private void LateAssignment()
        {
            // NOW load the assembly (this should register the default implementation)
            TestAmbientServices2.DefaultTestAmbientService.Load();
        }
        [TestMethod]
        public void TypesFromException()
        {
            ReflectionTypeLoadException ex = new ReflectionTypeLoadException(new Type[] { typeof(string) }, new Exception[0]);
            Assert.AreEqual(1, DefaultAmbientServices.TypesFromException(ex).Count());
            ex = new ReflectionTypeLoadException(new Type[] { typeof(string), null }, new Exception[0]);
            Assert.AreEqual(1, DefaultAmbientServices.TypesFromException(ex).Count());
        }
        [TestMethod]
        public void DoesAssemblyReferToAssembly()
        {
            Assert.IsFalse(DefaultAmbientServices.DoesAssemblyReferToAssembly(typeof(System.ValueTuple).Assembly, Assembly.GetExecutingAssembly()));
        }
        [TestMethod]
        public void TwoInterfacesOneInstance()
        {
            ITest1 test1 = ServiceBroker<ITest1>.GlobalImplementation;
            ITest2 test2 = ServiceBroker<ITest2>.GlobalImplementation;
            Assert.IsTrue(Object.ReferenceEquals(test1, test2));
        }
        [TestMethod]
        public void Override()
        {
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest));
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest));

            ServiceBroker<ILocalTest>.GlobalImplementation = new LocalTest2();
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest2));
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest2));

            ServiceBroker<ILocalTest>.LocalImplementation = null;
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest2));
            Assert.IsNull(ServiceBroker<ILocalTest>.LocalImplementation);

            ServiceBroker<ILocalTest>.GlobalImplementation = null;
            Assert.IsNull(ServiceBroker<ILocalTest>.GlobalImplementation);
            Assert.IsNull(ServiceBroker<ILocalTest>.LocalImplementation);

            ServiceBroker<ILocalTest>.LocalImplementation = new LocalTest3();
            Assert.IsNull(ServiceBroker<ILocalTest>.GlobalImplementation);
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest3));

            ServiceBroker<ILocalTest>.GlobalImplementation = new LocalTest2();
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest2));
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest3));

            ServiceBroker<ILocalTest>.LocalImplementation = ServiceBroker<ILocalTest>.GlobalImplementation;
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest2));
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest2));
            Assert.AreEqual(ServiceBroker<ILocalTest>.GlobalImplementation, ServiceBroker<ILocalTest>.LocalImplementation);

            ServiceBroker<ILocalTest>.GlobalImplementation = new LocalTest3();
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.GlobalImplementation, typeof(LocalTest3));
            Assert.IsInstanceOfType(ServiceBroker<ILocalTest>.LocalImplementation, typeof(LocalTest3));
            Assert.AreEqual(ServiceBroker<ILocalTest>.GlobalImplementation, ServiceBroker<ILocalTest>.LocalImplementation);
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
    class LocalTest2 : ILocalTest
    {
    }
    class LocalTest3 : ILocalTest
    {
    }
}
