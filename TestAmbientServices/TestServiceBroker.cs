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
            ITest test = ServiceBroker<ITest>.Implementation;
            Assert.IsNotNull(test);
            IAmbientLogger logger = ServiceBroker<IAmbientLogger>.Implementation;
            Assert.IsNotNull(logger);
            ILogger<TestServiceBroker> serviceBrokerLogger = logger.GetLogger<TestServiceBroker>();
            IAmbientProgress progressTracker = ServiceBroker<IAmbientProgress>.Implementation;
            Assert.IsNotNull(progressTracker);
            IAmbientSettings settings = ServiceBroker<IAmbientSettings>.Implementation;
            Assert.IsNotNull(settings);
            IJunk junk = ServiceBroker<IJunk>.Implementation;
            Assert.IsNull(junk);



            ITest compareTest = test;
            Assert.IsNotNull(test);

            int changed = 0;
            ServiceBroker<ITest>.ImplementationChanged += (s, e) => { Assert.AreEqual(e.OldImplementation, compareTest); ++changed; };

            ServiceBroker<ITest>.Implementation = null;
            Assert.AreEqual(1, changed);
            compareTest = null;

            ITest disabledTest = ServiceBroker<ITest>.Implementation;
            Assert.IsNull(disabledTest);

            ServiceBroker<ITest>.Implementation = test;
            Assert.AreEqual(2, changed);
            compareTest = test;

            ITest reenabledTest = ServiceBroker<ITest>.Implementation;
            Assert.IsNotNull(reenabledTest);
        }

        [TestMethod, ExpectedException(typeof(TypeInitializationException))]
        public void NonInterfaceType()
        {
            DefaultTest test = ServiceBroker<DefaultTest>.Implementation;
        }
        [TestMethod]
        public void AssemblyLoadAndLateAssignment()
        {
            // try to get this one now
            ILateAssignmentTest test = ServiceBroker<ILateAssignmentTest>.Implementation;
            Assert.IsNull(test);

            LateAssignment();

            // NOW this should be available
            test = ServiceBroker<ILateAssignmentTest>.Implementation;
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
    }
}
