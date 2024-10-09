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
    /// <param name="name">A name for the statistic, presumably to use as a chart title.</param>
    /// <param name="description">A human-readable description for the statistic.</param>
    /// <param name="replaceIfAlreadyExists">true to use a new statistic even if one already exists, false to return an existing statistic if one already exists.  Default is false.</param>
    /// <param name="units">An optional string describing the units of the statistic (after the decimal point is adjusted by <paramref name="fixedFlotingPointDigits"/>).</param>
    /// <param name="initialValue">The initial value for the statistic, if it is created.</param>
    /// <param name="minimumValue">An optional value indicating the minimum possible value, if applicable.</param>
    /// <param name="maximumValue">An optional value indicating the maximum possible value, if applicable.</param>
    /// <param name="fixedFlotingPointDigits">An optional value indicating how many digits past the decimal are always included in the sample value.</param>
    /// <param name="temporalAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated over time.</param>
    /// <param name="spatialAggregationTypes">A set of <see cref="AggregationTypes"/> indicating how this statistic should be aggregated across systems.</param>
    /// <param name="preferredTemporalAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated over time.</param>
    /// <param name="preferredSpatialAggregationType">A single <see cref="AggregationTypes"/> indicating the default way this statistic should be aggregated across systems.</param>
    /// <param name="missingSampleHandling">A <see cref="MissingSampleHandling"/> indicating how clients should treat missing samples from this statistic.</param>
    /// <returns>An <see cref="IAmbientStatistic"/> the caller can use to update the statistic samples.</returns>
    IAmbientStatistic GetOrAddStatistic(bool timeBased, string id, string name, string description, bool replaceIfAlreadyExists = false, string? units = null
        , long initialValue = 0, long? minimumValue = null, long? maximumValue = null, short fixedFlotingPointDigits = 0
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
    /// Gets whether or not the statistic is a time-based statistic.  Immutable.
    /// Has no effect on the internal implementation.
    /// Time-based statistics can be converted into seconds by dividing by <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    bool IsTimeBased { get; }
    /// <summary>
    /// Gets the identifier for the statistic.
    /// The identifier should be a dash-delimited path identifying the data.  Immutable.
    /// </summary>
    string Id { get; }
    /// <summary>
    /// Gets a human-readable name, presumbly for the chart title.  Immutable.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Gets a human-readable description of this statistic.  Immutable.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// Gets the current statistic sample value.  Thread-safe, possibly interlocked.
    /// </summary>
    long CurrentValue { get; }
    /// <summary>
    /// The expected maximum value, if any.  Null if there is no expected maximum.  Immutable.
    /// </summary>
    long? ExpectedMin { get; }
    /// <summary>
    /// The expected minimum value, if any.  Null if there is no expected minimum.  Immutable.
    /// </summary>
    long? ExpectedMax { get; }
    /// <summary>
    /// Gets an optional human-readable units name, presumbly for the y-axis of the chart.  Assumes that the numbers in the axis have already been divided by the FixedFloatingPointMultiplier.  Immutable.
    /// If not specified and a time-based statistic, the units (after dividing by FixedFloatingPointMultiplier) are assumed to be seconds.
    /// </summary>
    string? Units { get; }
    /// <summary>
    /// The number of fixed floating point digits (zero if not applicable).
    /// </summary>
    short FixedFloatingPointDigits { get; }
    /// <summary>
    /// The multiplier for the number of fixed floating point digits (1 if not applicable).
    /// </summary>
    long FixedFloatingPointMultiplier { get; }
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
/// Implementations are disposable, but should not throw exceptions if methods are called after disposal.
/// Disposability is meant to stop reporting results.
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


/// <summary>
/// A class that contains extension methods for various statistics interfaces that add functions to aggregate statistic samples.
/// </summary>
public static class IAmbientStatisticsExtensions
{
    /// <summary>
    /// Sets the statistic sample value.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="statistic">The <see cref="IAmbientStatistic"/> whose value should be set.</param>
    /// <param name="newValue">The new value to use.</param>
    public static void SetValue(this IAmbientStatistic statistic, float newValue)
    {
        if (statistic == null) throw new ArgumentNullException(nameof(statistic));
        statistic.SetValue((long)(Math.Pow(10, statistic.FixedFloatingPointDigits) * newValue));
    }
    /// <summary>
    /// Sets the statistic sample value.  Thread-safe, possibly interlocked.
    /// </summary>
    /// <param name="statistic">The <see cref="IAmbientStatistic"/> whose value should be set.</param>
    /// <param name="newValue">The new value to use.</param>
    public static void SetValue(this IAmbientStatistic statistic, double newValue)
    {
        if (statistic == null) throw new ArgumentNullException(nameof(statistic));
        statistic.SetValue((long)(Math.Pow(10, statistic.FixedFloatingPointDigits) * newValue));
    }
    /// <summary>
    /// Uses the preferred aggregation type to aggregate the samples.
    /// </summary>
    /// <param name="reader">The reader to use to aggregate the samples.</param>
    /// <param name="samples">An enumeration of samples to aggregate.</param>
    /// <returns>The preferred temporally aggregated sample.</returns>
    public static long? PreferredTemporalAggregation(this IAmbientStatisticReader reader, IEnumerable<long?> samples)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        return reader.PreferredTemporalAggregationType switch {
            AggregationTypes.Average => (long?)samples.Average(),
            AggregationTypes.Min => samples.Min() ?? 0,
            AggregationTypes.Max => samples.Max() ?? 0,
            AggregationTypes.MostRecent => samples.LastOrDefault() ?? 0,
            _ => samples.Sum() ?? 0,
        };
    }
    private interface IMissingSampleExtrapolator
    {
        long LeadingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues);
        long MiddleSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int missingValueIndex, int missingValueCount);
        long TrailingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues);
    }
    private class LinearExtrapolator : IMissingSampleExtrapolator
    {
        private static readonly LinearExtrapolator _Instance = new();
        public static LinearExtrapolator Instance => _Instance;

        public long LeadingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return firstNonNullSample - (secondNonNullSample - firstNonNullSample) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1);
        }
        public long MiddleSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int missingValueIndex, int missingValueCount)
        {
            return firstNonNullSample + ((secondNonNullSample - firstNonNullSample) * (missingValueIndex + 1) + ((missingValueCount + 1) / 2)) / (missingValueCount + 1);
        }
        public long TrailingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return secondNonNullSample + (secondNonNullSample - firstNonNullSample) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1);
        }
    }
    private class ExponentialExtrapolator : IMissingSampleExtrapolator
    {
        private static readonly ExponentialExtrapolator _Instance = new();
        public static ExponentialExtrapolator Instance => _Instance;

        public long LeadingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(lowSample) - (Math.Log(highSample) - Math.Log(lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1)));
        }
        public long MiddleSampleExtrapolation(long lowSample, long highSample, int missingValueIndex, int missingValueCount)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(lowSample) + ((Math.Log(highSample) - Math.Log(lowSample)) * (missingValueIndex + 1)) / (missingValueCount + 1)));
        }
        public long TrailingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(highSample) + (Math.Log(highSample) - Math.Log(lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1)));
        }
    }
    private class LogarithmicExtrapolator : IMissingSampleExtrapolator
    {
        private static readonly LogarithmicExtrapolator _Instance = new();
        public static LogarithmicExtrapolator Instance => _Instance;

        public long LeadingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, lowSample) - (Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1)));
        }
        public long MiddleSampleExtrapolation(long lowSample, long highSample, int missingValueIndex, int missingValueCount)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, lowSample) + ((Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) * (missingValueIndex + 1)) / (missingValueCount + 1)));
        }
        public long TrailingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, highSample) + (Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1)));
        }
    }
    /// <summary>
    /// Handle missing samples in the enumerated samples according to the reader's <see cref="IAmbientStatisticReader.MissingSampleHandling"/> enumeration.
    /// </summary>
    /// <param name="missingSamplesHandling">The <see cref="MissingSampleHandling"/> indicating how to handle null values in <paramref name="samples"/>.</param>
    /// <param name="samples">An enumeration of statistical samples.</param>
    /// <returns>An enumeration that has null samples values filled in according to <paramref name="missingSamplesHandling"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is null.</exception>
    public static IEnumerable<long?> HandleMissingSamples(this MissingSampleHandling missingSamplesHandling, IEnumerable<long?> samples)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));
        
        switch (missingSamplesHandling)
        {
            case MissingSampleHandling.Zero:
                foreach (long? sample in samples)
                {
                    yield return sample ?? 0;
                }
                break;
            case MissingSampleHandling.LinearEstimation:            // for leading null samples, extrapolate linarly from the first two non-null samples using MiddleLinerarExtrapolation, for leading samples between two non-null samples, extrapolate linearly using LeadingExtrapolation, for leading samples at the end of the list, extrapolate linearly from the last two non-null samples using TrailingExtrapolation, if there is only one non-null sample, use that same value for every sample, if there are no non-null samples, return a null sample
                foreach (long? sample in Extrapolate(samples, LinearExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            case MissingSampleHandling.ExponentialEstimation:
                foreach (long? sample in Extrapolate(samples, ExponentialExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            case MissingSampleHandling.LogarithmicEstimation:
                foreach (long? sample in Extrapolate(samples, LogarithmicExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            default:
            case MissingSampleHandling.Skip:
                foreach (long? sample in samples)
                {
                    if (sample != null) yield return sample.Value;
                }
                break;
        }
    }

    private static IEnumerable<long?> Extrapolate(IEnumerable<long?> samples, IMissingSampleExtrapolator extrapolator)
    {
        long? previousNonNullSample = null;
        long? lastNonNullSample = null;
        int distanceBetweenPreviousNonNullSampleAndLastNonNullSample = 0;
        int missingSamplesBeforeLastNonNullSample = 0;
        int missingSamplesAfterLastNonNullSample = 0;
        bool needAnotherNonNullSample = true;
        int sampleCount = 0;
        int samplesOutput = 0;
        foreach (long? sample in samples)
        {
            ++sampleCount;
            // do we need another non-null sample before we can return more (extrapolated) samples?
            if (needAnotherNonNullSample)
            {
                // a non-null sample
                if (sample != null)
                {
                    // no null samples before this one?
                    if (missingSamplesBeforeLastNonNullSample == 0)
                    {
                        yield return sample.Value;
                        ++samplesOutput;
                        // save this as a non-null sample, but keep looking for samples
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                        // we're no longer buffering--we hit a non-null sample without null samples preceeding it
                        needAnotherNonNullSample = false;
                    }
                    // was there a non-null sample before this one?
                    else if (lastNonNullSample == null)
                    {
                        // save this as a non-null sample, but keep looking for samples
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                    }
                    else
                    {
                        // were there leading samples before the two non-null samples?
                        for (int offset = 0; offset < missingSamplesBeforeLastNonNullSample; ++offset)
                        {
                            yield return extrapolator.LeadingSampleExtrapolation(lastNonNullSample.Value, sample.Value, missingSamplesAfterLastNonNullSample, offset);
                            ++samplesOutput;
                        }
                        missingSamplesBeforeLastNonNullSample = 0;
                        yield return lastNonNullSample.Value;
                        ++samplesOutput;
                        // were there missing samples since the last non-null sample?
                        for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
                        {
                            yield return extrapolator.MiddleSampleExtrapolation(lastNonNullSample.Value, sample.Value, offset, missingSamplesAfterLastNonNullSample);
                            ++samplesOutput;
                        }
                        distanceBetweenPreviousNonNullSampleAndLastNonNullSample = missingSamplesAfterLastNonNullSample;
                        missingSamplesAfterLastNonNullSample = 0;
                        yield return sample.Value;
                        ++samplesOutput;
                        // save this as the last non-null sample
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                        // we're no longer buffering
                        needAnotherNonNullSample = false;
                    }
                }
                else if (lastNonNullSample != null) ++missingSamplesAfterLastNonNullSample;
                else ++missingSamplesBeforeLastNonNullSample;
            }
            else // we just caught up, so we need to start counting missing samples again, or just output the non-null samples
            {
                System.Diagnostics.Debug.Assert(lastNonNullSample != null);
                if (sample != null)
                {
                    // were there missing samples since the last non-null sample?
                    for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
                    {
                        yield return extrapolator.MiddleSampleExtrapolation(lastNonNullSample!.Value, sample.Value, offset, missingSamplesAfterLastNonNullSample);
                        ++samplesOutput;
                    }
                    distanceBetweenPreviousNonNullSampleAndLastNonNullSample = missingSamplesAfterLastNonNullSample;
                    missingSamplesAfterLastNonNullSample = 0;
                    yield return sample.Value;
                    ++samplesOutput;
                    // save this as the last non-null sample
                    previousNonNullSample = lastNonNullSample;
                    lastNonNullSample = sample;
                }
                else // a missing sample, so just count how many
                {
                    ++missingSamplesAfterLastNonNullSample;
                }
            }
        }
        // were there leading samples missing?
        if (missingSamplesBeforeLastNonNullSample > 0)
        {
            // were they all missing?
            if (lastNonNullSample == null)
            {
                System.Diagnostics.Debug.Assert(missingSamplesBeforeLastNonNullSample == sampleCount);
                System.Diagnostics.Debug.Assert(samplesOutput == 0);
                System.Diagnostics.Debug.Assert(missingSamplesAfterLastNonNullSample == 0);
                // just leave everything null!
                for (int offset = 0; offset < sampleCount; ++offset)
                {
                    yield return null;
                    ++samplesOutput;
                }
            }
            else // we got at least one sample, but since leading samples is still non-zero, we must have had only ONE sample, so use it for everything
            {
                System.Diagnostics.Debug.Assert(missingSamplesBeforeLastNonNullSample + missingSamplesAfterLastNonNullSample == sampleCount - 1);
                System.Diagnostics.Debug.Assert(samplesOutput == 0);
                // use the one non-null sample for all values
                for (int offset = 0; offset < sampleCount; ++offset)
                {
                    yield return lastNonNullSample.Value;
                    ++samplesOutput;
                }
            }
        }
        // were there trailing missing samples?
        else if (missingSamplesAfterLastNonNullSample > 0)
        {
            System.Diagnostics.Debug.Assert(previousNonNullSample != null);
            System.Diagnostics.Debug.Assert(lastNonNullSample != null);
            // loop through the trailing null values
            for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
            {
                yield return extrapolator.TrailingSampleExtrapolation(previousNonNullSample!.Value, lastNonNullSample!.Value, distanceBetweenPreviousNonNullSampleAndLastNonNullSample, offset);
                ++samplesOutput;
            }
        }
        System.Diagnostics.Debug.Assert(samplesOutput == sampleCount);
    }
}
