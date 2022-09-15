using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A class that coordinates service profilers.
    /// </summary>
    public class AmbientServiceProfilerCoordinator : IAmbientServiceProfilerNotificationSink, IDisposable
    {
        private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
        private static readonly AmbientService<IAmbientServiceProfiler> _AmbientServiceProfiler = Ambient.GetService<IAmbientServiceProfiler>();

        private readonly IAmbientSetting<Regex?> _defaultSystemGroupTransformSetting;
        private readonly IAmbientServiceProfiler? _eventBroadcaster;
        private readonly AsyncLocal<ScopeOnSystemSwitchedDistributor> _scopeDistributor;
        private bool _disposedValue;

        /// <summary>
        /// Constructs an AmbientServiceProfilerCoordinator using settings obtained from the ambient settings set.
        /// </summary>
        public AmbientServiceProfilerCoordinator()
            : this(_SettingsSet.Local)
        {
        }
        /// <summary>
        /// Constructs an AmbientServiceProfilerCoordinator using the specified settings set.
        /// </summary>
        /// <param name="settingsSet"></param>
        public AmbientServiceProfilerCoordinator(IAmbientSettingsSet? settingsSet)
        {
            _defaultSystemGroupTransformSetting = AmbientSettings.GetSettingsSetSetting<Regex?>(settingsSet, nameof(AmbientServiceProfilerCoordinator) + "-DefaultSystemGroupTransform", 
                @"A `Regex` string used to transform the system identifier to a group identifier.
The regular expression will attempt to match the system identifier, with the values for any matching match groups being concatenated into the system group identifier.", 
                s => string.IsNullOrEmpty(s) ? null : new Regex(s, RegexOptions.Compiled));
            _scopeDistributor = new AsyncLocal<ScopeOnSystemSwitchedDistributor>();
            _eventBroadcaster = _AmbientServiceProfiler.Local;
            _eventBroadcaster?.RegisterSystemSwitchedNotificationSink(this);
        }

        /// <summary>
        /// Notifies the notification sink that the system has switched.
        /// </summary>
        /// <remarks>
        /// This function will be called whenever the service profiler is told that the currently-processing system has switched.
        /// Note that the previously-executing system may or may not be revised at this time.  
        /// Such revisions can be used to distinguish between processing that resulted in success or failure, or other similar outcomes that the notifier wishes to distinguish.
        /// </remarks>
        /// <param name="newSystemStartStopwatchTimestamp">The stopwatch timestamp when the new system started.</param>
        /// <param name="newSystem">The identifier for the system that is starting to run.</param>
        /// <param name="oldSystemStartStopwatchTimestamp">The stopwatch timestamp when the old system started running.</param>
        /// <param name="revisedOldSystem">The (possibly-revised) name for the system that has just finished running, or null if the identifier for the old system does not need revising.</param>
        public void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string? revisedOldSystem = null)
        {
            if (_scopeDistributor.Value == null) _scopeDistributor.Value = new ScopeOnSystemSwitchedDistributor();
            _scopeDistributor.Value.OnSystemSwitched(newSystemStartStopwatchTimestamp, newSystem, oldSystemStartStopwatchTimestamp, revisedOldSystem);
        }
        /// <summary>
        /// Creates a service profiler which profiles the current call context.
        /// </summary>
        /// <param name="scopeName">A name of the call context to attach to the analyzer.</param>
        /// <param name="overrideSystemGroupTransformRegex">A <see cref="Regex"/> string to transform the system into a system group.</param>
        /// <returns>A <see cref="IAmbientServiceProfile"/> that will profile systems executed in this call context, or null if there is no ambient service profiler event collector.  Note that the returned object is NOT thread-safe.</returns>
        public IAmbientServiceProfile? CreateCallContextProfiler(string scopeName, string? overrideSystemGroupTransformRegex = null)
        {
            IAmbientServiceProfiler? metrics = _AmbientServiceProfiler.Local;
            if (metrics != null)
            {
                Regex? groupTransform = (overrideSystemGroupTransformRegex == null) ? _defaultSystemGroupTransformSetting.Value : new Regex(overrideSystemGroupTransformRegex, RegexOptions.Compiled);
                if (_scopeDistributor.Value == null) _scopeDistributor.Value = new ScopeOnSystemSwitchedDistributor();
                CallContextServiceProfiler analyzer = new(_scopeDistributor.Value, scopeName, groupTransform);
                return analyzer;
            }
            return null;
        }
        /// <summary>
        /// Creates a service profiler which profiles the entire process in sequential time units of the specified size.
        /// </summary>
        /// <param name="scopeNamePrefix">A <see cref="TimeSpan"/> indicating the size of the window.</param>
        /// <param name="windowPeriod">A <see cref="TimeSpan"/> indicating how often reports are desired.</param>
        /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientServiceProfile"/> at the end of each time window.</param>
        /// <param name="overrideSystemGroupTransformRegex">A <see cref="Regex"/> string to transform the procesor into a processor group.</param>
        /// <returns>A <see cref="IDisposable"/> that scopes the collection of the profiles.</returns>
        public IDisposable? CreateTimeWindowProfiler(string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientServiceProfile, Task> onWindowComplete, string? overrideSystemGroupTransformRegex = null)
        {
            IAmbientServiceProfiler? metrics = _AmbientServiceProfiler.Local;
            if (metrics == null) return null;
            Regex? groupTransform = (overrideSystemGroupTransformRegex == null) ? _defaultSystemGroupTransformSetting.Value : new Regex(overrideSystemGroupTransformRegex, RegexOptions.Compiled);
            TimeWindowServiceProfiler tracker = new(metrics, scopeNamePrefix, windowPeriod, onWindowComplete, groupTransform);
            return tracker;
        }
        /// <summary>
        /// Creates a service profiler which profiles the entire process for the entire (remaining) duration of execution.
        /// Note that this is only useful to determine the distribution for an entire process from start to finish, which is not very useful if the process is very long-lived.
        /// <see cref="CreateTimeWindowProfiler"/> is a better match in most situations.
        /// </summary>
        /// <param name="scopeName">A name for the contxt to attach to the analyzer.</param>
        /// <param name="overrideSystemGroupTransformRegex">A <see cref="Regex"/> string to transform the procesor into a processor group.</param>
        /// <returns>A <see cref="IAmbientServiceProfile"/> containing a service profile for the entire process.  Note that the returned object is NOT thread-safe.</returns>
        /// <remarks>
        /// This is different from using <see cref="CreateCallContextProfiler"/> because that will only analyze the call context it's called from, 
        /// whereas this will analyze all threads and call contexts in the process.  
        /// They will produce the same results only for programs where there is only a single call context (no parallelization)
        /// </remarks>
        public IAmbientServiceProfile? CreateProcessProfiler(string scopeName, string? overrideSystemGroupTransformRegex = null)
        {
            IAmbientServiceProfiler? metrics = _AmbientServiceProfiler.Local;
            if (metrics != null)
            {
                Regex? groupTransform = (overrideSystemGroupTransformRegex == null) ? _defaultSystemGroupTransformSetting.Value : new Regex(overrideSystemGroupTransformRegex, RegexOptions.Compiled);
                ProcessOrSingleTimeWindowServiceProfiler tracker = new(metrics, scopeName, groupTransform);
                return tracker;
            }
            return null;
        }
        /// <summary>
        /// Disposes of this instance.  May be overridden by derived classes.
        /// </summary>
        /// <param name="disposing">Whether or not we're disposing (as opposed to finalizing).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _eventBroadcaster?.DeregisterSystemSwitchedNotificationSink(this);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }
        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    /// <summary>
    /// An interface that abstracts an ambient service profile.
    /// </summary>
    public interface IAmbientServiceProfile : IDisposable
    {
        /// <summary>
        /// Gets the name of the scope being analyzed.  The scope identifies the scope of the operations that were profiled.
        /// </summary>
        string ScopeName { get; }
        /// <summary>
        /// Gets an enumeration of <see cref="AmbientServiceProfilerAccumulator"/> instances indicating the relative ratios of time spent executing in each of the systems in the associated scope.
        /// </summary>
        IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics { get; }
    }
    /// <summary>
    /// A class that accumulates processing count and time for a specific system.  Thread-safe.
    /// </summary>
    public class AmbientServiceProfilerAccumulator
    {
        private readonly string _group;
        private readonly long _executionCount;           // interlocked
        private readonly long _totalStopwatchTicksUsed;  // interlocked

        /// <summary>
        /// Gets the group the accumulator is for.
        /// </summary>
        public string Group => _group;
        /// <summary>
        /// Gest the number of times systems in this group were executed.
        /// </summary>
        public long ExecutionCount => _executionCount;
        /// <summary>
        /// Gets the total number of stopwatch ticks used by this system group.
        /// </summary>
        public long TotalStopwatchTicksUsed => _totalStopwatchTicksUsed;
        /// <summary>
        /// Gets the amount of time used by this system group.
        /// </summary>
        public TimeSpan TimeUsed => new(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(_totalStopwatchTicksUsed));

        /// <summary>
        /// Constructs a AmbientServiceProfileAccumulator for the specified system.
        /// </summary>
        /// <param name="group">The system.</param>
        /// <param name="totalStopwatchTicksUsed">The number of stopwatch ticks used by this system.</param>
        /// <param name="executionCount">The initial execution count.  Defaults to one.</param>
        public AmbientServiceProfilerAccumulator(string group, long totalStopwatchTicksUsed, long executionCount = 1)
        {
            _group = group;
            _executionCount = executionCount;
            _totalStopwatchTicksUsed = totalStopwatchTicksUsed;
        }
    }
}
