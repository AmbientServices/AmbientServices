using AmbientServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A class used to rate and organize a tree of <see cref="StatusResults"/> such that single results are pushed up to parents, children are sorted by rating, and overall ratings for each node are assigned based on the nature of the system represented by each node.
    /// </summary>
    internal class StatusResultsOrganizer
    {
        private readonly IStatusThresholdsRegistry? _thresholds;
        private readonly List<StatusPropertyRange> _propertyRanges = new();
        private readonly List<StatusResultsOrganizer> _children = new();

        /// <summary>
        /// Gets the most recent time from all the <see cref="StatusResults.Time"/> property of all descendant <see cref="StatusResults"/>.
        /// </summary>
        public DateTime MostRecentTime { get; private set; }
        /// <summary>
        /// Gets the overall rating for this branch of the tree, if one is available and has been determined.
        /// </summary>
        public float? OverallRating { get; private set; }
        /// <summary>
        /// Gets whether or not some of the descandant nodes indicate that status tests have not yet been completed.
        /// </summary>
        public bool SomeRatingsPending { get; private set; }
        /// <summary>
        /// Gets the source.
        /// </summary>
        public string? Source { get; private set; }
        /// <summary>
        /// Gets the target.
        /// </summary>
        public string Target { get; private set; }
        /// <summary>
        /// Gets the <see cref="StatusNatureOfSystem"/> for the corresponding <see cref="StatusResults"/>.
        /// </summary>
        public StatusNatureOfSystem NatureOfSystem { get; private set; }
        /// <summary>
        /// Gets a <see cref="StatusAuditReport"/> summarizing the status for this node in the tree.
        /// </summary>
        public StatusAuditReport? OverallReport { get; private set; }
        /// <summary>
        /// Gets an enumeration of <see cref="StatusPropertyRange"/>s indicating the range of the various properties for this node and all descendant nodes.
        /// </summary>
        public IEnumerable<StatusPropertyRange> PropertyRanges => _propertyRanges;
        /// <summary>
        /// Gets the <see cref="StatusPropertyRange"/> for the property that has the worst rating according to the applied thresholds.
        /// </summary>
        internal StatusPropertyRange? WorstPropertyRange { get; private set; }
        /// <summary>
        /// Gets the <see cref="StatusAuditAlert"/> for the worst property range according to the applied thresholds.
        /// </summary>
        internal StatusAuditAlert? WorstPropertyAlert { get; private set; }
        /// <summary>
        /// Gets the rating used for sorting, which is the overall rating if there is one, or <see cref="StatusRating.Okay"/> if there is not.
        /// </summary>
        internal float SortRating => OverallRating ?? StatusRating.Okay;
        /// <summary>
        /// Gets the number of direct children to this node.
        /// </summary>
        public int ChildrenCount => _children.Count;
        /// <summary>
        /// Gets an enumeration of <see cref="StatusResultsOrganizer"/>s for the child nodes.
        /// </summary>
        public IEnumerable<StatusResultsOrganizer> Children => _children;

        private StatusResultsOrganizer(StatusResults results, string? source, string localTarget)
        {
            MostRecentTime = results.Time;
            Source = results.SourceSystem ?? source;
            Target = localTarget;
            NatureOfSystem = results.NatureOfSystem;
            OverallReport = results.Report;
        }

        public StatusResultsOrganizer(IStatusThresholdsRegistry? thresholds = null)
        {
            _thresholds = thresholds;
            Target = "/";
            NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
        }

        public StatusResultsOrganizer(StatusResults results, string? source = null, IStatusThresholdsRegistry? thresholds = null)
        {
            _thresholds = thresholds;
            Target = "/";
            NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
            Add(results, source);
        }
        /// <summary>
        /// Adds the specified <see cref="StatusResults"/> to this root node.
        /// </summary>
        /// <param name="results">The <see cref="StatusResults"/> for this node.</param>
        /// <param name="source">The source to assign for nodes that don't have a source assigned.</param>
        public void Add(StatusResults results, string? source = null)
        {
            Add(this, results, source);
        }
        /// <summary>
        /// Adds the specified <see cref="StatusResults"/> as a descendant of the specified root.
        /// The position in the tree where the results will be put is determined by the <see cref="StatusResults.TargetDisplayName"/> property, 
        /// with nodes that have the same target ending up as siblings to a common parent.
        /// </summary>
        /// <param name="root">The root <see cref="StatusResultsOrganizer"/> being added to.</param>
        /// <param name="results">The <see cref="StatusResults"/> being added.</param>
        /// <param name="source">A source, if one needs to be assigned.</param>
        /// <param name="target">A parent target for the specified node.</param>
        public void Add(StatusResultsOrganizer root, StatusResults results, string? source = null, string target = "")
        {
            // a leaf node?
            if (results.NatureOfSystem == StatusNatureOfSystem.Leaf)
            {
                // rate this leaf node if we can (we need the ratings here in order to collate and sort results in ComputeOverallRatingAndSort)
                string localTarget = results.TargetSystem;
                StatusResultsOrganizer organizedResults = new(results, source, localTarget);
                StatusResultsOrganizer parent = this;
                string? localtarget = results.TargetSystem;
                // a child of the root?
                if (localtarget?.StartsWith("/", StringComparison.Ordinal) ?? false)
                {
                    parent = root;
                    localtarget = localtarget.Substring(1);
                }
                parent._children.Add(organizedResults);
                // merge in the properties
                organizedResults.MergeProperties(results);
            }
            else // the results instance we're adding is an inner node
            {
                StatusResultsOrganizer? match;
                // do the results specify a target?
                if (!string.IsNullOrEmpty(results.TargetSystem))
                {
                    if (results.TargetSystem == "/")
                    {
                        match = root;
                    }
                    else
                    {
                        StatusResultsOrganizer parent = this;
                        string? localTarget = results.TargetSystem;
                        // a child of the root?
                        if (localTarget.StartsWith("/", StringComparison.Ordinal))
                        {
                            parent = root;
                            localTarget = localTarget.Substring(1);
                        }
                        // is there NOT a matching node that's a child of the parent?
                        match = parent._children.Where(c => c.Target == localTarget && c.Source == source && c.NatureOfSystem == results.NatureOfSystem).FirstOrDefault();  // note that the matching node cannot be a leaf in this case because the NatureOfSystem had to match the results and we already checked results for that
                        if (match == null)
                        { // we don't have a node to merge into, so build a new node now with the properties of these results
                            match = new StatusResultsOrganizer(results, source, localTarget);  // note that this new matching node cannot be a leaf in this case because the NatureOfSystem had to match the results and we already checked results for that
                            parent._children.Add(match);
                        }
                        System.Diagnostics.Debug.Assert(match.NatureOfSystem != StatusNatureOfSystem.Leaf);
                    }
                }
                else // no target is specified, so we should merge these results directly into this node
                {
                    match = this;
                }

                // does the matching node have children?
                if (match.NatureOfSystem != StatusNatureOfSystem.Leaf)
                {
                    DateTime? time = null;
                    // merge the children of the specified results
                    foreach (StatusResults child in results.Children)
                    {
                        match.Add(root, child, results.SourceSystem ?? source, ComputeTarget(target, results.TargetSystem));
                        if (time == null || child.Time > time)
                        {
                            time = child.Time;
                        }
                    }
                    MostRecentTime = (time > results.Time) ? time.Value : results.Time;
                }
                else // the matching node is a leaf node (this is strange because the node we're adding is NOT a leaf node)
                {
                    match.MostRecentTime = results.Time;
                    // the node we're adding is NOT a leaf node, so it CANNOT have a report (StatusResults cannot have both children and a report, though we can here once we rate parent nodes based on their children)
                    System.Diagnostics.Debug.Assert(results.Report == null);
                }
                // merge in the properties
                match.MergeProperties(results);
            }
        }

        private void MergeProperties(StatusResults results)
        {
            // merge any attibutes into the matching node
            foreach (StatusProperty property in results.Properties)
            {
                StatusPropertyRange? range = _propertyRanges.Where(ar => ar.Name == property.Name).FirstOrDefault();
                if (range != null)
                {
                    range.Merge(property.Value);
                }
                else
                {
                    _propertyRanges.Add(new StatusPropertyRange(property));
                }
            }
        }

        /// <summary>
        /// Clamps the status rating into the standard range between <see cref="StatusRating.Catastrophic"/> and <see cref="StatusRating.Okay"/>.
        /// </summary>
        /// <param name="rawRating">The raw range value.</param>
        /// <returns>A range value truncated to between <see cref="StatusRating.Catastrophic"/> and <see cref="StatusRating.Okay"/>.</returns>
        internal static float ClampedRating(float rawRating)
        {
            if (float.IsNaN(rawRating)) return StatusRating.Okay;
            return rawRating < StatusRating.Catastrophic ? StatusRating.Catastrophic
                : (rawRating > StatusRating.Okay ? StatusRating.Okay : rawRating);
        }
        public void ComputeOverallRatingAndSort(string target = "")
        {
            float? worstRating = null;
            StatusRatingRange worstRatingRange = StatusRatingRange.Superlative + 1;
            bool childPending = false;

            // keep track of the worst property rating
            StatusAuditAlert? worstAlert = null;
            StatusPropertyRange? worstAlertPropertyRange = null;
            foreach (StatusPropertyRange propertyRange in _propertyRanges)
            {
                string propertyPath = ComputeTarget(target, propertyRange.Name).TrimStart('/');
                // is there a thresholds to use to rate a property here or are there defaults?
                StatusPropertyThresholds? thresholds = (_thresholds ?? StatusPropertyThresholds.DefaultPropertyThresholds).GetThresholds(propertyPath);
                // is there a numeric value for which thresholds can be applied?
                float? minValue = null;
                if (!string.IsNullOrEmpty(propertyRange.MinValue))
                {
                    float f;
                    if (float.TryParse(propertyRange.MinValue, out f)) minValue = f;
                }
                float? maxValue = null;
                if (!string.IsNullOrEmpty(propertyRange.MaxValue))
                {
                    float f;
                    if (float.TryParse(propertyRange.MaxValue, out f)) maxValue = f;
                }
                // are there thresholds AND range values?
                if (thresholds != null && minValue != null && maxValue != null)
                {
                    // rate based on the value and the thresholds--is this now the worst rating?
                    StatusAuditAlert alert = thresholds.Rate(propertyRange.Name, minValue.Value, maxValue.Value);
                    if (worstAlert is null || alert.Rating < worstAlert.Rating)
                    {
                        worstAlert = alert;
                        worstAlertPropertyRange = propertyRange;
                    }
                }
            }
            WorstPropertyAlert = worstAlert;
            WorstPropertyRange = worstAlertPropertyRange;

            StatusAuditAlert? assignedAlert = OverallReport?.Alert;
            float? assignedRating = assignedAlert?.Rating;
            float? worstThresholdRating = WorstPropertyAlert?.Rating;

            // the overall rating will depend on the type of system we're rating
            switch (NatureOfSystem)
            {
                case StatusNatureOfSystem.ChildrenIrrelevant:
                    // there shouldn't be a report here--we don't care!
                    System.Diagnostics.Debug.Assert(OverallReport == null);
                    // let's rate the children anyway so we can add it to the report even if it doesn't affect the rating here
                    foreach (StatusResultsOrganizer child in _children)
                    {
                        // child not rated yet?
                        if (child.OverallRating == null) child.ComputeOverallRatingAndSort(ComputeTarget(target, child.Target));
                        // pending?
                        if (child.SomeRatingsPending) childPending = true;
                    }
                    OverallRating = StatusRating.Okay;
                    break;
                case StatusNatureOfSystem.Leaf:
                    // is there neither an explicitly-assigned rating nor a rating based on property thresholds?  bail out now without setting a new overall report
                    if (assignedRating == null && worstThresholdRating == null) return;
                    OverallRating = assignedRating;
                    break;
                default:
                case StatusNatureOfSystem.ChildrenHeterogenous:
                    // find the worst child rating
                    foreach (StatusResultsOrganizer child in _children)
                    {
                        // child not rated yet?
                        if (child.OverallRating == null) child.ComputeOverallRatingAndSort(ComputeTarget(target, child.Target));
                        if (child.OverallRating != null)
                        {
                            // aggregate results for each child
                            float childRating = child.OverallRating.Value;
                            if (worstRating == null || childRating < worstRating)
                            {
                                worstRating = childRating;
                            }
                            StatusRatingRange childRatingRange = StatusRating.FindRange(childRating);
                            if (childRatingRange < worstRatingRange)
                            {
                                worstRatingRange = childRatingRange;
                            }
                        }
                        // pending?
                        if (child.SomeRatingsPending) childPending = true;
                    }
                    OverallRating = assignedRating = worstRating ?? StatusRating.Okay;
                    break;
                case StatusNatureOfSystem.ChildrenHomogenous:
                    // compute both ways because we don't know up front what the distribution of status rating ranges is
                    float ratingSum = 0.0f;
                    float clampedRatingSum = 0.0f;
                    int ratedChildCount = 0;
                    // first count how many reports are in each clamped rating class
                    int[] childrenWithRating = new int[StatusRating.Ranges];
                    // make sure that if the number of clamped rating ranges is exactly three (we have to change the code here if this changes)
                    System.Diagnostics.Debug.Assert(ClampedRating(StatusRating.Catastrophic) - ClampedRating(StatusRating.Okay) <= 2);
                    // check all the child groups
                    foreach (StatusResultsOrganizer child in Children)
                    {
                        // child not rated yet?
                        if (child.OverallRating == null) child.ComputeOverallRatingAndSort(ComputeTarget(target, child.Target));
                        if (child.OverallRating != null)
                        {
                            float childRating = child.OverallRating.Value;
                            float clampedRating = ClampedRating(childRating);
                            StatusRatingRange range = StatusRating.FindRange(clampedRating);
                            clampedRatingSum += clampedRating;
                            ratingSum += childRating;
                            ++ratedChildCount;
                            System.Diagnostics.Debug.Assert(range >= StatusRatingRange.Fail && range <= StatusRatingRange.Okay);
                            ++childrenWithRating[(int)range];
                        }
                        // pending?
                        if (child.SomeRatingsPending) childPending = true;
                    }
                    float rating;
#pragma warning disable CA1508  // this check is explicitly to make sure that the subsequent condition is changed if the number of ranges changes
                    System.Diagnostics.Debug.Assert(StatusRating.Ranges == 5);
#pragma warning restore CA1508
                    // are all of the ratings in the same range?
                    if (childrenWithRating[(int)StatusRatingRange.Okay] == ratedChildCount || childrenWithRating[(int)StatusRatingRange.Alert] == ratedChildCount || childrenWithRating[(int)StatusRatingRange.Fail] == ratedChildCount)
                    {
                        // the rating is the average of all the children
                        rating = ratingSum / ratedChildCount;
                    }
                    else // we have ratings in more than one range, so the overall rating will be in the StatusRating.Alert range
                    {
                        // the average clamped rating cannot be out of range because it's clamped, and it cannot be on a boundary because one of the children was not in the same range with the others!
                        System.Diagnostics.Debug.Assert(clampedRatingSum / ratedChildCount > -1.0f && clampedRatingSum / ratedChildCount < 3.0f);
                        rating = StatusRating.Fail + ((clampedRatingSum / ratedChildCount) + 1.0f) / 4.0f;
                    }
                    OverallRating = assignedRating = rating;
                    break;
            }
            // is there a child that is pending (or is this node pending)?
            if (childPending || float.IsNaN(OverallReport?.Alert?.Rating ?? 0.0f)) SomeRatingsPending = true;

            // only one child and it counts?
            if (_children.Count == 1 && NatureOfSystem != StatusNatureOfSystem.ChildrenIrrelevant)
            {
                // move everything from that child up into us
                StatusResultsOrganizer child = _children[0];
                _propertyRanges.Clear();
                _propertyRanges.AddRange(child.PropertyRanges);
                _children.Clear();
                _children.AddRange(child.Children);
                NatureOfSystem = child.NatureOfSystem;
                OverallRating = child.OverallRating;
                OverallReport = child.OverallReport;
                Source = child.Source ?? Source;
                Target = ComputeTarget(Target, child.Target);
                // note that the child's children should already be sorted
            }
            else if (_children.Count > 1) // sort the children (if any)
            {
                _children.Sort((a, b) => (a.OverallRating ?? StatusRating.Okay).CompareTo(b.OverallRating ?? StatusRating.Okay));
            }
            // is the threshold rating worse than the assigned rating?
            if (worstThresholdRating < OverallRating)
            {
                System.Diagnostics.Debug.Assert(worstThresholdRating == WorstPropertyAlert?.Rating);
                OverallRating = worstThresholdRating;
                // was there no report before
                if (OverallReport == null)
                {
                    // create a new report with the worst property value
                    OverallReport = new StatusAuditReport(AmbientClock.UtcNow, TimeSpan.Zero, null, WorstPropertyAlert);
                }
                else // there was an existing report
                {
                    // replace that report with a new one with the alert from the worst property rating
                    OverallReport = new StatusAuditReport(OverallReport.AuditStartTime, OverallReport.AuditDuration, OverallReport.NextAuditTime, WorstPropertyAlert);
                }
            }
            // still no rating?  that's okay (literally)
            else if (OverallRating == null) OverallRating = StatusRating.Okay;
        }

        private static string ComputeTarget(string parentTargetSystem, string childTarget)
        {
            if (childTarget.StartsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(parentTargetSystem)) return childTarget;
            if (parentTargetSystem.EndsWith("/", StringComparison.Ordinal)) return parentTargetSystem + childTarget.TrimStart('.');
            return parentTargetSystem.TrimEnd('.') + "." + childTarget.TrimStart('.');
        }

        public override string ToString()
        {
            StringBuilder output = new();
            if (Source == null && string.IsNullOrEmpty(Target.Trim('/')))
            {
                output.Append("Overall:");
            }
            else
            {
                if (Source != null)
                {
                    output.Append(Source);
                    output.Append("->");
                }
                output.Append(Target);
                output.Append(':');
            }
            if (OverallReport != null)
            {
                output.Append(OverallReport.ToString());
            }
            return output.ToString();
        }
    }

    internal class AggregatedAlert
    {
        private static string RenderSource(string? source) { return source ?? Status.DefaultSource; }
        private static string RenderTarget(string? target) { return target ?? Status.DefaultTarget; }


        public StatusAuditAlert? CommonAlert { get; private set; }
        public float RatingSum { get; private set; }
        public List<string> Sources { get; private set; }
        public string Target { get; private set; }
        public List<StatusPropertyRange> PropertyRanges { get; private set; }
        public DateTimeRange TimeRange { get; private set; }
        public StatusAuditReport Report { get; private set; }
        public DateTimeRange AuditStartRange { get; private set; }
        public TimeSpanRange AuditDurationRange { get; private set; }
        public DateTime? NextAuditTime { get; private set; }

        public float AverageRating => RatingSum / Sources.Count;

        public AggregatedAlert(StatusResults initialResults)
        {
            StatusAuditReport report = initialResults.Report ?? new StatusAuditReport(initialResults.Time, TimeSpan.Zero, null, StatusAuditAlert.None);
            CommonAlert = report.Alert;
            RatingSum = report.Alert?.Rating ?? StatusRating.Okay;
            Sources = new List<string>();
            Sources.Add(RenderSource(initialResults.SourceSystem));
            Target = RenderTarget(initialResults.TargetSystem);
            TimeRange = new DateTimeRange(initialResults.Time);
            Report = report;
            AuditStartRange = new DateTimeRange(report.AuditStartTime);
            AuditDurationRange = new TimeSpanRange(report.AuditDuration);
            NextAuditTime = report.NextAuditTime;
            PropertyRanges = new List<StatusPropertyRange>();
        }

        public AggregatedAlert(string? source, string? target, DateTime time, StatusAuditReport? initialReport)
        {
            StatusAuditReport report = initialReport ?? new StatusAuditReport(time, TimeSpan.Zero, null, StatusAuditAlert.None);
            CommonAlert = report.Alert;
            RatingSum = report.Alert?.Rating ?? StatusRating.Okay;
            Sources = new List<string>();
            Sources.Add(RenderSource(source));
            Target = RenderTarget(target);
            TimeRange = new DateTimeRange(time);
            Report = report;
            AuditStartRange = new DateTimeRange(report.AuditStartTime);
            AuditDurationRange = new TimeSpanRange(report.AuditDuration);
            NextAuditTime = report.NextAuditTime;
            PropertyRanges = new List<StatusPropertyRange>();
        }

        public void Aggregate(StatusResults additionalResults)
        {
            StatusAuditReport report = additionalResults.Report ?? new StatusAuditReport(additionalResults.Time, TimeSpan.Zero, null, StatusAuditAlert.None);
            if (CommonAlert != report.Alert) throw new InvalidOperationException("Only results with the same alert can be aggregated!");
            if (Target != RenderTarget(additionalResults.TargetSystem)) throw new InvalidOperationException("Only results with the same target can be aggregated!");
            RatingSum += report.Alert?.Rating ?? StatusRating.Okay;
            Sources.Add(RenderSource(additionalResults.SourceSystem));
            TimeRange.AddSample(additionalResults.Time);
            AuditStartRange.AddSample(report.AuditStartTime);
            AuditDurationRange.AddSample(report.AuditDuration);
            NextAuditTime = (NextAuditTime == null) ? report.NextAuditTime : new DateTime(Math.Min(NextAuditTime.Value.Ticks, (report.NextAuditTime ?? DateTime.MaxValue).Ticks));
            PropertyRanges = new List<StatusPropertyRange>();
        }

        public void Aggregate(string? source, string target, DateTime time, StatusAuditReport? additionalReport)
        {
            StatusAuditReport report = additionalReport ?? new StatusAuditReport(time, TimeSpan.Zero, null, StatusAuditAlert.None);
            if (CommonAlert != report.Alert) throw new InvalidOperationException("Only results with the same alert can be aggregated!");
            if (Target != RenderTarget(target)) throw new InvalidOperationException("Only results with the same target can be aggregated!");
            RatingSum += report.Alert?.Rating ?? StatusRating.Okay;
            Sources.Add(RenderSource(source));
            TimeRange.AddSample(time);
            AuditStartRange.AddSample(report.AuditStartTime);
            AuditDurationRange.AddSample(report.AuditDuration);
            NextAuditTime = (NextAuditTime == null) ? report.NextAuditTime : new DateTime(Math.Min(NextAuditTime.Value.Ticks, (report.NextAuditTime ?? DateTime.MaxValue).Ticks));
            PropertyRanges = new List<StatusPropertyRange>();
        }

        internal bool CanBeAggregated(string target, StatusAuditReport? candidateReport)
        {
            return Target == RenderTarget(target) && candidateReport != null && CommonAlert == candidateReport.Alert;
        }

        internal string TerseSources
        {
            get
            {
                if (Sources.Count == 1) return Sources[0];
                return "[" + Sources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            }
        }
        internal string DetailsSources
        {
            get
            {
                if (Sources.Count == 1) return Sources[0] + " is";
                return "[" + string.Join(",", Sources) + "] are";
            }
        }
    }

    internal class StatusPropertyRange
    {
        public string Name { get; private set; }
        public string MinValue { get; private set; }
        public string MaxValue { get; private set; }

        public StatusPropertyRange(StatusProperty property)
        {
            Name = property.Name;
            MinValue = property.Value;
            MaxValue = property.Value;
        }
        public void Merge(string propertyValue)
        {
            if (propertyValue.CompareNaturalInvariant(MinValue) < 0)
            {
                MinValue = propertyValue;
            }
            if (propertyValue.CompareNaturalInvariant(MaxValue) > 0)
            {
                MaxValue = propertyValue;
            }
        }
        public override string ToString()
        {
            return Name + "=" + MinValue + ((MinValue == MaxValue) ? "" : ("-" + MaxValue));
        }
    }

    internal class DateTimeRange
    {
        public DateTime Earliest { get; private set; }
        public DateTime Latest { get; private set; }

        public DateTimeRange(DateTime time)
        {
            Earliest = Latest = time;
        }

        public void AddSample(DateTime time)
        {
            if (time < Earliest) Earliest = time;
            if (time > Latest) Latest = time;
        }

        public string ToShortTimeString()
        {
            return (Earliest == Latest)
                ? Earliest.ToString("t", System.Globalization.CultureInfo.InvariantCulture)
                : Earliest.ToString("t", System.Globalization.CultureInfo.InvariantCulture) + "-" + Latest.ToString("t", System.Globalization.CultureInfo.InvariantCulture);
        }
        public string ToLongTimeString()
        {
            return (Earliest == Latest)
                ? Earliest.ToString("T", System.Globalization.CultureInfo.InvariantCulture)
                : Earliest.ToString("T", System.Globalization.CultureInfo.InvariantCulture) + "-" + Latest.ToString("T", System.Globalization.CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToLongTimeString();
        }
    }

    internal class TimeSpanRange
    {
        public TimeSpan Shortest { get; private set; }
        public TimeSpan Longest { get; private set; }

        public TimeSpanRange(TimeSpan time)
        {
            Shortest = Longest = time;
        }

        public void AddSample(TimeSpan time)
        {
            if (time < Shortest) Shortest = time;
            if (time > Longest) Longest = time;
        }

        public string ToShortString()
        {
            return (Shortest == Longest)
                ? Shortest.ToShortHumanReadableString()
                : Shortest.ToShortHumanReadableString() + "-" + Longest.ToShortHumanReadableString();
        }
        public string ToLongString()
        {
            return (Shortest == Longest)
                ? Shortest.ToLongHumanReadableString()
                : Shortest.ToLongHumanReadableString() + "-" + Longest.ToLongHumanReadableString();
        }

        public override string ToString()
        {
            return ToLongString();
        }
    }
}
