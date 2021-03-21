using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// Summary description for StatusTest.
    /// </summary>
    [TestClass]
    public class TestStatus
    {
        [TestMethod]
        public async Task StatusClass()
        {
            await Status.DefaultInstance.Start();
            Assert.IsFalse(Status.DefaultInstance.ShuttingDown);
            try
            {
                DateTime start = AmbientClock.UtcNow;
                await Status.DefaultInstance.RefreshAsync();

                StatusResults overallStatus = Status.DefaultInstance.Results;

                Assert.AreEqual("/", overallStatus.TargetSystem);
                Assert.AreEqual(0, overallStatus.RelativeDetailLevel);
                Assert.AreEqual(StatusNatureOfSystem.ChildrenHeterogenous, overallStatus.NatureOfSystem);
                Assert.AreEqual(0, overallStatus.Properties.Count());
                Assert.IsTrue(overallStatus.Children.Count() > 0);
                Assert.IsFalse(string.IsNullOrEmpty(overallStatus.ToString()));

                StatusResults sampleDisk = overallStatus.Children.FirstOrDefault(c => c.TargetSystem == "SampleDisk" && c.Children.Any()) as StatusResults;
                Assert.IsNotNull(sampleDisk);
                StatusResults disk = sampleDisk!.Children.FirstOrDefault() as StatusResults;
                Assert.IsNotNull(disk);
                Assert.IsTrue(disk!.Properties.Count() > 0); // the properties in the node itself are the constant properties, ie. the path for a disk test
                Assert.IsFalse(string.IsNullOrEmpty(sampleDisk!.ToString()));

                StatusProperty att = disk.Properties.FirstOrDefault(a => a.Name == "TotalBytes");
                Assert.IsNotNull(att);
                Assert.IsFalse(string.IsNullOrEmpty(att!.ToString()));
                Assert.IsFalse(String.IsNullOrEmpty(att!.Name));
                Assert.IsFalse(String.IsNullOrEmpty(att!.Value));

                HashSet<StatusResults> test = new HashSet<StatusResults>();
                StatusResults c1 = overallStatus.Children.FirstOrDefault(c => c.TargetSystem == nameof(TestHeterogenousExplicitRating));
                Assert.IsNotNull(c1);
                Assert.IsNotNull(c1!.Report);
                Assert.IsNotNull(c1!.Report?.Alert?.Rating);
                Assert.IsFalse(string.IsNullOrEmpty(c1!.ToString()));
                test.Add(c1);

                StatusResults c2 = overallStatus.Children.FirstOrDefault(c => c.TargetSystem == nameof(TestHomogeneousExplicitFailure));
                Assert.IsNotNull(c2);
                Assert.IsNotNull(c2!.Report);
                Assert.IsNotNull(c2!.Report?.Alert?.Rating);
                Assert.AreNotEqual(c1!.Report?.Alert, c2!.Report?.Alert);
                Assert.IsFalse(string.IsNullOrEmpty(c2!.ToString()));
                test.Add(c2);

                StatusResults c3 = overallStatus.Children.FirstOrDefault(c => c.TargetSystem == nameof(TestMachineConstantStatus));
                Assert.IsNotNull(c3);
                Assert.IsNull(c3!.Report);
                Assert.IsFalse(string.IsNullOrEmpty(c3!.ToString()));
                test.Add(c3);

                StatusAuditAlert auditResult = overallStatus.GetSummaryAlerts(true, StatusRating.Alert, false);

                Assert.AreEqual(0, overallStatus.Properties.Count());

                HashSet<StatusAuditReport> test3 = new HashSet<StatusAuditReport>();
                Assert.IsTrue(auditResult.Rating >= -1.0f && auditResult.Rating <= 4.0f);
                Assert.IsFalse(string.IsNullOrEmpty(auditResult.Terse));
                Assert.IsFalse(string.IsNullOrEmpty(auditResult.Details));
                Assert.IsTrue(overallStatus.SourceSystem == null);
                Assert.IsTrue(auditResult.Rating <= StatusRating.Fail);
                Assert.IsFalse(string.IsNullOrEmpty(auditResult.ToString()));

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                await Status.DefaultInstance.RefreshAsync();
                overallStatus = Status.DefaultInstance.Results;

                StatusAuditAlert notOkayResult = overallStatus.GetSummaryAlerts(true, StatusRating.Okay, false);
                StatusAuditAlert fullResult = overallStatus.GetSummaryAlerts(true, float.MaxValue, false);

                StatusAuditAlert overallSummaryAlerts = Status.DefaultInstance.Summary;
                Assert.IsTrue(overallSummaryAlerts.Rating < StatusRating.Fail && overallSummaryAlerts.Rating > StatusRating.Catastrophic);
                StatusAuditAlert overallSummaryAlertsAndFailures = Status.DefaultInstance.SummaryAlertsAndFailures;
                Assert.IsTrue(overallSummaryAlertsAndFailures.Rating < StatusRating.Fail && overallSummaryAlertsAndFailures.Rating > StatusRating.Catastrophic);
                StatusAuditAlert overallSummaryFailures = Status.DefaultInstance.SummaryFailures;
                Assert.IsTrue(overallSummaryFailures.Rating < StatusRating.Fail && overallSummaryFailures.Rating > StatusRating.Catastrophic);
            }
            finally
            {
                await Status.DefaultInstance.Stop();
            }
        }
        [TestMethod]
        public async Task StatusExceptions()
        {
            Status s = new Status(false);
            await s.Start();
            Assert.ThrowsException<InvalidOperationException>(() => s.Start());
            Assert.ThrowsException<ArgumentNullException>(() => Status.IsTestableStatusCheckerClass(null!));
            Assert.ThrowsException<ArgumentNullException>(() => s.AddCheckerOrAuditor(null!));
            Assert.ThrowsException<ArgumentNullException>(() => s.RemoveCheckerOrAuditor(null!));
        }
        [TestMethod]
        public async Task StatusEmpty()
        {
            Status s = new Status(false);
            await s.Start();
            await s.Stop();
        }
        [TestMethod]
        public async Task StatusStartStop()
        {
            Status s = new Status(true);
            await s.Start();
            await s.Stop();
        }
        [TestMethod]
        public async Task StatusAddRemoveChecker()
        {
            Status s = new Status(false);
            using (TestMachineConstantStatus checkerToAdd = new TestMachineConstantStatus())
            {
                s.AddCheckerOrAuditor(checkerToAdd);
                await s.Start();
                await s.Stop();
                s.RemoveCheckerOrAuditor(checkerToAdd);
            }
            using (TestMachineConstantStatus checkerToAdd = new TestMachineConstantStatus())
            {
                s.AddCheckerOrAuditor(checkerToAdd);
                await s.Start();
                await s.Stop();
                s.RemoveCheckerOrAuditor(checkerToAdd);
            }
        }
        [TestMethod]
        public async Task StatusAuditAfterDisposal()
        {
            Status s = new Status(false);
            using (TestConstantAuditResults auditorToAdd = new TestConstantAuditResults(null, nameof(StatusAuditAfterDisposal), StatusRating.Okay, nameof(StatusAuditAfterDisposal), "Terse", "Details"))
            {
                s.AddCheckerOrAuditor(auditorToAdd);
                await s.Start();
                await s.Stop();
                s.RemoveCheckerOrAuditor(auditorToAdd);
                // run the initial audit manually and synchronously
                auditorToAdd.InitialAuditTimer_Elapsed(null, null);
            }
        }
    }
    internal class TestMachineConstantStatus : StatusChecker
    {
        private readonly StatusResults _results;

        public TestMachineConstantStatus()
            : base(nameof(TestMachineConstantStatus))
        {
            StatusResultsBuilder sb = new StatusResultsBuilder(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            sb.AddProperty("MachineName", Environment.MachineName);
            sb.AddProperty("StartTime", AmbientClock.UtcNow);
            _results = sb.FinalResults;
            SetLatestResults(_results);
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task<StatusResults> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            return Task.FromResult(_results);
        }
    }
    internal class TestAlwaysPending : StatusChecker
    {
        private readonly StatusResults _results = StatusResults.GetPendingResults(null, nameof(TestAlwaysPending));

        public TestAlwaysPending()
            : base(nameof(TestAlwaysPending))
        {
            SetLatestResults(_results);
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task<StatusResults> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            return Task.FromResult(_results);
        }
    }
    internal class TestNotApplicableStatus : StatusChecker
    {
        private readonly StatusResults _results;
        public TestNotApplicableStatus()
            : base(nameof(TestNotApplicableStatus))
        {
            StatusResultsBuilder sb = new StatusResultsBuilder(this) { NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant };
            _results = sb.FinalResults;
            SetLatestResults(_results);
        }
        protected internal override bool Applicable { get { return false; } }
        public override Task<StatusResults> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            return Task.FromResult(_results);
        }
    }
    internal class TestAuditableStatusNoChildren : StatusAuditor
    {
        public TestAuditableStatusNoChildren()
            : base(nameof(TestAuditableStatusNoChildren), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant;
            statusBuilder.AddProperty("nc1", "a");
            statusBuilder.AddProperty("nc2", "b");
            statusBuilder.AddProperty("nc2", AmbientClock.UtcNow.AddMinutes(-3));
            return Task.CompletedTask;
        }
    }
    internal class TestConstantAuditResults : StatusAuditor
    {
        private readonly string _sourceSystem;
        private readonly float _rating;
        private readonly string _auditCode;
        private readonly string _terse;
        private readonly string _details;

        public TestConstantAuditResults(string sourceSystem, string targetSystem, float rating, string auditCode, string terse, string details)
            : base(targetSystem, TimeSpan.FromSeconds(60))
        {
            _sourceSystem = sourceSystem;
            _rating = rating;
            _auditCode = auditCode;
            _terse = terse;
            _details = details;
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant;
            if (_rating <= StatusRating.Fail)
            {
                statusBuilder.AddFailure(_auditCode, _terse, _details, _rating);
            }
            else if (_rating <= StatusRating.Alert)
            {
                statusBuilder.AddAlert(_auditCode, _terse, _details, StatusRating.Alert - _rating);
            }
            else
            {
                statusBuilder.AddOkay(_auditCode, _terse, _details, StatusRating.Okay - _rating);
            }
            return Task.CompletedTask;
        }
    }
    internal class TestIrrelevantException : StatusAuditor
    {
        public TestIrrelevantException()
            : base(nameof(TestIrrelevantException), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenIrrelevant;
            StatusResultsBuilder child = new StatusResultsBuilder("IrrelevantChild");
            child.AddException(new ExpectedException(nameof(TestIrrelevantException)));
            statusBuilder.AddChild(child);
            return Task.CompletedTask;
        }
    }
    /// <summary>
    /// A class which does a simulated audit similar to what a disk volume might report.
    /// </summary>
    [DefaultPropertyThresholds("AvailableBytes", 1000000000.0f, 10000000000.0f, 100000000000.0f, StatusThresholdNature.HighIsGood)]
    [DefaultPropertyThresholds("AvailablePercent", 1.0f, 2.5f, 5.0f, StatusThresholdNature.HighIsGood)]
    class SampleVolumeAuditor
    {
        private readonly string _targetSystem;
        private readonly string _fakeFolderPath;
        private readonly int? _hiddenFailuresTolerated;
        private readonly float? _spatialDistributionOfRedundancy;

        /// <summary>
        /// Constructs a SampleAuditor which audits available disk space about one per minute.
        /// </summary>
        /// <param name="targetSystem">The name of the target system to put into the results.</param>
        /// <param name="fakeFolderPath">The file path to check the available disk space on.</param>
        /// <param name="hiddenFailuresTolerated">An optional indicator of the extent to which failures are tolerated by the system without being apparent directly in the form of child status data.</param>
        /// <param name="spatialDistributionOfRedundancy">An optional indicator of the spatial distribution (in meters) of the most remote redundancy employed by this system.</param>
        public SampleVolumeAuditor(string targetSystem, string fakeFolderPath, int? hiddenFailuresTolerated = null, float? spatialDistributionOfRedundancy = null)
        {
            _targetSystem = targetSystem;
            _fakeFolderPath = fakeFolderPath;
            _hiddenFailuresTolerated = hiddenFailuresTolerated;
            _spatialDistributionOfRedundancy = spatialDistributionOfRedundancy;
        }
        /// <summary>
        /// Gets the current status of the disk established in the constructor.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> that may be used to cancel the operation before it completes.</param>
        /// <returns>A <see cref="StatusResultsBuilder"/> containing the status of the disk.</returns>
        public Task<StatusResultsBuilder> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            StatusResultsBuilder sb = new StatusResultsBuilder(_targetSystem);
            sb.NatureOfSystem = StatusNatureOfSystem.Leaf;
            if (_fakeFolderPath != null)
            {
                long totalBytes = Math.Abs(_fakeFolderPath.GetHashCode());
                long freeBytes = totalBytes * 8 / 10;
                long availableBytes = totalBytes * 8 / 10;
                double availablePercent = .8;
                sb.AddProperty("TotalBytes", totalBytes);
                sb.AddProperty("FreeBytes", freeBytes);
                sb.AddProperty("AvailableBytes", availableBytes);
                sb.AddProperty("AvailablePercent", availablePercent);
                return Task.FromResult(sb);
            }
            return null;
        }
    }
    /// <summary>
    /// A status node for the local disk storage.
    /// </summary>
    [DefaultPropertyThresholds("SampleDisk.SystemTemp", typeof(SampleVolumeAuditor))]
    [DefaultPropertyThresholds("SampleDisk.Temp", typeof(SampleVolumeAuditor))]
    [DefaultPropertyThresholds("SampleDisk.OperatingSystem", typeof(SampleVolumeAuditor))]
    class SampleDiskAuditor : StatusAuditor
    {
        private readonly SampleVolumeAuditor[] _diskAuditors = new SampleVolumeAuditor[] {
            new SampleVolumeAuditor("SystemTemp", @"C:\windows\temp", 0),
            new SampleVolumeAuditor("Temp", @"C:\temp", 0),
            new SampleVolumeAuditor("OperatingSystem", @"C:\windows", 0),
        };
        /// <summary>
        /// Constructs a <see cref="SampleDiskAuditor"/> to audit the default system volumes.
        /// </summary>
        public SampleDiskAuditor()
            : base("SampleDisk", TimeSpan.FromMinutes(1))
        {
        }
        /// <summary>
        /// Gets whether or not this status node is applicable and should be included in the list of statuses for this machine.
        /// </summary>
        protected internal override bool Applicable
        {
            get { return true; }
        }
        /// <summary>
        /// Computes the current status, building a <see cref="StatusResults"/> to hold information about the status.
        /// </summary>
        /// <param name="statusBuilder">A <see cref="StatusResultsBuilder"/> that may be used to fill in audit information.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public override async Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            foreach (SampleVolumeAuditor da in _diskAuditors)
            {
                try
                {
                    StatusResultsBuilder childResults = await da.GetStatus(cancel);
                    statusBuilder.AddChild(childResults);
                }
                catch (Exception ex)
                {
                    statusBuilder.AddException(ex);
                }
            }
        }
    }
    internal class TestHeterogenousNoExplicitRating : StatusAuditor
    {
        private readonly SampleVolumeAuditor[] _diskAuditors = new SampleVolumeAuditor[] {
            new SampleVolumeAuditor ("UserDocuments", @"C:\Documents"),
            new SampleVolumeAuditor ("SystemDefaultTemp", @"C:\Temp"),
        };

        public TestHeterogenousNoExplicitRating()
            : base(nameof(TestHeterogenousNoExplicitRating), TimeSpan.FromSeconds(3))
        {
        }

        protected internal override bool Applicable { get { return true; } }
        public override async Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            foreach (SampleVolumeAuditor da in _diskAuditors)
            {
                try
                {
                    StatusResultsBuilder childResults = await da.GetStatus(cancel);
                    statusBuilder.AddChild(childResults);
                }
                catch (Exception ex)
                {
                    statusBuilder.AddException(ex);
                }
            }
        }
    }
    internal class TestHeterogenousExplicitRating : StatusAuditor
    {
        private readonly SampleVolumeAuditor[] _diskAuditors = new SampleVolumeAuditor[] {
            new SampleVolumeAuditor ("UserDocuments", @"C:\Documents"),
            new SampleVolumeAuditor ("SystemDefaultTemp", @"C:\Temp"),
        };

        public TestHeterogenousExplicitRating()
            : base(nameof(TestHeterogenousExplicitRating), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override async Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            foreach (SampleVolumeAuditor da in _diskAuditors)
            {
                try
                {
                    StatusResultsBuilder childResults = await da.GetStatus(cancel);
                    statusBuilder.AddChild(childResults);
                }
                catch (Exception ex)
                {
                    statusBuilder.AddException(ex);
                }
            }
            statusBuilder.AddProperty("wc1", "a");
            statusBuilder.AddProperty("wc2", "b");
            statusBuilder.AddProperty("wc2", AmbientClock.UtcNow.AddMinutes(-10));
            statusBuilder.AddAlert("TestAlertCode", "test-terseAlert", "This is the detailed alert message", 0.1f);
        }
    }
    internal class TestHeterogenousOnlyExplicit : StatusAuditor
    {
        public TestHeterogenousOnlyExplicit()
            : base(nameof(TestHeterogenousOnlyExplicit), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            statusBuilder.AddProperty("wc1", "a");
            statusBuilder.AddProperty("wc2", "b");
            statusBuilder.AddProperty("wc2", AmbientClock.UtcNow.AddMinutes(-10));
            statusBuilder.AddException(new ApplicationException("This is a test"));
            return Task.CompletedTask;
        }
    }
    internal class TestSuperlativeExplicit : StatusAuditor
    {
        public TestSuperlativeExplicit()
            : base(nameof(TestSuperlativeExplicit), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            statusBuilder.AddSuperlative("test-superlative", "superlative terse", "superlative details");
            return Task.CompletedTask;
        }
    }
    internal class TestHeterogenousNoExplicit : StatusAuditor
    {
        public TestHeterogenousNoExplicit()
            : base(nameof(TestHeterogenousNoExplicit), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            return Task.CompletedTask;
        }
    }
    internal class TestHomogeneousExplicitFailure : StatusAuditor
    {
        private readonly SampleVolumeAuditor[] _diskAuditors = new SampleVolumeAuditor[] {
            new SampleVolumeAuditor ("UserDocuments", @"C:\Documents"),
            new SampleVolumeAuditor ("SystemDefaultTemp", @"C:\Temp"),
            new SampleVolumeAuditor ("OperatingSystem", @"C:\Windows"),
        };
        public TestHomogeneousExplicitFailure()
            : base(nameof(TestHomogeneousExplicitFailure), TimeSpan.FromSeconds(10))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHomogenous;
            statusBuilder.AddProperty("ChildCount", _diskAuditors.Count());
            statusBuilder.AddFailure("TestFailCode", "TEST-FAIL!", "This is the detailed fail message", 0.0f);
            return Task.CompletedTask;
        }
    }
    internal class TestHomogeneousWithFailure : StatusAuditor
    {
        private StatusResults _alwaysSuperlative;
        private StatusResults _alwaysOkay;
        private StatusResults _alwaysAlerting;
        private StatusResults _alwaysFailing;
        private StatusResults _alwaysCatastrophic;
        public TestHomogeneousWithFailure()
            : base(nameof(TestHomogeneousWithFailure), TimeSpan.FromSeconds(10))
        {
            _alwaysSuperlative = StatusResultsBuilder.CreateRawStatusResults("Child1AlwaysSuperlative", StatusRating.Superlative, "TestSuperlativeCode", "", "");
            _alwaysOkay = StatusResultsBuilder.CreateRawStatusResults("Child1AlwaysOkay", StatusRating.Okay, "TestOkayCode", "", "");
            _alwaysAlerting = StatusResultsBuilder.CreateRawStatusResults("Child2AlwaysAlerting", StatusRating.Alert, "TestAlertCode", "test-alert", "This status node always alerts!");
            _alwaysFailing = StatusResultsBuilder.CreateRawStatusResults("Child3AlwaysFailing", StatusRating.Fail, "TestFailCode", "test-fail", "This status node always fails!");
            _alwaysCatastrophic = StatusResultsBuilder.CreateRawStatusResults("Child3AlwaysCatastrophic", StatusRating.Catastrophic, "TestCatastrophicCode", "test-catastrophic", "This status node always catastrophic!");
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHomogenous;
            statusBuilder.AddChild(_alwaysSuperlative);
            statusBuilder.AddChild(_alwaysOkay);
            statusBuilder.AddChild(_alwaysAlerting);
            statusBuilder.AddChild(_alwaysFailing);
            statusBuilder.AddChild(_alwaysCatastrophic);
            return Task.CompletedTask;
        }
    }
    internal class TestHomogeneousWithMultipleFailure : StatusAuditor
    {
        private StatusResults _alwaysFailing1;
        private StatusResults _alwaysFailing2;
        public TestHomogeneousWithMultipleFailure()
            : base(nameof(TestHomogeneousWithMultipleFailure), TimeSpan.FromSeconds(10))
        {
            _alwaysFailing1 = StatusResultsBuilder.CreateRawStatusResults("Child1AlwaysFailing1", StatusRating.Fail, "TestFailCode", "test-fail", "This status node always fails!");
            _alwaysFailing2 = StatusResultsBuilder.CreateRawStatusResults("Child2AlwaysFailing2", StatusRating.Fail, "TestFailCode", "test-fail", "This status node always fails!");
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHomogenous;
            statusBuilder.AddChild(_alwaysFailing1);
            statusBuilder.AddChild(_alwaysFailing2);
            return Task.CompletedTask;
        }
    }
    internal class TestHomogeneousWithMultipleAlert : StatusAuditor
    {
        private StatusResults _alwaysAlerting1;
        private StatusResults _alwaysAlerting2;
        public TestHomogeneousWithMultipleAlert()
            : base(nameof(TestHomogeneousWithMultipleAlert), TimeSpan.FromSeconds(10))
        {
            _alwaysAlerting1 = StatusResultsBuilder.CreateRawStatusResults("Child1AlwaysAlerting1", StatusRating.Alert, "TestAlertCode", "test-alert1", "This status node always alerts!");
            _alwaysAlerting2 = StatusResultsBuilder.CreateRawStatusResults("Child2AlwaysAlerting2", StatusRating.Alert, "TestAlertCode", "test-alert2", "This status node always alerts!");
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHomogenous;
            statusBuilder.AddChild(_alwaysAlerting1);
            statusBuilder.AddChild(_alwaysAlerting2);
            return Task.CompletedTask;
        }
    }
    internal class TestDeepFailure : StatusAuditor
    {
        public TestDeepFailure()
            : base(nameof(TestDeepFailure), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        private void Recurse(StatusResultsBuilder statusBuilder, int level)
        {
            if (level == 0)
            {
                statusBuilder.AddFailure(nameof(TestDeepFailure), "test-" + nameof(TestDeepFailure), "This is the deep failure", 0.5f);
            }
            else
            {
                StatusResultsBuilder child = new StatusResultsBuilder("Level" + level.ToString());
                Recurse(child, level - 1);
                statusBuilder.AddChild(child.FinalResults);
            }
        }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            Recurse(statusBuilder, 4);
            return Task.CompletedTask;
        }
    }
    internal class TestMultipleSource : StatusAuditor
    {
        public TestMultipleSource()
            : base(nameof(TestMultipleSource), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        private void AddSource(StatusResultsBuilder statusBuilder, int sourceNumber)
        {
            if (sourceNumber < 3)
            {
                statusBuilder.AddOkay(nameof(TestMultipleSource), "test-" + nameof(TestMultipleSource) + "Okay", "This is the multiple source okay", 0.5f);
            }
            else if (sourceNumber < 6)
            {
                statusBuilder.AddAlert(nameof(TestMultipleSource), "test-" + nameof(TestMultipleSource) + "Alert", "This is the multiple source alert", 0.5f);
            }
            else
            {
                statusBuilder.AddFailure(nameof(TestMultipleSource), "test-" + nameof(TestMultipleSource) + "Fail", "This is the multiple source failure", 0.5f);
            }
            statusBuilder.SourceSystem = "Source " + sourceNumber.ToString();
        }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            for (int source = 0; source < 10; ++source)
            {
                StatusResultsBuilder childBuilder = new StatusResultsBuilder("Subsystem");
                childBuilder.NatureOfSystem = StatusNatureOfSystem.Leaf;
                childBuilder.SourceSystem = "Source " + source.ToString();
                childBuilder.AddProperty("sourceNumber", source);
                AddSource(childBuilder, source);
                statusBuilder.AddChild(childBuilder.FinalResults);
            }
            return Task.CompletedTask;
        }
    }
    internal class TestDeepMultipleSource : StatusAuditor
    {
        public TestDeepMultipleSource()
            : base(nameof(TestDeepMultipleSource), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        private void Recurse(StatusResultsBuilder statusBuilder, int level, int sourceNumber)
        {
            if (level == 0)
            {
                statusBuilder.NatureOfSystem = StatusNatureOfSystem.Leaf;
                if (sourceNumber < 3)
                {
                    statusBuilder.AddOkay(nameof(TestDeepMultipleSource), "test-" + nameof(TestMultipleSource) + "Okay", "This is the multiple source okay", 0.5f);
                }
                else if (sourceNumber < 6)
                {
                    statusBuilder.AddAlert(nameof(TestDeepMultipleSource), "test-" + nameof(TestMultipleSource) + "Alert", "This is the multiple source alert", 0.5f);
                }
                else
                {
                    statusBuilder.AddFailure(nameof(TestDeepMultipleSource), "test-" + nameof(TestMultipleSource) + "Fail", "This is the multiple source failure", 0.5f);
                }
            }
            else
            {
                if (level % 2 == 0) statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
                StatusResultsBuilder child = new StatusResultsBuilder("Level" + level.ToString());
                child.AddProperty("Level", level);
                child.AddProperty("SourceNumber", sourceNumber);
                Recurse(child, level - 1, sourceNumber);
                statusBuilder.AddChild(child.FinalResults);
            }
        }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            for (int source = 0; source < 10; ++source)
            {
                StatusResultsBuilder childBuilder = new StatusResultsBuilder(string.Empty);
                childBuilder.SourceSystem = "Source " + source.ToString();
                Recurse(childBuilder, 4, source);
                statusBuilder.AddChild(childBuilder.FinalResults);
            }
            return Task.CompletedTask;
        }
    }
    internal class TestAuditException : StatusAuditor
    {
        public TestAuditException()
            : base(nameof(TestAuditException), TimeSpan.FromSeconds(3))
        {
        }
        protected internal override bool Applicable { get { return true; } }
        public override Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken))
        {
            throw new ExpectedException(nameof(TestAuditException));
        }
    }
}
