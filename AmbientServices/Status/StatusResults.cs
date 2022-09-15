using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An enumeration of possible indicators as to the nature of a system and its children.
    /// </summary>
    public enum StatusNatureOfSystem
    {
        /// <summary>
        /// Indicates that this system doesn't have any children.
        /// Used for nodes with properties that may trigger threshold status ratings.
        /// </summary>
        Leaf,
        /// <summary>
        /// Indicates that this system is irrelevant when computing redundancy and overall status.
        /// Used for nodes that are purely informational and which cannot trigger threshold status ratings.
        /// </summary>
        /// <remarks>
        /// Nodes of this nature will lack a status report and one should not be computed for them.
        /// </remarks>
        ChildrenIrrelevant,
        /// <summary>
        /// Indicates that a node is a system of children of varying functionality or nature, each of which must be working for the overall system to be working.
        /// These nodes may also contain properties, but will primarily rely on the rating determined by by descendants.
        /// </summary>
        /// <remarks>
        /// Nodes of this nature will always return the worst (least) status rating gathered or computed from all of their children.
        /// </remarks>
        ChildrenHeterogenous,
        /// <summary>
        /// Indicates that a node is the parent node of children which are all identical, only one of which has to be working in order for the system to function without errors.
        /// These nodes may also contain properties, but will primarily rely on the rating determined by by descendants.
        /// </summary>
        /// <remarks>
        /// When all the children have status ratings in the same range, nodes of this nature will use the average rating from all the children.
        /// When one or more children have status ratings in ranges different from the other children, the overall rating will be in the <see cref="StatusRating.Alert"/> range, with the status dropping towards <see cref="StatusRating.Fail"/> according to how badly the children rate.
        /// </remarks>
        ChildrenHomogenous,
    }
    /// <summary>
    /// An immutable class that contains properties describing the nature or state of a status node.
    /// </summary>
    public sealed class StatusProperty
    {
        private readonly string _name;
        private readonly string _value;

        /// <summary>
        /// The name of the status node property.  If the name begins with an underscore, the value contains sensitive information that should not be shared publicly, such as a user or machine name, or a network address.
        /// </summary>
        public string Name => _name;
        /// <summary>
        /// The value of the status node property.
        /// </summary>
        public string Value => _value;

        /// <summary>
        /// Constructs a <see cref="StatusProperty"/> with the specified name and value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        public StatusProperty(string name, string value) { _name = name; _value = value; }
        /// <summary>
        /// Creates a <see cref="StatusProperty"/> with the specified name and value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        public static StatusProperty Create<T>(string name, T value) where T : notnull { return new StatusProperty(name, value.ToString() ?? "<null>"); }
        /// <summary>
        /// Gets a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return _name + "=" + _value;
        }
    }
    /// <summary>
    /// An immutable class that holds a snapshot of a status results tree (whether from an audit or not), with all the information needed to compute the overall status for a particular system, subsystem, or group of systems.
    /// </summary>
    public sealed class StatusResults
    {
#if RAWRATINGS
        private const string DebugRatingFloatFormat = "0.00";
#endif
        private readonly string? _sourceSystem;
        private readonly string _targetSystem;
        private readonly DateTime _time;
        private readonly int _relativeDetailLevel;
        private readonly ImmutableArray<StatusProperty> _properties;
        private readonly StatusNatureOfSystem _natureOfSystem;
        private readonly ImmutableArray<StatusResults> _children;
        private readonly StatusAuditReport? _report;

        private StatusResults(string? sourceSystem, string targetSystem)
        {
            _sourceSystem = sourceSystem;
            _targetSystem = targetSystem;
            _time = AmbientClock.UtcNow;
            _relativeDetailLevel = 0;
            _natureOfSystem = StatusNatureOfSystem.Leaf;
            _properties = ImmutableArray<StatusProperty>.Empty;
            _children = ImmutableArray<StatusResults>.Empty;
            _report = StatusAuditReport.Pending;
        }
        /// <summary>
        /// Gets a <see cref="StatusResults"/> that indicates that the results for the specified source and target are pending.
        /// </summary>
        /// <param name="source">The source system (usually null, indicating the local system).</param>
        /// <param name="target">The target system, with a leading slash if it is a shared system.</param>
        /// <returns></returns>
        public static StatusResults GetPendingResults(string? source, string target)
        {
            return new StatusResults(source, target);
        }
#if LATER
        /// <summary>
        /// Cosntructs a <see cref="StatusResults"/> from the specified property data (for serialization).
        /// </summary>
        /// <param name="sourceSystem">The name of the source system (if known).</param>
        /// <param name="targetSystem">The name of the target system (if any).</param>
        /// <param name="time">The <see cref="DateTime"/> when the properties were gathered.</param>
        /// <param name="relativeDetailLevel">The relative level of detail provided by the properties at this level.</param>
        /// <param name="properties">An enumeration of <see cref="StatusProperty"/> indicating various properties of the system.</param>
        /// <param name="natureOfSystem">A <see cref="StatusNatureOfSystem"/> indicating if or how audit results from children should be aggregated and how redundancy might be inferred.  Ignored if <paramref name="report"/> is not null.</param>
        /// <param name="children">An enumeration of <see cref="StatusResults"/> for child nodes in the status tree.  Ignored if <paramref name="report"/> is not null.</param>
        /// <param name="report">An optional <see cref="StatusAuditReport"/> containing the results of the most recent audit in case this is an auditable node.</param>
        private StatusResults(string sourceSystem, string targetSystem, DateTime time, int relativeDetailLevel, IEnumerable<StatusProperty> properties, StatusNatureOfSystem natureOfSystem, IEnumerable<StatusResults> children = null, StatusAuditReport report = null)
        {
            _sourceSystem = sourceSystem;
            _targetSystem = targetSystem;
            _time = time;
            _relativeDetailLevel = relativeDetailLevel;
            _properties = ImmutableArrayExtensions.FromEnumerable(properties);
            if (report != null)
            {
                _report = report;
                _natureOfSystem = StatusNatureOfSystem.Leaf;
                _children = ImmutableArray<StatusResults>.Empty;
            }
            else
            {
                _report = null;
                _natureOfSystem = natureOfSystem;
                _children = ImmutableArrayExtensions.FromEnumerable(children);
            }
        }
#endif
        /// <summary>
        /// Cosntructs a <see cref="StatusResults"/> from the specified property data.
        /// </summary>
        /// <param name="sourceSystem">The name of the source system (if known).</param>
        /// <param name="targetSystem">The name of the target system, or null or an empty string if these results are not associated with any particular named subsystem of the parent.</param>
        /// <param name="time">The <see cref="DateTime"/> when the properties were gathered.</param>
        /// <param name="relativeDetailLevel">The relative level of detail provided by the properties at this level.</param>
        /// <param name="properties">An enumeration of <see cref="StatusProperty"/> indicating various properties of the system.</param>
        /// <param name="natureOfSystem">A <see cref="StatusNatureOfSystem"/> indicating if or how audit results from children should be aggregated and how redundancy might be inferred.</param>
        /// <param name="children">An enumeration of <see cref="StatusResults"/> for child nodes in the status tree.</param>
        public StatusResults(string? sourceSystem, string? targetSystem, DateTime time, int relativeDetailLevel, IEnumerable<StatusProperty> properties, StatusNatureOfSystem natureOfSystem, IEnumerable<StatusResults> children)
        {
            _sourceSystem = sourceSystem;
            _targetSystem = targetSystem ?? "";
            _time = time;
            _relativeDetailLevel = relativeDetailLevel;
            _properties = ImmutableArrayUtilities.FromEnumerable(properties);
            _report = null;
            _natureOfSystem = natureOfSystem;
            _children = ImmutableArrayUtilities.FromEnumerable(children);
        }
        /// <summary>
        /// Cosntructs a <see cref="StatusResults"/> from the specified property data.
        /// </summary>
        /// <param name="sourceSystem">The name of the source system (if known).</param>
        /// <param name="targetSystem">The name of the target system, or an empty string if these results are not associated with any particular named subsystem of the parent.</param>
        /// <param name="time">The <see cref="DateTime"/> when the properties were gathered.</param>
        /// <param name="relativeDetailLevel">The relative level of detail provided by the properties at this level.</param>
        /// <param name="properties">An enumeration of <see cref="StatusProperty"/> indicating various properties of the system.</param>
        /// <param name="report">An optional <see cref="StatusAuditReport"/> containing the results of the most recent audit in case this is an auditable node.</param>
        public StatusResults(string? sourceSystem, string targetSystem, DateTime time, int relativeDetailLevel, IEnumerable<StatusProperty> properties, StatusAuditReport? report = null)
        {
            _sourceSystem = sourceSystem;
            _targetSystem = targetSystem;
            _time = time;
            _relativeDetailLevel = relativeDetailLevel;
            _properties = ImmutableArrayUtilities.FromEnumerable(properties);
            _report = report;
            _natureOfSystem = StatusNatureOfSystem.Leaf;
            _children = ImmutableArray<StatusResults>.Empty;
        }
        /// <summary>
        /// Cosntructs a <see cref="StatusResults"/> including a summary report from the specified children.
        /// </summary>
        /// <param name="sourceSystem">The name of the source system (if known).</param>
        /// <param name="targetSystem">The name of the target system, or an empty string if these results are not associated with any particular named subsystem of the parent.</param>
        /// <param name="children">An enumeration of <see cref="StatusResults"/> for child nodes in the status tree.</param>
        public StatusResults(string? sourceSystem, string targetSystem, IEnumerable<StatusResults> children)
        {
            _sourceSystem = sourceSystem;
            _targetSystem = targetSystem;
            _time = AmbientClock.UtcNow;
            _relativeDetailLevel = 0;
            _properties = ImmutableArray<StatusProperty>.Empty;
            _natureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            _children = ImmutableArrayUtilities.FromEnumerable(children);
            _report = null;
        }

        /// <summary>
        /// A string indicating which system performed the audit.
        /// Null except for results gathered from other systems.
        /// When specified, all parent and ancestor source system identifiers are overridden and ignored.
        /// For summarization purposes, source system names for the same system should match exactly no matter what path the status data flowed through.
        /// </summary>
        public string? SourceSystem => _sourceSystem;
        /// <summary>
        /// A string indicating which system performed the audit.
        /// "Localhost" when <see cref="SourceSystem"/> is null.
        /// </summary>
        public string SourceSystemDisplayName => SourceDisplayName(_sourceSystem);
        /// <summary>
        /// A string indicating which backend system, subsystem, or feature the results were gathered about.  
        /// If the string does not begin with a slash, the target system is a non-shared subsystem of the target system that is identified by the parent node (and possibly ancestor nodes).
        /// If the string begins with a slash, the target system is not a subsystem of the parent node, but rather an independent system may be shared and which should be identified starting with this name (though child nodes may augment that name).
        /// May be empty string if this node represents a feature of the parent system that does not require unique identification.
        /// For summarization purposes, target system names for the same backend service should match exactly no matter which server in the farm the reports come from.
        /// </summary>
        /// <remarks>
        /// For example, the local disk system should be identified with something like "LocalDisk" (no leading slash) because it's a subsystem that is different from other local disk systems.
        /// A shared database system should be identified with something like "/Database" (with a leading slash) because it's not unique to the local system.
        /// </remarks>
        public string TargetSystem => _targetSystem;
        /// <summary>
        /// A string indicating which backend system, subsystem, or feature the results were gathered about.  
        /// If <see cref="TargetSystem"/> is "/", this will be "Overall".
        /// If <see cref="TargetSystem"/> is null or empty, this will be "Unknown Target".
        /// </summary>
        public string TargetSystemDisplayName => TargetDisplayName(_targetSystem);
        /// <summary>
        /// A <see cref="DateTime"/> indicating when the information was gathered (which may or may not be periodically audited and updated).
        /// </summary>
        public DateTime Time => _time;
        /// <summary>
        /// The relative detail level of the properties of this node.  
        /// Usually zero, indicating that these properties are at the same level of detail as the parent node's properties; 
        /// or one, indicating that the properties here provide just slightly more detail than the parent node's properties.
        /// May be used to filter properties at deeper level status nodes, but may be overridden by status ratings (any nodes with poor status ratings will have properties included anyway).
        /// </summary>
        public int RelativeDetailLevel => _relativeDetailLevel;
        /// <summary>
        /// An enumeration of <see cref="StatusProperty"/> key-value pairs containing detailed information about the system.
        /// </summary>
        public IEnumerable<StatusProperty> Properties => _properties;
        /// <summary>
        /// A <see cref="StatusNatureOfSystem"/> indicating the nature of the system represented by these results for the purposes of audit result aggregation and inferring failure tolerance.
        /// </summary>
        public StatusNatureOfSystem NatureOfSystem => _natureOfSystem;
        /// <summary>
        /// An enumeration of <see cref="StatusResults"/> from child nodes which may or may not be aggregatable, depending on if they are auditable.
        /// StatusResults may have either children or a report, but never both.
        /// </summary>
        public IEnumerable<StatusResults> Children => _children;
        /// <summary>
        /// An optional <see cref="StatusAuditReport"/> that might contain results of an audit.
        /// Overrides any information that might be in child nodes.
        /// Null if audits don't apply to this node, or the report must be computed based on <see cref="Properties"/>, <see cref="NatureOfSystem"/>, and <see cref="Children"/>.
        /// Set to the special <see cref="StatusAuditReport.Pending"/> value if the first audit is still pending, 
        /// StatusResults may have either children or a report, but never both.
        /// </summary>
        public StatusAuditReport? Report => _report;

        private static string SourceDisplayName(string? source) { return source ?? "Localhost"; }
        private static string TargetDisplayName(string? target) { return (target == "/") ? "Overall" : (string.IsNullOrEmpty(target) ? "Unknown Target" : target!); }   // string.IsNullOrEmpty ensures response is not null
        /// <summary>
        /// Computes a summary status report based on <see cref="Properties"/>, <see cref="NatureOfSystem"/>, and <see cref="Children"/>, when a node-level report is not available.
        /// </summary>
        /// <param name="includeHtmlTag">Whether or not to include the html and body tags.</param>
        /// <param name="ignoreRatingsBetterThan">A value indicating which reports to completely ignore.</param>
        /// <param name="ignorePendingRatings">Whether or not to ignore pending ratings.</param>
        /// <param name="notificationTimeZone">An optional <see cref="TimeZoneInfo"/> that will be used to convert the notification time.  If not specified, UTC will be used.</param>
        /// <returns>A <see cref="StatusAuditAlert"/> summarizing the overall state of the system.</returns>
        public StatusAuditAlert GetSummaryAlerts(bool includeHtmlTag, float ignoreRatingsBetterThan, bool ignorePendingRatings, TimeZoneInfo? notificationTimeZone = null)
        {
            DateTime start = AmbientClock.UtcNow;
            StatusResultsOrganizer organized = new(this);

            organized.ComputeOverallRatingAndSort();

            DateTime notificationTime = TimeZoneInfo.ConvertTimeFromUtc(organized.MostRecentTime, notificationTimeZone ?? TimeZoneInfo.Utc);
            StatusNotificationWriter writer = new(notificationTime);

            // build HTML style and header for the indicated rating and rating range
            float overallRating = organized.SortRating;
            if (includeHtmlTag) writer.EnterHtmlAndBody(overallRating);
            writer.EnterStatusRange(overallRating);
            StatusRatingRange ratingRange = StatusRating.FindRange(overallRating);

            // filter irrelevant top-level reports
            AggregatedAlert? aggregatedAlert = null;
            foreach (StatusResultsOrganizer child in organized.Children)
            {
                // use the specified child rating, or okay if one is not specified
                float childRating = child.SortRating;
                // is this one better than the cutoff?  stop now because all the subsequent reports are better than this one! (because they're sorted by rating)
                if (childRating > ignoreRatingsBetterThan || (ignorePendingRatings && float.IsNaN(childRating))) break;
                StatusRatingRange childRatingRange = StatusRating.FindRange(childRating);
                if (childRatingRange != ratingRange)
                {
                    if (aggregatedAlert != null)
                    {
                        writer.WriteAggregatedAlert(aggregatedAlert);
                        aggregatedAlert = null;
                    }
                    writer.LeaveStatusRange();
                    writer.EnterStatusRange(childRating);
                    ratingRange = childRatingRange;
                }
                Aggregate(ref aggregatedAlert, writer, start, child, ignoreRatingsBetterThan);
            }
            if (aggregatedAlert != null)
            {
                writer.WriteAggregatedAlert(aggregatedAlert);
                aggregatedAlert = null;
            }
            writer.LeaveStatusRange();
            if (includeHtmlTag) writer.LeaveBodyAndHtml();
            StatusAuditAlert alert = new(overallRating, string.Empty, writer.Terse, writer.Details);
            return alert;
        }

        private void RenderTargetedResults(StatusNotificationWriter writer, DateTime start, StatusResultsOrganizer results, float ignoreRatingsBetterThan)
        {
            float rating = results.SortRating;
            writer.EnterTarget(results.Target, rating);
            AggregatedAlert? aggregatedAlert = null;
            foreach (StatusResultsOrganizer child in results.Children)
            {
                // is this one better than the cutoff?  stop now because all the subsequent reports are better than this one!
                if (child.OverallRating > ignoreRatingsBetterThan) break;
                Aggregate(ref aggregatedAlert, writer, start, child, ignoreRatingsBetterThan);
            }
            if (aggregatedAlert != null) writer.WriteAggregatedAlert(aggregatedAlert);
            writer.LeaveTarget();
        }

        private void Aggregate(ref AggregatedAlert? aggregatedAlert, StatusNotificationWriter writer, DateTime start, StatusResultsOrganizer child, float ignoreRatingsBetterThan)
        {
            // does this child have children?
            if (child.ChildrenCount > 0)
            {
                System.Diagnostics.Debug.Assert(child.NatureOfSystem == StatusNatureOfSystem.ChildrenIrrelevant || child.ChildrenCount > 1);
                // recurse for this level too
                RenderTargetedResults(writer, start, child, ignoreRatingsBetterThan);
            }
            else
            {
                // can we aggregate this one?
                if (aggregatedAlert != null && aggregatedAlert.CanBeAggregated(child.Target, child.OverallReport))
                {
                    aggregatedAlert.Aggregate(child.Source, child.Target, child.MostRecentTime, child.OverallReport);
                }
                else // this one can't be aggregated with previous ones, so we need to flush the previously-aggregated alerts and start a new aggregation
                {
                    if (aggregatedAlert != null) writer.WriteAggregatedAlert(aggregatedAlert);
                    aggregatedAlert = new AggregatedAlert(child.Source, child.Target, child.MostRecentTime, child.OverallReport);
                }
            }
        }

        /// <summary>
        /// Gets a string representing the instance.
        /// </summary>
        /// <returns>A string representing the instance.</returns>
        public override string ToString()
        {
            StringBuilder output = new();
            if (_sourceSystem != null)
            {
                output.Append(SourceDisplayName(_sourceSystem));
                output.Append("->");
            }
            if (!string.IsNullOrEmpty(_targetSystem))
            {
                output.Append(TargetDisplayName(_targetSystem));
                output.Append(':');
            }
            if (_report != null)
            {
                output.Append(_report.ToString());
            }
            return output.ToString();
        }
    }
}
