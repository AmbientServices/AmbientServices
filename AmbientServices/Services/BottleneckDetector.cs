using AmbientServices.Utilities;
using System;

namespace AmbientServices;

/// <summary>
/// An interface that callers implement to receive bottleneck exit notifications.
/// </summary>
/// <remarks>
/// <pitch>The push side of bottleneck detection: implement this to receive every bottleneck access as it completes and accumulate it however you like.  Surveyors that build an <see cref="IAmbientBottleneckSurvey"/> are the canonical implementers.</pitch>
/// <pledge>
/// <see cref="BottleneckExited"/> is called once per access, after the access has ended, with the <see cref="AmbientBottleneckAccessor"/> carrying the bottleneck identity, the access time range, and the final usage and utilization numbers — everything needed to attribute and rank the access without retaining any per-access state.
/// Calls may arrive concurrently from any call context or thread; implementations must be thread-safe unless the registration source guarantees single-context delivery.
/// </pledge>
/// </remarks>
public interface IAmbientBottleneckExitNotificationSink
{
    /// <summary>
    /// Notification that the bottleneck was exited.
    /// </summary>
    /// <param name="bottleneckAccessor">The <see cref="AmbientBottleneckAccessor"/> representing the access.</param>
    void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor);
}
/// <summary>
/// An interface that callers implement to receive bottleneck entry notifications.
/// </summary>
/// <remarks>
/// <pitch>The optional companion to <see cref="IAmbientBottleneckExitNotificationSink"/> for sinks that need to see accesses as they begin (for example to track in-progress accesses in a time-windowed survey) rather than only after they finish.</pitch>
/// <pledge>
/// <see cref="BottleneckEntered"/> is called once per access, when the access begins; the delivered <see cref="AmbientBottleneckAccessor"/> is still in progress, so its end time, usage, and utilization are not yet final and will change as the access proceeds.
/// There is no separate registration for this interface: the detector notifies entries only to registered exit sinks that also implement this interface.
/// Calls may arrive concurrently from any call context or thread; implementations must be thread-safe.
/// </pledge>
/// </remarks>
public interface IAmbientBottleneckEnterNotificationSink
{
    /// <summary>
    /// Notification that the bottleneck was entered.
    /// </summary>
    /// <param name="bottleneckAccessor">The <see cref="AmbientBottleneckAccessor"/> representing the access.</param>
    void BottleneckEntered(AmbientBottleneckAccessor bottleneckAccessor);
}
/// <summary>
/// An interface that abstracts an ambient bottleneck detector.
/// </summary>
/// <remarks>
/// Each time a bottleneck is used, the caller informs this interface, telling it when the bottleneck is entered and exited, as well as how much usage has occurred.
/// A survey that ranks the bottlenecks by how close to (or how much over) their limits they are may then be accessed.
/// <pitch>
/// Cheap, always-on measurement of how close the system is to maxing out — saturation of known scalability limits (API rate limits, connection pools, disk queues, etc.) can be seen even before load testing.  Callers bracket each use of a potentially-limiting resource; surveyors turn those accesses into a ranked "which limit will we hit first" report.
/// It measures only what callers explicitly bracket, and only against limits the caller declares — it does not discover bottlenecks on its own.
/// </pitch>
/// <pledge>
/// <see cref="EnterBottleneck"/> begins an access to the specified bottleneck and returns the accessor whose disposal ends it; accesses may overlap freely within and across call contexts, and each access's usage is attributed per the owning <see cref="AmbientBottleneck"/>'s automatic-versus-manual accounting.
/// Registered <see cref="IAmbientBottleneckExitNotificationSink"/> instances are notified once per access when it ends; a registered sink that also implements <see cref="IAmbientBottleneckEnterNotificationSink"/> is additionally notified when each access begins.  The detector itself accumulates nothing — all aggregation and ranking happens in the sinks.
/// </pledge>
/// <priority>
/// 1. Always-on affordability over finding bottlenecks by itself: it measures only what callers explicitly bracket, and only against limits the caller declares.  A sibling that instrumented broadly and inferred limits would find bottlenecks nobody thought to declare, and is rejected because it could no longer be cheap enough to leave on — and a detector that gets turned off measures nothing at all. (public)
/// 2. Accumulating nothing here over sink simplicity: raw accesses are broadcast and every aggregation, windowing, and ranking decision belongs to the sinks, so two surveys may weigh the same accesses differently without this interface choosing for them.  The cost is that a sink can ask this interface nothing and must carry its own state. (public)
/// </priority>
/// </remarks>
public interface IAmbientBottleneckDetector
{
    /// <summary>
    /// Enters the specified bottleneck.
    /// </summary>
    /// <param name="bottleneck">The <see cref="AmbientBottleneck"/> for the bottleneck being accessed.</param>
    /// <returns>An <see cref="AmbientBottleneckAccessor"/> that should be disposed when exiting the bottleneck.</returns>
    AmbientBottleneckAccessor EnterBottleneck(AmbientBottleneck bottleneck);
    /// <summary>
    /// Registers an access notification sink with this ambient bottleneck detector.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientBottleneckExitNotificationSink"/> that will receive notifications when bottlenecks are entered.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    bool RegisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink);
    /// <summary>
    /// Deregisters an access notification sink with this ambient bottleneck detector.
    /// </summary>
    /// <param name="sink">The previously-registered <see cref="IAmbientBottleneckExitNotificationSink"/> that received notifications when bottlenecks were entered.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    bool DeregisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink);
}
/// <summary>
/// An enumeration of possible bottleneck types.
/// </summary>
public enum AmbientBottleneckUtilizationAlgorithm
{
    /// <summary>
    /// Utilization will always be zero, so this type of bottleneck will never rank above any bottlenecks with a non-zero utilization.
    /// </summary>
    Zero,
    /// <summary>
    /// Utilization will be computed as (<see cref="AmbientBottleneckAccessor.LimitUsed"/> / <see cref="AmbientBottleneckAccessor.AccessDurationStopwatchTicks"/>) / ((<see cref="AmbientBottleneck.Limit"/> ?? 1.0) / (<see cref="AmbientBottleneck.LimitPeriod"/>?.Ticks ?? 1.0)).
    /// </summary>
    Linear,
    /// <summary>
    /// There are no known fixed limits for this bottleneck, but it should be tracked anyway.
    /// Utilization will be computed as 1.0 - 1.0 / (1.0 + Linear Result * 2.0).
    /// This formula starts at zero, hits one half when the limit used hits half the limit (relative to the limit window), hits two thirds when the linear result would have returned one, and never returns a utilization of more than one.
    /// </summary>
    ExponentialLimitApproach,
}
/// <summary>
/// A class that tracks a single access to a bottleneck.
/// </summary>
/// <remarks>
/// The access may or may not be in-progress.
/// The access counts the same whether the operation being performed succeeds or not because the contention will be the same either way.
/// <pitch>The record of usage against one bottleneck — a single access (dispose it to end the access) or an aggregate of many — carrying the usage numbers and the computed <see cref="Utilization"/> by which surveys rank bottlenecks.</pitch>
/// <pledge>
/// For a bottleneck marked <see cref="AmbientBottleneck.Automatic"/>, usage accrues as elapsed stopwatch time and is finalized on disposal, and <see cref="SetUsage"/>/<see cref="AddUsage"/> throw; for a manual bottleneck the caller reports usage through those methods instead, and anything unreported records no usage.
/// <see cref="Utilization"/> is recomputed only at construction, on each usage report, and at disposal; instances sort by utilization, then by limit used, then by access count, so ordering reflects proximity to the limit rather than raw usage.
/// Property reads and usage updates are thread-safe; disposal ends the access exactly once and notifies the owning detector, which fans the completed access out to its exit sinks.
/// </pledge>
/// <plan>
/// Mutable state (end timestamp, access count, limit used, utilization) lives in fields updated with <see cref="System.Threading.Interlocked"/> (plus <see cref="AmbientServices.Utilities.InterlockedUtilities"/> optimistic adds for the double); an end timestamp of <see cref="long.MaxValue"/> marks an in-progress access, in which case duration is measured against <see cref="AmbientClock.Ticks"/> on read.
/// Utilization derives from the bottleneck's <see cref="AmbientBottleneckUtilizationAlgorithm"/>: Linear compares the rate of limit usage over the access duration against the declared limit per limit-period; ExponentialLimitApproach maps the same rate through an asymptotic curve that never exceeds one, for bottlenecks with no known hard limit.
/// The internal <c>Split</c>/<c>Combine</c> operations exist for windowed surveys: Split clips an access at a window boundary (charging each window only the usage that occurred within it, using window-start snapshots for manual usage) and Combine merges same-bottleneck records by unioning time ranges, summing usage, and recomputing utilization over the merged span.
/// </plan>
/// </remarks>
public sealed class AmbientBottleneckAccessor : IComparable<AmbientBottleneckAccessor>, IDisposable
{
    private readonly BasicAmbientBottleneckDetector _owner;
    private long _accessEndStopwatchTimestamp;  // interlocked, long.MaxValue until the access finishes
    private long _accessCount;                  // interlocked, zero until the access finishes
    private double _limitUsed;                  // interlocked, zero until set by Dispose, SetUsage or AddUsage
    private double _utilization;                // interlocked, zero until set by Dispose, SetUsage or AddUsage

    internal long AccessBeginStopwatchTimestamp { get; }
    internal long AccessEndStopwatchTimestamp => _accessEndStopwatchTimestamp;
    /// <summary>
    /// Gets the <see cref="AmbientBottleneck"/> for the bottleneck.
    /// </summary>
    public AmbientBottleneck Bottleneck { get; }
    /// <summary>
    /// Gets the beginning of the time range for the access.
    /// </summary>
    public DateTime AccessBegin => new(TimeSpanUtilities.StopwatchTimestampToDateTime(AccessBeginStopwatchTimestamp));
    /// <summary>
    /// Gets the end of the time range for the access, if the access is finished.
    /// </summary>
    public DateTime? AccessEnd => (_accessEndStopwatchTimestamp >= long.MaxValue) ? null : new DateTime(TimeSpanUtilities.StopwatchTimestampToDateTime(_accessEndStopwatchTimestamp));
    /// <summary>
    /// Gets the number of times the bottleneck was accessed.  This is only statistical and is not used to compute <see cref="Utilization"/> or rank bottlenecks.
    /// </summary>
    public long AccessCount => _accessCount;
    /// <summary>
    /// Gets the amount of the limit which was used.  Note that this is in units of <see cref="System.Diagnostics.Stopwatch"/> ticks.
    /// </summary>
    public double LimitUsed => _limitUsed;
    /// <summary>
    /// Gets the utilization factor, usually between 0.0 and 1.0, where 1.0 indicates that the limit was just reached, and numbers larger than 1.0 indicate contention beyond what was possible to satisfy (similar to the average queue disk length in windows)
    /// </summary>
    /// <remarks>
    /// This computed value is used to sort the seriousness of the usage, with larger values indicating more of a problem, ie. being closer to a system overload.
    /// The value is only updated when constructed, when <see cref="SetUsage"/> or <see cref="AddUsage"/> is called, or when disposed at the end of the access.
    /// </remarks>
    public double Utilization => _utilization;
    /// <summary>
    /// Gets number of stopwatch ticks between the beginning and end of the access.
    /// </summary>
    public long AccessDurationStopwatchTicks => ((_accessEndStopwatchTimestamp >= long.MaxValue) ? AmbientClock.Ticks : _accessEndStopwatchTimestamp) - AccessBeginStopwatchTimestamp;

    /// <summary>
    /// Constructs a single-access AmbientBottleneckAccessRecord for the specified bottleneck with access starting at the specified timestamp.
    /// </summary>
    internal AmbientBottleneckAccessor(BasicAmbientBottleneckDetector owner, AmbientBottleneck bottleneck, long accessBeginStopwatchTimestamp)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Bottleneck = bottleneck ?? throw new ArgumentNullException(nameof(bottleneck));
        AccessBeginStopwatchTimestamp = accessBeginStopwatchTimestamp;
        _accessEndStopwatchTimestamp = long.MaxValue;
    }
    /// <summary>
    /// Constructs an AmbientBottleneckAccessRecord from the completely specified property values.
    /// </summary>
    internal AmbientBottleneckAccessor(BasicAmbientBottleneckDetector owner, AmbientBottleneck bottleneck, long accessBeginStopwatchTimestamp, long accessEndStopwatchTimestamp, long accessCount, double limitUsed)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Bottleneck = bottleneck ?? throw new ArgumentNullException(nameof(bottleneck));
        AccessBeginStopwatchTimestamp = accessBeginStopwatchTimestamp;
        _accessEndStopwatchTimestamp = accessEndStopwatchTimestamp;
        _accessCount = accessCount;
        _limitUsed = limitUsed;
        UpdateUtilization();
    }
    /// <summary>
    /// Constructs a AmbientBottleneckAccessRecord from the specified property values.
    /// </summary>
    /// <param name="owner">The <see cref="BasicAmbientBottleneckDetector"/> that owns this access.</param>
    /// <param name="bottleneck">The <see cref="AmbientBottleneck"/> being accessed.</param>
    /// <param name="accessBegin">The <see cref="DateTime"/> indicating the beginning of the access (presumably now, or just a bit ago).  Note that <see cref="DateTime"/> may not have the resolution of stopwatch ticks, so the start time may be slightly truncated as a result of the conversion.</param>
    internal AmbientBottleneckAccessor(BasicAmbientBottleneckDetector owner, AmbientBottleneck bottleneck, DateTime accessBegin)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Bottleneck = bottleneck ?? throw new ArgumentNullException(nameof(bottleneck));
        AccessBeginStopwatchTimestamp = TimeSpanUtilities.DateTimeToStopwatchTimestamp(accessBegin.Ticks);
        _accessEndStopwatchTimestamp = long.MaxValue;
    }

    /// <summary>
    /// Sets the access count and limit used.
    /// </summary>
    /// <param name="accessCount">The number of access counts.</param>
    /// <param name="limitUsed">The amount towards the associated limit that has been used.</param>
    public void SetUsage(long accessCount, double limitUsed)
    {
        if (Bottleneck.Automatic) throw new InvalidOperationException("SetUsage cannot be used on Automatic bottlenecks!");
        System.Threading.Interlocked.Exchange(ref _accessCount, accessCount);
        System.Threading.Interlocked.Exchange(ref _limitUsed, limitUsed);
        UpdateUtilization();
    }

    /// <summary>
    /// Adds to the access count and limit used.
    /// </summary>
    /// <param name="additionalAccessCount">The additional number of access counts.</param>
    /// <param name="additionalLimitUsed">The additional amount towards the associated limit that has been used.</param>
    public void AddUsage(long additionalAccessCount, double additionalLimitUsed)
    {
        if (Bottleneck.Automatic) throw new InvalidOperationException("SetUsage cannot be used on Automatic bottlenecks!");
        System.Threading.Interlocked.Add(ref _accessCount, additionalAccessCount);
        InterlockedUtilities.TryOptimisticAdd(ref _limitUsed, additionalLimitUsed);
        UpdateUtilization();
    }

    private void UpdateUtilization()
    {
        System.Threading.Interlocked.Exchange(ref _utilization, ComputeUtilization(Bottleneck.UtilizationAlgorithm, _accessEndStopwatchTimestamp - AccessBeginStopwatchTimestamp, _limitUsed, Bottleneck.Limit, Bottleneck.LimitPeriod));
    }

    private static double ComputeUtilization(AmbientBottleneckUtilizationAlgorithm limitType, long totalStopwatchTicks, double limitUsed, double? limit, TimeSpan? limitPeriod)
    {
        if (limit == null || Math.Abs(limit.Value) < 1) limit = 1.0;
        double limitPeriodStopwatchTicks = (limitPeriod == null) ? 1.0 : TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(limitPeriod.Value.Ticks);
        if (totalStopwatchTicks < 1) totalStopwatchTicks = 1;
        double result = limitType switch {
            AmbientBottleneckUtilizationAlgorithm.Linear => (1.0 * limitUsed / totalStopwatchTicks) / (1.0 * limit.Value / limitPeriodStopwatchTicks),
            AmbientBottleneckUtilizationAlgorithm.ExponentialLimitApproach => 1.0 - 1.0 / (2.0 * limitUsed / totalStopwatchTicks + 1.0),
            _ => 0.0,
        };
        return result;
    }

    /// <summary>
    /// Disposes of this instance, indicating that the access of the bottleneck has completed.
    /// </summary>
    public void Dispose()
    {
        System.Threading.Interlocked.Exchange(ref _accessEndStopwatchTimestamp, AmbientClock.Ticks);
        if (Bottleneck.Automatic && _accessCount == 0)
        {
            System.Threading.Interlocked.Exchange(ref _accessCount, 1);
            System.Threading.Interlocked.Exchange(ref _limitUsed, _accessEndStopwatchTimestamp - AccessBeginStopwatchTimestamp);
        }
        UpdateUtilization();
        _owner.LeaveBottleneck(this);
        GC.SuppressFinalize(this);
    }

    internal (AmbientBottleneckAccessor, AmbientBottleneckAccessor?) Split(long oldWindowBeginStopwatchTicks, long splitStopwatchTicks, long windowStartAccessCount, double windowStartLimitUsage)
    {
        long accessCount;
        double limitUsedSoFar;
        if (Bottleneck.Automatic)
        {
            accessCount = _accessCount;
            limitUsedSoFar = ((_accessEndStopwatchTimestamp >= long.MaxValue) ? splitStopwatchTicks : _accessEndStopwatchTimestamp)
                - AccessBeginStopwatchTimestamp - windowStartLimitUsage;
        }
        else
        {
            accessCount = _accessCount - windowStartAccessCount;
            limitUsedSoFar = _limitUsed - windowStartLimitUsage;
        }
        // the old part will be the usage that has occurred since the last window started and will be clipped to the old window
        AmbientBottleneckAccessor oldPart = new( _owner, Bottleneck, 
            Math.Max(AccessBeginStopwatchTimestamp, oldWindowBeginStopwatchTicks), splitStopwatchTicks,
            accessCount, limitUsedSoFar);
        // is this entry *not* still open?  // the entry is *not* still open, but it could have span multiple windows earlier, so while we're not going to copy it forward to this window, we are going to adjust values to represent usage within that window
        if (_accessEndStopwatchTimestamp < long.MaxValue) return (oldPart, null);
        // else the new part will exist, and it will use the fork time as the start
        AmbientBottleneckAccessor newPart = new(_owner, Bottleneck, splitStopwatchTicks, splitStopwatchTicks, 0, 0);
        return (oldPart, newPart);
    }
    internal AmbientBottleneckAccessor Combine(AmbientBottleneckAccessor that)
    {
        if (Bottleneck != that.Bottleneck) throw new ArgumentException("The bottlenecks must be the same in order to combine!", nameof(that));
        return Combine(that.AccessBeginStopwatchTimestamp, that._accessEndStopwatchTimestamp, that._accessCount, that._limitUsed);
    }
    internal AmbientBottleneckAccessor Combine(long accessBeginTicks, long accessEndTicks, long accessCount, double limitUsed)
    {
        long beginTicks = Math.Min(AccessBeginStopwatchTimestamp, accessBeginTicks);
        long endTicks = Math.Max(_accessEndStopwatchTimestamp, accessEndTicks);
        double sumLimitUsed = _limitUsed + limitUsed;
        // if the access hasn't finished, compute everything as if it finished right now
        long totalStopwatchTicks = (endTicks >= long.MaxValue) ? (AmbientClock.Ticks - beginTicks) : (endTicks - beginTicks);
        if (totalStopwatchTicks == 0) totalStopwatchTicks = 1;    // make sure total ticks is not zero to avoid a divide by zero exception (note that we allow negative values--they shouldn't be possible for time, but we have a test case that at least makes sure they don't crash)
        double limitProportionUsed = // if the usage is equal to the limit and the range is equal to the period, 1.0 should be the result
            ComputeUtilization(Bottleneck.UtilizationAlgorithm, totalStopwatchTicks, sumLimitUsed, Bottleneck.Limit, Bottleneck.LimitPeriod);
        return new AmbientBottleneckAccessor(_owner, Bottleneck, beginTicks)
        {
            _accessEndStopwatchTimestamp = endTicks,
            _accessCount = accessCount,
            _limitUsed = limitUsed,
            _utilization = limitProportionUsed
        };
    }

    /// <summary>
    /// Compares this AmbientBottleneckAccessRecord to another one to see which one has used more towards any set limit.
    /// </summary>
    /// <param name="other">The other AmbientBottleneckAccessRecord.</param>
    /// <returns>&gt;0 if this one has used more than <paramref name="other"/>, &lt;0 if this one has used less than <paramref name="other"/>, or 0 if they have used the same amount.</returns>
    public int CompareTo(AmbientBottleneckAccessor? other)
    {
        if (other is null) return 1;
        int diff = _utilization.CompareTo(other._utilization);
        if (diff != 0) return diff;
        diff = _limitUsed.CompareTo(other._limitUsed);
        if (diff != 0) return diff;
        return AccessCount.CompareTo(other.AccessCount);
    }
    /// <summary>
    /// Gets whether or not the specified object is logically equivalent to this one.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>true if the objects are logically equivalent, false if they are not.</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not AmbientBottleneckAccessor that) return false;
        return Bottleneck.Equals(that.Bottleneck) && AccessBeginStopwatchTimestamp.Equals(that.AccessBeginStopwatchTimestamp) && _accessEndStopwatchTimestamp.Equals(that._accessEndStopwatchTimestamp);
    }
    /// <summary>
    /// Gets a 32-bit hash code for the value of this object.
    /// </summary>
    /// <returns>A 32-bit hash code for the value of this object.</returns>
    public override int GetHashCode()
    {
        return Bottleneck.GetHashCode() ^ AccessBeginStopwatchTimestamp.GetHashCode() ^ _accessEndStopwatchTimestamp.GetHashCode();
    }

    /// <summary>
    /// Checks to see if two AmbientBottleneckAccessRecord are logically equal.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the AmbientBottleneckAccessRecords are logically equal, false if they are not.</returns>
    public static bool operator ==(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }
    /// <summary>
    /// Checks to see if two AmbientBottleneckAccessRecord are logically not equal.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the AmbientBottleneckAccessRecords are logically not equal, false if they are logically equal.</returns>
    public static bool operator !=(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        return !(left == right);
    }
    /// <summary>
    /// Checks to see if a AmbientBottleneckAccessRecord is less than another one.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the <paramref name="left"/> is less than <paramref name="right"/>, false if not.</returns>
    public static bool operator <(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }
    /// <summary>
    /// Checks to see if a AmbientBottleneckAccessRecord is less than or equal to another one.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the <paramref name="left"/> is less than or equal to <paramref name="right"/>, false if not.</returns>
    public static bool operator <=(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }
    /// <summary>
    /// Checks to see if a AmbientBottleneckAccessRecord is greater than another one.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the <paramref name="left"/> is greater than <paramref name="right"/>, false if not.</returns>
    public static bool operator >(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }
    /// <summary>
    /// Checks to see if a AmbientBottleneckAccessRecord is greater than or equal to another one.
    /// </summary>
    /// <param name="left">The left AmbientBottleneckAccessRecord.</param>
    /// <param name="right">The right AmbientBottleneckAccessRecord.</param>
    /// <returns>true if the <paramref name="left"/> is greater than or equal to <paramref name="right"/>, false if not.</returns>
    public static bool operator >=(AmbientBottleneckAccessor? left, AmbientBottleneckAccessor? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }
}
