using AmbientServices.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AmbientServices
{
    /// <summary>
    /// A mutable class that can make gathering data for a <see cref="StatusResults"/> object easier.
    /// </summary>
    public class StatusResultsBuilder
    {
        private string? _sourceSystem;
        private readonly string _targetSystem;
        private int _relativeDetailLevel;
        private StatusNatureOfSystem _natureOfSystem;
        private DateTime _auditStartTime;
        private DateTime? _nextAuditTime;
        private TimeSpan? _auditDuration;
        private StatusAuditAlert? _worstAlert;
        private readonly List<StatusProperty> _properties = new();
        private readonly List<StatusResultsBuilder> _children = new();

        //private int? _hiddenFailuresTolerated;
        //private float? _spatialDistributionOfRedundancy;


        /// <summary>
        /// Constructs an empty StatusResultsBuilder, ready to fill with properties, children, and alerts.
        /// </summary>
        /// <param name="targetSystem">The target system (if any apply at this level).</param>
        public StatusResultsBuilder(string targetSystem)
        {
            _targetSystem = targetSystem;
            _auditStartTime = AmbientClock.UtcNow;
            _relativeDetailLevel = 1;
            _natureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
        }
        /// <summary>
        /// Constructs a StatusResultsBuilder from the specified <see cref="StatusResults"/>.
        /// </summary>
        /// <param name="results">The <see cref="StatusResults"/> to copy from.</param>
        public StatusResultsBuilder(StatusResults results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            _sourceSystem = results.SourceSystem;
            _targetSystem = results.TargetSystem;
            _auditStartTime = results.Time;
            _relativeDetailLevel = results.RelativeDetailLevel;
            _natureOfSystem = results.NatureOfSystem;
            _worstAlert = results.Report?.Alert;
            _nextAuditTime = results.Report?.NextAuditTime;
            _properties = new List<StatusProperty>(results.Properties);
            _children = new List<StatusResultsBuilder>(results.Children.Select(c => new StatusResultsBuilder(c)));
        }
        /// <summary>
        /// Constructs an empty StatusResultsBuilder, ready to fill with properties, children, and alerts.
        /// <see cref="NatureOfSystem"/> start with a value of <see cref="StatusNatureOfSystem.ChildrenHeterogenous"/>.
        /// </summary>
        /// <param name="checker">The <see cref="StatusChecker"/> we are going to build results for.</param>
        public StatusResultsBuilder(StatusChecker checker)
        {
            if (checker == null) throw new ArgumentNullException(nameof(checker));
            _targetSystem = checker.TargetSystem;
            _auditStartTime = AmbientClock.UtcNow;
            _relativeDetailLevel = 1;
            _natureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
        }
        /// <summary>
        /// Constructs a StatusResultsBuilder with a set of baseline properties.
        /// <see cref="NatureOfSystem"/> start with a value of <see cref="StatusNatureOfSystem.ChildrenHeterogenous"/>.
        /// </summary>
        /// <param name="checker">The <see cref="StatusChecker"/> we are going to build results for.</param>
        /// <param name="baselineProperties">An enumeration of baseline <see cref="StatusProperty"/>s to initalize the property list with.</param>
        public StatusResultsBuilder(StatusChecker checker, IEnumerable<StatusProperty> baselineProperties)
            : this(checker)
        {
            _properties.AddRange(baselineProperties);
        }
        /// <summary>
        /// Gets or sets the source system name, which identifies the system doing the measurement.
        /// </summary>
        /// <remarks>
        /// Defaults to null, which indicates that the measurement was done on the local system.
        /// The source system name can be used to sumarize multiple results from different sources for the same target.
        /// When results are gathered from a remote system, a node will be inserted indicating the system the results originated from, and that node should set the source system name.
        /// Only the last (closest) source system name in the tree will be used.
        /// </remarks>
        public string? SourceSystem
        {
            get { return _sourceSystem; }
            set { _sourceSystem = value; }
        }
        /// <summary>
        /// Gets the target system name, which identifies either a top-level system or the part of a system being audited or measured.
        /// The target system may only be specified explicitly in the constructor, or implicilty through the <see cref="StatusChecker"/> or <see cref="StatusAuditor"/>.
        /// </summary>
        /// <remarks>
        /// Target system names are concatenated with ancestor and descendant nodes and used to aggregate errors from the same system reported by multiple sources so that they can be summarized rather than listed individually.
        /// Targets with a leading slash character indicate that the system is a shared system and may have status results measured by other source systems which should be combined.
        /// Shared targets are not concatenated to the targets indicated by ancestor nodes, and their parents are ignored during summarization, treating shared systems as top-level nodes.
        /// Defaults to null, but should almost always be set to a non-empty string.
        /// Null should only be used to indicate that this node is not related to any specific target system, which would probably only happen if <see cref="NatureOfSystem"/> this, parent, and child nodes is such that some kind of special grouping is needed to make the overall status rating computation work correctly and the target system identifier for child nodes makes more sense without any identifier at this level.
        /// </remarks>
        public string? TargetSystem => _targetSystem;
        /// <summary>
        /// Gets or sets the audit start time for this node.  Defaults to the time the constuctor was called.
        /// </summary>
        public DateTime AuditStartTime
        {
            get { return _auditStartTime; }
            set { _auditStartTime = value; }
        }
        /// <summary>
        /// Gets or sets the audit duration for this node.  If not set, will use the difference between <see cref="AuditStartTime"/> and the current time when the resulting <see cref="StatusRating"/> is requested.
        /// </summary>
        public TimeSpan? AuditDuration
        {
            get { return _auditDuration; }
            set { _auditDuration = value; }
        }
        /// <summary>
        /// Gets or sets the relative detail level for this node, with zero meaning that data from this node should always be included with the parent node, and one meaning that this data provides slightly more detailed information.
        /// Nodes may be filtered based on detail level.
        /// Defaults to one.
        /// </summary>
        public int RelativeDetailLevel
        {
            get { return _relativeDetailLevel; }
            set { _relativeDetailLevel = value; }
        }
        /// <summary>
        /// Gets or sets the <see cref="StatusNatureOfSystem"/> value indicating what type of node this is relative to its children.
        /// Defaults to <see cref="StatusNatureOfSystem.ChildrenHeterogenous"/>.
        /// </summary>
        public StatusNatureOfSystem NatureOfSystem
        {
            get { return _natureOfSystem; }
            set { _natureOfSystem = value; }
        }
        /// <summary>
        /// Gets or sets the optional <see cref="DateTime"/> indicating when the next audit will happen (if any).
        /// Defaults to null.
        /// </summary>
        public DateTime? NextAuditTime
        {
            get { return _nextAuditTime; }
            set { _nextAuditTime = value; }
        }
        /// <summary>
        /// Gets the worst rated <see cref="StatusAuditAlert"/> that has been reported so far.
        /// </summary>
        public StatusAuditAlert? WorstAlert => _worstAlert;
        /// <summary>
        /// Gets the elapsed time since the audit start time.
        /// </summary>
        public TimeSpan Elapsed => AmbientClock.UtcNow - _auditStartTime;

        /// <summary>
        /// Gets the final <see cref="StatusResults"/> generated from the results builder.
        /// </summary>
        public StatusResults FinalResults
        {
            get
            {
                TimeSpan duration = (_auditDuration == null) ? (AmbientClock.UtcNow - _auditStartTime) : _auditDuration.Value;
                StatusAuditReport? report = _worstAlert is null ? null : new StatusAuditReport(_auditStartTime, duration, _nextAuditTime, _worstAlert);
                return (report == null)
                    ? new StatusResults(_sourceSystem, _targetSystem, _auditStartTime, _relativeDetailLevel, _properties, _natureOfSystem, _children.Select(c => c.FinalResults))
                    : new StatusResults(_sourceSystem, _targetSystem, _auditStartTime, _relativeDetailLevel, _properties, report);
            }
        }
        /// <summary>
        /// Adds the specified property.
        /// </summary>
        /// <param name="name">The name for the property.</param>
        /// <param name="value">The value for the property.</param>
        public void AddProperty(string name, string value)
        {
            _properties.Add(new StatusProperty(name, value));
        }
        /// <summary>
        /// Adds the specified property.
        /// </summary>
        /// <param name="name">The name for the property.</param>
        /// <param name="value">The value for the propertiy, for which <see cref="object.ToString()"/> will be called to convert it into a string.</param>
        public void AddProperty<T>(string name, T value) where T : notnull
        {
            _properties.Add(StatusProperty.Create(name, value));
        }
        /// <summary>
        /// Finds a property with the specified name, if possible.
        /// </summary>
        /// <param name="name">The name of the property to look for.</param>
        /// <returns>The <see cref="StatusProperty"/> that was found, or null if no status property with that name exists in the list.</returns>
        public StatusProperty? FindProperty(string name)
        {
            return _properties.Find(a => string.Equals(a.Name, name, StringComparison.Ordinal));
        }
        /// <summary>
        /// Adds the specified <see cref="StatusResultsBuilder"/> as a child to the node we're building.
        /// </summary>
        /// <param name="child">The child <see cref="StatusResultsBuilder"/>.</param>
        public void AddChild(StatusResultsBuilder child)
        {
            _children.Add(child);
        }
        /// <summary>
        /// Adds the specified <see cref="StatusResults"/> as a child to the node we're building.
        /// </summary>
        /// <param name="child">The child <see cref="StatusResults"/>.</param>
        public void AddChild(StatusResults child)
        {
            _children.Add(new StatusResultsBuilder(child));
        }
        /// <summary>
        /// Adds a child node with the specified name and returns the corresponding <see cref="StatusResultsBuilder"/>.
        /// </summary>
        /// <param name="childName">The name of the child node to add.</param>
        public StatusResultsBuilder AddChild(string childName)
        {
            StatusResultsBuilder childNode = new(childName);
            _children.Add(childNode);
            return childNode;
        }
        /// <summary>
        /// Records an exception alert.
        /// </summary>
        /// <param name="severity">A positive number greater than or equal to 0.0 indicating the relative severity of the failure.  (Will be subtracted from <see cref="StatusRating.Fail"/> for the associated status rating).  Defaults to 0.5.</param>
        /// <param name="ex">The <see cref="Exception"/> that occurred.</param>
        public void AddException(Exception ex, float severity = 0.5f)
        {
            if (severity < 0.0) throw new ArgumentOutOfRangeException(nameof(severity), "The specified severity must be greater than or equal to zero!");
            float rating = StatusRating.Fail - severity;
            string exceptionType = ex.TypeName();
            string exceptionTerse = "[" + exceptionType + "] " + ex.Message.Replace(Environment.NewLine, Environment.NewLine + "  ", StringComparison.Ordinal);
            string exceptionDetails = HttpUtility.HtmlEncode(ex.ToFilteredString().Trim()).Replace(Environment.NewLine, "<br/>", StringComparison.Ordinal);
            if (_worstAlert is null || rating < _worstAlert.Rating)
            {
                _worstAlert = new StatusAuditAlert(rating, exceptionType, exceptionTerse, exceptionDetails);
            }
        }
        /// <summary>
        /// Records a failure alert.
        /// </summary>
        /// <param name="auditAlertCode">An audit alert code which can be used to identify this type of error across multiple source and target systems.</param>
        /// <param name="terse">A terse string (suitable for SMS) indicating the nature of the failure.</param>
        /// <param name="details">A detailed string indicating the nature of the failure.  Should use the same formatting as documentation.</param>
        /// <param name="severity">A positive number greater than or equal to 0.0 indicating the relative severity of the failure.  (Will be subtracted from <see cref="StatusRating.Fail"/> for the associated status rating).  Defaults to 0.5.</param>
        public void AddFailure(string auditAlertCode, string terse, string details, float severity = 0.5f)
        {
            if (severity < 0.0) throw new ArgumentOutOfRangeException(nameof(severity), "The specified severity must be greater than or equal to zero!");
            float rating = StatusRating.Fail - severity;
            if (_worstAlert is null || rating < _worstAlert.Rating)
            {
                _worstAlert = new StatusAuditAlert(rating, auditAlertCode, terse, details);
            }
        }
        /// <summary>
        /// Records a normal alert.
        /// </summary>
        /// <param name="auditAlertCode">An audit alert code which can be used to identify this type of alert across multiple source and target systems.</param>
        /// <param name="terse">A terse string (suitable for SMS) indicating the nature of the alert.</param>
        /// <param name="details">A detailed string indicating the nature of the alert.  Should use the same formatting as documentation.</param>
        /// <param name="severity">A positive number between 0.0 (inclusive) and 1.0 (exclusive) indicating the relative severity of the failure.  (Will be subtracted from <see cref="StatusRating.Alert"/> for the associated status rating).  Defaults to 0.5.</param>
        public void AddAlert(string auditAlertCode, string terse, string details, float severity = 0.5f)
        {
            if (severity < 0.0 || severity >= 1.0) throw new ArgumentOutOfRangeException(nameof(severity), "The specified severity must be less than one and greater than or equal to zero!");
            float rating = StatusRating.Alert - severity;
            if (_worstAlert is null || rating < _worstAlert.Rating)
            {
                _worstAlert = new StatusAuditAlert(rating, auditAlertCode, terse, details);
            }
        }
        /// <summary>
        /// Records an okay alert.
        /// </summary>
        /// <param name="auditAlertCode">An audit alert code which can be used to identify this type of alert across multiple source and target systems.</param>
        /// <param name="terse">A terse string (suitable for SMS) indicating the nature of the issue.</param>
        /// <param name="details">A detailed string indicating the nature of the issue.  Should use the same formatting as documentation.</param>
        /// <param name="severity">A positive number between 0.0 (inclusive) and 1.0 (exclusive) indicating the relative severity of the failure.  (Will be subtracted from <see cref="StatusRating.Okay"/> for the associated status rating).  Defaults to 0.5.</param>
        public void AddOkay(string auditAlertCode, string terse, string details, float severity = 0.5f)
        {
            if (severity >= 1.0) throw new ArgumentOutOfRangeException(nameof(severity), "The specified severity must less than one!");
            float rating = StatusRating.Okay - severity;
            if (_worstAlert is null || rating < _worstAlert.Rating)
            {
                _worstAlert = new StatusAuditAlert(rating, auditAlertCode, terse, details);
            }
        }
        /// <summary>
        /// Records a superlative alert.
        /// </summary>
        /// <param name="auditAlertCode">An audit alert code which can be used to identify this type of alert across multiple source and target systems.</param>
        /// <param name="terse">A terse string (suitable for SMS) indicating the nature of the issue.</param>
        /// <param name="details">A detailed string indicating the nature of the issue.  Should use the same formatting as documentation.</param>
        public void AddSuperlative(string auditAlertCode, string terse, string details)
        {
            float rating = StatusRating.Superlative;
            if (_worstAlert is null)
            {
                _worstAlert = new StatusAuditAlert(rating, auditAlertCode, terse, details);
            }
        }
        /// <summary>
        /// Constructs a leaf node <see cref="StatusResultsBuilder"/> directly from the information for a single alert.
        /// </summary>
        /// <param name="targetSystem">The name of the target system.</param>
        /// <param name="rating">The status rating.</param>
        /// <param name="auditAlertCode">The audit alert code.</param>
        /// <param name="terse">The terse message.</param>
        /// <param name="details">The detailed message.</param>
        /// <returns>A <see cref="StatusResultsBuilder"/> constructed from the specified parameters.</returns>
        public static StatusResultsBuilder CreateRawStatusResultsBuilder(string targetSystem, float rating, string auditAlertCode, string terse, string details)
        {
            StatusResultsBuilder temp = new(targetSystem);
            temp.NatureOfSystem = StatusNatureOfSystem.Leaf;
            temp._worstAlert = new StatusAuditAlert(rating, auditAlertCode, terse, details);
            return temp;
        }
        /// <summary>
        /// Constructs a leaf node <see cref="StatusResults"/> directly from the information for a single alert.
        /// </summary>
        /// <param name="targetSystem">The name of the target system.</param>
        /// <param name="rating">The status rating.</param>
        /// <param name="auditAlertCode">The audit alert code.</param>
        /// <param name="terse">The terse message.</param>
        /// <param name="details">The detailed message.</param>
        /// <returns>A <see cref="StatusResults"/> constructed from the specified parameters.</returns>
        public static StatusResults CreateRawStatusResults(string targetSystem, float rating, string auditAlertCode, string terse, string details)
        {
            return CreateRawStatusResultsBuilder(targetSystem, rating, auditAlertCode, terse, details).FinalResults;
        }
    }
}
