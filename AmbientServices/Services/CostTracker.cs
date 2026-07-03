
namespace AmbientServices;

#if NET5_0_OR_GREATER
/// <summary>
/// An interface that abstracts a cost tracker notification sink.
/// </summary>
/// <remarks>
/// <pitch>The push side of cost tracking: implement this to receive every reported charge and ongoing-cost change as it happens and accumulate it however you like.  Collectors that build an <see cref="IAmbientAccruedChargesAndCostChanges"/> are the canonical implementers.</pitch>
/// <pledge>
/// Each call delivers one self-contained report — a one-time charge or a change to an ongoing cost rate — already attributed to a service and a customer, so a sink can aggregate along either axis without any per-context state.
/// Calls may arrive concurrently from multiple call contexts and are not guaranteed to arrive in the order the costs were incurred; implementations must be thread-safe.
/// </pledge>
/// </remarks>
public interface IAmbientCostTrackerNotificationSink
{
    /// <summary>
    /// Notifies the notification sink that charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    void OnChargesAccrued(string serviceId, string customerId, long charge);
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
        /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
    void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth);
}
/// <summary>
/// An interface that abstracts a cost tracker service.
/// </summary>
/// <remarks>
/// <pitch>
/// Cheap, always-on attribution of monetary cost to the service that incurred it and the customer it was incurred for.  Callers report costs as they happen; paired collectors turn the reports into per-service and per-customer accumulations for a call context, a time window, or a whole process.
/// It records only what callers explicitly report — it does not meter anything on its own, and it does no currency conversion or billing.
/// </pitch>
/// <pledge>
/// Costs come in two shapes: one-time charges (a point-in-time amount, in picodollars) and ongoing-cost changes (a signed delta to a recurring cost rate, in picodollars per month — for example storage that will now cost more every month until deleted); the two are reported and accumulated separately and are never combined by the tracker.
/// The tracker itself accumulates nothing: every report is fanned out to all registered <see cref="IAmbientCostTrackerNotificationSink"/> instances, which do the aggregation.  Sink registration is idempotent.
/// The empty-string service identifier denotes the system itself; reports may arrive from any call context concurrently.
/// </pledge>
/// </remarks>
public interface IAmbientCostTracker
{
    /// <summary>
    /// Notifies the notification sink that charges have accrued.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="charge">The charge (in picodollars).</param>
    void OnChargesAccrued(string serviceId, string customerId, long charge);
    /// <summary>
    /// Notifies the notification sink that an ongoing cost has changed.
    /// </summary>
    /// <param name="serviceId">An optional service identifier, with empty string indicating the system itself.</param>
    /// <param name="customerId">A string identifying the customer.</param>
    /// <param name="changePerMonth">The change in cost (in picodollars per month).</param> 
    void OnOngoingCostChanged(string serviceId, string customerId, long changePerMonth);  // S3 is $0.023/GB/month, so in picodollars per kilobyte per month, that's $0.023 GB*m * 10^12p$/$ * 1GB/10^9B * 30.4375d/m = 0.023 * 10^3 * 1000 = ~23000 picodollars per kilobyte per month, which seems like a reasonable resolution
    /// <summary>
    /// Registers a cost tracker notification sink with this ambient cost tracker.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    bool RegisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink);
    /// <summary>
    /// Deregisters a cost tracker notification sink with this ambient cost tracker.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    bool DeregisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink);
}
#endif
