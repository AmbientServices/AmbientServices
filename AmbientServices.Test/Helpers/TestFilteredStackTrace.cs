using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestFilteredStackTrace
    {
        [TestMethod]
        public void FilteredStackTrace()
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
            trace = new FilteredStackTrace(new ExpectedException("This is a test"), 0);
            Assert.IsTrue(string.IsNullOrEmpty(trace.ToString().Trim()));

            trace = new FilteredStackTrace();
            Subfunction(trace.FrameCount);

            Assert.AreEqual(trace, trace);
            Assert.AreNotEqual(trace, new FilteredStackTrace());
            Assert.AreNotEqual(new FilteredStackTrace(), new object());

            Dictionary<FilteredStackTrace, string> dict = new Dictionary<FilteredStackTrace, string>();
            dict.Add(trace, trace.ToString());
            Assert.AreEqual(trace.ToString(), dict[trace]);

            IEnumerable<StackFrame> filtered;
            
            filtered = AmbientServices.FilteredStackTrace.FilterSystemAndMicrosoftFrames(Array.Empty<StackFrame>());
            Assert.AreEqual(0, filtered.Count());

            filtered = AmbientServices.FilteredStackTrace.FilterSystemAndMicrosoftFrames(new StackFrame[1] { new FilteredStackTrace().GetFrames().FirstOrDefault() });
            Assert.AreEqual(1, filtered.Count());

            Assert.AreEqual("", AmbientServices.FilteredStackTrace.FilterFilename(null!));
            Assert.AreEqual("", AmbientServices.FilteredStackTrace.FilterFilename(""));
        }
        private void Subfunction(int parentStackFrames)
        {
            Assert.AreEqual(parentStackFrames + 1, new FilteredStackTrace().FrameCount);

            IEnumerable<StackFrame> filtered = AmbientServices.FilteredStackTrace.FilterSystemAndMicrosoftFrames(new FilteredStackTrace().GetFrames());
            Assert.IsTrue(filtered.Count() > 1);
        }
    }
}
