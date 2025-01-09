#if NET5_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

[DefaultAmbientService]
internal class BasicAmbientCostTracker : IAmbientCostTracker
{
    private readonly ConcurrentHashSet<IAmbientCostTrackerNotificationSink> _notificationSinks = new();

    public BasicAmbientCostTracker()
    {
    }

    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // call all the notification sinks
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnChargesAccrued(serviceId, customerId, charge);
        }
    }
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in coste (in picodollars per minute).</param> 
    public void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth)
    {
        // call all the notification sinks
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnOngoingCostChanged(serviceId, customerId, changePerMonth);
        }
    }
    /// <summary>
    /// Registers a cost tracker notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    public bool RegisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    /// <summary>
    /// Deregisters a cost tracker notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    public bool DeregisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}

/// <summary>
/// A class that tracks service profile statistics across multiple call contexts in a process or a single time window.
/// </summary>
internal class ProcessOrSingleTimeWindowCostTracker : IAmbientAccruedChargesAndCostChanges, IAmbientCostTrackerNotificationSink, IDisposable
{
    private readonly IAmbientCostTracker _profiler;
    private readonly ConcurrentDictionary<string, ChargeAccumulator> _chargeAccumulatorsByService = new();
    private readonly ConcurrentDictionary<string, ChargeAccumulator> _chargeAccumulatorsByCustomer = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _costAccumulatorsByService = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _costAccumulatorsByCustomer = new();
    private int _chargeCount;       // interlocked
    private int _costChangeCount;   // interlocked
    private long _totalCharges;     // interlocked
    private long _totalCostChange;  // interlocked
    private bool _disposedValue;

    public string ScopeName { get; }

    public int ChargeCount => _chargeCount;
    public long AccumulatedChargeSum => _totalCharges;
    public int CostChangeCount => _costChangeCount;
    public long AccumulatedCostChangeSum => _totalCostChange;

    public ProcessOrSingleTimeWindowCostTracker(IAmbientCostTracker metrics, string scopeName)
    {
        _profiler = metrics;
        ScopeName = scopeName;
        _profiler.RegisterCostTrackerNotificationSink(this);
    }
    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // track the charges per service
        ChargeAccumulator.Accrue(_chargeAccumulatorsByService, serviceId, charge);
        // track the charges per customer
        ChargeAccumulator.Accrue(_chargeAccumulatorsByCustomer, customerId, charge);
        // track the total cost
        Interlocked.Add(ref _totalCharges, charge);
        // track the number of charges
        Interlocked.Increment(ref _chargeCount);
    }
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
    public void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth)
    {
        // track the cost changes per service
        CostAccumulator.ChangeCost(_costAccumulatorsByService, serviceId, changePerMonth);
        // track the cost changes per customer
        CostAccumulator.ChangeCost(_costAccumulatorsByCustomer, customerId, changePerMonth);
        // track the total cost
        Interlocked.Add(ref _totalCostChange, changePerMonth);
        // track the number of charges
        Interlocked.Increment(ref _costChangeCount);
    }
    internal void CloseTracking()
    {
        _profiler.DeregisterCostTrackerNotificationSink(this);
        _disposedValue = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _profiler.DeregisterCostTrackerNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ProcessingDistributionAccumulator()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

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

internal class ScopeOnChargesAccruedDistributor : IAmbientCostTrackerNotificationSink
{
    private readonly ConcurrentHashSet<IAmbientCostTrackerNotificationSink> _notificationSinks = new();
    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnChargesAccrued(serviceId, customerId, charge);
        }
    }
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
    public void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth)
    {
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnOngoingCostChanged(serviceId, customerId, changePerMonth);
        }
    }
    public bool RegisterSystemSwitchedNotificationSink(IAmbientCostTrackerNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterSystemSwitchedNotificationSink(IAmbientCostTrackerNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}

/// <summary>
/// A class that tracks service profile statistics for a specific call context.
/// </summary>
internal class CallContextCostTracker : IAmbientAccruedChargesAndCostChanges, IAmbientCostTrackerNotificationSink, IDisposable
{
    private readonly ScopeOnChargesAccruedDistributor _distributor;
    private readonly ConcurrentDictionary<string, ChargeAccumulator> _accumulatorsByService = new();
    private readonly ConcurrentDictionary<string, ChargeAccumulator> _accumulatorsByCustomer = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _costAccumulatorsByService = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _costAccumulatorsByCustomer = new();
    private int _chargeCount;       // interlocked
    private int _costChangeCount;   // interlocked
    private long _totalCharges;     // interlocked
    private long _totalCostChange;  // interlocked
    private bool _disposedValue;

    public string ScopeName { get; }
    public int ChargeCount => _chargeCount;
    public long AccumulatedChargeSum => _totalCharges;
    public int CostChangeCount => _costChangeCount;
    public long AccumulatedCostChangeSum => _totalCostChange;

    /// <summary>
    /// Constructs a CallContextCostTracker.
    /// </summary>
    /// <param name="distributor">A <see cref="ScopeOnChargesAccruedDistributor"/> to hook into to receive system change events.</param>
    /// <param name="scopeName">The name of the call contxt being tracked.</param>
    public CallContextCostTracker(ScopeOnChargesAccruedDistributor distributor, string scopeName)
    {
        _distributor = distributor;
        ScopeName = scopeName;
        distributor.RegisterSystemSwitchedNotificationSink(this);
    }
    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // track the charges per service
        _accumulatorsByService[serviceId] = new(charge);
        // track the charges per customer
        _accumulatorsByCustomer[customerId] = new(charge);
        // track the total charges
        Interlocked.Add(ref _totalCharges, charge);
        // track the number of charges
        Interlocked.Increment(ref _chargeCount);
    }
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
    public void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth)
    {
        // track the cost change per service
        _costAccumulatorsByService[serviceId] = new(changePerMonth);
        // track the cost change per customer
        _costAccumulatorsByCustomer[customerId] = new(changePerMonth);
        // track the total cost change
        Interlocked.Add(ref _totalCostChange, changePerMonth);
        // track the number of charges
        Interlocked.Increment(ref _costChangeCount);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _distributor.DeregisterSystemSwitchedNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~CallContextCostTracker()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

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
/// A class that tracks service profile statistics for a moving time window.
/// </summary>
internal class TimeWindowCostTracker : IDisposable
{
    private readonly string _scopeNamePrefix;
    private readonly AmbientEventTimer _timeWindowRotator;
    private ProcessOrSingleTimeWindowCostTracker? _timeWindowCallContextCollector;  // interlocked
    private bool _disposedValue;

    /// <summary>
    /// Constructs a TimeWindowProcessingDistributionTracker.
    /// </summary>
    /// <param name="metrics">A <see cref="IAmbientCostTracker"/> to hook into to receive processor change events.</param>
    /// <param name="scopeNamePrefix">A <see cref="TimeSpan"/> indicating the size of the window.</param>
    /// <param name="windowPeriod">A <see cref="TimeSpan"/> indicating how often reports are desired.</param>
    /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientAccruedChargesAndCostChanges"/> at the end of each time window.</param>
    public TimeWindowCostTracker(IAmbientCostTracker metrics, string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientAccruedChargesAndCostChanges, Task> onWindowComplete)
    {
        if (onWindowComplete == null) throw new ArgumentNullException(nameof(onWindowComplete), "Time Window Collection is pointless without a completion delegate!");
        _scopeNamePrefix = scopeNamePrefix;
        using (Rotate(metrics, windowPeriod)) { }
        _timeWindowRotator = new AmbientEventTimer(windowPeriod);
        _timeWindowRotator.Elapsed +=
            (sender, handler) =>
            {
                ProcessOrSingleTimeWindowCostTracker? oldAccumulator = Rotate(metrics, windowPeriod);
                if (oldAccumulator != null)
                {
                    Task t = onWindowComplete(oldAccumulator);
                    t.Wait();
                }
            };
        _timeWindowRotator.AutoReset = true;
        _timeWindowRotator.Enabled = true;
    }

    private ProcessOrSingleTimeWindowCostTracker? Rotate(IAmbientCostTracker metrics, TimeSpan windowPeriod)
    {
        string windowName = WindowScope.WindowId(AmbientClock.UtcNow, windowPeriod);
        string newAccumulatorScopeName = _scopeNamePrefix + windowName + "(" + WindowScope.WindowSize(windowPeriod) + ")"; ;
        ProcessOrSingleTimeWindowCostTracker newAccumulator = new(metrics, newAccumulatorScopeName);
        ProcessOrSingleTimeWindowCostTracker? oldAccumulator = Interlocked.Exchange(ref _timeWindowCallContextCollector, newAccumulator);
        oldAccumulator?.CloseTracking();
        return oldAccumulator;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _timeWindowRotator.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ScopeProcessingDistributionTracker()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

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
/// A class that accumulates charges for some scope.
/// </summary>
public class ChargeAccumulator
{
    private long _chargeCount;      // interlocked
    private long _totalCharges;     // interlocked

    /// <summary>
    /// Constructs a charge accumulator.
    /// </summary>
    /// <param name="charge">The initial charge.</param>
    public ChargeAccumulator(long charge)
    {
        _chargeCount = 1;
        _totalCharges = charge;
    }

    internal static void Accrue(ConcurrentDictionary<string, ChargeAccumulator> chargeAccumulators, string key, long charge)
    {
        chargeAccumulators.AddOrUpdate(key, new ChargeAccumulator(charge), (k, v) => { v.AddCharge(charge); return v; });
    }

    /// <summary>
    /// Adds a charge to the accumulator.
    /// </summary>
    /// <param name="charge">The charge amount (in picodollars).</param>
    public void AddCharge(long charge)
    {
        Interlocked.Increment(ref _chargeCount);
        Interlocked.Add(ref _totalCharges, charge);
    }
}
/// <summary>
/// A class that accumulates cost for some scope.
/// </summary>
public class CostAccumulator
{
    private long _chargeCount;                  // interlocked
    private long _totalCostPerMinuteChange;     // interlocked

    /// <summary>
    /// Constructs a cost accumulator.
    /// </summary>
    /// <param name="costPerMinuteChange">The initial change in cost.</param>
    public CostAccumulator(long costPerMinuteChange)
    {
        _chargeCount = 1;
        _totalCostPerMinuteChange = costPerMinuteChange;
    }

    internal static void ChangeCost(ConcurrentDictionary<string, CostAccumulator> chargeAccumulators, string key, long costPerMinuteChange)
    {
        chargeAccumulators.AddOrUpdate(key, new CostAccumulator(costPerMinuteChange), (k, v) => { v.AddCostChange(costPerMinuteChange); return v; });
    }

    /// <summary>
    /// Adds a cost changes to the accumulator.
    /// </summary>
    /// <param name="costPerMinuteChange">The cost change (in picodollars per minute).</param>
    public void AddCostChange(long costPerMinuteChange)
    {
        Interlocked.Increment(ref _chargeCount);
        Interlocked.Add(ref _totalCostPerMinuteChange, costPerMinuteChange);
    }
}
#endif
