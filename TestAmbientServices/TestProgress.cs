using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientProgress"/>.
    /// </summary>
    [TestClass]
    public class TestProgress
    {
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod]
        public void Progress()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
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
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod]
        public void ProgressPartStack()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (IProgress subprogress = progress.TrackPart(0.05f, 0.10f))
            {
                Assert.AreEqual(subprogress, ambientProgress.Progress);
            }
            Assert.AreEqual(progress, ambientProgress.Progress);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod]
        public void ProgressPart()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (IProgress subprogress = progress.TrackPart(0.05f, 0.05f, "prefix-"))
            {
                Assert.AreEqual(0.0f, subprogress.PortionComplete);
                Assert.AreEqual(subprogress, ambientProgress.Progress);
                subprogress.Update(.5f, "subitem");
                Assert.AreEqual(0.5f, subprogress.PortionComplete);
                Assert.AreEqual("subitem", subprogress.ItemCurrentlyBeingProcessed);
                Assert.AreEqual(0.075f, progress.PortionComplete);
                Assert.AreEqual("prefix-subitem", progress.ItemCurrentlyBeingProcessed);
            }
            Assert.AreEqual(progress, ambientProgress.Progress);
            Assert.AreEqual(0.10f, progress.PortionComplete);
            Assert.AreEqual("prefix-subitem", progress.ItemCurrentlyBeingProcessed);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionCompleteTooLowError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            progress.Update(-.01f);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionCompleteTooHighError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            progress.Update(1.01f);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PartPortionCompleteTooLowError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(0.01f, 0.02f))
            {
                progress.Update(-.01f);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PartPortionCompleteTooHighError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(0.01f, 0.02f))
            {
                progress.Update(1.01f);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void StartPortionTooLowError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(-0.01f, 1.0f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void StartPortionTooHighError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(0.0f, 1.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionPartTooLowError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(1.0f, -0.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionPartTooHighError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(1.0f, 1.01f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PortionTooLargeError()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            using (progress = progress.TrackPart(0.5f, 0.51f))
            {
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void PartStackCorruption()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            IProgress subProgress1 = progress.TrackPart(0.05f, 0.51f);
            using (IProgress subProgress2 = subProgress1.TrackPart(0.05f, 0.51f))
            {
                subProgress1.Dispose();
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod]
        public void GetCancellationToken()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            CancellationToken token = progress.CancellationToken;
            Assert.IsNotNull(token);

            token = IProgressExtensions.GetCancellationTokenOrDefault(null);
            Assert.IsNotNull(token);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientProgress"/>.
        /// </summary>
        [TestMethod]
        public void CancellationToken()
        {
            IAmbientProgress ambientProgress = Registry<IAmbientProgress>.Implementation;
            IProgress progress = ambientProgress.Progress;
            IProgress subProgress1 = progress.TrackPart(0.05f, 0.51f);
            using (IProgress subProgress2 = subProgress1.TrackPart(0.05f, 0.51f))
            {
                CancellationToken token = progress.CancellationToken;
                Assert.IsNotNull(token);
            }
        }
    }
}
