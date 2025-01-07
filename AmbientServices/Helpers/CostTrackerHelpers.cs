#if NET5_0_OR_GREATER
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace AmbientServices;

#if NET5_0_OR_GREATER
/// <summary>
/// A class that coordinates service profilers.
/// </summary>
public class AmbientCostTrackerCoordinator : IAmbientCostTrackerNotificationSink, IDisposable, IAsyncDisposable
{
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
    private static readonly AmbientService<IAmbientCostTracker> _AmbientCostTracker = Ambient.GetService<IAmbientCostTracker>();

    private readonly IAmbientSetting<Regex?> _defaultSystemGroupTransformSetting;
    private readonly IAmbientCostTracker? _eventBroadcaster;
    private readonly AsyncLocal<ScopeOnChargesAccruedDistributor> _scopeDistributor;
    private bool _disposedValue;

    /// <summary>
    /// Constructs an AmbientCostTrackerCoordinator using settings obtained from the ambient settings set.
    /// </summary>
    public AmbientCostTrackerCoordinator()
        : this(_SettingsSet.Local)
    {
    }
    /// <summary>
    /// Constructs an AmbientCostTrackerCoordinator using the specified settings set.
    /// </summary>
    /// <param name="settingsSet"></param>
    public AmbientCostTrackerCoordinator(IAmbientSettingsSet? settingsSet)
    {
        _defaultSystemGroupTransformSetting = AmbientSettings.GetSettingsSetSetting<Regex?>(settingsSet, nameof(AmbientCostTrackerCoordinator) + "-DefaultSystemGroupTransform",
            @"A `Regex` string used to transform the system identifier to a group identifier.
The regular expression will attempt to match the system identifier, with the values for any matching match groups being concatenated into the system group identifier.",
            s => string.IsNullOrEmpty(s) ? null : new Regex(s, RegexOptions.Compiled));
        _scopeDistributor = new AsyncLocal<ScopeOnChargesAccruedDistributor>();
        _eventBroadcaster = _AmbientCostTracker.Local;
        _eventBroadcaster?.RegisterCostTrackerNotificationSink(this);
    }

    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in predetermined units).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        _scopeDistributor.Value ??= new ScopeOnChargesAccruedDistributor();
        _scopeDistributor.Value.OnChargesAccrued(serviceId, customerId, charge);
    }
    /// <summary>
    /// Creates a cost tracker which profiles the current call context.
    /// </summary>
    /// <param name="scopeName">A name of the call context to attach to the analyzer.</param>
    /// <returns>A <see cref="IAmbientAccruedCharges"/> that will profile systems executed in this call context, or null if there is no ambient service profiler event collector.  Note that the returned object is NOT thread-safe.</returns>
    public IAmbientAccruedCharges? CreateCallContextProfiler(string scopeName)
    {
        IAmbientCostTracker? metrics = _AmbientCostTracker.Local;
        if (metrics != null)
        {
            _scopeDistributor.Value ??= new ScopeOnChargesAccruedDistributor();
            CallContextCostTracker analyzer = new(_scopeDistributor.Value, scopeName);
            return analyzer;
        }
        return null;
    }
    /// <summary>
    /// Creates a service profiler which profiles the entire process in sequential time units of the specified size.
    /// </summary>
    /// <param name="scopeNamePrefix">A <see cref="TimeSpan"/> indicating the size of the window.</param>
    /// <param name="windowPeriod">A <see cref="TimeSpan"/> indicating how often reports are desired.</param>
    /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientServiceProfile"/> at the end of each time window.</param>
    /// <returns>A <see cref="IDisposable"/> that scopes the collection of the profiles.</returns>
    public IDisposable? CreateTimeWindowProfiler(string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientAccruedCharges, Task> onWindowComplete)
    {
        IAmbientCostTracker? metrics = _AmbientCostTracker.Local;
        if (metrics == null) return null;
        TimeWindowCostTracker tracker = new(metrics, scopeNamePrefix, windowPeriod, onWindowComplete);
        return tracker;
    }
    /// <summary>
    /// Creates a service profiler which profiles the entire process for the entire (remaining) duration of execution.
    /// Note that this is only useful to determine the distribution for an entire process from start to finish, which is not very useful if the process is very long-lived.
    /// <see cref="CreateTimeWindowProfiler"/> is a better match in most situations.
    /// </summary>
    /// <param name="scopeName">A name for the contxt to attach to the analyzer.</param>
    /// <returns>A <see cref="IAmbientAccruedCharges"/> containing a service profile for the entire process.  Note that the returned object is NOT thread-safe.</returns>
    /// <remarks>
    /// This is different from using <see cref="CreateCallContextProfiler"/> because that will only analyze the call context it's called from, 
    /// whereas this will analyze all threads and call contexts in the process.  
    /// They will produce the same results only for programs where there is only a single call context (no parallelization)
    /// </remarks>
    public IAmbientAccruedCharges? CreateProcessProfiler(string scopeName)
    {
        IAmbientCostTracker? metrics = _AmbientCostTracker.Local;
        if (metrics != null)
        {
            ProcessOrSingleTimeWindowCostTracker tracker = new(metrics, scopeName);
            return tracker;
        }
        return null;
    }
    /// <summary>
    /// Disposes of this instance.  May be overridden by derived classes.
    /// </summary>
    /// <param name="disposing">Whether or not we're disposing (as opposed to finalizing).</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _eventBroadcaster?.DeregisterCostTrackerNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }
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
/// An interface that abstracts accrued charges.
/// </summary>
public interface IAmbientAccruedCharges : IDisposable
{
    /// <summary>
    /// Gets the name of the scope being analyzed.  The scope identifies the scope of the operations that were profiled.
    /// </summary>
    string ScopeName { get; }
    /// <summary>
    /// Gets the number of separate operations triggering charge accumulation.
    /// </summary>
    int ChargeCount{ get; }
    /// <summary>
    /// Gets the accumulated sum of all the charges.
    /// </summary>
    long AccumulatedChargeSum { get; }
}
/// <summary>
/// A class that accumulates cost for some scope.
/// </summary>
public class CostAccumulator
{
    private long _chargeCount;      // interlocked
    private long _totalCharges;     // interlocked

    /// <summary>
    /// Constructs a cost accumulator.
    /// </summary>
    public CostAccumulator()
    {
    }
    /// <summary>
    /// Constructs a cost accumulator.
    /// </summary>
    /// <param name="charge">The initial charge.</param>
    public CostAccumulator(long charge)
    {
        _chargeCount = 1;
        _totalCharges = charge;
    }
    /// <summary>
    /// Adds a charge to the accumulator.
    /// </summary>
    /// <param name="charge">The charge amount (in predetermined units).</param>
    public void AddCharge(long charge)
    {
        Interlocked.Increment(ref _chargeCount);
        Interlocked.Add(ref _totalCharges, charge);
    }
}
#endif
