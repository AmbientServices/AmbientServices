using AmbientServices;
using AmbientServices.Extensions;
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
        [TestMethod]
        public void DoesAssemblyReferToAssembly()
        {
            Assert.IsFalse(typeof(System.ValueTuple).Assembly.DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly()));
            Assert.IsTrue(Assembly.GetExecutingAssembly().DoesAssemblyReferToAssembly(typeof(IAmbientLocalCache).Assembly));
            Assert.IsTrue(Assembly.GetExecutingAssembly().DoesAssemblyReferToAssembly(Assembly.GetExecutingAssembly()));
        }
        [TestMethod]
        public void ReflectionTypeLoadException()
        {
            // Note that Assembly.Location returns empty string if the assembly is a single file application, but this is a test application, which is *not* a single file executable, so we should get a location
            string dllPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "ReflectionTypeLoadException.Assembly.dll");
            Type[] types = Assembly.LoadFrom(dllPath).GetLoadableTypes().ToArray();
        }
        private void LateAssignment()
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // NOW load the assembly (this should register the default implementation)
            Assembly assembly = Assembly.LoadFile(path + System.IO.Path.PathSeparator + "AmbientServices.Test.DelayedLoad.dll");
        }
    }
}
