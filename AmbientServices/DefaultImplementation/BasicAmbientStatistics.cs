using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AmbientServices
{
    [DefaultAmbientService]
    class BasicAmbientStatistics : IAmbientStatistics
    {
        private readonly ProcessExecutionTimeStatistic _executionTime;
        private readonly ConcurrentDictionary<string, IAmbientStatisticReader> _statistics = new ConcurrentDictionary<string, IAmbientStatisticReader>();

        public BasicAmbientStatistics()
        {
            _executionTime = new ProcessExecutionTimeStatistic();
            _statistics = new ConcurrentDictionary<string, IAmbientStatisticReader>();
            _statistics.GetOrAdd(_executionTime.Id, _executionTime);
        }

        public IDictionary<string, IAmbientStatisticReader> Statistics => _statistics;

        public IAmbientStatisticReader? ReadStatistic(string id)
        {
            IAmbientStatisticReader? statistic;
            if (_statistics.TryGetValue(id, out statistic)) return statistic;
            return null;
        }

        public IAmbientStatistic GetOrAddStatistic(bool timeBased, string id, string description, bool resetIfAlreadyExists
            , long initialValue = 0
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
                statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, id, description, initialValue, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
                _statistics.AddOrUpdate(id, statistic, (k, v) => statistic);
            }
            else
            {
                statistic = new Statistic(() => _statistics.TryRemove(id, out _), timeBased, id, description, initialValue, temporalAggregationTypes, spatialAggregationTypes, preferredTemporalAggregationType, preferredSpatialAggregationType, missingSampleHandling);
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
    class Statistic : IAmbientStatistic
    {
        private readonly Action _removeRegistration;
        private readonly bool _timeBased;
        private readonly string _id;
        private readonly string _description;
        private long _sampleValue;    // interlocked

        public Statistic(Action removeRegistration, bool timeBased, string id, string description, long initialValue = 0
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
            _sampleValue = initialValue;
            TemporalAggregationTypes = temporalAggregationTypes;
            SpatialAggregationTypes = spatialAggregationTypes;
            PreferredTemporalAggregationType = preferredTemporalAggregationType;
            PreferredSpatialAggregationType = preferredSpatialAggregationType;
            MissingSampleHandling = missingSampleHandling;
        }
        public bool IsTimeBased => _timeBased;

        public string Id => _id;

        public string Description => _description;

        public long SampleValue => _sampleValue;

        public AggregationTypes TemporalAggregationTypes { get; private set; }

        public AggregationTypes SpatialAggregationTypes { get; private set; }

        public AggregationTypes PreferredTemporalAggregationType { get; private set; }

        public AggregationTypes PreferredSpatialAggregationType { get; private set; }

        public MissingSampleHandling MissingSampleHandling { get; private set; }

        public long Increment()
        {
            return System.Threading.Interlocked.Increment(ref _sampleValue);
        }
        public long Add(long addend)
        {
            return System.Threading.Interlocked.Add(ref _sampleValue, addend);
        }
        public void SetValue(long newValue)
        {
            System.Threading.Interlocked.Exchange(ref _sampleValue, newValue);
        }
        public long SetMin(long newPossibleMinValue)
        {
            return InterlockedExtensions.TryOptomisticMin(ref _sampleValue, newPossibleMinValue);
        }
        public long SetMax(long newPossibleMaxValue)
        {
            return InterlockedExtensions.TryOptomisticMax(ref _sampleValue, newPossibleMaxValue);
        }
        public void Dispose()
        {
            _removeRegistration();
        }
    }

    class ProcessExecutionTimeStatistic : IAmbientStatisticReader
    {
        private readonly long _startTime;

        public ProcessExecutionTimeStatistic()
        {
            _startTime = AmbientClock.Ticks;
        }

        public bool IsTimeBased => true;

        public string Id => "ExecutionTime";

        public string Description => "The number of ticks elapsed since the statistics system started.";

        public long SampleValue => AmbientClock.Ticks - _startTime;

        public AggregationTypes TemporalAggregationTypes => AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max;

        public AggregationTypes SpatialAggregationTypes => AggregationTypes.Min | AggregationTypes.Average | AggregationTypes.Max;

        public AggregationTypes PreferredTemporalAggregationType => AggregationTypes.Average;

        public AggregationTypes PreferredSpatialAggregationType => AggregationTypes.Average;

        public MissingSampleHandling MissingSampleHandling => MissingSampleHandling.LinearEstimation;
    }
}
