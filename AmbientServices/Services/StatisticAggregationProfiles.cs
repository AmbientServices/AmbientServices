using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices;

/// <summary>
/// The predefined ways a statistic's values combine over time and across systems, each fixing both aggregations and the order they are applied in.
/// </summary>
/// <remarks>
/// <pitch>
/// One choice per statistic in place of four flag sets and two preferences that can be combined into nonsense: a statistic says what it is (a count, a
/// level, a mean, ...) and every consumer derives the same aggregations, in the same order, from that.
/// </pitch>
/// <pledge>
/// A profile describes the statistic's VALUE PER PERIOD: for a <see cref="AmbientStatisticType.Cumulative"/> statistic, whose samples are running totals,
/// that value is the change over the period; for every other type it is the sample. See <see cref="AggregationProfiles"/> for what each profile fixes.
/// </pledge>
/// </remarks>
public enum AggregationProfile
{
    /// <summary>No profile: the statistic's aggregations match none of the predefined combinations.</summary>
    None = 0,
    /// <summary>A count of events or an amount of work: summed over time and across systems, in either order.  The only profile a cumulative statistic may have.</summary>
    Count,
    /// <summary>A level read as its latest value, such as a running total or a queue depth now: the most recent value of each system, summed across systems.</summary>
    Total,
    /// <summary>A level each system holds, such as pending requests: summed across systems at each moment, then averaged over time.</summary>
    Level,
    /// <summary>A per-system rate, ratio or pressure: averaged across systems at each moment, then averaged over time.</summary>
    Mean,
    /// <summary>A high-water mark: the largest value over time and across systems, in either order.</summary>
    Peak,
    /// <summary>A low-water mark: the smallest value over time and across systems, in either order.</summary>
    Floor,
    /// <summary>A value every system reports the same way, such as a version or a setting: the most recent value over time and across systems, in either order.</summary>
    Latest,
}

/// <summary>Which of a statistic's two aggregations is applied first.</summary>
public enum AggregationOrder
{
    /// <summary>The two aggregations give the same result in either order (they are the same one of sum, min, max or most recent).</summary>
    Either = 0,
    /// <summary>Each system's values are aggregated over the period first, then those results across systems.</summary>
    OverTimeFirst,
    /// <summary>The systems' values at each moment are aggregated across systems first, then those results over the period.</summary>
    AcrossSystemsFirst,
}

/// <summary>One way to aggregate a statistic's values: over time, across systems, and in which order.  Immutable.</summary>
public sealed class AggregationView
{
    /// <summary>Constructs a view.</summary>
    /// <param name="name">A short, stable, lower-case name for the view, unique within its profile ("preferred" for the profile's own).</param>
    /// <param name="temporal">The single aggregation over time.</param>
    /// <param name="spatial">The single aggregation across systems.</param>
    /// <param name="order">Which aggregation is applied first.</param>
    public AggregationView(string name, AggregationTypes temporal, AggregationTypes spatial, AggregationOrder order)
    {
        Name = name;
        Temporal = temporal;
        Spatial = spatial;
        Order = order;
    }
    /// <summary>A short, stable, lower-case name for the view, unique within its profile ("preferred" for the profile's own).</summary>
    public string Name { get; }
    /// <summary>The single aggregation over time.</summary>
    public AggregationTypes Temporal { get; }
    /// <summary>The single aggregation across systems.</summary>
    public AggregationTypes Spatial { get; }
    /// <summary>Which aggregation is applied first.</summary>
    public AggregationOrder Order { get; }
}

/// <summary>What one <see cref="AggregationProfile"/> fixes: its preferred view, and the alternative views a consumer may offer instead.  Immutable.</summary>
public sealed class AggregationProfileDescription
{
    /// <summary>Constructs a description.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="preferred">The view consumers use unless asked for another.</param>
    /// <param name="alternatives">The other views that make sense for the profile, each as fixed as the preferred one.</param>
    public AggregationProfileDescription(AggregationProfile profile, AggregationView preferred, IReadOnlyList<AggregationView> alternatives)
    {
        Profile = profile;
        Preferred = preferred;
        Alternatives = alternatives;
    }
    /// <summary>The profile.</summary>
    public AggregationProfile Profile { get; }
    /// <summary>The view consumers use unless asked for another.</summary>
    public AggregationView Preferred { get; }
    /// <summary>The other views that make sense for the profile, each as fixed as the preferred one.</summary>
    public IReadOnlyList<AggregationView> Alternatives { get; }
    /// <summary>Every view of the profile, the preferred one first.</summary>
    public IEnumerable<AggregationView> Views => new[] { Preferred }.Concat(Alternatives);
    /// <summary>Every aggregation over time any view uses.</summary>
    public AggregationTypes TemporalAggregationTypes => Views.Aggregate(AggregationTypes.None, static (all, view) => all | view.Temporal);
    /// <summary>Every aggregation across systems any view uses.</summary>
    public AggregationTypes SpatialAggregationTypes => Views.Aggregate(AggregationTypes.None, static (all, view) => all | view.Spatial);
}

/// <summary>
/// The table of <see cref="AggregationProfile"/>s, and the mapping between a profile and the aggregation preferences an <see cref="IAmbientStatisticReader"/> reports.
/// </summary>
/// <remarks>
/// <pitch>Says what each profile means, which profile a statistic declared the older way has, and which preferences declaring a profile sets.</pitch>
/// <pledge>
/// Each profile's PREFERRED view (temporal, spatial, order), over the statistic's value per period (see <see cref="AggregationProfile"/>):
/// Count (sum, sum, either); Total (most recent, sum, over time first); Level (average, sum, across systems first); Mean (average, average, across systems
/// first); Peak (max, max, either); Floor (min, min, either); Latest (most recent, most recent, either).  An order is "either" exactly when both
/// aggregations are the same one of sum, min, max or most recent, since only then do the two orders agree.
/// The order follows from the pair: an aggregation over time that only one system can do (most recent: each system's own latest reading) is applied
/// first; an aggregation across systems that combines simultaneous values (a total at a moment) is applied first.
/// REPORTED PREFERENCES (<see cref="IAmbientStatisticReader.PreferredTemporalAggregationType"/> and <see cref="IAmbientStatisticReader.PreferredSpatialAggregationType"/>)
/// are over the statistic's SAMPLES, which differ from its values per period only for a cumulative statistic: its samples are running totals, so its
/// Count is reported as most recent over time (the change over a period is the difference of consecutive most recent totals) and sum across systems.
/// Every other profile is reported as its preferred pair.  Each (type, reported pair) names at most one profile, so a statistic's profile is recovered from
/// its preferences alone, whichever way it was declared; a pair no profile reports has <see cref="AggregationProfile.None"/>, which consumers may flag as a
/// misdeclaration.  A cumulative statistic's only profile is Count, and the only aggregation over time it reports is most recent; a min statistic's default
/// is Floor, a max statistic's Peak, a raw statistic's Mean.  The built-in execution time has no profile: it is the denominator of rates, averaged across
/// systems so that a fleet's rate is its summed changes over one period's length.
/// The names, preferred views and alternative views are a public contract: consumers persist and exchange profile and view names.
/// </pledge>
/// </remarks>
public static class AggregationProfiles
{
    private const string PreferredName = "preferred";

    private static readonly Dictionary<AggregationProfile, AggregationProfileDescription> _Descriptions = new()
    {
        [AggregationProfile.Count] = new(AggregationProfile.Count, new(PreferredName, AggregationTypes.Sum, AggregationTypes.Sum, AggregationOrder.Either),
            [
                new("busiest-system", AggregationTypes.Sum, AggregationTypes.Max, AggregationOrder.OverTimeFirst),
                new("per-system", AggregationTypes.Sum, AggregationTypes.Average, AggregationOrder.OverTimeFirst),
                new("peak-period", AggregationTypes.Max, AggregationTypes.Sum, AggregationOrder.AcrossSystemsFirst),
            ]),
        [AggregationProfile.Total] = new(AggregationProfile.Total, new(PreferredName, AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationOrder.OverTimeFirst),
            [
                new("average-total", AggregationTypes.Average, AggregationTypes.Sum, AggregationOrder.AcrossSystemsFirst),
                new("peak-total", AggregationTypes.Max, AggregationTypes.Sum, AggregationOrder.AcrossSystemsFirst),
                new("largest-system", AggregationTypes.MostRecent, AggregationTypes.Max, AggregationOrder.OverTimeFirst),
            ]),
        [AggregationProfile.Level] = new(AggregationProfile.Level, new(PreferredName, AggregationTypes.Average, AggregationTypes.Sum, AggregationOrder.AcrossSystemsFirst),
            [
                new("latest-total", AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationOrder.OverTimeFirst),
                new("peak-total", AggregationTypes.Max, AggregationTypes.Sum, AggregationOrder.AcrossSystemsFirst),
                new("busiest-system", AggregationTypes.Average, AggregationTypes.Max, AggregationOrder.OverTimeFirst),
            ]),
        [AggregationProfile.Mean] = new(AggregationProfile.Mean, new(PreferredName, AggregationTypes.Average, AggregationTypes.Average, AggregationOrder.AcrossSystemsFirst),
            [
                new("each-system-equally", AggregationTypes.Average, AggregationTypes.Average, AggregationOrder.OverTimeFirst),
                new("worst-system", AggregationTypes.Average, AggregationTypes.Max, AggregationOrder.OverTimeFirst),
                new("best-system", AggregationTypes.Average, AggregationTypes.Min, AggregationOrder.OverTimeFirst),
                new("peak", AggregationTypes.Max, AggregationTypes.Max, AggregationOrder.Either),
            ]),
        [AggregationProfile.Peak] = new(AggregationProfile.Peak, new(PreferredName, AggregationTypes.Max, AggregationTypes.Max, AggregationOrder.Either),
            [
                new("typical-system", AggregationTypes.Max, AggregationTypes.Average, AggregationOrder.OverTimeFirst),
            ]),
        [AggregationProfile.Floor] = new(AggregationProfile.Floor, new(PreferredName, AggregationTypes.Min, AggregationTypes.Min, AggregationOrder.Either),
            [
                new("typical-system", AggregationTypes.Min, AggregationTypes.Average, AggregationOrder.OverTimeFirst),
            ]),
        [AggregationProfile.Latest] = new(AggregationProfile.Latest, new(PreferredName, AggregationTypes.MostRecent, AggregationTypes.MostRecent, AggregationOrder.Either), []),
    };

    /// <summary>Every profile but <see cref="AggregationProfile.None"/>, in declaration order.</summary>
    public static IReadOnlyList<AggregationProfile> All { get; } = [.. _Descriptions.Keys];

    /// <summary>What <paramref name="profile"/> fixes.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is <see cref="AggregationProfile.None"/> or not a defined profile.</exception>
    public static AggregationProfileDescription Describe(AggregationProfile profile)
    {
        return _Descriptions.TryGetValue(profile, out AggregationProfileDescription? description) ? description : throw new ArgumentOutOfRangeException(nameof(profile), profile, "Only a defined profile other than None has a description.");
    }

    /// <summary>The profile a statistic of <paramref name="type"/> has when it is declared with none.</summary>
    /// <param name="type">The statistic's type.</param>
    /// <returns>Count for cumulative, Floor for min, Peak for max, and Mean otherwise.</returns>
    public static AggregationProfile DefaultFor(AmbientStatisticType type)
    {
        return type switch
        {
            AmbientStatisticType.Cumulative => AggregationProfile.Count,
            AmbientStatisticType.Min => AggregationProfile.Floor,
            AmbientStatisticType.Max => AggregationProfile.Peak,
            _ => AggregationProfile.Mean,
        };
    }

    /// <summary>Whether a statistic of <paramref name="type"/> may have <paramref name="profile"/>: a cumulative statistic only Count, any other type any profile.</summary>
    /// <param name="type">The statistic's type.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>Whether the profile is allowed.</returns>
    public static bool Allows(AmbientStatisticType type, AggregationProfile profile)
    {
        if (!_Descriptions.ContainsKey(profile)) return false;
        return type != AmbientStatisticType.Cumulative || profile == AggregationProfile.Count;
    }

    /// <summary>The preferences a statistic of <paramref name="type"/> with <paramref name="profile"/> reports (see the Pledge): over its samples, not its values per period.</summary>
    /// <param name="type">The statistic's type.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>The reported preferred pair, and the allowed sets every view of the profile uses.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not one a statistic of <paramref name="type"/> may have.</exception>
    public static (AggregationTypes PreferredTemporal, AggregationTypes PreferredSpatial, AggregationTypes Temporal, AggregationTypes Spatial) ReportedFor(AmbientStatisticType type, AggregationProfile profile)
    {
        if (!Allows(type, profile)) throw new ArgumentOutOfRangeException(nameof(profile), profile, $"A {type} statistic may not have the {profile} profile.");
        AggregationProfileDescription description = Describe(profile);
        // A RUNNING TOTAL's change over a period is the difference of consecutive most recent totals, so a sum of changes is reported as most recent.
        // ITS ONLY AGGREGATION OVER TIME is then most recent: a running total says nothing of the largest or smallest change within a period.
        if (type == AmbientStatisticType.Cumulative) return (AggregationTypes.MostRecent, description.Preferred.Spatial, AggregationTypes.MostRecent, description.SpatialAggregationTypes);
        return (description.Preferred.Temporal, description.Preferred.Spatial, description.TemporalAggregationTypes, description.SpatialAggregationTypes);
    }

    /// <summary>The profile a statistic of <paramref name="type"/> reporting this preferred pair has, or <see cref="AggregationProfile.None"/> when no profile reports it.</summary>
    /// <param name="type">The statistic's type.</param>
    /// <param name="preferredTemporal">The reported preferred aggregation over time.</param>
    /// <param name="preferredSpatial">The reported preferred aggregation across systems.</param>
    /// <returns>The profile, or <see cref="AggregationProfile.None"/>.</returns>
    public static AggregationProfile Of(AmbientStatisticType type, AggregationTypes preferredTemporal, AggregationTypes preferredSpatial)
    {
        foreach (AggregationProfile profile in All)
        {
            if (!Allows(type, profile)) continue;
            (AggregationTypes temporal, AggregationTypes spatial, _, _) = ReportedFor(type, profile);
            if (temporal == preferredTemporal && spatial == preferredSpatial) return profile;
        }
        return AggregationProfile.None;
    }

    /// <summary>The profile <paramref name="reader"/>'s preferences report (see <see cref="Of(AmbientStatisticType, AggregationTypes, AggregationTypes)"/>).</summary>
    /// <param name="reader">The statistic.</param>
    /// <returns>The profile, or <see cref="AggregationProfile.None"/>.</returns>
    public static AggregationProfile Of(IAmbientStatisticReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        return Of(reader.StatisticType, reader.PreferredTemporalAggregationType, reader.PreferredSpatialAggregationType);
    }
}
