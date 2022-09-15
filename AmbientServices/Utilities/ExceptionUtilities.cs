using System;
using System.Text;

namespace AmbientServices.Utilities
{
    /// <summary>
    /// A static class that contains utilities for <see cref="System.Exception"/>.
    /// </summary>
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
}
