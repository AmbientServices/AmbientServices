#if NET5_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic default implementation of <see cref="IAmbientCostTracker"/> that broadcasts every cost report to registered sinks.
/// </summary>
/// <remarks>
/// <pitch>The zero-configuration, in-process cost tracker used unless overridden.  Each report costs only a sink fan-out, so it is cheap enough to leave on in production.</pitch>
/// <pledge><see cref="IAmbientCostTracker"/></pledge>
/// <plan>Entirely stateless except for the sink set (a <see cref="ConcurrentHashSet{T}"/>, so registration is idempotent and fan-out is lock-free); charge and ongoing-cost reports are synchronously forwarded verbatim to every registered <see cref="IAmbientCostTrackerNotificationSink"/>.  No accumulation happens here — collectors do that from the broadcast reports.</plan>
/// </remarks>
[DefaultAmbientService]
internal class BasicAmbientCostTracker : IAmbientCostTracker
{
    private readonly ConcurrentHashSet<IAmbientCostTrackerNotificationSink> _notificationSinks = new();

    public BasicAmbientCostTracker()
    {
    }

    /// <summary>
    /// Notifies the notification sink that charges have accrued.
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
    /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
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
/// <remarks>
/// <pitch>The all-contexts cost accumulator: hook it to a cost tracker and it totals every charge and ongoing-cost change reported anywhere in the process until tracking is closed — used directly for process-lifetime totals and as the per-window collector inside <see cref="TimeWindowCostTracker"/>.</pitch>
/// <pledge><see cref="IAmbientAccruedChargesAndCostChanges"/></pledge>
/// <pledge><see cref="IAmbientCostTrackerNotificationSink"/></pledge>
/// <plan>Registers with the <see cref="IAmbientCostTracker"/> at construction and deregisters on disposal (or on the internal close used at window rotation).  Totals and counts are maintained with <see cref="Interlocked"/> adds; per-service and per-customer breakdowns accumulate in <see cref="ConcurrentDictionary{TKey,TValue}"/>s of <see cref="ChargeAccumulator"/>/<see cref="CostAccumulator"/> (currently internal-only — not exposed through the reporting interface).</plan>
/// </remarks>
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
    /// Notifies the notification sink that charges have accrued.
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

/// <summary>
/// A class that fans cost notifications out to the sinks registered within one call context.
/// </summary>
/// <remarks>
/// <pitch>The fan-out node for one call context's cost stream: call-context cost trackers register here rather than with the process-wide tracker, so their view is limited to their own context's reports.</pitch>
/// <pledge><see cref="IAmbientCostTrackerNotificationSink"/></pledge>
/// <plan>A <see cref="ConcurrentHashSet{T}"/> of sinks with synchronous fan-out; idempotent registration.</plan>
/// </remarks>
internal class ScopeOnChargesAccruedDistributor : IAmbientCostTrackerNotificationSink
{
    private readonly ConcurrentHashSet<IAmbientCostTrackerNotificationSink> _notificationSinks = new();
    /// <summary>
    /// Notifies the notification sink that charges have accrued.
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
/// <remarks>
/// <pitch>The per-request cost accumulator: totals the charges and ongoing-cost changes reported within one call context (for example, one web request) between construction and disposal.</pitch>
/// <pledge><see cref="IAmbientAccruedChargesAndCostChanges"/></pledge>
/// <pledge><see cref="IAmbientCostTrackerNotificationSink"/></pledge>
/// <plan>Registers with its call context's <see cref="ScopeOnChargesAccruedDistributor"/> at construction and deregisters on disposal.  Totals and counts are maintained with <see cref="Interlocked"/> adds; per-service and per-customer breakdown dictionaries exist but are internal-only and not exposed through the reporting interface.</plan>
/// </remarks>
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
    /// <param name="scopeName">The name of the call context being tracked.</param>
    public CallContextCostTracker(ScopeOnChargesAccruedDistributor distributor, string scopeName)
    {
        _distributor = distributor;
        ScopeName = scopeName;
        distributor.RegisterSystemSwitchedNotificationSink(this);
    }
    /// <summary>
    /// Notifies the notification sink that charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // track the charges per service
        ChargeAccumulator.Accrue(_accumulatorsByService, serviceId, charge);
        // track the charges per customer
        ChargeAccumulator.Accrue(_accumulatorsByCustomer, customerId, charge);
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
        CostAccumulator.ChangeCost(_costAccumulatorsByService, serviceId, changePerMonth);
        // track the cost change per customer
        CostAccumulator.ChangeCost(_costAccumulatorsByCustomer, customerId, changePerMonth);
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
/// <remarks>
/// <pitch>Periodic cost reporting: every window of the configured size yields a finished <see cref="IAmbientAccruedChargesAndCostChanges"/> totaling exactly that window's reports, delivered to a callback.</pitch>
/// <pledge>Each report is delivered once, after its window closes; costs reported near a boundary land in whichever window's collector was current when the report arrived.  Disposal stops the rotation.</pledge>
/// <plan>An <see cref="AmbientEventTimer"/> rotates a <see cref="ProcessOrSingleTimeWindowCostTracker"/> atomically (via <see cref="Interlocked.Exchange{T}(ref T, T)"/>) at each boundary, closes the old collector's tracking, and hands it to the completion delegate; window scope names embed the UTC window identifier and size from <see cref="WindowScope"/>.</plan>
/// </remarks>
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
            async (sender, handler) =>
            {
                using ProcessOrSingleTimeWindowCostTracker? oldAccumulator = Rotate(metrics, windowPeriod);
                if (oldAccumulator != null)
                {
                    await onWindowComplete(oldAccumulator);
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
/// <remarks>
/// <pitch>A tiny thread-safe accumulator pairing a charge count with a running charge total, for per-service and per-customer breakdowns.</pitch>
/// <pledge>Construction records the first charge; <see cref="AddCharge"/> atomically bumps the count and adds to the total, and concurrent additions never lose each other's effects.</pledge>
/// </remarks>
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
/// <remarks>
/// <pitch>A tiny thread-safe accumulator pairing a change count with a running total of ongoing-cost-rate changes, for per-service and per-customer breakdowns.</pitch>
/// <pledge>Construction records the first change; <see cref="AddCostChange"/> atomically bumps the count and adds the (possibly negative) rate change to the total, and concurrent additions never lose each other's effects.</pledge>
/// </remarks>
public class CostAccumulator
{
    private long _chargeCount;                  // interlocked
    private long _totalCostPerMonthChange;     // interlocked

    /// <summary>
    /// Constructs a cost accumulator.
    /// </summary>
    /// <param name="costPerMonthChange">The initial change in cost.</param>
    public CostAccumulator(long costPerMonthChange)
    {
        _chargeCount = 1;
        _totalCostPerMonthChange = costPerMonthChange;
    }

    internal static void ChangeCost(ConcurrentDictionary<string, CostAccumulator> chargeAccumulators, string key, long costPerMonthChange)
    {
        chargeAccumulators.AddOrUpdate(key, new CostAccumulator(costPerMonthChange), (k, v) => { v.AddCostChange(costPerMonthChange); return v; });
    }

    /// <summary>
    /// Adds a cost change to the accumulator.
    /// </summary>
    /// <param name="costPerMonthChange">The cost change (in picodollars per month).</param>
    public void AddCostChange(long costPerMonthChange)
    {
        Interlocked.Increment(ref _chargeCount);
        Interlocked.Add(ref _totalCostPerMonthChange, costPerMonthChange);
    }
}
#endif
