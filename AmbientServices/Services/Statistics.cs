using System;
using System.Collections.Generic;

namespace AmbientServices
{
    /// <summary>
    /// An interface to create and manage system statistics, which provide long-lived high-performance tracking of accumulated, minimum, maximum, or raw samples.
    /// Statistics can be used to track memory allocated, time waited, minimum or maximum sizes or times, request processing time, cache hit and misses, etc.
    /// Ratios of two statistics can be used to track things like average sizes or times, events per second, bytes per second, hit ratios, etc.
    /// All times are in terms of ticks whose frequency is <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    public interface IAmbientStatistics
    {
        /// <summary>
        /// Gets a <see cref="IDictionary{TKey, TValue}"/> with all the statistics.
        /// </summary>
        IDictionary<string, IAmbientStatisticReader> Statistics { get; }
        /// <summary>
        /// Finds the specified statistic.
        /// </summary>
        /// <returns>A <see cref="IAmbientStatisticReader"/> the caller can use to read the statistic, or null if there is no statistic with the specified ID.</returns>
        IAmbientStatisticReader? ReadStatistic(string id);
        /// <summary>
        /// Adds or updates a statistic with the specified identifier, description, and properties.
        /// </summary>
        /// <param name="timeBased">Whether or not this statistic is a time-based statistic.</param>
        /// <param name="id">A dash-delimited identifier for the statistic.</param>
        /// <param name="description">A human-readable description for the statistic.</param>
        /// <param name="replaceIfAlreadyExists">true to use a new statistic even if one already exists, false to return an existing statistic if one already exists.  Default is false.</param>
        /// <param name="initialValue">The initial value for the statistic, if it is created.</param>
        /// <param name="temporalAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated over time.</param>
        /// <param name="spatialAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated across systems.</param>
        /// <param name="preferredTemporalAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated over time.</param>
        /// <param name="preferredSpatialAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated across systems.</param>
        /// <param name="missingSampleHandling">A <see cref="MissingSampleHandling"/> indicating how clients should treat missing samples from this statistic.</param>
        /// <returns>An <see cref="IAmbientStatistic"/> the caller can use to update the statistic samples.</returns>
        IAmbientStatistic GetOrAddStatistic(bool timeBased, string id, string description, bool replaceIfAlreadyExists = false
            , long initialValue = 0
            , AggregationTypes temporalAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
            , AggregationTypes spatialAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
            , AggregationTypes preferredTemporalAggregationType = AggregationTypes.Average
            , AggregationTypes preferredSpatialAggregationType = AggregationTypes.Average
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
    }
    /// <summary>
    /// An enumeration of ways to aggregate statistic data.
    /// </summary>
    [Flags]
    public enum AggregationTypes
    {
        /// <summary>
        /// The aggregation should sum the values.  Any statistics that use <see cref="IAmbientStatistic.Increment"/> or <see cref="IAmbientStatistic.Add"/> should probably use this type of aggregation.
        /// </summary>
        None = 0,
        /// <summary>
        /// The aggregation should sum the values.  Any statistics that use <see cref="IAmbientStatistic.Increment"/> or <see cref="IAmbientStatistic.Add"/> should probably use this type of aggregation.
        /// </summary>
        Sum = 1,
        /// <summary>
        /// The aggregation should average the values.  Statistics that use <see cref="IAmbientStatistic.SetValue"/> might use this type of aggregation.
        /// </summary>
        Average = 2,
        /// <summary>
        /// The aggregation should take the least of the values.  Statistics that use <see cref="IAmbientStatistic.SetMin"/> would likely use this type of aggregation.
        /// </summary>
        Min = 4,
        /// <summary>
        /// The aggregation should take the greatest of the values.  Statistics that use <see cref="IAmbientStatistic.SetMax"/> would likely use this type of aggregation.
        /// </summary>
        Max = 8,
        /// <summary>
        /// The aggregation should take the most recent value.  Statistics that use <see cref="IAmbientStatistic.SetValue"/> might use this type of aggregation.
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
    }
    /// <summary>
    /// An interface that give read access to a single statistic.
    /// Note that many user-facing statistics will naturally be a ratio of the samples of two statistics or the changes in those samples over time.
    /// </summary>
    public interface IAmbientStatisticReader
    {
        /// <summary>
        /// Gets whether or not the statistic is a time-based statistic.  Immutable.
        /// Has no effect on how the internal implementation.
        /// Time-based statistics can be converted into seconds by dividing by <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
        /// </summary>
        bool IsTimeBased { get; }
        /// <summary>
        /// Gets the identifier for the statistic.
        /// The identifier should be a dash-delimited path identifying the data.  Immutable.
        /// </summary>
        string Id { get; }
        /// <summary>
        /// Gets a human-readable description of this statistic.  Immutable.
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Gets the current statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        long SampleValue { get; }
        /// <summary>
        /// The types of aggregation that should be used when aggregating samples over time.
        /// </summary>
        AggregationTypes TemporalAggregationTypes { get; }
        /// <summary>
        /// The types of aggregation that should be used when aggregating samples from different systems.
        /// </summary>
        AggregationTypes SpatialAggregationTypes { get; }
        /// <summary>
        /// The type of aggregation that should be used when aggregating samples over time and only one aggregation can be kept.
        /// </summary>
        AggregationTypes PreferredTemporalAggregationType { get; }
        /// <summary>
        /// The type of aggregation that should be used when aggregating samples from different systems and only one aggregation can be kept.
        /// </summary>
        AggregationTypes PreferredSpatialAggregationType { get; }
        /// <summary>
        /// How missing samples should be handled.
        /// </summary>
        MissingSampleHandling MissingSampleHandling { get; }
    }
    /// <summary>
    /// An interface that gives write access to a single statistic.
    /// </summary>
    public interface IAmbientStatistic : IAmbientStatisticReader, IDisposable
    {
        /// <summary>
        /// Increments the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <returns>The incremented sample value.</returns>
        long Increment();
        /// <summary>
        /// Decrements the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <returns>The decremented sample value.</returns>
        long Decrement();
        /// <summary>
        /// Adds to the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <param name="addend">The amount to add to the statistic sample value.</param>
        /// <returns>The new sample value.</returns>
        long Add(long addend);
        /// <summary>
        /// Sets the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <param name="newValue">The new value to use.</param>
        void SetValue(long newValue);
        /// <summary>
        /// Sets the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <param name="newPossibleMinValue">A value which will be the new sample value if it is smaller than the current sample value.</param>
        /// <returns>The new sample value.</returns>
        long SetMin(long newPossibleMinValue);
        /// <summary>
        /// Sets the statistic sample value.  Thread-safe, possibly interlocked.
        /// </summary>
        /// <param name="newPossibleMaxValue">A value which will be the new sample value if it is larger than the current sample value.</param>
        /// <returns>The new sample value.</returns>
        long SetMax(long newPossibleMaxValue);
    }
}
