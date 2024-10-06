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

    public BasicAmbientStatistics()
    {
        _executionTime = new ProcessExecutionTimeStatistic();
        _statistics = new ConcurrentDictionary<string, IAmbientStatisticReader>();
        _statistics.GetOrAdd(_executionTime.Id, _executionTime);
    }

    public IDictionary<string, IAmbientStatisticReader> Statistics => _statistics;

    public IAmbientStatisticReader? ReadStatistic(string id)
    {
        if (_statistics.TryGetValue(id, out IAmbientStatisticReader? statistic)) return statistic;
        return null;
    }

    public IAmbientStatistic GetOrAddStatistic(bool timeBased, string id, string description, bool resetIfAlreadyExists
        , long initialValue = 0, long? minimumValue = null, long? maximumValue = null, short fixedFlotingPointDigits = 0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
        , AggregationTypes spatialAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.Average
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.Average
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        IAmbientStatistic? statistic;
        if (resetIfAlreadyExists)
        {
            statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, id, description, initialValue, minimumValue, maximumValue, fixedFlotingPointDigits, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
            _statistics.AddOrUpdate(id, statistic, (k, v) => statistic);
        }
        else
        {
            statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, id, description, initialValue, minimumValue, maximumValue, fixedFlotingPointDigits, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
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
}

internal class Statistic : IAmbientStatistic
{
    private readonly Action _removeRegistration;
    private readonly bool _timeBased;
    private readonly string _id;
    private readonly string _description;
    private long _currentValue;    // interlocked

    public Statistic(Action removeRegistration, bool timeBased, string id, string description
        , long initialValue = 0, long? expectedMinValue = null, long? expectedMaxValue = null, short fixedFloatingPointDigits = 0
        , AggregationTypes temporalAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
        , AggregationTypes spatialAggregationTypes = AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max
        , AggregationTypes preferredTemporalAggregationType = AggregationTypes.Average
        , AggregationTypes preferredSpatialAggregationType = AggregationTypes.Average
        , MissingSampleHandling missingSampleHandling = MissingSampleHandling.LinearEstimation
        )
    {
        _removeRegistration = removeRegistration;
        _timeBased = timeBased;
        _id = id;
        _description = description;
        _currentValue = initialValue;
        ExpectedMin = expectedMinValue;
        ExpectedMax = expectedMaxValue;
        FixedFloatingPointDigits = fixedFloatingPointDigits;
        FixedFloatingPointMultiplier = TenPow(fixedFloatingPointDigits);
        TemporalAggregationTypes = temporalAggregationTypes;
        SpatialAggregationTypes = spatialAggregationTypes;
        PreferredTemporalAggregationType = preferredTemporalAggregationType;
        PreferredSpatialAggregationType = preferredSpatialAggregationType;
        MissingSampleHandling = missingSampleHandling;
    }
    private static long TenPow(int pow)
    {
        int ret = 1;
        for (int loop = 0; loop < pow; ++loop) ret *= 10;
        return ret;
    }
    public bool IsTimeBased => _timeBased;

    public string Id => _id;

    public string Description => _description;

    public long CurrentValue => _currentValue;

    public long? ExpectedMin { get; private set; }

    public long? ExpectedMax { get; private set; }

    public short FixedFloatingPointDigits { get; private set; }

    public long FixedFloatingPointMultiplier { get; private set;}

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

    public string Id => "ExecutionTime";

    public string Description => "The number of ticks elapsed since the statistics system started.";

    public long CurrentValue => AmbientClock.Ticks - _startTime;

    public long? ExpectedMin => 0;

    public long? ExpectedMax => null;

    public short FixedFloatingPointDigits => 0;
   
    public long FixedFloatingPointMultiplier => 1;

    public AggregationTypes TemporalAggregationTypes => AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max;

    public AggregationTypes SpatialAggregationTypes => AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max;

    public AggregationTypes PreferredTemporalAggregationType => AggregationTypes.MostRecent;

    public AggregationTypes PreferredSpatialAggregationType => AggregationTypes.Average;

    public MissingSampleHandling MissingSampleHandling => MissingSampleHandling.LinearEstimation;
}
