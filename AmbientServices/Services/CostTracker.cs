
namespace AmbientServices;

#if NET5_0_OR_GREATER
/// <summary>
/// An interface that abstracts a cost tracker notification sink.
/// </summary>
public interface IAmbientCostTrackerNotificationSink
{
    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
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
public interface IAmbientCostTracker
{
    /// <summary>
    /// Notifies the notification sink that a charges have accrued.
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
    /// Registers a cost tracker notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
    bool RegisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink);
    /// <summary>
    /// Deregisters a cost tracker notification sink with this ambient service profiler.
    /// </summary>
    /// <param name="sink">An <see cref="IAmbientCostTrackerNotificationSink"/> that will receive notifications as charges accrue.</param>
    /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
    bool DeregisterCostTrackerNotificationSink(IAmbientCostTrackerNotificationSink sink);
}
#endif
