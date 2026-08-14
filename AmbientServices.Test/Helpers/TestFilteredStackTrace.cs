using AmbientServices;
using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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
            trace = new FilteredStackTrace(0, true);
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

            Assert.AreNotEqual(trace.ToString(), FilteredStackTrace.Current);

            Dictionary<FilteredStackTrace, string> dict = new();
            dict.Add(trace, trace.ToString());
            Assert.AreEqual(trace.ToString(), dict[trace]);

            IEnumerable<StackFrame> filtered;
            
            filtered = FilteredStackTrace.FilterFrames(Array.Empty<StackFrame>());
            Assert.AreEqual(0, filtered.Count());

            filtered = FilteredStackTrace.FilterFrames(new StackFrame[1] { new FilteredStackTrace().GetFrames().FirstOrDefault()! });
            Assert.AreEqual(1, filtered.Count());

            Assert.AreEqual("", FilteredStackTrace.EraseSourcePath(null!));
            Assert.AreEqual("", FilteredStackTrace.EraseSourcePath(""));
        }
        private void Subfunction(int parentStackFrames)
        {
            Assert.AreEqual(parentStackFrames + 1, new FilteredStackTrace().FrameCount);

            IEnumerable<StackFrame> filtered = FilteredStackTrace.FilterFrames(new FilteredStackTrace().GetFrames().Where(f => f != null)!);  // we filter null frames at runtime
            Assert.IsGreaterThan(1, filtered.Count());
        }
        [TestMethod]
        public void StackTraceExtensions_GetFilteredString()
        {
            StackTrace? nullStackTrace = null;
            Assert.Throws<ArgumentNullException>(() => nullStackTrace!.GetFilteredString());   // we're intentionally testing an invalid null here
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
            Assert.IsGreaterThan(0, fst.FrameCount);
            Assert.AreNotEqual(0, fst.GetHashCode());
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
        [TestMethod]
        public void ShouldFilterMethodAfterFirst()
        {
            // the default filter-after-first namespaces (System., Microsoft., Amazon.) match by namespace-qualified prefix
            Assert.IsTrue(FilteredStackTrace.ShouldFilterMethodAfterFirst("System.Console.WriteLine"));
            Assert.IsTrue(FilteredStackTrace.ShouldFilterMethodAfterFirst("Microsoft.Extensions.Logging.Logger.Log"));
            Assert.IsTrue(FilteredStackTrace.ShouldFilterMethodAfterFirst("Amazon.S3.AmazonS3Client.GetObject"));

            // a method in an unrelated namespace is not filtered
            Assert.IsFalse(FilteredStackTrace.ShouldFilterMethodAfterFirst("ZzzUnfilteredNamespace.SomeType.SomeMethod"));
            // the match is an ordinal prefix on the full namespace-qualified name, so a namespace that merely shares leading characters with a default (but not the trailing dot) is not matched
            Assert.IsFalse(FilteredStackTrace.ShouldFilterMethodAfterFirst("Systematic.Analysis.Run"));

            // a null method name is treated as not-to-be-filtered: this covers the null-guard branch (it returns false rather than throwing)
            Assert.IsFalse(FilteredStackTrace.ShouldFilterMethodAfterFirst(null!));   // intentionally passing null to exercise the null branch

            // a namespace added at runtime becomes a filter-after-first prefix (the set is process-wide and add-only, and this name is unique to this test so concurrent tests are unaffected)
            FilteredStackTrace.AddNamespaceToFilterAfterFirst("NonExistentAfterFirstNamespace.Subspace.");
            Assert.IsTrue(FilteredStackTrace.ShouldFilterMethodAfterFirst("NonExistentAfterFirstNamespace.Subspace.SomeType.SomeMethod"));
            // a sibling that only shares the added prefix's leading text (but diverges before the trailing dot) is still not matched
            Assert.IsFalse(FilteredStackTrace.ShouldFilterMethodAfterFirst("NonExistentAfterFirstNamespaceX.Other.Method"));
        }
        [TestMethod]
        public void AddSourcePathToErase()
        {
            string separator = System.IO.Path.DirectorySeparatorChar.ToString();
            // a unique root per invocation keeps the true/false return values deterministic even when this test runs concurrently with itself (the underlying set is process-wide and add-only)
            string uniqueRoot = "ZzzFakeEraseRoot_" + Guid.NewGuid().ToString("N");
            Assert.IsTrue(FilteredStackTrace.AddSourcePathToErase(uniqueRoot), "a brand-new source path should be reported as newly added");
            Assert.IsFalse(FilteredStackTrace.AddSourcePathToErase(uniqueRoot), "re-adding the same source path should report that it was already present");
            // the configured prefix (and the separator immediately after it) is stripped from a matching filename
            string erased = FilteredStackTrace.EraseSourcePath(uniqueRoot + separator + "Sub" + separator + "File.cs");
            Assert.AreEqual("Sub" + separator + "File.cs", erased);
            // a filename that does not start with the configured prefix is returned with only leading separators trimmed
            Assert.AreEqual("Other" + separator + "File.cs", FilteredStackTrace.EraseSourcePath("Other" + separator + "File.cs"));
        }
        [TestMethod]
        public void ToString_DegradesToErrorStringWhenBuildingFails()
        {
            // a sequence that throws while it is being enumerated forces the string build to fail; per the pledge that filtering never throws into the caller, ToString must return an error-describing string rather than propagate the exception
            IEnumerable<StackFrame> throwingFrames = Enumerable.Range(0, 1).Select<int, StackFrame>(_ => throw new InvalidOperationException("boom-from-test"));
            string result = FilteredStackTrace.ToString(throwingFrames);
            StringAssert.StartsWith(result, "Error generating stack trace string:");
            StringAssert.Contains(result, "boom-from-test");
        }
    }
}
