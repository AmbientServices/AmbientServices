using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace Ignore
{
    class IgnoreNamespace
    {
        public static void TestEraseAndFilter()
        {
            FilteredStackTrace.AddNamespacesToErase("Ignore.IgnoreNamespace.");
            FilteredStackTrace.AddNamespaceToFilterAfterFirst("Ignore.");
            Assert.AreEqual("TestEraseAndFilter", FilteredStackTrace.EraseNamespace("Ignore.IgnoreNamespace.TestEraseAndFilter"));
            FilteredStackTrace trace = new(new AmbientServices.Test.ExpectedException("This is a test"), 0, true);
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
        }
    }
}
namespace AmbientServices.Test
{
    [TestClass]
    public class TestFilteredStackTrace
    {
        [TestMethod]
        public void FilteredStackTrace_()
        {
            FilteredStackTrace trace;
            trace = new FilteredStackTrace();
            Assert.IsFalse(string.IsNullOrEmpty(trace.ToString()));
            trace = new FilteredStackTrace(new ExpectedException("This is a test"));
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
            trace = new FilteredStackTrace(0);
            Assert.IsFalse(string.IsNullOrEmpty(trace.ToString()));
            trace = new FilteredStackTrace(new System.Diagnostics.StackFrame());
            Assert.IsFalse(string.IsNullOrEmpty(trace.ToString()));
            trace = new FilteredStackTrace(new ExpectedException("This is a test"), true);
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
            trace = new FilteredStackTrace(new ExpectedException("This is a test"), 0);
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
            trace = new FilteredStackTrace(0, true);
            Assert.IsFalse(string.IsNullOrEmpty(trace.ToString()));
            trace = new FilteredStackTrace(new ExpectedException("This is a test"), 0, true);
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
            Async.Synchronize(async () => 
            {
                string? projectPath = AssemblyExtensions.GetCallingCodeSourceFolder(1, 1)?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                FilteredStackTrace.AddSourcePathToErase(projectPath ?? "Z:\\");
                FilteredStackTrace.AddNamespaceToFilter("Async.Synchronize");
                trace = new FilteredStackTrace(new ExpectedException("This is a test"), 0, true);
                Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));
                await Task.CompletedTask;
            });
            Ignore.IgnoreNamespace.TestEraseAndFilter();
            FilteredStackTrace.AddNamespaceToFilter("NonExistentNamespace.Subspace");
            Assert.IsTrue(FilteredStackTrace.ShouldFilterMethod("NonExistentNamespace.Subspace.TestNamespaceFilter"));

            Assert.AreEqual(String.Empty, FilteredStackTrace.EraseSourcePath(null!));
            Assert.AreEqual(String.Empty, FilteredStackTrace.EraseNamespace(null!));


            trace = new FilteredStackTrace();
            Subfunction(trace.FrameCount);

            Assert.AreEqual(trace, trace);
            Assert.AreNotEqual(trace, new FilteredStackTrace());
            Assert.AreNotEqual(new FilteredStackTrace(), new object());

            Dictionary<FilteredStackTrace, string> dict = new();
            dict.Add(trace, trace.ToString());
            Assert.AreEqual(trace.ToString(), dict[trace]);

            IEnumerable<StackFrame> filtered;
            
            filtered = AmbientServices.FilteredStackTrace.FilterFrames(Array.Empty<StackFrame>());
            Assert.AreEqual(0, filtered.Count());

            filtered = AmbientServices.FilteredStackTrace.FilterFrames(new StackFrame[1] { new FilteredStackTrace().GetFrames().FirstOrDefault()! });
            Assert.AreEqual(1, filtered.Count());

            Assert.AreEqual("", AmbientServices.FilteredStackTrace.EraseSourcePath(null!));
            Assert.AreEqual("", AmbientServices.FilteredStackTrace.EraseSourcePath(""));
        }
        private void Subfunction(int parentStackFrames)
        {
            Assert.AreEqual(parentStackFrames + 1, new FilteredStackTrace().FrameCount);

            IEnumerable<StackFrame> filtered = AmbientServices.FilteredStackTrace.FilterFrames(new FilteredStackTrace().GetFrames().Where(f => f != null)!);  // we filter null frames at runtime
            Assert.IsTrue(filtered.Count() > 1);
        }
        [TestMethod]
        public void StackTraceExtensions_GetFilteredString()
        {
            StackTrace? nullStackTrace = null;
            Assert.ThrowsException<ArgumentNullException>(() => nullStackTrace!.GetFilteredString());   // we're intentionally testing an invalid null here
            Assert.IsNotNull(new StackTrace().GetFilteredString());
            Assert.IsNotNull(new FilteredStackTrace().GetFilteredString());
        }
        [TestMethod]
        public void FilteredStackTrace_Misc()
        {
            FilteredStackTrace.EraseCallingSourcePath(1);
            FilteredStackTrace fst = new(true);
            Assert.IsTrue(fst.Equals(fst));
            Assert.IsFalse(fst.Equals(null));
            Assert.IsTrue(fst.FrameCount > 0);
            Assert.IsTrue(fst.GetHashCode() != 0);
            int frameNum = 0;
            foreach (StackFrame frame in fst.GetFrames())
            {
                Assert.AreEqual(frame, fst.GetFrame(frameNum));
                ++frameNum;
            }
            // try to get another frame (this should fall through and return an original (unfiltered stack frame)
            Assert.IsFalse(string.IsNullOrEmpty(fst.GetFrame(frameNum)?.ToString()));
            InnerFunc();
        }

        private static void InnerFunc([CallerFilePath] string filepath = "")
        {
            Assert.AreEqual(System.IO.Path.GetFileName(filepath), System.IO.Path.GetFileName(FilteredStackTrace.EraseSourcePath(filepath)));
        }
    }
}
