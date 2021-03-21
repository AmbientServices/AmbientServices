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

namespace AmbientServices.Test.Samples
{
    /// <summary>
    /// A class that holds tests for sample code.
    /// </summary>
    [TestClass]
    public class TestSamples
    {
        /// <summary>
        /// Performs tests on the ambient call stack sample code.
        /// </summary>
        [TestMethod]
        public void AmbientCallStack()
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
                    // run all the tests (just the one here) right now
                    await s.RefreshAsync();
                    StatusAuditAlert a = s.Summary;
                    Assert.AreEqual(StatusRatingRange.Okay, StatusRating.FindRange(a.Rating));
                }
                finally
                {
                    await s.Stop();
                    s.RemoveCheckerOrAuditor(lda!);
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
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            // somehow _CallStack is null here occasionally--how is this possible?!  Fail with more information in order to narrow it down
            if (_CallStack == null)
            {
                Assert.Fail($"{_AmbientCallStack.Global},{_AmbientCallStack.Override},{_AmbientCallStack.Local}");
                Assert.Fail($"{GlobalServiceReference<IAmbientCallStack>.DefaultImplementation()}");
                Assert.Fail($"{_AmbientCallStack.GlobalReference.LateAssignedDefaultServiceImplementation()}");
                Assert.Fail($"{DefaultAmbientServices.TryFind(typeof(IAmbientCallStack))?.Name},{new StackTrace()}");
            }
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
            using (_CallStack?.Scope("OuterFunc"))
            {
                Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
                Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
                InnerFunc();
                Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
                Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
            }
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
        }
        private static void InnerFunc()
        {
            Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
            using (_CallStack?.Scope("InnerFunc"))
            {
                Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
                Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
            }
            Assert.IsTrue(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("OuterFunc"));
            Assert.IsFalse(String.Join(Environment.NewLine, _CallStack?.Entries ?? Array.Empty<string>()).Contains("InnerFunc"));
        }
    }
}
