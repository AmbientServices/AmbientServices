#if NET5_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace AmbientServices;

#if NET5_0_OR_GREATER
/// <summary>
/// A class that coordinates cost trackers.
/// </summary>
/// <remarks>
/// <pitch>The factory you use to turn the raw <see cref="IAmbientCostTracker"/> report stream into actual accumulations.  It builds three flavors of <see cref="IAmbientAccruedChargesAndCostChanges"/> — one scoped to the current call context (per request), one that rotates on a time window (per-window reporting), and one for the whole process.</pitch>
/// <pledge><see cref="IAmbientCostTrackerNotificationSink"/></pledge>
/// <pledge>
/// Returns null from every factory method when there is no ambient <see cref="IAmbientCostTracker"/> to observe.  Each returned tracker must be disposed to stop collecting; the call-context and process trackers are not thread-safe to read.
/// A call-context tracker sees only costs reported from within its own call context; time-window and process trackers see costs from all contexts.
/// </pledge>
/// <plan>
/// The coordinator registers itself with the ambient cost tracker (captured at construction) as a notification sink and re-dispatches each report into an <see cref="AsyncLocal{T}"/> per-call-context <see cref="ScopeOnChargesAccruedDistributor"/>, with which <see cref="CallContextCostTracker"/>s register — one coordinator registration serves any number of call-context trackers.  Process trackers (<see cref="ProcessOrSingleTimeWindowCostTracker"/>) and time-window trackers (<see cref="TimeWindowCostTracker"/>, which rotates a process tracker on an <see cref="AmbientEventTimer"/>) register with the cost tracker directly.  Disposal deregisters the coordinator's sink.
/// </plan>
/// </remarks>
public class AmbientCostTrackerCoordinator : IAmbientCostTrackerNotificationSink, IDisposable
{
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
    private static readonly AmbientService<IAmbientCostTracker> _AmbientCostTracker = Ambient.GetService<IAmbientCostTracker>();

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
        _scopeDistributor = new AsyncLocal<ScopeOnChargesAccruedDistributor>();
        _eventBroadcaster = _AmbientCostTracker.Local;
        _eventBroadcaster?.RegisterCostTrackerNotificationSink(this);
    }

    /// <summary>
    /// Notifies the notification sink that charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    public void OnChargesAccrued(string serviceId, string customerId, long charge)
    {
        _scopeDistributor.Value ??= new ScopeOnChargesAccruedDistributor();
        _scopeDistributor.Value.OnChargesAccrued(serviceId, customerId, charge);
    }
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in cost (in picodollars per minute).</param> 
    public void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth)
    {
        _scopeDistributor.Value ??= new ScopeOnChargesAccruedDistributor();
        _scopeDistributor.Value.OnOngoingCostChanged(serviceId, customerId, changePerMonth);
    }
    /// <summary>
    /// Creates a cost tracker which profiles the current call context.
    /// </summary>
    /// <param name="scopeName">A name of the call context to attach to the analyzer.</param>
    /// <returns>A <see cref="IAmbientAccruedChargesAndCostChanges"/> that will profile systems executed in this call context, or null if there is no ambient cost tracker event collector.  Note that the returned object is NOT thread-safe.</returns>
    public IAmbientAccruedChargesAndCostChanges? CreateCallContextProfiler(string scopeName)
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
    /// Creates a cost tracker which profiles the entire process in sequential time units of the specified size.
    /// </summary>
    /// <param name="scopeNamePrefix">A <see cref="TimeSpan"/> indicating the size of the window.</param>
    /// <param name="windowPeriod">A <see cref="TimeSpan"/> indicating how often reports are desired.</param>
    /// <param name="onWindowComplete">An async delegate that receives a <see cref="IAmbientServiceProfile"/> at the end of each time window.</param>
    /// <returns>A <see cref="IDisposable"/> that scopes the collection of the profiles.</returns>
#pragma warning disable CA1822 // Mark members as static--I reserve the right to use member data in this function in the future and don't want to break the interface
    public IDisposable? CreateTimeWindowProfiler(string scopeNamePrefix, TimeSpan windowPeriod, Func<IAmbientAccruedChargesAndCostChanges, Task> onWindowComplete)
#pragma warning restore CA1822 // Mark members as static
    {
        IAmbientCostTracker? metrics = _AmbientCostTracker.Local;
        if (metrics == null) return null;
        TimeWindowCostTracker tracker = new(metrics, scopeNamePrefix, windowPeriod, onWindowComplete);
        return tracker;
    }
    /// <summary>
    /// Creates a cost tracker which profiles the entire process for the entire (remaining) duration of execution.
    /// Note that this is only useful to determine the distribution for an entire process from start to finish, which is not very useful if the process is very long-lived.
    /// <see cref="CreateTimeWindowProfiler"/> is a better match in most situations.
    /// </summary>
    /// <param name="scopeName">A name for the context to attach to the analyzer.</param>
    /// <returns>A <see cref="IAmbientAccruedChargesAndCostChanges"/> containing a service profile for the entire process.  Note that the returned object is NOT thread-safe.</returns>
    /// <remarks>
    /// This is different from using <see cref="CreateCallContextProfiler"/> because that will only analyze the call context it's called from, 
    /// whereas this will analyze all threads and call contexts in the process.  
    /// They will produce the same results only for programs where there is only a single call context (no parallelization)
    /// </remarks>
#pragma warning disable CA1822 // Mark members as static--I reserve the right to use member data in this function in the future and don't want to break the interface
    public IAmbientAccruedChargesAndCostChanges? CreateProcessProfiler(string scopeName)
#pragma warning restore CA1822 // Mark members as static
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
}
/// <summary>
/// An interface that abstracts accrued charges and cost changes.
/// </summary>
/// <remarks>
/// <pitch>The read side of cost tracking: for one scope (a call context, a time window, or a whole process), the total one-time charges and total ongoing-cost-rate change accrued, with counts to distinguish many small costs from one big one.</pitch>
/// <pledge>
/// Charges and ongoing-cost changes accumulate separately and are never combined: the charge sum is a total amount (picodollars) while the cost-change sum is a net change to a recurring rate, and each has its own operation count.  Sums are signed, since ongoing costs can decrease.
/// Values may be read while the scope is still collecting (a point-in-time snapshot) or after disposal ends the collection.
/// </pledge>
/// </remarks>
public interface IAmbientAccruedChargesAndCostChanges : IDisposable
{
    /// <summary>
    /// Gets the name of the scope being analyzed.  The scope identifies the scope of the operations that were profiled.
    /// </summary>
    string ScopeName { get; }
    /// <summary>
    /// Gets the number of separate operations triggering charge accumulation.
    /// </summary>
    int ChargeCount { get; }
    /// <summary>
    /// Gets the accumulated sum of all the charges.
    /// </summary>
    long AccumulatedChargeSum { get; }
    /// <summary>
    /// Gets the number of separate operations triggering cost changes.
    /// </summary>
    int CostChangeCount { get; }
    /// <summary>
    /// Gets the accumulated sum of all the cost changes.
    /// </summary>
    long AccumulatedCostChangeSum { get; }
}
#endif
