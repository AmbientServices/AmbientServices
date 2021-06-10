using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for assembly extension methods.
    /// </summary>
    [TestClass]
    public class TestAssemblyExtensions
    {
        private static readonly AmbientService<ILateAssignmentTest> _LateAssignmentTest = Ambient.GetService<ILateAssignmentTest>();

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
            Assert.AreEqual(1, AmbientServices.Utility.AssemblyExtensions.TypesFromException(ex).Count());
            ex = new ReflectionTypeLoadException(new Type[] { typeof(string), null }, new Exception[0]);
            Assert.AreEqual(1, AmbientServices.Utility.AssemblyExtensions.TypesFromException(ex).Count());
        }
        [TestMethod]
        public void DoesAssemblyReferToAssembly()
        {
            Assert.IsFalse(AmbientServices.Utility.AssemblyExtensions.DoesAssemblyReferToAssembly(typeof(System.ValueTuple).Assembly, Assembly.GetExecutingAssembly()));
            Assert.IsTrue(AmbientServices.Utility.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), typeof(IAmbientCache).Assembly));
            Assert.IsTrue(AmbientServices.Utility.AssemblyExtensions.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly(), Assembly.GetExecutingAssembly()));
        }
        [TestMethod]
        public void ReflectionTypeLoadException()
        {
            // Note that Assembly.Location returns empty string if the assembly is a single file application, but this is a test application, which is *not* a single file executable, so we should get a location
            string dllPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "ReflectionTypeLoadException.Assembly.dll");
            Type[] types = AmbientServices.Utility.AssemblyExtensions.GetLoadableTypes(Assembly.LoadFrom(dllPath)).ToArray();
        }
        [TestMethod]
        public void AssemblyExtensionsNullArgumentExceptions()
        {
            Assembly n = null!;
            Assert.ThrowsException<ArgumentNullException>(() => n.GetLoadableTypes());
            Assert.ThrowsException<ArgumentNullException>(() => n.DoesAssemblyReferToAssembly(n));
            ReflectionTypeLoadException ex = null!;
            Assert.ThrowsException<ArgumentNullException>(() => AmbientServices.Utility.AssemblyExtensions.TypesFromException(ex));
        }
    }
}
