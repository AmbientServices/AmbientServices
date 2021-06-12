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
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public void DiskInformation()
        {
            string tempPath = System.IO.Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System)!;
            string systemDrive = Path.GetPathRoot(systemPath) ?? GetApplicationCodePath() ?? "/";   // use the application code path if we can't find the system root, if we can't get that either, try to use the root.  on linux, we should get the application code path
            if (string.IsNullOrEmpty(systemPath) || string.IsNullOrEmpty(systemDrive)) systemDrive = systemPath = "/";
            if (systemPath?[0] == '/') systemDrive = "/";
            string systemPathRelative = systemPath!.Substring(systemDrive.Length);
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public void GetTempPathRoot()
        {
            string tempPath = System.IO.Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public void DiskAuditorTempSetup()
        {
            string tempPath = System.IO.Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DiskAuditor da = new DiskAuditor(tempDrive, tempPathRelative, true);
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task DiskAuditorTemp()
        {
            string tempPath = System.IO.Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DiskAuditor da = new DiskAuditor(tempDrive, tempPathRelative, true);
            StatusResultsBuilder builder = new StatusResultsBuilder("TempDisk");
            await da.Audit(builder);
            StatusAuditAlert alert = builder.WorstAlert;
            Assert.IsNull(alert, alert?.ToString());
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task DiskAuditorSystem()
        {
            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System)!;
            string systemDrive = Path.GetPathRoot(systemPath) ?? GetApplicationCodePath() ?? "/";   // use the application code path if we can't find the system root, if we can't get that either, try to use the root.  on linux, we should get the application code path
            if (string.IsNullOrEmpty(systemPath) || string.IsNullOrEmpty(systemDrive)) systemDrive = systemPath = "/";
            if (systemPath?[0] == '/') systemDrive = "/";
            string systemPathRelative = systemPath!.Substring(systemDrive.Length);
            DiskAuditor da = new DiskAuditor(systemDrive, systemPath, false);
            StatusResultsBuilder builder = new StatusResultsBuilder("TempDisk");
            await da.Audit(builder);
            StatusAuditAlert alert = builder.WorstAlert;
            Assert.IsNull(alert, alert?.ToString());
        }
        private static string GetApplicationCodePath()
        {
            AppDomain current = AppDomain.CurrentDomain;
            return (current.RelativeSearchPath ?? current.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        }
        /// <summary>
        /// Performs tests on the LocalDiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task LocalDiskAuditor()
        {
            LocalDiskAuditor lda = new LocalDiskAuditor();
            StatusResultsBuilder builder = new StatusResultsBuilder(lda);
            await lda.Audit(builder);
            StatusAuditAlert alert = builder.WorstAlert;
            Assert.IsNull(alert, alert?.ToString());
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
                AmbientCancellationTokenSource cts = new AmbientCancellationTokenSource(5000);
                try
                {
                    lda = new LocalDiskAuditor();
                    s.AddCheckerOrAuditor(lda);
                    await s.Start(cts.Token);
                    // run all the tests (just the one here) right now
                    await s.RefreshAsync(cts.Token);
                    StatusAuditAlert a = s.Summary;
                    Assert.AreEqual(StatusRatingRange.Okay, StatusRating.FindRange(a.Rating));
                }
                finally
                {
                    await s.Stop();
                    if (lda != null) s.RemoveCheckerOrAuditor(lda);     // note that lda could be null if the constructor throws!
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
