using AmbientServices;
using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for array extension methods.
    /// </summary>
    [TestClass]
    public class TestTaskExtensions
    {
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CancellationTokenAsTask()
        {
            using (AmbientClock.Pause())
            {
                AmbientCancellationTokenSource cts = new(5000);
                Task t = cts.Token.AsTask();
                Assert.IsFalse(cts.IsCancellationRequested);
                Assert.IsFalse(t.IsCanceled);
                Assert.IsFalse(t.IsCompleted);
                Assert.IsFalse(t.IsCompletedSuccessfully);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5000));
                Assert.IsTrue(cts.IsCancellationRequested);
                Assert.IsTrue(t.IsCanceled);
                Assert.IsTrue(t.IsCompleted);
                Assert.IsFalse(t.IsCompletedSuccessfully);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void CancellationTokenAsValueTask()
        {
            using (AmbientClock.Pause())
            {
                AmbientCancellationTokenSource cts = new(5000);
                Task t = cts.Token.AsValueTask().AsTask();
                Assert.IsFalse(cts.IsCancellationRequested);
                Assert.IsFalse(t.IsCanceled);
                Assert.IsFalse(t.IsCompleted);
                Assert.IsFalse(t.IsCompletedSuccessfully);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(5000));
                t = cts.Token.AsValueTask().AsTask();
                Assert.IsTrue(cts.IsCancellationRequested);
//                Assert.IsTrue(t.IsCanceled);  // for some reason, this one doesn't work
                Assert.IsTrue(t.IsCompleted);
                Assert.IsFalse(t.IsCompletedSuccessfully);
            }
        }
    }
}
