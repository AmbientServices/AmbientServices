using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IProgressTracker"/>.
    /// </summary>
    [TestClass]
    public class TestProgressTracker
    {
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod]
        public void ProgressTracker()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            progress.Update(0.01f);
            Assert.AreEqual(0.01f, progress.PortionComplete);
            Assert.AreEqual("", progress.ItemCurrentlyBeingProcessed);
            progress.Update(0.02f, "test");
            Assert.AreEqual(0.02f, progress.PortionComplete);
            Assert.AreEqual("test", progress.ItemCurrentlyBeingProcessed);
            progress.Update(0.03f);
            Assert.AreEqual(0.03f, progress.PortionComplete);
            Assert.AreEqual("test", progress.ItemCurrentlyBeingProcessed);
            progress.Update(0.04f, "");
            Assert.AreEqual(0.04f, progress.PortionComplete);
            Assert.AreEqual("", progress.ItemCurrentlyBeingProcessed);
            using (IProgress subprogress = progress.TrackPart(0.05f, 0.10f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod]
        public void ProgressPartStack()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (IProgress subprogress = progress.TrackPart(0.05f, 0.10f))
            {
                Assert.AreEqual(subprogress, progressTracker.Progress);
            }
            Assert.AreEqual(progress, progressTracker.Progress);
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod]
        public void ProgressPart()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (IProgress subprogress = progress.TrackPart(0.05f, 0.05f, "prefix-"))
            {
                Assert.AreEqual(subprogress, progressTracker.Progress);
                subprogress.Update(.5f, "subitem");
                Assert.AreEqual(0.075f, progress.PortionComplete);
                Assert.AreEqual("prefix-subitem", progress.ItemCurrentlyBeingProcessed);
            }
            Assert.AreEqual(progress, progressTracker.Progress);
            Assert.AreEqual(0.10f, progress.PortionComplete);
            Assert.AreEqual("prefix-subitem", progress.ItemCurrentlyBeingProcessed);
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionCompleteTooLowError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            progress.Update(-.01f);
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionCompleteTooHighError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            progress.Update(1.01f);
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PartPortionCompleteTooLowError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(0.01f, 0.02f))
            {
                progress.Update(-.01f);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PartPortionCompleteTooHighError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(0.01f, 0.02f))
            {
                progress.Update(1.01f);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void StartPortionTooLowError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(-0.01f, 1.0f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void StartPortionTooHighError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(0.0f, 1.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionPartTooLowError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(1.0f, -0.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionPartTooHighError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(1.0f, 1.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionTooLargeError()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            using (progress = progress.TrackPart(0.5f, 0.51f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IProgressTracker"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void PartStackCorruption()
        {
            IProgressTracker progressTracker = Registry<IProgressTracker>.Implementation;
            IProgress progress = progressTracker.Progress;
            IProgress subProgress1 = progress.TrackPart(0.05f, 0.51f);
            using (IProgress subProgress2 = subProgress1.TrackPart(0.05f, 0.51f))
            {
                subProgress1.Dispose();
            }
        }
    }
}
