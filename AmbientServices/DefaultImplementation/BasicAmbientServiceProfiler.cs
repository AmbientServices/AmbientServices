using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic default implementation of <see cref="IAmbientServiceProfiler"/> that tracks the active system per call context and broadcasts every switch to registered sinks.
/// </summary>
/// <remarks>
/// <pitch>The zero-configuration, in-process profiler used unless overridden.  It adds only a single <see cref="AsyncLocal{T}"/> read/write and a sink fan-out per switch, so it is cheap enough to leave on in production.</pitch>
/// <pledge><see cref="IAmbientServiceProfiler"/></pledge>
/// <plan>
/// Holds the currently-active system for each call context in an <see cref="AsyncLocal{T}"/> of <see cref="CallContextActiveSystemData"/> (a struct, so a fresh context starts at the default/unattributed system as of the first switch).  On <see cref="SwitchSystem"/> it stamps the new system's start with <see cref="AmbientClock.Ticks"/>, replaces the active value, then synchronously notifies every registered <see cref="IAmbientServiceProfilerNotificationSink"/> with both the new and old start timestamps so each notification is a self-contained completed interval.  Sinks are held in a <see cref="ConcurrentHashSet{T}"/>; registration is idempotent.  No time math or grouping happens here — collectors do that from the broadcast intervals.
/// </plan>
/// </remarks>
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
            notificationSink.OnSystemSwitched(newSystem.StartStopwatchTimestamp, newSystem.Group, oldSystem.StartStopwatchTimestamp, oldSystem.Group, updatedPreviousSystem);
        }
    }
    public void ResetForkedCallContext()
    {
        // Re-stamp the active system to the default, started now, with NO sink notification.  Not notifying is the whole
        // point: it discards the inherited [parentStart, now) span (the parent keeps its own copy and records that span
        // itself) instead of duplicating it onto every fork, which is what inflates the default group's aggregate time.
        _activeSystem.Value = new CallContextActiveSystemData(null, AmbientClock.Ticks);
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
/// A half-open stopwatch-tick interval [<see cref="Start"/>, <see cref="End"/>) during which one system or system group was active in one call context.
/// </summary>
internal readonly struct StopwatchInterval
{
    /// <summary>Gets the inclusive start stopwatch timestamp.</summary>
    public long Start { get; }
    /// <summary>Gets the exclusive end stopwatch timestamp.</summary>
    public long End { get; }
    /// <summary>Gets the length of the interval in stopwatch ticks (never negative for a valid interval).</summary>
    public long Length => End - Start;
    /// <summary>Constructs a StopwatchInterval.</summary>
    public StopwatchInterval(long start, long end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// A thread-safe collector that turns the self-contained switch intervals broadcast by an <see cref="IAmbientServiceProfiler"/> into per-group aggregate (busy) and critical-path (union) statistics.
/// </summary>
/// <remarks>
/// <plan>
/// Stores every completed interval per group in a <see cref="ConcurrentQueue{T}"/> keyed by group, plus the currently-active system per call context (keyed by an opaque context marker) so in-flight time can be attributed at read time or finalized at scope close.
/// Completed intervals are reconstructed purely from each switch event's old/new start timestamps and the ended-system identity delivered with the event, so both their extent and their attribution are correct regardless of cross-context arrival order, and even when parallel children share an inherited context marker.  Only in-flight attribution still depends on the per-context active map, which remains best-effort under such shared markers (a still-running child may be mis-snapshotted at read time, but is recorded correctly once it switches or the scope finalizes).
/// At report time it copies each group's intervals into a list, optionally appends in-flight intervals ending at "now", and computes the aggregate as the simple sum of interval lengths (concurrent use counted multiply) and the critical-path measure as the union via an order-independent sweep-line merge (concurrent use counted once).  Memory is proportional to the number of completed intervals, which is bounded for call-context and time-window scopes but grows for a whole-process scope.
/// </plan>
/// </remarks>
internal sealed class ServiceProfileSampleCollector
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<StopwatchInterval>> _intervalsByGroup = new();
    private readonly ConcurrentDictionary<object, CallContextActiveSystemData> _activeByContext = new();

    /// <summary>
    /// Records a completed interval for the system just ended in the specified call context and marks the new system active.
    /// </summary>
    /// <param name="contextKey">An opaque marker identifying the call context the switch happened in.</param>
    /// <param name="oldStart">The stopwatch timestamp when the just-ended system became active.</param>
    /// <param name="newStart">The stopwatch timestamp when the new system became active (the end of the just-ended interval).</param>
    /// <param name="newGroup">The (already group-transformed) system that is now active.</param>
    /// <param name="endedGroup">The (already group-transformed) system that just ended, as known to the call context that ran it.  Delivered with the event so concurrent forked contexts are attributed correctly even when they share an inherited <paramref name="contextKey"/>.</param>
    /// <param name="revisedEndedGroup">An optional (already group-transformed) replacement identity for the just-ended system that overrides <paramref name="endedGroup"/>, or null to use <paramref name="endedGroup"/>.</param>
    public void RecordSwitch(object contextKey, long oldStart, long newStart, string newGroup, string endedGroup, string? revisedEndedGroup)
    {
        string justEndedGroup = revisedEndedGroup ?? endedGroup;
        AddInterval(justEndedGroup, oldStart, newStart);
        _activeByContext[contextKey] = new CallContextActiveSystemData(newGroup, newStart);
    }
    /// <summary>
    /// Seeds the system that is considered active for the specified call context before any switch has occurred (used so a freshly-created scope reports its starting system as in-flight).
    /// </summary>
    public void SeedActive(object contextKey, string group, long startStopwatchTimestamp)
    {
        _activeByContext[contextKey] = new CallContextActiveSystemData(group, startStopwatchTimestamp);
    }
    /// <summary>
    /// Converts every currently-active (in-flight) system into a completed interval ending at the specified timestamp, then clears the active set.  Used when a sampling scope closes.
    /// </summary>
    public void FinalizeActive(long endStopwatchTimestamp)
    {
        foreach (KeyValuePair<object, CallContextActiveSystemData> kvp in _activeByContext)
        {
            AddInterval(kvp.Value.Group, kvp.Value.StartStopwatchTimestamp, endStopwatchTimestamp);
        }
        _activeByContext.Clear();
    }
    /// <summary>
    /// Produces a per-group statistics snapshot.
    /// </summary>
    /// <param name="includeActive">Whether to include currently-active (not-yet-ended) systems as intervals ending at <paramref name="nowStopwatchTimestamp"/>.</param>
    /// <param name="nowStopwatchTimestamp">The timestamp to use as the end of in-flight intervals.</param>
    public IEnumerable<AmbientServiceProfilerAccumulator> GetStatistics(bool includeActive, long nowStopwatchTimestamp)
    {
        Dictionary<string, List<StopwatchInterval>> byGroup = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, ConcurrentQueue<StopwatchInterval>> kvp in _intervalsByGroup)
        {
            byGroup[kvp.Key] = new List<StopwatchInterval>(kvp.Value.ToArray());
        }
        if (includeActive)
        {
            foreach (KeyValuePair<object, CallContextActiveSystemData> kvp in _activeByContext)
            {
                if (!byGroup.TryGetValue(kvp.Value.Group, out List<StopwatchInterval>? list))
                {
                    list = new List<StopwatchInterval>();
                    byGroup[kvp.Value.Group] = list;
                }
                list.Add(new StopwatchInterval(kvp.Value.StartStopwatchTimestamp, nowStopwatchTimestamp));
            }
        }
        foreach (KeyValuePair<string, List<StopwatchInterval>> kvp in byGroup)
        {
            long total = 0;
            foreach (StopwatchInterval interval in kvp.Value) total += interval.Length;
            yield return new AmbientServiceProfilerAccumulator(kvp.Key, total, UnionStopwatchTicks(kvp.Value), kvp.Value.Count);
        }
    }
    private void AddInterval(string group, long start, long end)
    {
        _intervalsByGroup.GetOrAdd(group, _ => new ConcurrentQueue<StopwatchInterval>()).Enqueue(new StopwatchInterval(start, end));
    }
    /// <summary>
    /// Computes the total length of the union of the specified intervals (overlapping intervals counted once) using an order-independent sweep-line merge.  The list is sorted in place.
    /// </summary>
    internal static long UnionStopwatchTicks(List<StopwatchInterval> intervals)
    {
        if (intervals.Count == 0) return 0;
        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        long union = 0;
        long mergedStart = intervals[0].Start;
        long mergedEnd = intervals[0].End;
        for (int i = 1; i < intervals.Count; ++i)
        {
            StopwatchInterval interval = intervals[i];
            if (interval.Start > mergedEnd)
            {
                // disjoint (sequential) from the current merged interval: close it out and start a new one
                union += mergedEnd - mergedStart;
                mergedStart = interval.Start;
                mergedEnd = interval.End;
            }
            else if (interval.End > mergedEnd)
            {
                // overlapping (concurrent): extend the current merged interval
                mergedEnd = interval.End;
            }
        }
        union += mergedEnd - mergedStart;
        return union;
    }
}

/// <summary>
/// A class that tracks service profile statistics across multiple call contexts in a process or a single time window.
/// </summary>
/// <remarks>
/// <pitch>The process-wide / time-window view: every call context's switches roll into one breakdown, so concurrent backend use across the whole process shows up as aggregate &gt; critical-path.</pitch>
/// <pledge><see cref="IAmbientServiceProfile"/></pledge>
/// <pledge>Live reads of <see cref="ProfilerStatistics"/> include only completed intervals; in-flight systems are folded in only when <see cref="CloseSampling"/> is called (which the time-window rotator does on each window).</pledge>
/// <plan>Subscribes to the whole-process <see cref="IAmbientServiceProfiler"/> and delegates accumulation to a <see cref="ServiceProfileSampleCollector"/>, tagging each context with an <see cref="AsyncLocal{T}"/> marker so in-flight time can be attributed per context.</plan>
/// </remarks>
internal class ProcessOrSingleTimeWindowServiceProfiler : IAmbientServiceProfile, IAmbientServiceProfilerNotificationSink, IDisposable
{
    private readonly IAmbientServiceProfiler _profiler;
    private readonly Regex? _systemToGroupTransform;
    private readonly AsyncLocal<object> _callContextKey;
    private readonly ServiceProfileSampleCollector _collector;
    private bool _disposedValue;

    public string ScopeName { get; }

    public IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics => _collector.GetStatistics(false, AmbientClock.Ticks);

    public ProcessOrSingleTimeWindowServiceProfiler(IAmbientServiceProfiler metrics, string scopeName, Regex? systemGroupTransform)
    {
        _profiler = metrics;
        ScopeName = scopeName;
        _systemToGroupTransform = systemGroupTransform;
        _collector = new ServiceProfileSampleCollector();
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
    /// <param name="oldSystem">The identifier for the system that has just finished running, as known to the call context that ran it; used to attribute the completed interval.</param>
    /// <param name="revisedOldSystem">An optional revised name for the system that has just finished running that overrides <paramref name="oldSystem"/>, or null if the identifier for the old system does not need revising.</param>
    public void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string oldSystem, string? revisedOldSystem = null)
    {
        // assign a call context key for the current call context if we haven't assigned one yet
        if (_callContextKey.Value == null) _callContextKey.Value = new object();
        string newGroup = GroupSystem(_systemToGroupTransform, newSystem);
        string endedGroup = GroupSystem(_systemToGroupTransform, oldSystem);
        string? revisedEndedGroup = (revisedOldSystem == null) ? null : GroupSystem(_systemToGroupTransform, revisedOldSystem);
        _collector.RecordSwitch(_callContextKey.Value, oldSystemStartStopwatchTimestamp, newSystemStartStopwatchTimestamp, newGroup, endedGroup, revisedEndedGroup);
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
        // now that we have an end, add the time spent on each still-active system to the collection
        _collector.FinalizeActive(AmbientClock.Ticks);
    }
}

/// <summary>
/// A class that distributes system switch notifications to the sinks scoped to a single call context subtree.
/// </summary>
/// <remarks>
/// <pitch>The per-call-context fan-out hub: an <see cref="AsyncLocal{T}"/>-held instance receives the whole process's switches but is only seen by collectors created within its call context subtree, so a call-context profile observes just its own work and the contexts it forks.</pitch>
/// <pledge><see cref="IAmbientServiceProfilerNotificationSink"/></pledge>
/// </remarks>
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
    /// <param name="oldSystem">The identifier for the system that has just finished running, as known to the call context that ran it; used to attribute the completed interval.</param>
    /// <param name="revisedOldSystem">An optional revised name for the system that has just finished running that overrides <paramref name="oldSystem"/>, or null if the identifier for the old system does not need revising.</param>
    public void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string oldSystem, string? revisedOldSystem = null)
    {
        foreach (IAmbientServiceProfilerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnSystemSwitched(newSystemStartStopwatchTimestamp, newSystem, oldSystemStartStopwatchTimestamp, oldSystem, revisedOldSystem);
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
/// A class that tracks service profile statistics for a specific call context (and the contexts it forks).
/// </summary>
/// <remarks>
/// <pitch>The per-request view: profiles one operation and the parallel contexts it spawns, so a request that fans work out concurrently shows aggregate backend time above its critical-path latency.</pitch>
/// <pledge><see cref="IAmbientServiceProfile"/></pledge>
/// <pledge>Reads of <see cref="ProfilerStatistics"/> include the currently-active (in-flight) system(s) as intervals ending "now", so the breakdown is meaningful before the scope ends.</pledge>
/// <plan>Subscribes to a call-context-scoped <see cref="ScopeOnSystemSwitchedDistributor"/> and delegates to a <see cref="ServiceProfileSampleCollector"/>.  Completed intervals are reconstructed from self-contained switch events that carry the ended system's identity, so they are attributed correctly under fork-based parallelism even though forked children share the creating context's inherited <see cref="AsyncLocal{T}"/> marker; that shared marker only limits the in-flight (not-yet-ended) snapshot.</plan>
/// </remarks>
internal class CallContextServiceProfiler : IAmbientServiceProfile, IAmbientServiceProfilerNotificationSink, IDisposable
{
    private readonly ScopeOnSystemSwitchedDistributor _distributor;
    private readonly Regex? _systemGroupTransform;
    private readonly ServiceProfileSampleCollector _collector;
    private readonly AsyncLocal<object> _callContextKey;
    private bool _disposedValue;

    public string ScopeName { get; }

    public IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics => _collector.GetStatistics(true, AmbientClock.Ticks);

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
        _collector = new ServiceProfileSampleCollector();
        _callContextKey = new AsyncLocal<object>();
        // seed the active system for the creating call context so a read before the first switch reports the starting system
        object contextKey = new();
        _callContextKey.Value = contextKey;
        _collector.SeedActive(contextKey, ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, startSystem), AmbientClock.Ticks);
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
    /// <param name="oldSystem">The identifier for the system that has just finished running, as known to the call context that ran it; used to attribute the completed interval.</param>
    /// <param name="revisedOldSystem">An optional revised name for the system that has just finished running that overrides <paramref name="oldSystem"/>, or null if the identifier for the old system does not need revising.</param>
    public void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string oldSystem, string? revisedOldSystem = null)
    {
        // assign a call context key for the current call context if we haven't assigned one yet (forked children may inherit the creating context's key)
        if (_callContextKey.Value == null) _callContextKey.Value = new object();
        string newGroup = ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, newSystem);
        string endedGroup = ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, oldSystem);
        string? revisedEndedGroup = (revisedOldSystem == null) ? null : ProcessOrSingleTimeWindowServiceProfiler.GroupSystem(_systemGroupTransform, revisedOldSystem);
        _collector.RecordSwitch(_callContextKey.Value, oldSystemStartStopwatchTimestamp, newSystemStartStopwatchTimestamp, newGroup, endedGroup, revisedEndedGroup);
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
/// <remarks>
/// <pitch>Continuous reporting: rotates a fresh <see cref="ProcessOrSingleTimeWindowServiceProfiler"/> every window period and hands the closed one to a completion delegate, so a long-running process emits a steady stream of per-window backend breakdowns.</pitch>
/// <plan>Drives rotation with an <see cref="AmbientEventTimer"/> on the window period; on each tick it atomically swaps in a new collector via <see cref="Interlocked.Exchange{T}(ref T, T)"/> and calls <c>CloseSampling</c> on the old one to fold in in-flight time before reporting.</plan>
/// </remarks>
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
            async (sender, handler) =>
            {
                ProcessOrSingleTimeWindowServiceProfiler? oldAccumulator = Rotate(metrics, windowPeriod, systemGroupTransform);
                if (oldAccumulator != null)
                {
                    await onWindowComplete(oldAccumulator);
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
        // close out the old accumulator
        oldAccumulator?.CloseSampling();
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
