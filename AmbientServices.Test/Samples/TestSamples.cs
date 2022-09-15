using AmbientServices;
using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
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
            string tempPath = Path.GetTempPath()!;
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
            string tempPath = Path.GetTempPath()!;
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
            string tempPath = Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DiskAuditor da = new(tempDrive, tempPathRelative, false);
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public void TempDiskAuditorEmulateMetadata()
        {
            string tempPath = Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DriveInfo _driveInfo = new(tempDrive);
            string name = _driveInfo.Name;
            string volumeLabel = _driveInfo.VolumeLabel;
            string driveFormat = _driveInfo.DriveFormat;
            DriveType driveType = _driveInfo.DriveType;
            long availableFreeSpace = _driveInfo.AvailableFreeSpace;
            long totalFreeBytes = _driveInfo.TotalFreeSpace;
            long totalBytes = _driveInfo.TotalSize;
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public void TempDiskAuditorEmulateEnumerate()
        {
            string tempPath = Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DriveInfo _driveInfo = new(tempDrive);
            StringBuilder sb = new();

            if (!string.IsNullOrEmpty(tempPath))
            {
                StatusResultsBuilder readBuilder = new("Read");
                try
                {
                    // attempt to read a file (if one exists)
                    foreach (string file in Directory.EnumerateFiles(Path.Combine(_driveInfo.RootDirectory.FullName, tempPath)))
                    {
                        sb.Append(file);
                    }
                }
                catch (Exception e)
                {
                    readBuilder.AddException(e);
                }
            }
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task TempDiskAuditorEmulateWrite()
        {
            string tempPath = Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DriveInfo _driveInfo = new(tempDrive);

            StatusResultsBuilder writeBuilder = new("Write");
            try
            {
                // attempt to write a temporary file
                string targetPath = Path.Combine(_driveInfo.RootDirectory.FullName, Guid.NewGuid().ToString("N"));
                AmbientStopwatch s = AmbientStopwatch.StartNew();
                using FileStream fs = new(targetPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                byte[] b = new byte[1];
                await fs.WriteAsync(b, 0, 1);
                await fs.FlushAsync();
                writeBuilder.AddProperty("ResponseMs", s.ElapsedMilliseconds);
                writeBuilder.AddOkay("Ok", "Success", "The write operation succeeded.");
            }
            catch (Exception e)
            {
                writeBuilder.AddException(e);
            }
        }
        /// <summary>
        /// Performs tests on the DiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task DiskAuditorTempReadWrite()
        {
            string tempPath = Path.GetTempPath()!;
            string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
            if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
            string tempPathRelative = tempPath!.Substring(tempDrive.Length);
            DiskAuditor da = new(tempDrive, tempPathRelative, false);
            StatusResultsBuilder builder = new("TempDisk");
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
            DiskAuditor da = new(systemDrive, systemPath, false);
            StatusResultsBuilder builder = new("TempDisk");
            await da.Audit(builder);
            StatusAuditAlert alert = builder.WorstAlert;
            Assert.IsNull(alert, alert?.ToString());
        }
        private static string GetApplicationCodePath()
        {
            AppDomain current = AppDomain.CurrentDomain;
            return (current.RelativeSearchPath ?? current.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        }
        /// <summary>
        /// Performs tests on the LocalDiskAuditor sample code.
        /// </summary>
        [TestMethod]
        public async Task LocalDiskAuditor()
        {
            LocalDiskAuditor lda = new();
            StatusResultsBuilder builder = new(lda);
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
                Status s = new(false);
                LocalDiskAuditor lda = null;
                AmbientCancellationTokenSource cts = new(5000);
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

        private static readonly IAmbientCallStack _CallStack = _AmbientCallStack.Global;
        public static void OuterFunc()
        {
//            BasicAmbientCallStack test = new();   // note that this didn't seem to have any effect on the occasional issue below
            if (_CallStack != null) Debug.WriteLine(String.Join(Environment.NewLine, _CallStack.Entries));
            // somehow _CallStack is null here occasionally--how is this possible?!  Fail with more information in order to narrow it down
            if (_CallStack == null)
            {
                // this showed that when _CallStack is null, everything was null
                //                Assert.Fail($"{_AmbientCallStack.Global},{_AmbientCallStack.Override},{_AmbientCallStack.Local},{GlobalServiceReference<IAmbientCallStack>.DefaultImplementation()},{_AmbientCallStack.GlobalReference.LateAssignedDefaultServiceImplementation()},{DefaultAmbientServices.TryFind(typeof(IAmbientCallStack))?.Name},{new StackTrace()}");
                // this shows that AssemblyExtensions.GetLoadableTypes sometimes fails to return any types, at least on the samples assembly
                // and that in AssemblyExtensions.GetLoadableTypes, the line before:
                // types = assembly.GetTypes();
                // runs, but lines after that do not, not do any catch blocks or finally blocks, and yet the function returns an empty array somehow
                // add the following to help diagnose the issue: bool testSamples = (assembly.GetName().Name == "AmbientServices.Samples");
                //Assert.Fail($"{_AmbientCallStack.Global},{_AmbientCallStack.Override},{_AmbientCallStack.Local},{DefaultAmbientServices.TestSamplesLoaded},{DefaultAmbientServices.TestSamplesDependent},{DefaultAmbientServices.TestSamplesTypes},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoaded},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedLoading},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedLoaded},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedException},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedOtherException},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedFinally},{AmbientServices.Utilities.AssemblyUtilities.TestSamplesLoadedTypes},{new StackTrace()}");
                Assert.Fail($"{_AmbientCallStack.Global},{_AmbientCallStack.Override},{_AmbientCallStack.Local},{typeof(BasicAmbientCallStack).Assembly.GetLoadableTypes().Length},{new StackTrace()}");
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
