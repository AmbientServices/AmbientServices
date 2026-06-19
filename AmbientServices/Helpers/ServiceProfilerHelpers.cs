using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A class that coordinates service profilers.
/// </summary>
/// <remarks>
/// <pitch>
/// The factory you use to turn the raw <see cref="IAmbientServiceProfiler"/> switch stream into actual profiles.  It builds three flavors of <see cref="IAmbientServiceProfile"/> — one scoped to the current call context (per request), one that rotates on a time window (per-window reporting), and one for the whole process — and applies the configurable system-to-group transform so related systems collapse into reportable groups.
/// </pitch>
/// <pledge><see cref="IAmbientServiceProfilerNotificationSink"/></pledge>
/// <pledge>
/// Returns null from every factory method when there is no ambient <see cref="IAmbientServiceProfiler"/> to observe.  Each returned profile must be disposed to stop collecting; the call-context and process profiles are not thread-safe to read.
/// The system-to-group transform is a <see cref="Regex"/> whose successful capture groups are concatenated to form the reported group; a null/empty transform passes the system identifier through unchanged.  An explicit transform argument overrides the ambient settings default.
/// </pledge>
/// <plan>
/// Registers itself as a sink on the ambient <see cref="IAmbientServiceProfiler"/> and fans switch events out through a call-context-scoped <c>ScopeOnSystemSwitchedDistributor</c> (held in an <see cref="System.Threading.AsyncLocal{T}"/>) so that a call-context profile only observes the subtree of contexts that descend from where it was created, while time-window and process profiles observe every context.  The group transform <see cref="Regex"/> is compiled once (<see cref="RegexOptions.Compiled"/>) per profile.  Wall-clock-vs-aggregate computation lives in the collectors the factory methods construct, not here.
/// </plan>
/// </remarks>
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
        _scopeDistributor.Value ??= new ScopeOnSystemSwitchedDistributor();
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
            _scopeDistributor.Value ??= new ScopeOnSystemSwitchedDistributor();
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
/// An interface that abstracts an ambient service profile: the per-system breakdown of time spent within a profiled scope.
/// </summary>
/// <remarks>
/// <pitch>
/// The read side of profiling.  Hand it a scope (a call context, a time window, or a whole process) and it tells you, per system or system group, how much time that scope spent in each — both as aggregate busy time and as wall-clock occupancy, so you can see backend resource cost and request latency from the same sample.
/// </pitch>
/// <pledge>
/// Each system or system group active during the scope appears as exactly one <see cref="AmbientServiceProfilerAccumulator"/>; the empty/null group represents <em>unattributed</em> wall-clock time (time not inside any <see cref="IAmbientServiceProfiler.SwitchSystem"/> scope, which mixes on-CPU work and idle waiting — the profile does not distinguish or measure CPU time).
/// For each group the profile reports an aggregate measure (the sum of that group's active intervals across every call context in scope, counting concurrent use multiply) and a wall-clock measure (the union of those intervals, counting concurrent use once); the two are equal for serial scopes and the aggregate is never smaller.
/// A scope that spans multiple call contexts (a process, a time window, or an operation and the contexts it forks) merges intervals across all of them.
/// Reading <see cref="ProfilerStatistics"/> is a snapshot and may be read more than once; whether the currently-executing (not-yet-ended) systems are included in that snapshot is realization-specific.
/// </pledge>
/// </remarks>
public interface IAmbientServiceProfile : IDisposable
{
    /// <summary>
    /// Gets the name of the scope being analyzed.  The scope identifies the scope of the operations that were profiled.
    /// </summary>
    string ScopeName { get; }
    /// <summary>
    /// Gets an enumeration of <see cref="AmbientServiceProfilerAccumulator"/> instances indicating the time spent executing in each of the systems in the associated scope, with both aggregate and wall-clock measures.
    /// </summary>
    IEnumerable<AmbientServiceProfilerAccumulator> ProfilerStatistics { get; }
}
/// <summary>
/// A class that accumulates processing count and both time measures for a specific system or system group.  Immutable and thread-safe.
/// </summary>
/// <remarks>
/// <pitch>
/// The per-group row reported by an <see cref="IAmbientServiceProfile"/>.  Carries two distinct time measures so a caller can tell resource cost apart from latency:
/// <see cref="TotalStopwatchTicksUsed"/> (aggregate busy time, parallel use counted multiply) and <see cref="WallClockStopwatchTicksUsed"/> (wall-clock occupancy, parallel use counted once).
/// </pitch>
/// <pledge>
/// A pure data carrier; it performs no measurement of its own and simply reports what a collector computed.
/// The two measures obey <c>WallClockStopwatchTicksUsed</c> &lt;= <c>TotalStopwatchTicksUsed</c> for any valid set of intervals: they are equal when the group's executions never overlapped in time (the serial case) and diverge in proportion to how much concurrent use overlapped.
/// Their difference (<see cref="TotalStopwatchTicksUsed"/> - <see cref="WallClockStopwatchTicksUsed"/>) is the time saved by concurrency; combined with <see cref="ExecutionCount"/> it lets a consumer distinguish sequential repeats (which add into both measures) from parallel overlap (which adds into the aggregate but collapses in the wall-clock measure) without any per-execution flag.
/// Both measures are reported in stopwatch ticks; the <c>TimeUsed</c>/<c>WallClockTimeUsed</c> properties convert to <see cref="TimeSpan"/>.
/// </pledge>
/// </remarks>
public class AmbientServiceProfilerAccumulator
{

    /// <summary>
    /// Gets the group the accumulator is for.
    /// </summary>
    public string Group { get; }
    /// <summary>
    /// Gest the number of times systems in this group were executed.
    /// </summary>
    public long ExecutionCount { get; }
    /// <summary>
    /// Gets the aggregate (busy) number of stopwatch ticks used by this system group, summing every execution's interval even when executions ran concurrently (so concurrent use is counted multiply).
    /// This is the resource-cost measure.
    /// </summary>
    public long TotalStopwatchTicksUsed { get; }
    /// <summary>
    /// Gets the wall-clock number of stopwatch ticks during which this system group was in use, counting time when two or more of its executions overlapped only once (the union of the executions' intervals).
    /// This is the latency-contribution measure.  Equals <see cref="TotalStopwatchTicksUsed"/> when no executions of this group overlapped.
    /// </summary>
    public long WallClockStopwatchTicksUsed { get; }
    /// <summary>
    /// Gets the aggregate (busy) amount of time used by this system group.  See <see cref="TotalStopwatchTicksUsed"/>.
    /// </summary>
    public TimeSpan TimeUsed => new(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TotalStopwatchTicksUsed));
    /// <summary>
    /// Gets the wall-clock amount of time during which this system group was in use.  See <see cref="WallClockStopwatchTicksUsed"/>.
    /// </summary>
    public TimeSpan WallClockTimeUsed => new(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(WallClockStopwatchTicksUsed));

    /// <summary>
    /// Constructs an AmbientServiceProfilerAccumulator for the specified system, treating it as a serial group whose wall-clock time equals its aggregate time.
    /// </summary>
    /// <param name="group">The system.</param>
    /// <param name="totalStopwatchTicksUsed">The number of stopwatch ticks used by this system.</param>
    /// <param name="executionCount">The initial execution count.  Defaults to one.</param>
    /// <remarks>This overload assumes no concurrent use within the group; use the overload that takes a separate wall-clock value when executions may have overlapped.</remarks>
    public AmbientServiceProfilerAccumulator(string group, long totalStopwatchTicksUsed, long executionCount = 1)
        : this(group, totalStopwatchTicksUsed, totalStopwatchTicksUsed, executionCount)
    {
    }
    /// <summary>
    /// Constructs an AmbientServiceProfilerAccumulator for the specified system with explicit aggregate and wall-clock measures.
    /// </summary>
    /// <param name="group">The system.</param>
    /// <param name="totalStopwatchTicksUsed">The aggregate (busy) number of stopwatch ticks used by this system, with concurrent use counted multiply.</param>
    /// <param name="wallClockStopwatchTicksUsed">The wall-clock number of stopwatch ticks during which this system was in use, with concurrent use counted once.  Must not exceed <paramref name="totalStopwatchTicksUsed"/>.</param>
    /// <param name="executionCount">The execution count.</param>
    public AmbientServiceProfilerAccumulator(string group, long totalStopwatchTicksUsed, long wallClockStopwatchTicksUsed, long executionCount)
    {
        Group = group;
        ExecutionCount = executionCount;
        TotalStopwatchTicksUsed = totalStopwatchTicksUsed;
        WallClockStopwatchTicksUsed = wallClockStopwatchTicksUsed;
    }
}
