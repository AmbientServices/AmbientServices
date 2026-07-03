using System;
using System.Diagnostics;

namespace AmbientServices.Extensions;

/// <summary>
/// A class that holds extensions to the <see cref="StackTrace"/> class.
/// </summary>
/// <remarks>
/// <pitch>One-call access to <see cref="FilteredStackTrace"/>'s noise filtering for any existing <see cref="StackTrace"/> instance.</pitch>
/// <pledge>Filtering is idempotent: a trace that is already a <see cref="FilteredStackTrace"/> renders unchanged, and any other trace is rendered with the same frame-filtering rules <see cref="FilteredStackTrace"/> applies.</pledge>
/// </remarks>
public static class StackTraceExtensions
{
    /// <summary>
    /// Gets a filtered string of the stack trace.
    /// </summary>
    /// <param name="input">The <see cref="StackTrace"/>.</param>
    /// <returns>A filtered string of the stack trace.</returns>
    public static string GetFilteredString(this StackTrace input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input), "The specified StackTrace must be non-null!");
        if (input is FilteredStackTrace) return input.ToString();
        return FilteredStackTrace.ToString(FilteredStackTrace.FilterFrames(input.GetFrames()));
    }
}
