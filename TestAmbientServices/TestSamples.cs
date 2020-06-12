using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for sample code.
    /// </summary>
    [TestClass]
    public class TestSamples
    {
        /// <summary>
        /// Performs tests on sample code.
        /// </summary>
        [TestMethod]
        public void SampleCode()
        {
            CallStackTest.OuterFunc();
        }
    }

    class CallStackTest
    {
        private static ICallStack _CallStack = Registry<ICallStack>.Implementation;
        public static void OuterFunc()
        {
            Debug.WriteLine("Before outer push:");
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            using (_CallStack.Scope("OuterFunc"))
            {
                Debug.WriteLine("After outer push:");
                if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
                InnerFunc();
                Debug.WriteLine("After inner return:");
                if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            }
            Debug.WriteLine("After outer pop:");
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
        }
        private static void InnerFunc()
        {
            Debug.WriteLine("Before inner push:");
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            using (_CallStack.Scope("InnerFunc"))
            {
                Debug.WriteLine("After inner push:");
                if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            }
            Debug.WriteLine("After inner pop:");
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
        }
    }
}
