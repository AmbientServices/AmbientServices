using System;

namespace AmbientServices;

/// <summary>
/// A static class that extends <see cref="System.DateTime"/>.
/// </summary>
/// <remarks>
/// <pitch>Compact, human-readable labels for time windows: <see cref="WindowId"/> names which window a moment falls in at a chosen resolution, and <see cref="WindowSize"/> renders a duration as a short unit string — both intended for embedding in keys and report output rather than for parsing.</pitch>
/// <pledge>Window identifiers are derived from the invariant-culture universal sortable format of the given <see cref="DateTime"/>, keeping only the fields significant near the requested resolution: any two moments within roughly one resolution of each other get distinct identifiers, while moments much further apart may share one (fields coarser than the resolution are dropped to keep labels short).  <see cref="WindowSize"/> renders using the largest unit that keeps the magnitude readable (ms, s, m, h, D, M, Y), showing at most one decimal place and preserving sign.</pledge>
/// <plan>Pure string slicing of the fixed-position "u" format at per-resolution-bucket positions; durations pick their unit at roughly five-of-the-next-unit thresholds, approximating months as 30.5 days and years as 365.25 days.  No clock access and no state.</plan>
/// </remarks>
public static class WindowScope
{
    /// <summary>
    /// Gets a timestamp for the specified <see cref="DateTime"/>, with a resolution appropriate for the units near the specified <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to get an window identifier for.</param>
    /// <param name="resolution">A <see cref="TimeSpan"/> indicating what type of resolution is needed.</param>
    /// <returns>A string containing a timespan that will be distinguishable from timestamps for other <see cref="DateTime"/>s plus or minus the specified <see cref="TimeSpan"/>.</returns>
    public static string WindowId(DateTime dateTime, TimeSpan resolution)
    {
        string timestamp = dateTime.ToString("u", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('Z').Replace(' ', '_');
        if (resolution > TimeSpan.FromDays(365))
        {
            timestamp = timestamp.Substring(0, 7);
        }
        else if (resolution > TimeSpan.FromDays(30))
        {
            timestamp = timestamp.Substring(5, 5);
        }
        else if (resolution > TimeSpan.FromDays(1))
        {
            timestamp = timestamp.Substring(8, 5);
        }
        else if (resolution > TimeSpan.FromHours(1))
        {
            timestamp = timestamp.Substring(11, 5);
        }
        else if (resolution > TimeSpan.FromMinutes(1))
        {
            timestamp = timestamp.Substring(11, 8);
        }
        else if (resolution > TimeSpan.FromSeconds(1))
        {
            timestamp = string.Concat(timestamp.Substring(14), ".", dateTime.Millisecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            timestamp = string.Concat(timestamp.Substring(17), ".", dateTime.Millisecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return timestamp;
    }
    private static string UnitString(string prefix, double count, string postfix)
    {
        double rounded = Math.Round(count, 1);
        int intPart = (int)rounded;
        int firstDecimal = (int)((rounded * 10) - (intPart * 10));
        if (intPart < 10 && firstDecimal != 0)
        {
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        return prefix + Math.Round(count, 0).ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
    }
    /// <summary>
    /// Gets a short string representing the specified timespan.
    /// </summary>
    /// <param name="duration">The <see cref="TimeSpan"/> whose string representation is to be generated</param>
    /// <returns>An easily human-readable string representing the time span with a postfix character indicating the units (ms, s, m, h, d).</returns>
    public static string WindowSize(TimeSpan duration)
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
        if (absTimeSpan.TotalDays > 1827)
        {
            return UnitString(sign, absTimeSpan.TotalDays / 365.25, "Y");
        }
        if (absTimeSpan.TotalDays > 160)
        {
            return UnitString(sign, absTimeSpan.TotalDays / 30.5, "M");
        }
        if (absTimeSpan.TotalDays > 5)
        {
            return UnitString(sign, absTimeSpan.TotalDays, "D");
        }
        if (absTimeSpan.TotalHours > 5)
        {
            return UnitString(sign, absTimeSpan.TotalHours, "h");
        }
        if (absTimeSpan.TotalMinutes > 5)
        {
            return UnitString(sign, absTimeSpan.TotalMinutes, "m");
        }
        if (absTimeSpan.TotalSeconds > 5)
        {
            return UnitString(sign, absTimeSpan.TotalSeconds, "s");
        }
        return UnitString(sign, absTimeSpan.TotalMilliseconds, "ms");
    }
}
