using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

#if NET5_0_OR_GREATER
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
    /// <param name="charge">The charge (in predetermined units).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // call all the notification sinks
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnChargesAccrued(serviceId, customerId, charge);
        }
    }
    /// <summary>
    /// Registers a cost tracker notificatoin sink with this ambient service profiler.
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
internal class ProcessOrSingleTimeWindowCostTracker : IAmbientAccruedCharges, IAmbientCostTrackerNotificationSink, IDisposable, IAsyncDisposable
{
    private readonly IAmbientCostTracker _profiler;
    private readonly ConcurrentDictionary<string, CostAccumulator> _accumulatorsByService = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _accumulatorsByCustomer = new();
    private int _chargeCount;       // interlocked
    private long _totalCharges;     // interlocked
    private bool _disposedValue;

    public string ScopeName { get; }

    public int ChargeCount => _chargeCount;

    public long AccumulatedChargeSum => _totalCharges;

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
    /// <param name="charge">The charge (in predetermined units).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // track the cost per service
        _accumulatorsByService[serviceId] = new(charge);
        // track the cost per customer
        _accumulatorsByCustomer[customerId] = new(charge);
        // track the total cost
        Interlocked.Add(ref _totalCharges, charge);
        // track the number of charges
        Interlocked.Increment(ref _chargeCount);
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
    /// <summary>
    /// Disposes of this instance asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
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
    /// <param name="charge">The charge (in predetermined units).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        foreach (IAmbientCostTrackerNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.OnChargesAccrued(serviceId, customerId, charge);
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
internal class CallContextCostTracker : IAmbientAccruedCharges, IAmbientCostTrackerNotificationSink, IDisposable, IAsyncDisposable
{
    private readonly ScopeOnChargesAccruedDistributor _distributor;
    private readonly ConcurrentDictionary<string, CostAccumulator> _accumulatorsByService = new();
    private readonly ConcurrentDictionary<string, CostAccumulator> _accumulatorsByCustomer = new();
    private int _chargeCount;       // interlocked
    private long _totalCharges;     // interlocked
    private bool _disposedValue;

    public string ScopeName { get; }

    public int ChargeCount => _chargeCount;

    public long AccumulatedChargeSum => _totalCharges;

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
    /// <param name="charge">The charge (in predetermined units).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        // track the cost per service
        _accumulatorsByService[serviceId] = new(charge);
        // track the cost per customer
        _accumulatorsByCustomer[customerId] = new(charge);
        // track the total cost
        Interlocked.Add(ref _totalCharges, charge);
        // track the number of charges
        Interlocked.Increment(ref _chargeCount);
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
    /// <summary>
    /// Disposes of this instance asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A class that tracks service profile statistics for a moving time window.
/// </summary>
internal class TimeWindowCostTracker : IDisposable, IAsyncDisposable
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
    /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientAccruedCharges"/> at the end of each time window.</param>
    public TimeWindowCostTracker(IAmbientCostTracker metrics, string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientAccruedCharges, Task> onWindowComplete)
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
    /// <summary>
    /// Disposes of this instance asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
#endif
