using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

[DefaultAmbientService]
internal class BasicAmbientServiceProfiler : IAmbientServiceProfiler
{
    private readonly ConcurrentHashSet<IAmbientServiceProfilerNotificationSink> _notificationSinks = new();
    private readonly AsyncLocal<CallContextActiveSystemData> _activeSystem;

    public BasicAmbientServiceProfiler()
    {
        _activeSystem = new AsyncLocal<CallContextActiveSystemData>();
    }

    public void SwitchSystem(string? system, string? updatedPreviousSystem = null)
    {
        CallContextActiveSystemData oldSystem = _activeSystem.Value;
        // value not yet initialized? // note that this is a struct so it can't be null, so we need to initialize this to the default value for the context, which apparently just started
        if (oldSystem.RawGroup == null) oldSystem = new CallContextActiveSystemData(null, AmbientClock.Ticks);
        CallContextActiveSystemData newSystem = new(system);
        _activeSystem.Value = newSystem;
        // call all the notification sinks
        foreach (IAmbientServiceProfilerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnSystemSwitched(newSystem.StartStopwatchTimestamp, newSystem.Group, oldSystem.StartStopwatchTimestamp, updatedPreviousSystem);
        }
    }
    public bool RegisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}

/// <summary>
/// A struct that holds information about which system is currently active in a call context.
/// </summary>
internal struct CallContextActiveSystemData
{
    /// <summary>
    /// The currently-active system or system group identifier.
    /// </summary>
    public string Group => string.IsNullOrEmpty(RawGroup) ? "" : RawGroup;
    /// <summary>
    /// The currently-active system or system group identifier (null if this struct is default).
    /// </summary>
    internal string RawGroup { get; }
    /// <summary>
    /// The stopwatch timestamp when this system or group became active.
    /// Based on <see cref="AmbientClock.Ticks"/>.
    /// </summary>
    public long StartStopwatchTimestamp { get; private set; }

    /// <summary>
    /// Constructs a CallContextActiveSystemData with the specified system, starting right now.
    /// </summary>
    /// <param name="system">The identifier for the active system (or system group).</param>
    public CallContextActiveSystemData(string? system)
    {
        RawGroup = system ?? "";
        StartStopwatchTimestamp = AmbientClock.Ticks;
    }
    /// <summary>
    /// Constructs a CallContextActiveSystemData with the specified system (or system group) and start timestamp.
    /// </summary>
    /// <param name="system">The identifier for the active system (or system group).</param>
    /// <param name="startStopwatchTimestamp">The start timestamp, which presumably originated from a previous get of <see cref="AmbientClock.Ticks"/>.</param>
    public CallContextActiveSystemData(string? system, long startStopwatchTimestamp)
    {
        RawGroup = system ?? "";
        StartStopwatchTimestamp = startStopwatchTimestamp;
    }
}

/// <summary>
/// A class that tracks service profile statistics across multiple call contexts in a process or a single time window.
/// </summary>
internal class ProcessOrSingleTimeWindowServiceProfiler : IAmbientServiceProfile, IAmbientServiceProfilerNotificationSink
{
    private readonly IAmbientServiceProfiler _profiler;
    private readonly Regex? _systemToGroupTransform;
    private readonly AsyncLocal<object> _callContextKey;
    private readonly ConcurrentDictionary<string, AmbientServiceProfilerAccumulator> _accumulatorsByGroup;
    private readonly ConcurrentDictionary<object, CallContextActiveSystemData> _activeGroupByCallContext;
    private bool _disposedValue;

    public string ScopeName { get; }

    public IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics => _accumulatorsByGroup.Values;

    public ProcessOrSingleTimeWindowServiceProfiler(IAmbientServiceProfiler metrics, string scopeName, Regex? systemGroupTransform)
    {
        _profiler = metrics;
        ScopeName = scopeName;
        _systemToGroupTransform = systemGroupTransform;
        _accumulatorsByGroup = new ConcurrentDictionary<string, AmbientServiceProfilerAccumulator>();
        _activeGroupByCallContext = new ConcurrentDictionary<object, CallContextActiveSystemData>();
        _callContextKey = new AsyncLocal<object>();
        _profiler.RegisterSystemSwitchedNotificationSink(this);
    }
    internal static string GroupSystem(Regex? transform, string system)
    {
        if (transform == null) return system;
        Match match = transform.Match(system);
        StringBuilder group = new();
        GroupCollection groups = match.Groups;
        for (int groupNumber = 1; groupNumber < groups.Count; ++groupNumber)
        {
            Group matchGroup = groups[groupNumber];
            if (!matchGroup.Success) continue;
            group.Append(matchGroup.Value);
        }
        return group.ToString();
    }
    internal CallContextActiveSystemData GetActiveSystemData(object scopeKey)
    {
        CallContextActiveSystemData ret;
        _activeGroupByCallContext.TryGetValue(scopeKey, out ret);
        return ret;
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
        // assign a call context key for the current call context if we haven't assigned one yet
        if (_callContextKey.Value == null) _callContextKey.Value = new object();
        // are we revising the old system?
        string justEndedGroup = (revisedOldSystem == null) ? GetActiveSystemData(_callContextKey.Value).Group : GroupSystem(_systemToGroupTransform, revisedOldSystem);
        // add the just ended group stats to the group accumulators
        _accumulatorsByGroup.AddOrUpdate(justEndedGroup,
            s => new AmbientServiceProfilerAccumulator(justEndedGroup, newSystemStartStopwatchTimestamp - oldSystemStartStopwatchTimestamp),
            (s, old) => new AmbientServiceProfilerAccumulator(justEndedGroup, old.TotalStopwatchTicksUsed + newSystemStartStopwatchTimestamp - oldSystemStartStopwatchTimestamp, old.ExecutionCount + 1)
        );
        string newGroup = GroupSystem(_systemToGroupTransform, newSystem);
        // keep track of what is active on this call context as well so we can count partial results
        _activeGroupByCallContext[_callContextKey.Value] = new CallContextActiveSystemData(newGroup, newSystemStartStopwatchTimestamp);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _profiler.DeregisterSystemSwitchedNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ProcessingDistributionAccumulator()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void CloseSampling()
    {
        _profiler.DeregisterSystemSwitchedNotificationSink(this);
        long endStopwatchTimestamp = AmbientClock.Ticks;
        foreach (CallContextActiveSystemData activeCallContextSystems in _activeGroupByCallContext.Values)
        {
            // now that we have an end, add the time spent on the previous system to the collection
            string justEndedGroup = activeCallContextSystems.Group;
            _accumulatorsByGroup.AddOrUpdate(justEndedGroup,
                s => new AmbientServiceProfilerAccumulator(justEndedGroup, endStopwatchTimestamp - activeCallContextSystems.StartStopwatchTimestamp),
                (s, old) => new AmbientServiceProfilerAccumulator(justEndedGroup, old.TotalStopwatchTicksUsed + endStopwatchTimestamp - activeCallContextSystems.StartStopwatchTimestamp, old.ExecutionCount + 1)
            );
        }
        _activeGroupByCallContext.Clear();
    }
}

internal class ScopeOnSystemSwitchedDistributor : IAmbientServiceProfilerNotificationSink
{
    private readonly ConcurrentHashSet<IAmbientServiceProfilerNotificationSink> _notificationSinks = new();
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
        foreach (IAmbientServiceProfilerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnSystemSwitched(newSystemStartStopwatchTimestamp, newSystem, oldSystemStartStopwatchTimestamp, revisedOldSystem);
        }
    }

    public bool RegisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}

/// <summary>
/// A class that tracks service profile statistics for a specific call context.
/// </summary>
internal class CallContextServiceProfiler : IAmbientServiceProfile, IAmbientServiceProfilerNotificationSink
{
    private readonly ScopeOnSystemSwitchedDistributor _distributor;
    private readonly Regex? _systemGroupTransform;
    private readonly Dictionary<string, ValueTuple<long, long>> _stopwatchTicksUsedByGroup;
    private string _currentGroup;
    private long _currentGroupStartStopwatchTicks;
    private bool _disposedValue;

    public string ScopeName { get; }

    public IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics
    {
        get
        {
            bool skipCurrent = false;
            long currentTicks = AmbientClock.Ticks - _currentGroupStartStopwatchTicks;
            foreach (AmbientServiceProfilerAccumulator accumulator in _stopwatchTicksUsedByGroup.Select(kvp => new AmbientServiceProfilerAccumulator(kvp.Key, kvp.Value.Item1, kvp.Value.Item2)))
            {
                // is this accumulator the same as the current one?
                if (string.Equals(accumulator.Group, _currentGroup, StringComparison.Ordinal))
                {
                    skipCurrent = true;
                    yield return new AmbientServiceProfilerAccumulator(accumulator.Group, accumulator.TotalStopwatchTicksUsed + currentTicks, accumulator.ExecutionCount + 1);
                }
                else
                {
                    yield return accumulator;
                }
            }
            if (!skipCurrent) yield return new AmbientServiceProfilerAccumulator(_currentGroup, currentTicks);
        }
    }

    /// <summary>
    /// Constructs a CallContextServiceProfiler.
    /// </summary>
    /// <param name="distributor">A <see cref="ScopeOnSystemSwitchedDistributor"/> to hook into to receive system change events.</param>
    /// <param name="scopeName">The name of the call contxt being tracked.</param>
    /// <param name="systemGroupTransform">A <see cref="Regex"/> string to transform the procesor into a system group.</param>
    /// <param name="startSystem">The optional starting system.</param>
    public CallContextServiceProfiler(ScopeOnSystemSwitchedDistributor distributor, string scopeName, Regex? systemGroupTransform, string startSystem = "")
    {
        _distributor = distributor;
        _systemGroupTransform = systemGroupTransform;
        ScopeName = scopeName;
        _stopwatchTicksUsedByGroup = new Dictionary<string, (long, long)>();
        _currentGroup = ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, startSystem);
        _currentGroupStartStopwatchTicks = AmbientClock.Ticks;
        distributor.RegisterSystemSwitchedNotificationSink(this);
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
        string? justEndedGroup = (revisedOldSystem == null)
            ? _currentGroup
            : ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, revisedOldSystem);
        // update the just ended group
        ValueTuple<long, long> values;
        if (!_stopwatchTicksUsedByGroup.TryGetValue(justEndedGroup, out values))
        {
            _stopwatchTicksUsedByGroup.Add(justEndedGroup, (newSystemStartStopwatchTimestamp - oldSystemStartStopwatchTimestamp, 1));
        }
        else
        {
            _stopwatchTicksUsedByGroup[justEndedGroup] = (values.Item1 + newSystemStartStopwatchTimestamp - oldSystemStartStopwatchTimestamp, values.Item2 + 1);
        }
        string? newGroup = ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, newSystem);
        // switch to the new processor
        _currentGroup = newGroup;
        _currentGroupStartStopwatchTicks = newSystemStartStopwatchTimestamp;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _distributor.DeregisterSystemSwitchedNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~CallContextServiceProfiler()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A class that tracks service profile statistics for a moving time window.
/// </summary>
internal class TimeWindowServiceProfiler : IDisposable
{
    private readonly string _scopeNamePrefix;
    private readonly AmbientEventTimer _timeWindowRotator;
    private ProcessOrSingleTimeWindowServiceProfiler? _timeWindowCallContextCollector;  // interlocked
    private bool _disposedValue;

    /// <summary>
    /// Constructs a TimeWindowProcessingDistributionTracker.
    /// </summary>
    /// <param name="metrics">A <see cref="IAmbientServiceProfiler"/> to hook into to receive processor change events.</param>
    /// <param name="scopeNamePrefix">A <see cref="TimeSpan"/> indicating the size of the window.</param>
    /// <param name="windowPeriod">A <see cref="TimeSpan"/> indicating how often reports are desired.</param>
    /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientServiceProfile"/> at the end of each time window.</param>
    /// <param name="systemGroupTransform">A <see cref="Regex"/> string to transform the system into a system group.</param>
    public TimeWindowServiceProfiler(IAmbientServiceProfiler metrics, string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientServiceProfile, Task> onWindowComplete, Regex? systemGroupTransform)
    {
        if (onWindowComplete == null) throw new ArgumentNullException(nameof(onWindowComplete), "Time Window Collection is pointless without a completion delegate!");
        _scopeNamePrefix = scopeNamePrefix;
        using (Rotate(metrics, windowPeriod, systemGroupTransform)) { }
        _timeWindowRotator = new AmbientEventTimer(windowPeriod);
        _timeWindowRotator.Elapsed +=
            (sender, handler) =>
            {
                ProcessOrSingleTimeWindowServiceProfiler? oldAccumulator = Rotate(metrics, windowPeriod, systemGroupTransform);
                if (oldAccumulator != null)
                {
                    Task t = onWindowComplete(oldAccumulator);
                    t.Wait();
                }
            };
        _timeWindowRotator.AutoReset = true;
        _timeWindowRotator.Enabled = true;
    }

    private ProcessOrSingleTimeWindowServiceProfiler? Rotate(IAmbientServiceProfiler metrics, TimeSpan windowPeriod, Regex? systemGroupTransform)
    {
        string windowName = WindowScope.WindowId(AmbientClock.UtcNow, windowPeriod);
        string newAccumulatorScopeName = _scopeNamePrefix + windowName + "(" + WindowScope.WindowSize(windowPeriod) + ")"; ;
        ProcessOrSingleTimeWindowServiceProfiler newAccumulator = new(metrics, newAccumulatorScopeName, systemGroupTransform);
        ProcessOrSingleTimeWindowServiceProfiler? oldAccumulator = Interlocked.Exchange(ref _timeWindowCallContextCollector, newAccumulator);
        if (oldAccumulator != null)
        {
            // close out the old accumulator
            oldAccumulator.CloseSampling();
        }
        return oldAccumulator;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _timeWindowRotator.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ScopeProcessingDistributionTracker()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
