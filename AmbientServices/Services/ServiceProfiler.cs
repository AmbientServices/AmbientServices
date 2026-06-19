
using System;

namespace AmbientServices;

/// <summary>
/// An interface that abstracts a service profiler notification sink.
/// </summary>
/// <remarks>
/// <pitch>The push side of profiling: implement this to receive every system switch as it happens and accumulate it however you like.  Collectors that build an <see cref="IAmbientServiceProfile"/> are the canonical implementers.</pitch>
/// <pledge>
/// <see cref="OnSystemSwitched"/> is called once per switch, after the switch has been applied.  Each call delivers a complete, self-contained interval: the just-ended system ran from <c>oldSystemStartStopwatchTimestamp</c> to <c>newSystemStartStopwatchTimestamp</c>, so a sink can reconstruct intervals without retaining prior per-context state.
/// Calls may arrive concurrently from multiple call contexts and are not guaranteed to be ordered by timestamp across contexts; implementations must be thread-safe and must not assume arrival order matches chronological order.
/// </pledge>
/// </remarks>
public interface IAmbientServiceProfilerNotificationSink
{
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
    void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string? revisedOldSystem = null);
}
/// <summary>
/// An interface that abstracts a service profiler service.
/// </summary>
/// <remarks>
/// <pitch>
/// Cheap, always-on attribution of where a request's time goes across backend systems (databases, caches, remote services, CPU, etc.).  Callers mark which system is executing as they enter and leave it; a paired <see cref="IAmbientServiceProfile"/> turns those marks into a per-system time breakdown.
/// It deliberately does <em>not</em> measure CPU time, allocations, or call counts at finer than system-switch granularity, and it cannot mark two systems active at once within a single call context — that is what its limited, low-overhead model buys.
/// </pitch>
/// <pledge>
/// Within a single call context exactly one system is active at a time: <see cref="SwitchSystem"/> ends the currently-active system and begins the named one, attributing the elapsed interval to the system that just ended; the null or empty system denotes the default (unattributed) system.  Switching is mutually exclusive by construction — there is no enter/exit nesting and no way to make two systems simultaneously active in one context.
/// Concurrency is expressed only by multiple call contexts, each internally sequential; an <see cref="System.Threading.AsyncLocal{T}"/>-style flow carries the active-system state into forked contexts.  The just-ended system may be retroactively renamed via <c>updatedPreviousSystem</c> (for example to fold in a success/failure outcome only known on completion).
/// Registered <see cref="IAmbientServiceProfilerNotificationSink"/> instances are notified on every switch with the start timestamps of both the ending and beginning systems, which fully determine each completed interval.
/// </pledge>
/// </remarks>
public interface IAmbientServiceProfiler
{
    /// <summary>
    /// Switches the system that is executing in this call context.
    /// </summary>
    /// <param name="system">A string indicating which system is beginning to execute, or null or empty string to indicate that the default system (ie. CPU) is executing.</param>
    /// <param name="updatedPreviousSystem">An optional updated for the previous system in case part of the system identifier couldn't be determined until the execution completed.  For example, if the operation failed, we can retroactively reclassify the time spent in order to properly separately track time spent on successful, timed-out, and failed operations.</param>
    /// <remarks>
    /// The system should be identified using the following form:
    /// MainSystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem...
    /// For example:
    /// DynamoDB/Table:My-table/Partition:342644/Result:Success
    /// S3/Bucket:My-bucket/Prefix:abcdefg/Result:Retry
    /// SQL/Database:My-database/Table:User/Result:Failed
    /// In the analysis pipeline, systems are grouped using the system group transform Regex from the settings.
    /// Any matching Regex match groups found by the regex expression will be concatenated into the transformed string.
    /// To pass the system string through unaltered (as its own group), use null, empty string, or .* as the system group transform string.
    /// For example, to transform the group systems by only the main system, database, bucket, and result, while retaining the prefixes, use the following Regex:
    /// (?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)
    /// For example, to transform the group systems by only the main system, database, bucket, and result, without retaining the prefixes, use the following Regex:
    /// (?:([^:/]+)(?:(?:(/)(?:Database:)([^:/]*))|(?:(/)(?:Bucket:)([^:/]*))|(?:(/)(?:Result:)([^:/]*))|(?:/[^/]*))*)
    /// </remarks>
    void SwitchSystem(string? system, string? updatedPreviousSystem = null);
    /// <summary>
    /// Registers a system switch notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientServiceProfilerNotificationSink"/> that will receive notifications when the system is switched.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    bool RegisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink);
    /// <summary>
    /// Deregisters an access notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientServiceProfilerNotificationSink"/> that will receive notifications when the system is switched.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    bool DeregisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink);
#if NET5_0_OR_GREATER
    /// <summary>
    /// Scopes a switch to a specified system to simplify the syntax of tracking system usage when the full system identifier is known up-front.
    /// </summary>
    /// <param name="system">A string indicating which system is beginning to execute, or null or empty string to indicate that the default system (ie. CPU) is executing.</param>
    /// <param name="updatedPreviousSystem">An optional updated for the previous system in case part of the system identifier couldn't be determined until the execution completed.  For example, if the operation failed, we can retroactively reclassify the time spent in order to properly separately track time spent on successful, timed-out, and failed operations.</param>
    /// <returns>A <see cref="ScopedSystemSwitch"/> that should be disposed when usage of the system is finished.</returns>
    ScopedSystemSwitch ScopedSystemSwitch(string? system, string? updatedPreviousSystem = null)
    {
        return new ScopedSystemSwitch(this, system, updatedPreviousSystem);
    }
#endif
}
/// <summary>
/// A disposable scoping class that lets the caller indicate when system usage is complete.
/// </summary>
/// <remarks>
/// <pitch>The ergonomic way to mark a system active for the duration of a <c>using</c> block when the full system identifier is known up front — switch on construction, switch back to the default system on dispose.</pitch>
/// <pledge>Construction calls <see cref="IAmbientServiceProfiler.SwitchSystem"/> with the given system; disposal switches back to the default (null) system.  Because the underlying model is mutually exclusive per call context, these scopes are meant to be used one-deep per await within a context, not opened for two different systems concurrently in the same context.</pledge>
/// <plan>A thin wrapper that holds the <see cref="IAmbientServiceProfiler"/> and calls <c>SwitchSystem</c> on construction and disposal; it stores no timing state of its own.</plan>
/// </remarks>
public sealed class ScopedSystemSwitch : IDisposable
{
    private readonly IAmbientServiceProfiler _profiler;

    /// <summary>
    /// Constructs a scoped system switcher.
    /// </summary>
    /// <param name="profiler">The <see cref="IAmbientServiceProfiler"/> to use.</param>
    /// <param name="system">The name of the system that will execute during the scope.</param>
    /// <param name="updatedPreviousSystem">An update to the previously-running system in case the full name was not known at the beginning of its execution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profiler"/> is null.</exception>
    public ScopedSystemSwitch(IAmbientServiceProfiler profiler, string? system, string? updatedPreviousSystem = null)
    {
        if (profiler == null) throw new ArgumentNullException(nameof(profiler));
        _profiler = profiler;
        profiler.SwitchSystem(system, updatedPreviousSystem);
    }
    /// <summary>
    /// Disposes of the instance.
    /// </summary>
    public void Dispose()
    {
        _profiler.SwitchSystem(null);
    }
}
