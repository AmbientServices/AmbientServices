using AmbientServices.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AmbientServices;

[DefaultAmbientService]
internal class BasicAmbientStatistics : IAmbientStatistics
{
    private readonly ProcessExecutionTimeStatistic _executionTime;
    private readonly ConcurrentDictionary<string, IAmbientStatisticReader> _statistics = new();
    private readonly ConcurrentDictionary<string, IAmbientRatioStatistic> _ratioStatistics = new();

    public BasicAmbientStatistics()
    {
        _executionTime = new ProcessExecutionTimeStatistic();
        _statistics.GetOrAdd(_executionTime.Id, _executionTime);
    }

    public IDictionary<string, IAmbientStatisticReader> Statistics => _statistics;

    public IDictionary<string, IAmbientRatioStatistic> RatioStatistics => _ratioStatistics;

    public IAmbientStatisticReader? ReadStatistic(string id)
    {
        if (_statistics.TryGetValue(id, out IAmbientStatisticReader? statistic)) return statistic;
        return null;
    }

    public IAmbientStatistic GetOrAddStatistic(bool timeBased, AmbientStatisicType type, string id, string name, string description, bool resetIfAlreadyExists, string? units = null
        , long initialValue = 0, long? minimumValue = null, long? maximumValue = null, short fixedFlotingPointDigits = 0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        IAmbientStatistic? statistic;
        if (resetIfAlreadyExists)
        {
            statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, type, id, name, description, units, initialValue, minimumValue, maximumValue, fixedFlotingPointDigits, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
            _statistics.AddOrUpdate(id, statistic, (k, v) => statistic);
        }
        else
        {
            statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, type, id, name, description, units, initialValue, minimumValue, maximumValue, fixedFlotingPointDigits, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
            statistic = _statistics.GetOrAdd(id, statistic) as IAmbientStatistic;   // this *could* return something that is only an IAmbientStatisticReader!
            if (statistic == null) throw new InvalidOperationException("The specified statistic identifier is already in use by a read-only statistic!");
        }
        return statistic;
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
        IAmbientRatioStatistic? statistic;
        if (replaceIfAlreadyExists)
        {
            statistic = new RatioStatistic(() => _ratioStatistics.TryRemove(id, out _), id, name, description, units
                , numeratorStatistic, numeratorDelta, denominatorStatistic, denominatorDelta);
            _ratioStatistics.AddOrUpdate(id, statistic, (k, v) => statistic);
        }
        else
        {
            statistic = new RatioStatistic(() => _ratioStatistics.TryRemove(id, out _), id, name, description, units
                , numeratorStatistic, numeratorDelta, denominatorStatistic, denominatorDelta);
            statistic = _ratioStatistics.GetOrAdd(id, statistic);
            if (statistic == null) throw new InvalidOperationException("The specified ratio statistic identifier is already in us!");
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
    private readonly bool _timeBased;
    private readonly AmbientStatisicType _type;
    private readonly string _id;
    private readonly string _name;
    private readonly string _description;
    private readonly string? _units;
    private long _currentValue;    // interlocked

    public Statistic(Action removeRegistration, bool timeBased, AmbientStatisicType type, string id, string name, string description, string? units = null
        , long initialValue = 0, long? expectedMinValue = null, long? expectedMaxValue = null, short fixedFloatingPointDigits = 0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.None
        , AggregationTypes spatialAggregationTypes = AggregationTypes.None
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.None
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.None
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        _removeRegistration = removeRegistration;
        _timeBased = timeBased;
        _type = type;
        _id = id;
        _name = name;
        _description = description;
        _units = units;
        _currentValue = initialValue;
        ExpectedMin = expectedMinValue;
        ExpectedMax = expectedMaxValue;
        FixedFloatingPointDigits = fixedFloatingPointDigits;
        FixedFloatingPointAdjustment = TenPow(fixedFloatingPointDigits);
        TemporalAggregationTypes = temporalAggregationTypes == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultTemporalAggregation(type) : temporalAggregationTypes;
        SpatialAggregationTypes = spatialAggregationTypes == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultSpatialAggregation(type) : spatialAggregationTypes;
        PreferredTemporalAggregationType = preferredTemporalAggregationType == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultTemporalAggregation(type) : preferredTemporalAggregationType;
        PreferredSpatialAggregationType = preferredSpatialAggregationType == AggregationTypes.None ? IAmbientStatisticsExtensions.DefaultSpatialAggregation(type) : preferredSpatialAggregationType;
        MissingSampleHandling = missingSampleHandling;
    }
    private static long TenPow(int pow)
    {
        int ret = 1;
        for (int loop = 0; loop < pow; ++loop) ret *= 10;
        return ret;
    }
    public bool IsTimeBased => _timeBased;

    public AmbientStatisicType StatisicType => _type;

    public string Id => _id;

    public string Name => _name;

    public string Description => _description;

    public string? Units => _units;

    public long CurrentValue => _currentValue;

    public long? ExpectedMin { get; private set; }

    public long? ExpectedMax { get; private set; }

    public short FixedFloatingPointDigits { get; private set; }

    public double FixedFloatingPointAdjustment { get; private set;}

    public AggregationTypes TemporalAggregationTypes { get; private set; }

    public AggregationTypes SpatialAggregationTypes { get; private set; }

    public AggregationTypes PreferredTemporalAggregationType { get; private set; }

    public AggregationTypes PreferredSpatialAggregationType { get; private set; }

    public MissingSampleHandling MissingSampleHandling { get; private set; }

    public long Increment()
    {
        return System.Threading.Interlocked.Increment(ref _currentValue);
    }
    public long Decrement()
    {
        return System.Threading.Interlocked.Decrement(ref _currentValue);
    }
    public long Add(long addend)
    {
        return System.Threading.Interlocked.Add(ref _currentValue, addend);
    }
    public void SetValue(long newValue)
    {
        System.Threading.Interlocked.Exchange(ref _currentValue, newValue);
    }
    public long SetMin(long newPossibleMinValue)
    {
        return InterlockedUtilities.TryOptomisticMin(ref _currentValue, newPossibleMinValue);
    }
    public long SetMax(long newPossibleMaxValue)
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

    public ProcessExecutionTimeStatistic()
    {
        _startTime = AmbientClock.Ticks;
    }

    public bool IsTimeBased => true;

    public AmbientStatisicType StatisicType => AmbientStatisicType.Cumulative;

    public string Id => "ExecutionTime";

    public string Name => "Execution Time";

    public string Description => "The number of ticks elapsed since the statistics system started.";

    public string? Units => "ticks";

    public long CurrentValue => AmbientClock.Ticks - _startTime;

    public long? ExpectedMin => 0;

    public long? ExpectedMax => null;

    public short FixedFloatingPointDigits => 0;
   
    public double FixedFloatingPointAdjustment => 1.0;

    public AggregationTypes TemporalAggregationTypes => AggregationTypes.MostRecent | AggregationTypes.Min | AggregationTypes.Max;

    public AggregationTypes SpatialAggregationTypes => AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max;

    public AggregationTypes PreferredTemporalAggregationType => AggregationTypes.MostRecent;

    public AggregationTypes PreferredSpatialAggregationType => AggregationTypes.Average;

    public MissingSampleHandling MissingSampleHandling => MissingSampleHandling.LinearEstimation;
}

internal class RatioStatistic : IAmbientRatioStatistic
{
    private readonly Action _removeRegistration;
    private readonly string _id;
    private readonly string _name;
    private readonly string _description;
    private readonly string? _units;
    private readonly string? _numeratorStatistic;
    private readonly bool _numeratorDelta;
    private readonly string? _denominatorStatistic;
    private readonly bool _denominatorDelta;

    public RatioStatistic(Action removeRegistration, string id, string name, string description, string? units = null
        , string? numeratorStatistic = null, bool numeratorDelta = true
        , string? denominatorStatistic = null, bool denominatorDelta = true
        )
    {
        _removeRegistration = removeRegistration;
        _id = id;
        _name = name;
        _description = description;
        _units = units;
        _numeratorStatistic = numeratorStatistic;
        _numeratorDelta = numeratorDelta;
        _denominatorStatistic = denominatorStatistic;
        _denominatorDelta = denominatorDelta;
    }
    public string Id => _id;

    public string Name => _name;

    public string Description => _description;

    public string? Units => _units;

    public string? NumeratorStatistic => _numeratorStatistic;

    public bool NumeratorDelta => _numeratorDelta;

    public string? DenominatorStatistic => _denominatorStatistic;

    public bool DenominatorDelta => _denominatorDelta;

    public void Dispose()
    {
        _removeRegistration();
    }
}
