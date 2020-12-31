using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A abstract base class containing logic to compute status results for a particular part of the system.
    /// Any non-abstract derivitave of this class with an empty constructor will be automatically instantiated by the system and retained in a system-wide list to track status.
    /// Derived classes do not have to be immutable, but they must be threadsafe.
    /// StatusTestNode is disposable because derivatives may contain things like mutexes and timers that require disposal.
    /// </summary>
    public abstract class StatusChecker : IDisposable
    {
        private readonly string _targetSystem;
        private readonly StatusResultsTracker _resultsTracker = new StatusResultsTracker();
        private bool _disposedValue;

        /// <summary>
        /// Constructs a <see cref="StatusChecker"/>.
        /// </summary>
        /// <param name="targetSystem">The name of the target system (if any).</param>
        protected internal StatusChecker(string targetSystem)
        {
            _targetSystem = targetSystem;
            // set the status to pending for now just in case anyone asks for it before the initial check happens
            SetLatestResults(StatusResults.GetPendingResults(null, targetSystem));
        }

        /// <summary>
        /// Gets the name of the target system.
        /// </summary>
        public string TargetSystem
        {
            get { return _targetSystem; }
        }
        /// <summary>
        /// Gets the latest status results.
        /// </summary>
        public StatusResults LatestResults { get { return _resultsTracker.LatestResults; } }
        /// <summary>
        /// Gets an enumeration of previous <see cref="StatusResults"/>s.  
        /// Null or empty if no such test results are available or applicable.
        /// Note that previous ratings are limited to a set time span and a set number of entries (see settings).
        /// </summary>
        public virtual IEnumerable<StatusResults> History
        {
            get { return _resultsTracker.History; }
        }

        /// <summary>
        /// Starts stopping any asynchronous activity.
        /// </summary>
        protected internal virtual Task BeginStop()
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// Finishes stopping any asynchronous activity;
        /// </summary>
        protected internal virtual Task FinishStop()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the latest results.
        /// </summary>
        /// <param name="newResults">The new <see cref="StatusResults"/>.</param>
        protected void SetLatestResults(StatusResults newResults)
        {
            _resultsTracker.SetLatestResults(newResults);
        }

        /// <summary>
        /// Gets whether or not this status node is applicable and should be included in the list of statuses for this machine.
        /// </summary>
        protected internal abstract bool Applicable { get; }
        /// <summary>
        /// Computes the current status, building a <see cref="StatusResults"/> to hold information about the status.
        /// Note that this function may be called on multiple threads simultaneously.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the operation before it finishes.</param>
        public virtual Task<StatusResults> GetStatus(CancellationToken cancel = default(CancellationToken))
        {
            return Task.FromResult(LatestResults);
        }
        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <param name="disposing">Whether the instance is being disposed (as opposed to finalized).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~StatusChecker()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// A class used to track the status results from a configured top-level status checker.
    /// </summary>
    class StatusResultsTracker
    {
        private StatusResults _statusResults;                           // interlocked
        private ConcurrentQueue<StatusResults> _statusResultsHistory;   // interlocked

        public StatusResultsTracker()
        {
        }

        /// <summary>
        /// Gets the latest status results.
        /// </summary>
        public StatusResults LatestResults { get { return _statusResults; } }
        /// <summary>
        /// Gets an enumeration of previous <see cref="StatusResults"/>s.  
        /// Null or empty if no such test results are available or applicable.
        /// Note that previous ratings are limited to a set time span and a set number of entries (see settings).
        /// </summary>
        public IEnumerable<StatusResults> History
        {
            get { return _statusResultsHistory; }
        }

        /// <summary>
        /// Adds the specified results as the latest results, moving the previous results to the history.
        /// </summary>
        /// <param name="newResults">The new latest results.</param>
        /// <remarks>
        /// Note that the new results will replace the old results and the old results will briefly disappear before being put into the history.
        /// </remarks>
        public void SetLatestResults(StatusResults newResults)
        {
            StatusResults oldResults = Interlocked.Exchange(ref _statusResults, newResults);
            if (oldResults != null)
            {
                ConcurrentQueue<StatusResults> history = _statusResultsHistory;
                if (history == null)
                {
                    history = new ConcurrentQueue<StatusResults>();
                    ConcurrentQueue<StatusResults> result = Interlocked.CompareExchange(ref _statusResultsHistory, history, null);
                    // this condition can only be hit if two threads enter this block of code and both try to update the status results history, so tests can only cover it inconsistently
                    if (result != null) history = result;
                }
                history.Enqueue(oldResults);
                // NOTE that there is a race here that might remove too many previous entries--this should be rare and not catastrophic
                TruncateQueue(_statusResultsHistory);
            }
        }
        private static IAmbientSetting<int> _StatusResultsRetentionMinutes = AmbientSettings.GetAmbientSetting<int>(nameof(StatusChecker) + "-HistoryRetentionMinutes", "The maximum number of minutes to keep status results history", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "103860");
        private static IAmbientSetting<int> _StatusResultsRetentionEntries = AmbientSettings.GetAmbientSetting<int>(nameof(StatusChecker) + "-HistoryRetentionEntries", "The maximum number of status results history entries to keep", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "100");
        private static void TruncateQueue(ConcurrentQueue<StatusResults> queueToTruncate)
        {
            StatusResults oldRatingResults;
            // only keep entries newer than that set retention period
            while (!queueToTruncate.IsEmpty)
            {
                // nothing in the queue?
                if (!queueToTruncate.TryPeek(out oldRatingResults)) break;
                DateTime time = (oldRatingResults.Report == null) ? oldRatingResults.Time : oldRatingResults.Report.AuditStartTime;
                // first item in the queue is newer than the retention cutoff?
                if (time >= AmbientClock.UtcNow.AddMinutes(-_StatusResultsRetentionMinutes.Value)) break;
                queueToTruncate.TryDequeue(out oldRatingResults);
            }
            // only keep up to the configured number of values
            while (queueToTruncate.Count > _StatusResultsRetentionEntries.Value)
            {
                queueToTruncate.TryDequeue(out oldRatingResults);
            }
        }
    }
}
