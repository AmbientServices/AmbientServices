using System;

namespace AmbientServices.Extensions;

/// <summary>
/// A static class that extends <see cref="System.TimeSpan"/>.
/// </summary>
/// <remarks>
/// <pitch>Human-oriented duration handling: parse strings like &quot;30s&quot;, &quot;5 minutes&quot;, or &quot;1:30:00&quot; into a <see cref="TimeSpan"/>, and render a <see cref="TimeSpan"/> as a short (&quot;90m&quot;) or long (&quot;90 minutes&quot;) approximate string.</pitch>
/// <pledge>
/// Parsing accepts standard colon-delimited <see cref="TimeSpan"/> syntax, or a number followed by an optional unit (years, months, days, hours, minutes, seconds, or milliseconds, spelled out or abbreviated, case-insensitive except that lowercase &quot;m&quot; means minutes while uppercase &quot;M&quot; means months); a bare number is interpreted as ticks, and unparseable input returns null rather than throwing.  Years and months convert using calendar-average lengths (365.25 and 30.4375 days).
/// Rendering is lossy by design: it picks a single unit large enough for the value to read comfortably, keeps at most one decimal digit (and only for values under ten), pluralizes the long form, uses the invariant culture, and prefixes negative durations with a minus sign.  Round-tripping through these strings is approximate, not exact.
/// </pledge>
/// </remarks>
public static class TimeSpanExtensions
{
    private static readonly char[] Alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Numeric = "-0123456789. \t".ToCharArray();
    /// <summary>
    /// Attempts to parse a string as a timespan.
    /// </summary>
    /// <param name="span">The candidate string.</param>
    /// <returns>A <see cref="TimeSpan"/>, if one could be parsed, or <b>null</b> if not.</returns>
    public static TimeSpan? TryParseTimeSpan(this string span)
    {
        if (string.IsNullOrEmpty(span)) return null;
        // is a span specified and does it contain a : and does it parse correctly as a timespan?
        TimeSpan timeSpan;
        if (span.Contains(':', StringComparison.Ordinal) && TimeSpan.TryParse(span, out timeSpan))
        {
            // use that timespan
            return timeSpan;
        }
        // split out the numeric part
        double units;
        if (!double.TryParse(span.TrimEnd(Alpha), out units))
        {
            return null;
        }
        // figure out the unit type
        string unitType = span.TrimStart(Numeric);
        // handle "M" specially because it's months as opposed to minutes
        if (unitType == "m") unitType = "MINUTES";
        return unitType.ToUpperInvariant() switch {
            "Y" or "YEAR" or "YEARS" => TimeSpan.FromDays(365.25 * units),
            "M" or "MONTH" or "MONTHS" => TimeSpan.FromDays(30.4375 * units),
            "D" or "DAY" or "DAYS" => TimeSpan.FromDays(units),
            "H" or "HOUR" or "HOURS" => TimeSpan.FromHours(units),
            "MINUTE" or "MINUTES" => TimeSpan.FromMinutes(units),
            "S" or "SECOND" or "SECONDS" => TimeSpan.FromSeconds(units),
            "MS" or "MILLISECOND" or "MILLISECONDS" => TimeSpan.FromMilliseconds(units),
            _ => TimeSpan.FromTicks((long)units),
        };
    }
    /// <summary>
    /// Gets a short string representing the specified timespan.
    /// </summary>
    /// <param name="duration">The <see cref="TimeSpan"/> whose string representation is to be generated</param>
    /// <returns>An easily human-readable string representing the time span with a postfix character indicating the units (ms, s, m, h, d).</returns>
    public static string ToShortHumanReadableString(this TimeSpan duration)
    {
        string sign;
        TimeSpan absTimeSpan;
        if (duration.Ticks < 0)
        {
            sign = "-";
            absTimeSpan = new TimeSpan(-duration.Ticks);
        }
        else
        {
            sign = "";
            absTimeSpan = new TimeSpan(duration.Ticks);
        }
        if (MatchUnits(absTimeSpan.TotalDays, 731))
        {
            return UnitString(sign, absTimeSpan.TotalDays / 365.25, "Y");
        }
        if (MatchUnits(absTimeSpan.TotalDays, 61))
        {
            return UnitString(sign, absTimeSpan.TotalDays / 30.4375, "M");
        }
        if (MatchUnits(absTimeSpan.TotalHours, 48))
        {
            return UnitString(sign, absTimeSpan.TotalDays, "D");
        }
        if (MatchUnits(absTimeSpan.TotalMinutes, 120))
        {
            return UnitString(sign, absTimeSpan.TotalHours, "h");
        }
        if (MatchUnits(absTimeSpan.TotalSeconds, 120))
        {
            return UnitString(sign, absTimeSpan.TotalMinutes, "m");
        }
        if (MatchUnits(absTimeSpan.TotalMilliseconds, 2000))
        {
            return UnitString(sign, absTimeSpan.TotalSeconds, "s");
        }
        return UnitString(sign, absTimeSpan.TotalMilliseconds, "ms");
    }
    /// <summary>
    /// Gets a long string representing the specified timespan.
    /// </summary>
    /// <param name="duration">The <see cref="TimeSpan"/> whose string is to be generated.</param>
    /// <returns>An easily human-readable string representing the time span with a postfix string indicating the units (millisecond, second, minute, hour, day).</returns>
    public static string ToLongHumanReadableString(this TimeSpan duration)
    {
        string sign;
        TimeSpan absTimeSpan;
        if (duration.Ticks < 0)
        {
            sign = "-";
            absTimeSpan = new TimeSpan(-duration.Ticks);
        }
        else
        {
            sign = "";
            absTimeSpan = new TimeSpan(duration.Ticks);
        }
        if (MatchUnits(absTimeSpan.TotalDays, 731))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalDays / 365.25, " Year");
        }
        if (MatchUnits(absTimeSpan.TotalDays, 61))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalDays / 30.4375, " Month");
        }
        if (MatchUnits(absTimeSpan.TotalHours, 48))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalDays, " Day");
        }
        if (MatchUnits(absTimeSpan.TotalMinutes, 120))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalHours, " hour");
        }
        if (MatchUnits(absTimeSpan.TotalSeconds, 120))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalMinutes, " minute");
        }
        if (MatchUnits(absTimeSpan.TotalMilliseconds, 2000))
        {
            return UnitStringWithPlural(sign, absTimeSpan.TotalSeconds, " second");
        }
        return UnitStringWithPlural(sign, absTimeSpan.TotalMilliseconds, " millisecond");
    }
    private static bool MatchUnits(double total, int count)
    {
        if (total < 2.0)
        {
            return false;
        }
        return total >= count;
    }
    private static string UnitString(string prefix, double count, string postfix)
    {
        int intPart = (int)count;
        int firstDecimal = ((int)(count * 10.0) % 10);
        if (intPart < 10 && firstDecimal != 0)
        {
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
    }
    private static string UnitStringWithPlural(string prefix, double count, string postfix)
    {
        int intPart = (int)count;
        int firstDecimal = ((int)(count * 10.0) % 10);
        if (intPart < 10 && firstDecimal != 0)
        {
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        // else nothing past the decimal
        if (intPart != 1) postfix += "s";
        return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
    }
}
