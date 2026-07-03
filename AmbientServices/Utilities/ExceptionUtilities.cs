using System;
using System.Text;

namespace AmbientServices.Utilities;

/// <summary>
/// A static class that contains utilities for <see cref="System.Exception"/>.
/// </summary>
/// <remarks>
/// <pitch>The rendering engine behind <see cref="AmbientServices.Extensions.ExceptionExtensions"/>: builds the filtered, innermost-first exception string.</pitch>
/// <pledge>Inner exceptions render before the exceptions that wrap them, separated by blank lines; each renders as the bracketed exception type name and message (invariant culture) followed by its <see cref="FilteredStackTrace"/>.</pledge>
/// </remarks>
internal static class ExceptionUtilities
{
    internal static void BuildFilteredString(Exception exception, StringBuilder output)
    {
        Exception? innerException = exception.InnerException;
        if (innerException != null) BuildFilteredString(innerException, output);

        if (output.Length > 0) output.AppendLine();

        output.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "[{0}] {1}", exception.GetType().Name, exception.Message);
        output.AppendLine();
        output.Append(new FilteredStackTrace(exception));
    }
}
