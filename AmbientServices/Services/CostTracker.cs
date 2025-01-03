
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
    /// <param name="charge">The charge (in predetermined units).</param>
    void OnChargesAccrued(string serviceId, string customerId, long charge);
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
    /// <param name="charge">The charge (in predetermined units).</param>
    void OnChargesAccrued(string serviceId, string customerId, long charge);
    /// <summary>
    /// Registers a cost tracker notificatoin sink with this ambient service profiler.
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
