using AmbientServices.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace AmbientServices;

[DefaultAmbientService]
internal class BasicAmbientStatistics : IAmbientStatistics
{
    /// <summary>
    /// Gets the ID of the built-in execution time statistic, especially for ratio statistics.
    /// </summary>
    public const string ExecutionTimeStatisticId = "ExecutionTime";

    private readonly ProcessExecutionTimeStatistic _executionTime;
    private readonly ConcurrentDictionary<string, IAmbientStatisticReader> _statistics = new();
    private readonly ConcurrentDictionary<string, IAmbientRatioStatistic> _ratioStatistics = new();

    public BasicAmbientStatistics()
    {
        _executionTime = new ProcessExecutionTimeStatistic(this);
        _statistics.GetOrAdd(_executionTime.Id, _executionTime);
    }

    public IAmbientStatisticReader ExecutionTime => _executionTime;

    public IDictionary<string, IAmbientStatisticReader> Statistics => _statistics;

    public IDictionary<string, IAmbientRatioStatistic> RatioStatistics => _ratioStatistics;

    public IAmbientStatisticReader? ReadStatistic(string id)
    {
        if (_statistics.TryGetValue(id, out IAmbientStatisticReader? statistic)) return statistic;
        return null;
    }

    public IAmbientStatistic GetOrAddStatistic(AmbientStatisticType type, string id, string name, string description, bool replaceIfAlreadyExists
        , long initialRawValue = 0, long? minimumExpectedRawValue = null, long? maximumExpectedRawValue = null
        , string? units = null, double fixedFloatingPointAdjustment = 1.0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        IAmbientStatistic? statistic = new Statistic(this, () => _statistics.TryRemove(id, out _), type, id, name, description, initialRawValue, minimumExpectedRawValue, maximumExpectedRawValue, units, fixedFloatingPointAdjustment, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
        if (replaceIfAlreadyExists)
        {
            _statistics.AddOrUpdate(id, statistic, (k, v) => statistic);
        }
        else
        {
            statistic = _statistics.GetOrAdd(id, statistic) as IAmbientStatistic;   // this *could* return something that is only an IAmbientStatisticReader!
            if (statistic == null) throw new InvalidOperationException("The specified statistic identifier is already in use by a read-only statistic!");
        }
        return statistic;
    }
    public IAmbientStatistic GetOrAddTimeBasedStatistic(AmbientStatisticType type, string id, string name, string description, bool replaceIfAlreadyExists = false
        , long initialRawValue = 0, long? minimumExpectedRawValue = null, long? maximumExpectedRawValue = null
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        return GetOrAddStatistic(type, id, name, description, replaceIfAlreadyExists, initialRawValue, minimumExpectedRawValue, maximumExpectedRawValue, 
            "seconds", Stopwatch.Frequency, 
            temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
    }
    public bool RemoveStatistic(string id)
    {
        if (id == _executionTime.Id) return false;
        return _statistics.TryRemove(id, out _);
    }
    public IAmbientRatioStatistic GetOrAddRatioStatistic(string id, string name, string description, bool replaceIfAlreadyExists = false, string? units = null
        , string? numeratorStatistic = null, bool numeratorDelta = true
        , string? denominatorStatistic = null, bool denominatorDelta = true
        )
    {
        // no units specified?
        if (units == null)
        {
            // is there no denominator?
            if ((denominatorStatistic == null) && numeratorStatistic != null && _statistics.TryGetValue(numeratorStatistic, out IAmbientStatisticReader? numeratorStat))
            {
                // just use the numerator units
                units = numeratorStat.AdjustedUnits;
            } 
            // is there both a numerator and a denominator?
            else if (denominatorStatistic != null && numeratorStatistic != null && _statistics.TryGetValue(numeratorStatistic, out numeratorStat) && _statistics.TryGetValue(denominatorStatistic, out IAmbientStatisticReader? denominatorStat))
            {
                // use "numerator units / denominator units"
                units = $"{numeratorStat.AdjustedUnits}/{denominatorStat.AdjustedUnits}";
            }
        }
        IAmbientRatioStatistic? statistic = new RatioStatistic(this, () => _ratioStatistics.TryRemove(id, out _), id, name, description, units, numeratorStatistic, numeratorDelta, denominatorStatistic, denominatorDelta);
        if (replaceIfAlreadyExists)
        {
            _ratioStatistics.AddOrUpdate(id, statistic, (k, v) => statistic);
        }
        else
        {
            statistic = _ratioStatistics.GetOrAdd(id, statistic);
        }
        return statistic;
    }
    public bool RemoveRatioStatistic(string id)
    {
        return _ratioStatistics.TryRemove(id, out _);
    }
}

internal class Statistic : IAmbientStatistic
{
    private readonly Action _removeRegistration;
    private long _currentValue;    // interlocked

    public Statistic(IAmbientStatistics statisticsSet, Action removeRegistration, AmbientStatisticType type, string id, string name, string description
        , long initialRawValue = 0, long? expectedMinRawValue = null, long? expectedMaxRawValue = null
        , string? units = null, double fixedFloatingPointAdjustment = 1.0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        StatisticsSet = statisticsSet;
        _removeRegistration = removeRegistration;
        StatisicType = type;
        Id = id;
        Name = name;
        Description = description;
        AdjustedUnits = units;
        _currentValue = initialRawValue;
        ExpectedMinimumRawValue = expectedMinRawValue;
        ExpectedMaximumRawValue = expectedMaxRawValue;
        FixedFloatingPointAdjustment = (fixedFloatingPointAdjustment == 0) ? 1.0 : fixedFloatingPointAdjustment;    // don't allow zero for the floating point adjustment
        TemporalAggregationTypes = temporalAggregationTypes == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultTemporalAggregation(type) : temporalAggregationTypes;
        SpatialAggregationTypes = spatialAggregationTypes == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultSpatialAggregation(type) : spatialAggregationTypes;
        PreferredTemporalAggregationType = preferredTemporalAggregationType == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultTemporalAggregation(type) : preferredTemporalAggregationType;
        PreferredSpatialAggregationType = preferredSpatialAggregationType == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultSpatialAggregation(type) : preferredSpatialAggregationType;
        MissingSampleHandling = missingSampleHandling;
    }

    public IAmbientStatistics StatisticsSet { get; }

    public AmbientStatisticType StatisicType { get; }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string? AdjustedUnits { get; }

    public long CurrentRawValue => _currentValue;

    public long? ExpectedMinimumRawValue { get; private set; }

    public long? ExpectedMaximumRawValue { get; private set; }

    public double FixedFloatingPointAdjustment { get; private set;}

    public AggregationTypes TemporalAggregationTypes { get; private set; }

    public AggregationTypes SpatialAggregationTypes { get; private set; }

    public AggregationTypes PreferredTemporalAggregationType { get; private set; }

    public AggregationTypes PreferredSpatialAggregationType { get; private set; }

    public MissingSampleHandling MissingSampleHandling { get; private set; }

    public long IncrementRaw()
    {
        return System.Threading.Interlocked.Increment(ref _currentValue);
    }
    public long DecrementRaw()
    {
        return System.Threading.Interlocked.Decrement(ref _currentValue);
    }
    public long AddRaw(long addend)
    {
        return System.Threading.Interlocked.Add(ref _currentValue, addend);
    }
    public void SetRawValue(long newValue)
    {
        System.Threading.Interlocked.Exchange(ref _currentValue, newValue);
    }
    public long SetRawMin(long newPossibleMinValue)
    {
        return InterlockedUtilities.TryOptomisticMin(ref _currentValue, newPossibleMinValue);
    }
    public long SetRawMax(long newPossibleMaxValue)
    {
        return InterlockedUtilities.TryOptomisticMax(ref _currentValue, newPossibleMaxValue);
    }
    public void Dispose()
    {
        _removeRegistration();
    }
}

internal class ProcessExecutionTimeStatistic : IAmbientStatisticReader
{
    private readonly long _startTime;

    public ProcessExecutionTimeStatistic(IAmbientStatistics statisticsSet)
    {
        StatisticsSet = statisticsSet;
        _startTime = AmbientClock.Ticks;
    }

    public IAmbientStatistics StatisticsSet { get; }

    public AmbientStatisticType StatisicType => AmbientStatisticType.Cumulative;

    public string Id => "ExecutionTime";

    public string Name => "Execution Time";

    public string Description => "The number of seconds elapsed since the statistics system started.";

    public string? AdjustedUnits => "seconds";

    public long CurrentRawValue => AmbientClock.Ticks - _startTime;

    public long? ExpectedMinimumRawValue => 0;

    public long? ExpectedMaximumRawValue => null;

    public double FixedFloatingPointAdjustment => Stopwatch.Frequency;

    public AggregationTypes TemporalAggregationTypes => AggregationTypes.MostRecent | AggregationTypes.Min | AggregationTypes.Max;

    public AggregationTypes SpatialAggregationTypes => AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max;

    public AggregationTypes PreferredTemporalAggregationType => AggregationTypes.MostRecent;

    public AggregationTypes PreferredSpatialAggregationType => AggregationTypes.Average;

    public MissingSampleHandling MissingSampleHandling => MissingSampleHandling.LinearEstimation;
}

internal sealed class RatioStatistic : IAmbientRatioStatistic
{
    private readonly Action _removeRegistration;
    private readonly string? _numeratorStatistic;

    public RatioStatistic(IAmbientStatistics statisticsSet, Action removeRegistration, string id, string name, string description, string? units = null
        , string? numeratorStatistic = null, bool numeratorDelta = true
        , string? denominatorStatistic = null, bool denominatorDelta = true
        )
    {
        StatisticsSet = statisticsSet;
        _removeRegistration = removeRegistration;
        Id = id;
        Name = name;
        Description = description;
        AdjustedUnits = units;
        _numeratorStatistic = numeratorStatistic;
        NumeratorDelta = numeratorDelta;
        DenominatorStatisticId = denominatorStatistic;
        DenominatorDelta = denominatorDelta;
    }

    public IAmbientStatistics StatisticsSet { get; }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string? AdjustedUnits { get; }

    public string? NumeratorStatisticId => _numeratorStatistic;

    public bool NumeratorDelta { get; }

    public string? DenominatorStatisticId { get; }

    public bool DenominatorDelta { get; }

    public void Dispose()
    {
        _removeRegistration();
    }
}
