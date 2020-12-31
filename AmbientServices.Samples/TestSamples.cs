using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Samples
{
    /// <summary>
    /// A class that holds tests for sample code.
    /// </summary>
    [TestClass]
    public class TestSamples
    {
        /// <summary>
        /// Performs tests on the CallStackTest sample code.
        /// </summary>
        [TestMethod]
        public void CallStackTestClass()
        {
            CallStackTest.OuterFunc();
        }
        /// <summary>
        /// Performs tests on the Status sample code.
        /// </summary>
        [TestMethod]
        public async Task Status()
        {
            using (AmbientClock.Pause())
            {
                Status s = new Status(false);
                LocalDiskAuditor lda = null;
                try
                {
                    lda = new LocalDiskAuditor();
                    s.AddCheckerOrAuditor(lda);
                    await s.Start();
                    // skip ahead to run it at least once
                    AmbientClock.SkipAhead(TimeSpan.FromMinutes(20));
                }
                finally
                {
                    await s.Stop();
                    s.RemoveCheckerOrAuditor(lda);
                }
            }
        }
    }

    class CallStackTest
    {
        private static readonly AmbientService<IAmbientCallStack> _AmbientCallStack = Ambient.GetService<IAmbientCallStack>();

        private static IAmbientCallStack _CallStack = _AmbientCallStack.Global;
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
