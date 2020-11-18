using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An interface that abstracts a service profiler.
    /// </summary>
    public interface IAmbientServiceProfiler
    {
        /// <summary>
        /// Switches the system that is executing in this call context.
        /// </summary>
        /// <param name="system">A string indicating which system is beginning to execute, or null to indicate that the default system (ie. CPU) is executing.</param>
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
        /// An event that is raised whenever <see cref="SwitchSystem"/> is called.  
        /// Note that we break the normal style of inheriting the event arguments from <see cref="EventArgs"/> in order to avoid an allocation here.  
        /// Performance is critical for this.
        /// </summary>
        event EventHandler<AmbientServiceProfilerSystemSwitchedEvent> SystemSwitched;
    }
    /// <summary>
    /// A struct that contains the data about a profiler system changed event.
    /// </summary>
    public struct AmbientServiceProfilerSystemSwitchedEvent : IEquatable<AmbientServiceProfilerSystemSwitchedEvent>
    {
        /// <summary>
        /// Gets the new system.
        /// </summary>
        public string NewSystem { get; private set; }
        /// <summary>
        /// Gets the stopwatch timestamp when the old system started.
        /// </summary>
        public long OldSystemStartStopwatchTimestamp { get; private set; }
        /// <summary>
        /// Gets the stopwatch timestamp when the old system stopped and the new system started.
        /// </summary>
        public long NewSystemStartStopwatchTimestamp { get; private set; }
        /// <summary>
        /// Gets the revised old system.  If null, the old system was not revised during processing.
        /// </summary>
        public string RevisedOldSystem { get; private set; }

        /// <summary>
        /// Constructs an AmbientServiceProfilerSystemChangedEvent with the specified property values.
        /// </summary>
        /// <param name="newSystem">The new system.</param>
        /// <param name="oldSystemStartStopwatchTimestamp">The stopwatch timestamp when the old system started.</param>
        /// <param name="newSystemStartStopwatchTimestamp">The stopwatch timestamp when the old system stopped and the new system started.</param>
        /// <param name="revisedOldSystem">The revised old system. Default to null meaning that no revision is necessary.</param>
        public AmbientServiceProfilerSystemSwitchedEvent(string newSystem, long oldSystemStartStopwatchTimestamp, long newSystemStartStopwatchTimestamp, string revisedOldSystem = null)
        {
            RevisedOldSystem = revisedOldSystem;
            NewSystem = newSystem;
            OldSystemStartStopwatchTimestamp = oldSystemStartStopwatchTimestamp;
            NewSystemStartStopwatchTimestamp = newSystemStartStopwatchTimestamp;
        }
        /// <summary>
        /// Gets a 32-bit hash code for this value.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return RevisedOldSystem.GetHashCode() ^ NewSystem.GetHashCode() ^ OldSystemStartStopwatchTimestamp.GetHashCode() ^ NewSystemStartStopwatchTimestamp.GetHashCode();
        }
        /// <summary>
        /// Compares this value to another to see if they are logically equivalent.
        /// </summary>
        /// <param name="obj">The object to compare to this one.</param>
        /// <returns>true if the object has the same value as this one, false if the values differ.</returns>
        public override bool Equals(object obj)
        {
            // NOTE that this is a value type, so no null check is required
            if (!(obj is AmbientServiceProfilerSystemSwitchedEvent)) return false;
            return Equals((AmbientServiceProfilerSystemSwitchedEvent)obj);
        }
        /// <summary>
        /// Compares this value to another one to see if they are logically equivalent.
        /// </summary>
        /// <param name="that">The other <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> to compare to this one.</param>
        /// <returns>true if <paramref name="that"/> has the same value, false if it has a different value.</returns>
        public bool Equals(AmbientServiceProfilerSystemSwitchedEvent that)
        {
            return String.Equals(RevisedOldSystem, that.RevisedOldSystem, StringComparison.Ordinal) && String.Equals(NewSystem, NewSystem, StringComparison.Ordinal)
                && OldSystemStartStopwatchTimestamp.Equals(that.OldSystemStartStopwatchTimestamp)
                && NewSystemStartStopwatchTimestamp.Equals(that.NewSystemStartStopwatchTimestamp);
        }
        /// <summary>
        /// Checks to see if the specified <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> are logically equivalent.
        /// </summary>
        /// <param name="left">The first <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> to compare.</param>
        /// <param name="right">The second <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> to compare.</param>
        /// <returns>true if <paramref name="left"/> and <paramref name="right"/> have the same value, false if they have different values.</returns>
        public static bool operator ==(AmbientServiceProfilerSystemSwitchedEvent left, AmbientServiceProfilerSystemSwitchedEvent right)
        {
            return left.Equals(right);
        }
        /// <summary>
        /// Checks to see if the specified <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> are *not* logically equivalent.
        /// </summary>
        /// <param name="left">The first <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> to compare.</param>
        /// <param name="right">The second <see cref="AmbientServiceProfilerSystemSwitchedEvent"/> to compare.</param>
        /// <returns>true if <paramref name="left"/> and <paramref name="right"/> do *not* have the same value, false if they have the same values</returns>
        public static bool operator !=(AmbientServiceProfilerSystemSwitchedEvent left, AmbientServiceProfilerSystemSwitchedEvent right)
        {
            return !(left == right);
        }
    }
}
