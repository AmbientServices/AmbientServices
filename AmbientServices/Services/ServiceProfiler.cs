using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An interface that abstracts a service profiler notification sink.
    /// </summary>
    public interface IAmbientServiceProfilerNotificationSink
    {
        /// <summary>
        /// Notifies the notification sink that the system has switched.
        /// </summary>
        /// <remarks>
        /// This function will be called whenever the service profiler is told that the currently-processing system has switched.
        /// Note that the previously-executing system may or may not be revised at this time.  
        /// Such revisions can be used to distinguish between processing that resulted in success or failure, or other similar outcomes that the notifier wishes to distinguish.
        /// </remarks>
        /// <param name="newSystemStartStopwatchTimestamp">The stopwatch timestamp when the new system started.</param>
        /// <param name="newSystem">The identifier for the system that is starting to run.</param>
        /// <param name="oldSystemStartStopwatchTimestamp">The stopwatch timestamp when the old system started running.</param>
        /// <param name="revisedOldSystem">The (possibly-revised) name for the system that has just finished running, or null if the identifier for the old system does not need revising.</param>
        void OnSystemSwitched(long newSystemStartStopwatchTimestamp, string newSystem, long oldSystemStartStopwatchTimestamp, string revisedOldSystem = null);
    }
    /// <summary>
    /// An interface that abstracts a service profiler service.
    /// </summary>
    public interface IAmbientServiceProfiler
    {
        /// <summary>
        /// Switches the system that is executing in this call context.
        /// </summary>
        /// <param name="system">A string indicating which system is beginning to execute, empty string to indicate that the default system (ie. CPU) is executing.</param>
        /// <param name="updatedPreviousSystem">An optional updated for the previous system in case part of the system identifier couldn't be determined until the execution completed.  For example, if the operation failed, we can retroactively reclassify the time spent in order to properly separately track time spent on successful, timed-out, and failed operations.</param>
        /// <remarks>
        /// The system should be identified using the following form:
        /// MainSystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem/[Subsystem Type:]Subsystem...
        /// For example:
        /// DynamoDB/Table:My-table/Partition:342644/Result:Success
        /// S3/Bucket:My-bucket/Prefix:abcdefg/Result:Retry
        /// SQL/Database:My-database/Table:User/Result:Failed
        /// In the analysis pipeline, systems are grouped using the system group transform Regex from the settings.
        /// Any matching Regex match groups found by the regex expression will be concatenated into the transformed string.
        /// To pass the system string through unaltered (as its own group), use null, empty string, or .* as the system group transform string.
        /// For example, to transform the group systems by only the main system, database, bucket, and result, while retaining the prefixes, use the following Regex:
        /// (?:([^:/]+)(?:(/Database:[^:/]*)|(/Bucket:[^:/]*)|(/Result:[^:/]*)|(?:/[^/]*))*)
        /// For example, to transform the group systems by only the main system, database, bucket, and result, without retaining the prefixes, use the following Regex:
        /// (?:([^:/]+)(?:(?:(/)(?:Database:)([^:/]*))|(?:(/)(?:Bucket:)([^:/]*))|(?:(/)(?:Result:)([^:/]*))|(?:/[^/]*))*)
        /// </remarks>
        void SwitchSystem(string system, string updatedPreviousSystem = null);
        /// <summary>
        /// Registers a system switch notification sink with this ambient service profiler.
        /// </summary>
        /// <param name="sink">An <see cref="IAmbientServiceProfilerNotificationSink"/> that will receive notifications when the system is switched.</param>
        /// <returns>true if the registration was successful, false if the specified sink was already registered.</returns>
        bool RegisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink);
        /// <summary>
        /// Deregisters an access notification sink with this ambient service profiler.
        /// </summary>
        /// <param name="sink">An <see cref="IAmbientServiceProfilerNotificationSink"/> that will receive notifications when the system is switched.</param>
        /// <returns>true if the deregistration was successful, false if the specified sink was not registered.</returns>
        bool DeregisterSystemSwitchedNotificationSink(IAmbientServiceProfilerNotificationSink sink);
    }
}
