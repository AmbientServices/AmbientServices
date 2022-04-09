using System;
using System.Diagnostics;

namespace AmbientServices
{
    /// <summary>
    /// A class that holds extensions to the <see cref="StackTrace"/> class.
    /// </summary>
    public static class StackTraceExtensions
    {
        /// <summary>
        /// Gets a filtered string of the stack trace.
        /// </summary>
        /// <param name="input">The <see cref="StackTrace"/>.</param>
        /// <returns>A filtered string of the stack trace.</returns>
        public static string GetFilteredString(this StackTrace input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input), "The speficied StackTrace must be non-null!");
            if (input is FilteredStackTrace) return input.ToString();
            return FilteredStackTrace.ToString(FilteredStackTrace.FilterFrames(input.GetFrames()));
        }
    }
}
