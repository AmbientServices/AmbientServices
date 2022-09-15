using AmbientServices;
using AmbientServices.Extensions;
using AmbientServices.Utilities;
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
    public class TestAssemblyUtilities
    {
        private static readonly AmbientService<ILateAssignmentTest> _LateAssignmentTest = Ambient.GetService<ILateAssignmentTest>();

        [TestMethod]
        public void TypesFromException()
        {
            ReflectionTypeLoadException ex = new(new Type[] { typeof(string) }, new Exception[0]);
            Assert.AreEqual(1, AssemblyUtilities.TypesFromException(ex).Count());
            ex = new ReflectionTypeLoadException(new Type[] { typeof(string), null }, new Exception[0]);
            Assert.AreEqual(1, AssemblyUtilities.TypesFromException(ex).Count());
        }
        [TestMethod]
        public void AssemblyExtensionsNullArgumentExceptions()
        {
            Assembly n = null!;
            Assert.ThrowsException<ArgumentNullException>(() => n.GetLoadableTypes());
            Assert.ThrowsException<ArgumentNullException>(() => n.DoesAssemblyReferToAssembly(n));
            ReflectionTypeLoadException ex = null!;
            Assert.ThrowsException<ArgumentNullException>(() => AssemblyUtilities.TypesFromException(ex));
        }
    }
}
