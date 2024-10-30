using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices;

#pragma warning disable CA1510
/// <summary>
/// An interface to create and manage system statistics, which provide long-lived high-performance tracking of accumulated, minimum, maximum, or raw samples.
/// Statistics can be used to track memory allocated, time waited, minimum or maximum sizes or times, request processing time, cache hit and misses, etc.
/// Ratios of two statistics can be used to track things like average sizes or times, events per second, bytes per second, hit ratios, etc.
/// All times are in terms of ticks whose frequency is <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
/// </summary>
public interface IAmbientStatistics
{
    /// <summary>
    /// Gets the built-in execution time statistic.
    /// </summary>
    public IAmbientStatisticReader ExecutionTime { get; }
    /// <summary>
    /// Gets a <see cref="IDictionary{TKey, TValue}"/> with all the statistics.
    /// </summary>
    IDictionary<string, IAmbientStatisticReader> Statistics { get; }
    /// <summary>
    /// Gets a <see cref="IDictionary{TKey, TValue}"/> with all the ratio statistics.
    /// </summary>
    IDictionary<string, IAmbientRatioStatistic> RatioStatistics { get; }
    /// <summary>
    /// Finds the specified statistic.
    /// </summary>
    /// <returns>A <see cref="IAmbientStatisticReader"/> the caller can use to read the statistic, or null if there is no statistic with the specified ID.</returns>
    IAmbientStatisticReader? ReadStatistic(string id);
    /// <summary>
    /// Adds or updates a time-based statistic with the specified identifier, description, and properties.
    /// Time-based statistics are always in seconds.
    /// </summary>
    /// <param name="type">The <see cref="AmbientStatisicType"/> for the statistic.</param>
    /// <param name="id">A dash-delimited identifier for the statistic.</param>
    /// <param name="name">A name for the statistic, presumably to use as a chart title.</param>
    /// <param name="description">A human-readable description for the statistic.</param>
    /// <param name="replaceIfAlreadyExists">true to use a new statistic even if one already exists, false to return an existing statistic if one already exists.  Default is false.</param>
    /// <param name="initialValue">The initial value for the statistic, if it is created.</param>
    /// <param name="minimumValue">An optional value indicating the minimum possible value, if applicable.</param>
    /// <param name="maximumValue">An optional value indicating the maximum possible value, if applicable.</param>
    /// <param name="temporalAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated over time.</param>
    /// <param name="spatialAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated across systems.</param>
    /// <param name="preferredTemporalAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated over time.</param>
    /// <param name="preferredSpatialAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated across systems.</param>
    /// <param name="missingSampleHandling">A <see cref="MissingSampleHandling"/> indicating how clients should treat missing samples from this statistic.</param>
    /// <returns>An <see cref="IAmbientStatistic"/> the caller can use to update the statistic samples.</returns>
    IAmbientStatistic GetOrAddTimeBasedStatistic(AmbientStatisicType type, string id, string name, string description, bool replaceIfAlreadyExists = false
        , long initialValue = 0, long? minimumValue = null, long? maximumValue = null
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        );
    /// <summary>
    /// Adds or updates a statistic with the specified identifier, description, and properties.
    /// </summary>
    /// <param name="type">The <see cref="AmbientStatisicType"/> for the statistic.</param>
    /// <param name="id">A dash-delimited identifier for the statistic.</param>
    /// <param name="name">A name for the statistic, presumably to use as a chart title.</param>
    /// <param name="description">A human-readable description for the statistic.</param>
    /// <param name="replaceIfAlreadyExists">true to use a new statistic even if one already exists, false to return an existing statistic if one already exists.  Default is false.</param>
    /// <param name="initialValue">The initial value for the statistic, if it is created.</param>
    /// <param name="minimumValue">An optional value indicating the minimum possible value, if applicable.</param>
    /// <param name="maximumValue">An optional value indicating the maximum possible value, if applicable.</param>
    /// <param name="units">An optional string describing the units of the statistic (after the decimal point is adjusted by <paramref name="fixedFloatingPointAdjustment"/>).  Lower-cased, not an abbreviation.</param>
    /// <param name="fixedFloatingPointAdjustment">An optional value to divide raw samples by to get a floating-point value.  Defaults to 1.0.</param>
    /// <param name="temporalAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated over time.</param>
    /// <param name="spatialAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated across systems.</param>
    /// <param name="preferredTemporalAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated over time.</param>
    /// <param name="preferredSpatialAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated across systems.</param>
    /// <param name="missingSampleHandling">A <see cref="MissingSampleHandling"/> indicating how clients should treat missing samples from this statistic.</param>
    /// <returns>An <see cref="IAmbientStatistic"/> the caller can use to update the statistic samples.</returns>
    IAmbientStatistic GetOrAddStatistic(AmbientStatisicType type, string id, string name, string description, bool replaceIfAlreadyExists = false
        , long initialValue = 0, long? minimumValue = null, long? maximumValue = null
        , string? units = null, double fixedFloatingPointAdjustment = 1.0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        );
    /// <summary>
    /// Removes the specified statistic if it exists.
    /// </summary>
    /// <returns>Whether or not the statistic was successfully removed.</returns>
    /// <remarks>
    /// Note that the ExecutionTime statistic cannot be removed and will return false from this function.
    /// </remarks>
    bool RemoveStatistic(string id);
    /// <summary>
    /// Adds or updates a ratio statistic with the specified identifier, description, and statistics.
    /// </summary>
    /// <param name="id">A dash-delimited identifier for the statistic.</param>
    /// <param name="name">A name for the statistic, presumably to use as a chart title.</param>
    /// <param name="description">A human-readable description for the statistic.</param>
    /// <param name="replaceIfAlreadyExists">true to use a new statistic even if one already exists, false to return an existing statistic if one already exists.  Default is false.</param>
    /// <param name="units">An optional string describing the units of the statistic (after the decimal point is adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/> for both the numerator and denominator).  Lower-cased, not an abbreviation.  If not specified, will be derived from the numerator and denominator units.</param>
    /// <param name="numeratorStatistic">The ID of the numerator statistic.  The constant 1 will be used if null.</param>
    /// <param name="numeratorDelta">Whether or not the numerator should be the difference over time.</param>
    /// <param name="denominatorStatistic">The ID of the denominator statistic.  The constant 1 will be used if null.</param>
    /// <param name="denominatorDelta">Whether or not the denominator should be the difference over time.</param>
    /// <returns>An <see cref="IAmbientRatioStatistic"/> the caller can use to access the ratio statistic data.</returns>
    IAmbientRatioStatistic GetOrAddRatioStatistic(string id, string name, string description, bool replaceIfAlreadyExists = false, string? units = null
        , string? numeratorStatistic = null, bool numeratorDelta = true
        , string? denominatorStatistic = null, bool denominatorDelta = true
        );
    /// <summary>
    /// Removes the specified ratio statistic if it exists.
    /// </summary>
    /// <returns>Whether or not the ratio statistic was successfully removed.</returns>
    bool RemoveRatioStatistic(string id);
}
/// <summary>
/// An enumeration of the types of statistics that can be collected.
/// </summary>
public enum AmbientStatisicType
{
    /// <summary>
    /// A raw statistic is one that is not cumulative, that usually uses <see cref="IAmbientStatistic.SetRawValue"/> for updates, but may also use <see cref="IAmbientStatistic.IncrementRaw"/> and <see cref="IAmbientStatistic.DecrementRaw"/> to dynamically keep track of a count such as pending operations.
    /// </summary>
    Raw,
    /// <summary>
    /// A cumulative statistic is one that is added to over time, using <see cref="IAmbientStatistic.IncrementRaw"/> or <see cref="IAmbientStatistic.AddRaw"/> for updates.
    /// </summary>
    Cumulative,
    /// <summary>
    /// A min statistic is one that always uses <see cref="IAmbientStatistic.SetRawMin"/> for updates.
    /// </summary>
    Min,
    /// <summary>
    /// A min statistic is one that always uses <see cref="IAmbientStatistic.SetRawMax"/> for updates.
    /// </summary>
    Max,
}
/// <summary>
/// An enumeration of ways to aggregate statistic data.
/// </summary>
[Flags]
public enum AggregationTypes
{
    /// <summary>
    /// Since not aggregating doesn't ever make sense, we use this as the default value to do aggregation based on the <see cref="AmbientStatisicType"/>.
    /// See remarks for details about temporal and spatial aggregation defaults for each type.
    /// </summary>
    /// <remarks>
    /// For <see cref="AmbientStatisicType.Raw"/>, temporal aggregation is <see cref="Average"/> and spatial aggregation is <see cref="Average"/>.
    /// For <see cref="AmbientStatisicType.Cumulative"/>, temporal aggregation is <see cref="MostRecent"/> and spatial aggregation is <see cref="Average"/>.
    /// For <see cref="AmbientStatisicType.Min"/>, temporal aggregation is <see cref="Min"/> and spatial aggregation is <see cref="Min"/>.
    /// For <see cref="AmbientStatisicType.Max"/>, temporal aggregation is <see cref="Max"/> and spatial aggregation is <see cref="Max"/>.
    /// </remarks>
    None = 0,
    /// <summary>
    /// The aggregation should sum the values.  This type of aggregation would be useful for statistics that count items or an amount of processing but doesn't count cumulatively.
    /// Such statistics are not recommended unless the available graphing system can't display the change in time for a cumulative counter.
    /// </summary>
    Sum = 1,
    /// <summary>
    /// The aggregation should average the values.  Statistics that use <see cref="IAmbientStatistic.SetRawValue"/> might use this type of aggregation.
    /// </summary>
    Average = 2,
    /// <summary>
    /// The aggregation should take the least of the values.  Statistics that use <see cref="IAmbientStatistic.SetRawMin"/> would likely use this type of aggregation.
    /// </summary>
    Min = 4,
    /// <summary>
    /// The aggregation should take the greatest of the values.  Statistics that use <see cref="IAmbientStatistic.SetRawMax"/> would likely use this type of aggregation.
    /// </summary>
    Max = 8,
    /// <summary>
    /// The aggregation should take the most recent value.  Statistics that use <see cref="IAmbientStatistic.SetRawValue"/>, <see cref="IAmbientStatistic.IncrementRaw"/> or <see cref="IAmbientStatistic.AddRaw"/> might use this type of aggregation.
    /// For spatial aggregation, this would only be useful if every system is reporting some value from a shared external system.
    /// </summary>
    MostRecent = 16,
}
/// <summary>
/// An enumeration indicating how missing (null) samples should be handled, usually on the client side (perhaps on the server side if the server is generating higher-level statistics).
/// </summary>
public enum MissingSampleHandling
{ 
    /// <summary>
    /// When samples get missed, the missing samples should just be ignored.  This is useful when you want to see missing samples as gaps in graphs.
    /// </summary>
    Skip,
    /// <summary>
    /// When samples get missed, the missing samples should be filled in with zeros.
    /// </summary>
    Zero,
    /// <summary>
    /// When samples get missed, the missing values should be filled in using linear estimation.  This is the default type of missing sample handling.
    /// </summary>
    LinearEstimation,
    /// <summary>
    /// When samples get missed, the missing values should be filled in using exponential estimation.
    /// </summary>
    ExponentialEstimation,
    /// <summary>
    /// When samples get missed, the missing values should be filled in using logarithmic estimation.
    /// </summary>
    LogarithmicEstimation,
}
/// <summary>
/// An interface that give read access to a single statistic.
/// Note that many user-facing statistics will naturally be a ratio of the samples of two statistics or the changes in those samples over time.
/// </summary>
public interface IAmbientStatisticReader
{
    /// <summary>
    /// Gets the <see cref="IAmbientStatistics"/> this statistic belongs to.
    /// </summary>
    IAmbientStatistics StatisticsSet { get; }
    /// <summary>
    /// Gets the <see cref="AmbientStatisicType"/> for the statistic.  Immutable.
    /// </summary>
    AmbientStatisicType StatisicType { get; }
    /// <summary>
    /// Gets the identifier for the statistic.
    /// The identifier should be a dash-delimited path identifying the data.  Immutable.
    /// </summary>
    string Id { get; }
    /// <summary>
    /// Gets a human-readable name, presumbly for the chart title.  Should describe the adjusted values, not the raw values.  Immutable.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Gets a human-readable description of this statistic.  Should describe the adjusted values, not the raw values.  Immutable.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// Gets the current statistic sample value.  Thread-safe, possibly interlocked.
    /// </summary>
    long CurrentRawValue { get; }
    /// <summary>
    /// The expected minimum value, if any.  Null if there is no expected minimum.  Immutable.
    /// </summary>
    long? ExpectedMinimumRawValue { get; }
    /// <summary>
    /// The expected maximum value, if any.  Null if there is no expected maximum.  Immutable.
    /// </summary>
    long? ExpectedMaximumRawValue { get; }
    /// <summary>
    /// Gets an optional human-readable units name, presumbly for the y-axis of the chart.  
    /// Assumes that the numbers in the axis have already been divided by <see cref="FixedFloatingPointAdjustment"/>.  Immutable.
    /// Immutable.
    /// </summary>
    string? AdjustedUnits { get; }
    /// <summary>
    /// The number used to divide <see cref="CurrentRawValue"/> to adjust the integer sample into a floating point number in the specified units (1.0 if not applicable).
    /// Immutable.
    /// </summary>
    double FixedFloatingPointAdjustment { get; }
    /// <summary>
    /// The types of aggregation that should be used when aggregating samples over time.
    /// Immutable.
    /// </summary>
    AggregationTypes TemporalAggregationTypes { get; }
    /// <summary>
    /// The types of aggregation that should be used when aggregating samples from different systems.
    /// Immutable.
    /// </summary>
    AggregationTypes SpatialAggregationTypes { get; }
    /// <summary>
    /// The type of aggregation that should be used when aggregating samples over time and only one aggregation can be kept.
    /// Immutable.
    /// </summary>
    AggregationTypes PreferredTemporalAggregationType { get; }
    /// <summary>
    /// The type of aggregation that should be used when aggregating samples from different systems and only one aggregation can be kept.
    /// Immutable.
    /// </summary>
    AggregationTypes PreferredSpatialAggregationType { get; }
    /// <summary>
    /// How missing samples should be handled.
    /// Immutable.
    /// </summary>
    MissingSampleHandling MissingSampleHandling { get; }
}
/// <summary>
/// An interface that gives write access to a single statistic.
/// Implementations are disposable, but should not throw exceptions if methods are called after disposal.
/// Disposability is meant to stop reporting results.
/// </summary>
public interface IAmbientStatistic : IAmbientStatisticReader, IDisposable
{
    /// <summary>
    /// Increments the raw statistic sample value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <returns>The incremented sample value.</returns>
    long IncrementRaw();
    /// <summary>
    /// Decrements the raw statistic sample value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <returns>The decremented sample value.</returns>
    long DecrementRaw();
    /// <summary>
    /// Adds to the raw statistic sample value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="addend">The amount to add to the statistic sample value.</param>
    /// <returns>The new sample value.</returns>
    long AddRaw(long addend);
    /// <summary>
    /// Sets the raw integer statistic sample value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="newValue">The new value to use.</param>
    void SetRawValue(long newValue);
    /// <summary>
    /// Sets the raw statistic sample minimum value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="newPossibleMinValue">A value which will be the new sample value if it is smaller than the current sample value.</param>
    /// <returns>The new sample value.</returns>
    long SetRawMin(long newPossibleMinValue);
    /// <summary>
    /// Sets the raw statistic sample maximum value.  This value is not adjusted by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/>.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="newPossibleMaxValue">A value which will be the new sample value if it is larger than the current sample value.</param>
    /// <returns>The new sample value.</returns>
    long SetRawMax(long newPossibleMaxValue);
}
/// <summary>
/// An interface that indicates that a useful statistic exists that is the ratio of two other statistics or the change over time of those statistics.
/// These statistics should not be recorded independently, but should be recorded as a ratio of the two specified statistics, and can be applied after spatial and/or temporal aggregation.
/// Using ratio statistics instead of computing the ratio on the server prevents the need to send the raw data to the client, and avoids issues often caused by servers spinning up or down throwing off statistics.
/// For example, if a statistic for the average request processing time is computed on the server, aggregating that ratio across servers results in the server spinning up or down and handling a tiny fraction of requests to have the same weight as all the requests handled by fully-engaged servers.
/// Using ratio statistics to compute the ratios on the aggregated raw data weights all the requests equally, whereas using computing the average time on each server weights requests on servers processing only a few requests more heavily.
/// Implementations are disposable, but should not throw exceptions if methods are called after disposal.
/// Disposability is meant to stop reporting results.
/// </summary>
public interface IAmbientRatioStatistic : IDisposable
{
    /// <summary>
    /// Gets the <see cref="IAmbientStatistics"/> this statistic belongs to.
    /// </summary>
    IAmbientStatistics StatisticsSet { get; }
    /// <summary>
    /// Gets the ID of the numerator statistic.  Use the constant 1 (and ignore <see cref="NumeratorDelta"/>) if null.  Immutable.
    /// </summary>
    string? NumeratorStatisticId { get; }
    /// <summary>
    /// The numerator should be the change in the numerator statistic over time rather than the raw value.  Immutable.
    /// </summary>
    bool NumeratorDelta { get; }
    /// <summary>
    /// Gets the ID of the denominator statistic, often the built-in "ExecutionTime" statistic.  Use 1 (and ignore <see cref="NumeratorDelta"/>) if null.  Immutable.
    /// </summary>
    string? DenominatorStatisticId { get; }
    /// <summary>
    /// The denominator should be the change in the numerator statistic over time rather than the raw value.  Immutable.
    /// </summary>
    bool DenominatorDelta { get; }
}

/// <summary>
/// A class that contains extension methods for various statistics interfaces that add functions to aggregate statistic samples.
/// </summary>
public static class IAmbientStatisticsExtensions
{
    /// <summary>
    /// Sets the statistic sample value, adjusting it by dividing by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/> in the process.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="statistic">The <see cref="IAmbientStatistic"/> whose value should be set.</param>
    /// <param name="newValue">The new value to use.</param>
    public static void SetValue(this IAmbientStatistic statistic, long newValue)
    {
        if (statistic == null) throw new ArgumentNullException(nameof(statistic));
        statistic.SetRawValue((long)(newValue * statistic.FixedFloatingPointAdjustment));
    }
    /// <summary>
    /// Sets the statistic sample value, adjusting it by dividing by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/> in the process.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="statistic">The <see cref="IAmbientStatistic"/> whose value should be set.</param>
    /// <param name="newValue">The new value to use.</param>
    public static void SetValue(this IAmbientStatistic statistic, float newValue)
    {
        if (statistic == null) throw new ArgumentNullException(nameof(statistic));
        statistic.SetRawValue((long)(newValue * statistic.FixedFloatingPointAdjustment));
    }
    /// <summary>
    /// Sets the statistic sample value, adjusting it by dividing by <see cref="IAmbientStatisticReader.FixedFloatingPointAdjustment"/> in the process.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="statistic">The <see cref="IAmbientStatistic"/> whose value should be set.</param>
    /// <param name="newValue">The new value to use.</param>
    public static void SetValue(this IAmbientStatistic statistic, double newValue)
    {
        if (statistic == null) throw new ArgumentNullException(nameof(statistic));
        statistic.SetRawValue((long)(newValue * statistic.FixedFloatingPointAdjustment));
    }
    /// <summary>
    /// Uses the preferred aggregation type to aggregate samples from a time range.
    /// </summary>
    /// <param name="reader">The reader to use to aggregate the samples.</param>
    /// <param name="samples">An enumeration of samples to aggregate.</param>
    /// <returns>The preferred temporally aggregated sample.</returns>
    public static long? PreferredTemporalAggregation(this IAmbientStatisticReader reader, IEnumerable<long?> samples)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        AggregationTypes types = reader.PreferredTemporalAggregationType;
        if (types == AggregationTypes.None) types = DefaultTemporalAggregation(reader.StatisicType);
        return types switch
        {
            AggregationTypes.Average => (long?)samples.Average(),
            AggregationTypes.Min => samples.Min() ?? 0,
            AggregationTypes.Max => samples.Max() ?? 0,
            AggregationTypes.MostRecent => samples.LastOrDefault() ?? 0,
            _ => samples.Sum() ?? 0,
        };
    }
    /// <summary>
    /// Uses the preferred aggregation type to aggregate samples from different systems.
    /// </summary>
    /// <param name="reader">The reader to use to aggregate the samples.</param>
    /// <param name="samples">An enumeration of samples to aggregate.</param>
    /// <returns>The preferred temporally aggregated sample.</returns>
    public static long? PreferredSpatialAggregation(this IAmbientStatisticReader reader, IEnumerable<long?> samples)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        AggregationTypes types = reader.PreferredSpatialAggregationType;
        if (types == AggregationTypes.None) types = DefaultSpatialAggregation(reader.StatisicType);
        return types switch
        {
            AggregationTypes.Average => (long?)samples.Average(),
            AggregationTypes.Min => samples.Min() ?? 0,
            AggregationTypes.Max => samples.Max() ?? 0,
            AggregationTypes.MostRecent => samples.LastOrDefault() ?? 0,
            _ => samples.Sum() ?? 0,
        };
    }
    /// <summary>
    /// Gets the default temporal (over time) aggregation type for the specified statistic type.
    /// </summary>
    /// <param name="statisicType">The <see cref="AmbientStatisicType"/> for the statistic.</param>
    /// <returns>The default <see cref="AggregationTypes"/> for aggregating samples for specified statistic over time.</returns>
    public static AggregationTypes DefaultTemporalAggregation(AmbientStatisicType statisicType)
    {
        return statisicType switch
        {
            AmbientStatisicType.Raw => AggregationTypes.Average,
            AmbientStatisicType.Cumulative => AggregationTypes.MostRecent,
            AmbientStatisicType.Min => AggregationTypes.Min,
            AmbientStatisicType.Max => AggregationTypes.Max,
            _ => AggregationTypes.Average,
        };
    }
    /// <summary>
    /// Gets the default spatial (cross-system) aggregation type for the specified statistic type.
    /// </summary>
    /// <param name="statisicType">The <see cref="AmbientStatisicType"/> for the statistic.</param>
    /// <returns>The default <see cref="AggregationTypes"/> for aggregating samples for specified statistic across systems.</returns>
    public static AggregationTypes DefaultSpatialAggregation(AmbientStatisicType statisicType)
    {
        return statisicType switch
        {
            AmbientStatisicType.Raw => AggregationTypes.Average,
            AmbientStatisicType.Cumulative => AggregationTypes.Average,
            AmbientStatisicType.Min => AggregationTypes.Min,
            AmbientStatisicType.Max => AggregationTypes.Max,
            _ => AggregationTypes.Average,
        };
    }
}
