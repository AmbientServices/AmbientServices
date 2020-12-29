using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An immutable class that contains information about a status audit alert.
    /// Alerts with the same <see cref="StatusAuditAlert.Rating"/>, and <see cref="StatusAuditAlert.AuditAlertCode"/> are considered equivalent and will be combined during report aggregation.
    /// </summary>
    public sealed class StatusAuditAlert : IEquatable<StatusAuditAlert>
    {
        /// <summary>
        /// An empty <see cref="StatusAuditAlert"/> in case they need to be compared.
        /// </summary>
        public static readonly StatusAuditAlert Empty = new StatusAuditAlert();
        /// <summary>
        /// A <see cref="StatusAuditAlert"/> for when no alert was reported.
        /// </summary>
        public static readonly StatusAuditAlert None = new StatusAuditAlert(StatusRating.Okay, "NoAlert", "No Alerts", "There are no alerts.");

        private readonly float _rating;
        private readonly string _auditAlertCode;
        private readonly string _terse;
        private readonly string _details;

        /// <summary>
        /// Gets a short string containing a code for this condition, or empty string if the report should not be collated by status code.
        /// Audit alert codes should not contain numbers or strings that might vary for the same type of error, and the messages for a given code should be essentially the same.
        /// Some examples might be, Timeout, Configuration, BadRequest, AccessDenied, ProgramError, NotFound, or MirrorBroken.
        /// This string should never contain any sensitive details.
        /// </summary>
        public string AuditAlertCode
        {
            get { return _auditAlertCode; }
        }
        /// <summary>
        /// A status rating indicating the overall state of the system represented by the report.
        /// </summary>
        public float Rating
        {
            get { return _rating; }
        }
        /// <summary>
        /// Gets a short message indicating that there is a suboptimal status (one that would be appropriate for SMS or a mobile alert dialog).  
        /// The message must not contain any line breaks or markup so that it can be sent in a text message.
        /// This message must not contain any sensitive details.
        /// </summary>
        public string Terse
        {
            get { return _terse; }
        }
        /// <summary>
        /// Gets a detailed message indicating the cause of a suboptimal status (one that would be appropriate for email, web, or mobile display).  
        /// The message should use the same format as documentation details.
        /// If authorization was not granted, and the error might contain sensitive details, the message may have been replaced by an error identifier which will need to be looked up in the error logs by an authorized user.
        /// </summary>
        public string Details
        {
            get { return _details; }
        }

        /// <summary>
        /// Constructs an empty <see cref="StatusAuditAlert"/>.
        /// </summary>
        private StatusAuditAlert()
        {
            _rating = StatusRating.Okay;
            _auditAlertCode = "OkayCode";
            _terse = "ok";
            _details = "The system is functioning normally.";
        }
        /// <summary>
        /// Constructs a <see cref="StatusAuditAlert"/> with the specified values.
        /// </summary>
        /// <param name="rating">A status rating indicating whether or not the system is working.</param>
        /// <param name="auditAlertCode">The code for this audit result.</param>
        /// <param name="terse">The terse alert message.</param>
        /// <param name="details">The details alert message.</param>
        public StatusAuditAlert(float rating, string auditAlertCode, string terse, string details)
        {
            _rating = rating;
            if (auditAlertCode == null) throw new ArgumentNullException(nameof(auditAlertCode), "The audit alert code may not be empty");
            _auditAlertCode = auditAlertCode;
            _terse = terse;
            _details = details;
        }

        /// <summary>
        /// Gets the 32-bit hash code for this object.
        /// </summary>
        /// <returns>The 32-bit hash code for this object.</returns>
        public override int GetHashCode()
        {
            return _rating.GetHashCode() ^ _auditAlertCode.GetHashCode();
        }
        /// <summary>
        /// Checks to see if this <see cref="StatusAuditAlert"/> is logically equal to another one.
        /// </summary>
        /// <param name="obj">The other object to compare to.</param>
        /// <returns>true if the objects are logically equivalent, false if they are not.</returns>
        public override bool Equals(object obj)
        {
            StatusAuditAlert alert = obj as StatusAuditAlert;
            if (alert == null) return false;
            return Equals(alert);
        }

        /// <summary>
        /// Checks to see if this <see cref="StatusAuditAlert"/> is logically equal to another one.
        /// </summary>
        /// <param name="other">The other <see cref="StatusAuditAlert"/> to compare to.</param>
        /// <returns>true if the objects are logically equivalent, false if they are not.</returns>
        public bool Equals(StatusAuditAlert other)
        {
            if (other == null) return false;
            return this._rating.Equals(other._rating) && String.Equals(this._auditAlertCode, other._auditAlertCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compares <paramref name="a"/> and <paramref name="b"/> and returns whether or not <paramref name="a"/> is equal to <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first <see cref="StatusAuditAlert"/> to compare.</param>
        /// <param name="b">The second <see cref="StatusAuditAlert"/> to compare.</param>
        /// <returns><b>true</b> if <paramref name="a"/> is equal to <paramref name="b"/>.</returns>
        public static bool operator ==(StatusAuditAlert a, StatusAuditAlert b)
        {
            if (ReferenceEquals(a, null)) return ReferenceEquals(b, null);
            return a.Equals(b);
        }
        /// <summary>
        /// Compares <paramref name="a"/> and <paramref name="b"/> and returns whether or not <paramref name="a"/> is NOT equal to <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first <see cref="StatusAuditAlert"/> to compare.</param>
        /// <param name="b">The second <see cref="StatusAuditAlert"/> to compare.</param>
        /// <returns><b>true</b> if <paramref name="a"/> is NOT equal to <paramref name="b"/>.</returns>
        public static bool operator !=(StatusAuditAlert a, StatusAuditAlert b)
        {
            if (ReferenceEquals(a, null)) return !ReferenceEquals(b, null);
            return !a.Equals(b);
        }
        /// <summary>
        /// Gets a string representation of the instance.
        /// </summary>
        /// <returns>A string representation of the instance.</returns>
        public override string ToString()
        {
            return $"{_rating}({_auditAlertCode}): {_terse}({_details})";
        }
    }
    /// <summary>
    /// An immutable class that contains a report generated by an audit of specific target system.
    /// Separate from <see cref="StatusAuditAlert"/> because not all audits generate alerts.
    /// Reports with the same <see cref="StatusAuditReport.Alert"/> are considered equivalent and may be combined during report aggregation if they are reported by the same source or for the same target.
    /// </summary>
    public sealed class StatusAuditReport : IEquatable<StatusAuditReport>
    {
        /// <summary>
        /// A <see cref="StatusAuditReport"/> indicating that the first audit is pending.
        /// </summary>
        public static readonly StatusAuditReport Pending = new StatusAuditReport();

        private readonly DateTime _auditStartTime;
        private readonly TimeSpan _auditDuration;
        private readonly DateTime? _nextAuditTime;
        private readonly StatusAuditAlert _alert;

        /// <summary>
        /// Gets the time when the audit of the target system started.
        /// </summary>
        public DateTime AuditStartTime
        {
            get { return _auditStartTime; }
        }
        /// <summary>
        /// Gets the time when the audit occurred.
        /// </summary>
        public TimeSpan AuditDuration
        {
            get { return _auditDuration; }
        }
        /// <summary>
        /// Gets the time when the system should be reaudited (if any).
        /// </summary>
        public DateTime? NextAuditTime
        {
            get { return _nextAuditTime; }
        }
        /// <summary>
        /// Gets the <see cref="StatusAuditAlert"/> for this audit, if any.
        /// No <see cref="StatusAuditAlert"/> implies that there were no issues, which should default to <see cref="StatusRating.Okay"/>.
        /// </summary>
        public StatusAuditAlert Alert
        {
            get { return _alert; }
        }

        /// <summary>
        /// Constructs the "Pending" <see cref="StatusAuditReport"/>.
        /// </summary>
        private StatusAuditReport()
        {
            _auditStartTime = AmbientClock.UtcNow;
            _auditDuration = TimeSpan.FromTicks(0);
            _nextAuditTime = AmbientClock.UtcNow;
            _alert = new StatusAuditAlert(StatusRating.Pending, "Pending", "Pending", "The first audit has not run yet!");
        }
        /// <summary>
        /// Constructs a <see cref="StatusAuditReport"/> with the specified values.
        /// </summary>
        /// <param name="auditStartTime">The <see cref="DateTime"/> when the status was assessed.</param>
        /// <param name="auditDuration">A <see cref="TimeSpan"/> indicating how long the audit took.</param>
        /// <param name="nextAuditTime">The next time the audit should happen (if any).</param>
        /// <param name="alert">A <see cref="StatusAuditAlert"/> if the audit includes an alert, if any</param>
        public StatusAuditReport(DateTime auditStartTime, TimeSpan auditDuration, DateTime? nextAuditTime = null, StatusAuditAlert alert = null)
        {
            _auditStartTime = auditStartTime;
            _auditDuration = auditDuration;
            _nextAuditTime = nextAuditTime;
            _alert = alert;
        }

        /// <summary>
        /// Gets a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            output.Append('@');
            output.Append(_auditStartTime.ToShortTimeString().Replace(" ", ""));
            if (_alert != null)
            {
                output.Append(':');
                output.Append(StatusRating.GetRangeName(_alert.Rating));
                if (!string.IsNullOrEmpty(_alert.Terse))
                {
                    output.Append('(');
                    output.Append(_alert.Terse);
                    output.Append(')');
                }
            }
            return output.ToString();
        }
        /// <summary>
        /// Gets the 32-bit hash code for this object.
        /// </summary>
        /// <returns>The 32-bit hash code for this object.</returns>
        public override int GetHashCode()
        {
            return _alert?.GetHashCode() ?? 0;
        }
        /// <summary>
        /// Checks to see if this <see cref="StatusAuditReport"/> is logically equal to another one.
        /// </summary>
        /// <param name="obj">The other object to compare to.</param>
        /// <returns>true if the objects are logically equivalent, false if they are not.</returns>
        public override bool Equals(object obj)
        {
            StatusAuditReport alert = obj as StatusAuditReport;
            if (alert == null) return false;
            return Equals(alert);
        }

        /// <summary>
        /// Checks to see if this <see cref="StatusAuditReport"/> is logically equal to another one.
        /// </summary>
        /// <param name="other">The other <see cref="StatusAuditReport"/> to compare to.</param>
        /// <returns>true if the objects are logically equivalent, false if they are not.</returns>
        public bool Equals(StatusAuditReport other)
        {
            if (other == null) return false;
            return Object.Equals(this._alert, other._alert);
        }

        /// <summary>
        /// Compares <paramref name="a"/> and <paramref name="b"/> and returns whether or not <paramref name="a"/> is equal to <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first <see cref="StatusAuditReport"/> to compare.</param>
        /// <param name="b">The second <see cref="StatusAuditReport"/> to compare.</param>
        /// <returns><b>true</b> if <paramref name="a"/> is equal to <paramref name="b"/>.</returns>
        public static bool operator ==(StatusAuditReport a, StatusAuditReport b)
        {
            if (ReferenceEquals(a, null)) return ReferenceEquals(b, null);
            return a.Equals(b);
        }
        /// <summary>
        /// Compares <paramref name="a"/> and <paramref name="b"/> and returns whether or not <paramref name="a"/> is NOT equal to <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first <see cref="StatusAuditReport"/> to compare.</param>
        /// <param name="b">The second <see cref="StatusAuditReport"/> to compare.</param>
        /// <returns><b>true</b> if <paramref name="a"/> is NOT equal to <paramref name="b"/>.</returns>
        public static bool operator !=(StatusAuditReport a, StatusAuditReport b)
        {
            if (ReferenceEquals(a, null)) return !ReferenceEquals(b, null);
            return !a.Equals(b);
        }
    }
    /// <summary>
    /// An abstract class that manages periodic status auditing of a system.
    /// Any derivitave of this class will be automatically instantiated by the system retained in a system-wide list to track status.
    /// </summary>
    public abstract class StatusAuditor : StatusChecker
    {
        private readonly Status _status;
        private readonly TimeSpan _baselineAuditFrequency;

        private AmbientCancellationTokenSource _backgroundCancelSource = new AmbientCancellationTokenSource(); // interlocked
        private AmbientEventTimer _auditTimer;  // interlocked
        private int _backgroundAuditCount;      // interlocked
        private int _foregroundAuditCount;      // interlocked
        private long _nextAuditTime;            // interlocked
        private long _frequencyTicks;           // interlocked -- the current audit frequency, adjusted based on how long the audit takes (slower audits should run less frequently) and what the resulting rating status is.

        /// <summary>
        /// Constructs an <see cref="StatusAuditor"/> with the specified values.
        /// </summary>
        /// <param name="targetSystem">The name of the target system (if any).</param>
        /// <param name="baselineAuditFrequency">
        /// The baseline frequency with which audits should be run.  
        /// The system will start running tests this frequently but will automatically tune the frequency to between one quarter this frequency and ten times this frequency based on the status rating (worse rating means do audits more frequently) and the duration of the audit (fast audits cause more frequent audits).
        /// This means that systems that process status audits faster will automatically be more responsive in reporting issues.
        /// If audits start to timeout, audits will happen less frequently even if they are failing so that the status tests don't contribute to system congestion.
        /// <see cref="TimeSpan.Zero"/> and negative time spans are treated as if they were <see cref="TimeSpan.MaxValue"/>.
        /// </param>
        /// <param name="status">The <see cref="Status"/> this auditor belongs to, or null if this should be a standalone auditor owned by the caller.</param>
        protected internal StatusAuditor(string targetSystem, TimeSpan baselineAuditFrequency, Status status)
            : base(targetSystem)
        {
            _status = status;
            _baselineAuditFrequency = baselineAuditFrequency;

            _frequencyTicks = baselineAuditFrequency.Ticks;
            _nextAuditTime = AmbientClock.UtcNow.AddMilliseconds(10).Ticks;
            System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(InitialAudit));  // queue the initial status test to run immediately but on a threadpool thread
            // should we update periodically?
            if (baselineAuditFrequency < TimeSpan.MaxValue && baselineAuditFrequency > TimeSpan.FromTicks(0))
            {
                _auditTimer = new AmbientEventTimer(TimeSpan.FromTicks(_frequencyTicks).TotalMilliseconds);
                _auditTimer.Elapsed += AuditTimer_Elapsed;
                _auditTimer.AutoReset = false;  // note that the timer should remain stopped until we start it after the first audit happens
            }
        }

        /// <summary>
        /// Constructs an <see cref="StatusAuditor"/> associated with the default status instance and with the specified property values.
        /// </summary>
        /// <param name="targetSystem">The name of the target system (if any).</param>
        /// <param name="baselineAuditFrequency">
        /// The baseline frequency with which audits should be run.  
        /// The system will start running tests this frequently but will automatically tune the frequency to between one quarter this frequency and ten times this frequency based on the status rating (worse rating means do audits more frequently) and the duration of the audit (fast audits cause more frequent audits).
        /// This means that systems that process status audits faster will automatically be more responsive in reporting issues.
        /// If audits start to timeout, audits will happen less frequently even if they are failing so that the status tests don't contribute to system congestion.
        /// <see cref="TimeSpan.Zero"/> and negative time spans are treated as if they were <see cref="TimeSpan.MaxValue"/>.
        /// </param>
        protected internal StatusAuditor(string targetSystem, TimeSpan baselineAuditFrequency)
            : this(targetSystem, baselineAuditFrequency, Status.DefaultInstance)
        {
        }

        private void InitialAudit(object sender)
        {
            InternalAuditAsync().GetAwaiter().GetResult();
        }
        private void AuditTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            InternalAuditAsync(false, _backgroundCancelSource.Token).GetAwaiter().GetResult();
        }
        /// <summary>
        /// Computes the current status, building a <see cref="StatusResults"/> to hold information about the status.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        sealed protected internal override Task<StatusResults> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            return InternalAuditAsync(true, cancel);
        }
        private async Task<StatusResults> InternalAuditAsync(bool foreground = false, CancellationToken cancel = default(CancellationToken))
        {
            StatusResultsBuilder builder = new StatusResultsBuilder(this);
            try
            {
                // in case the timer went off more than once due to test (or overall system) slowness, disable the timer until we're done here
                _auditTimer?.Stop();
                // have we already shut down?  bail out now!
                if (foreground) Interlocked.Increment(ref _foregroundAuditCount); else Interlocked.Increment(ref _backgroundAuditCount);   // NOTE: these audit counts are structs so using a ternary operator here doesn't do what you might think
                // have we already shut down?  bail out now!
                if (_status?.ShuttingDown ?? false) return null;
                // call the derived object to get the status
                await Audit(builder, cancel).ConfigureAwait(false);
                // schedule the next audit
                builder.NextAuditTime = ScheduleNextAudit(builder.WorstAlert == null ? (float?)null : builder.WorstAlert.Rating, builder.Elapsed);
            }
#pragma warning disable CA1031  // we really DO want to catch ALL exceptions here--this is a status test, and the exception will be reported through the status system.  if we rethrew it, it would crash the program
            catch (Exception ex)
#pragma warning restore CA1031
            {
                builder.AddException(ex);
            }
            finally
            {
                _auditTimer?.Start();
            }
            // get the results
            StatusResults newStatusResults = builder.FinalResults;
            // save the results AND return them
            SetLatestResults(newStatusResults);
            return newStatusResults;
        }
        private DateTime ScheduleNextAudit(float? rating, TimeSpan auditDuration)
        {
            // set the next audit time
            TimeSpan nextInterval = AdjustedAuditInterval(rating, auditDuration);
            System.Diagnostics.Debug.Assert(nextInterval.Ticks > 0);
            DateTime nextAudit = (nextInterval == TimeSpan.MaxValue) ? DateTime.MaxValue : AmbientClock.UtcNow + nextInterval;
            Interlocked.Exchange(ref _nextAuditTime, nextAudit.Ticks);
            if (_auditTimer != null) _auditTimer.Interval = nextInterval.TotalMilliseconds;
            return nextAudit;
        }
        private TimeSpan AdjustedAuditInterval(float? rating, TimeSpan auditDuration)
        {
            if (_baselineAuditFrequency.Ticks <= 0 || _baselineAuditFrequency == TimeSpan.MaxValue) return TimeSpan.MaxValue;
            float ratingAdjustment;
            if (rating == null)
            {
                ratingAdjustment = 1.0f;    // go back to the default and bail out without doing all the complicated calculations
                Interlocked.Exchange(ref _frequencyTicks, _baselineAuditFrequency.Ticks);
                return TimeSpan.FromTicks(_frequencyTicks);
            }
            else // we have a rating, so we'll adjust based on both the rating and the test duration
            {
                if (rating <= StatusRating.Fail)
                {
                    ratingAdjustment = 0.75f;   // do the test a lot more frequently because the system is failing and we want to know right away when it has recovered or if the test was a fluke
                }
                else if (rating <= StatusRating.Alert)
                {
                    ratingAdjustment = 0.9f;    // do the tests a little more frequently because the system may be in a bad state or it may be a fluke
                }
                else if (rating <= StatusRating.Okay)
                {
                    ratingAdjustment = 1.1f;    // do the tests less frequently because the system is working just fine
                }
                else
                {
                    ratingAdjustment = 1.5f;    // do the tests much less frequently because the system is working superlatively
                }
            }
            TimeSpan oldFrequency = TimeSpan.FromTicks(_frequencyTicks);
            // if the test took more than 1/1000th of the current frequency, slow it down--status tests shouldn't be taking a significant amount of processing time.  Note that this adjustment will affect the frequency more slowly than the rating will.
            float durationAdjustment = (float)Math.Pow((1000 * auditDuration.TotalMilliseconds + 1) / (oldFrequency.TotalMilliseconds + 1), .1);
            // adjust the frequency based on the duration of the audit and the rating--the result should always be between one quarter the baseline frequency and 10x the baseline frequency
            Interlocked.Exchange(ref _frequencyTicks, (long)Math.Max(_baselineAuditFrequency.Ticks / 10, Math.Min(_baselineAuditFrequency.Ticks * 4, _frequencyTicks * ratingAdjustment * durationAdjustment)));
            System.Diagnostics.Debug.Assert(_frequencyTicks > 0);
            return TimeSpan.FromTicks(_frequencyTicks);
        }
        /// <summary>
        /// Computes the current status, building a <see cref="StatusResults"/> to hold information about the status.
        /// </summary>
        /// <param name="statusBuilder">A <see cref="StatusResultsBuilder"/> that may be used to fill in audit information.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        protected internal abstract Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Starts stopping any asynchronous activity.
        /// </summary>
        protected internal override Task BeginStop()
        {
            _auditTimer?.Stop();
            _backgroundCancelSource?.Cancel();
            return Task.CompletedTask;
        }
        /// <summary>
        /// Finishes stopping any asynchronous activity;
        /// </summary>
        protected internal override Task FinishStop()
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// Dispose the instance (only used by derived classes).
        /// </summary>
        /// <param name="disposing">Whether or not we are disposing (as opposed to finalizing).</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (_auditTimer != null)
                {
                    _auditTimer.Dispose();
                    System.Threading.Interlocked.Exchange(ref _auditTimer, null);
                }
                if (_backgroundCancelSource != null)
                {
                    _backgroundCancelSource.Dispose();
                    System.Threading.Interlocked.Exchange(ref _backgroundCancelSource, null);
                }
            }
        }
    }
}
