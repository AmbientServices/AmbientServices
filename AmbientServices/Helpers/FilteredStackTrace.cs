using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static System.FormattableString;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A class that is a stack trace that always filters the output to hide file paths and not-very-helpful system stack frames.
    /// </summary>
    public class FilteredStackTrace : StackTrace
    {
        private readonly Lazy<StackFrame[]> _filteredFrames;

        private Lazy<StackFrame[]> FilterFrames()
        {
            return new Lazy<StackFrame[]>(() =>
            {
                List<StackFrame> filteredFrames = new List<StackFrame>();
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NET5_0_OR_GREATER
                StackFrame?[] baseFrames = base.GetFrames();
#else
                StackFrame[] baseFrames = base.GetFrames();
#endif
                if (baseFrames != null)
                {
                    filteredFrames.AddRange(FilterSystemAndMicrosoftFrames(baseFrames.Where(f => f != null)!));
                }
                return filteredFrames.ToArray();
            }, System.Threading.LazyThreadSafetyMode.PublicationOnly);
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class from the caller's frame.
        /// </summary>
        public FilteredStackTrace()
            : base()
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class using the provided exception object.
        /// </summary>
        /// <param name="e">The exception object from which to construct the stack trace.</param>
        /// <exception cref="System.ArgumentNullException">The parameter e is null.</exception>
        public FilteredStackTrace(Exception e)
            : base(e)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class from the caller's frame, skipping the specified number of frames.
        /// </summary>
        /// <param name="skipFrames">The number of frames up the stack from which to start the trace.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">The skipFrames parameter is negative.</exception>
        public FilteredStackTrace(int skipFrames)
            : base(skipFrames)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class that contains a single frame.
        /// </summary>
        /// <param name="frame">The frame that the System.Diagnostics.StackTrace object should contain.</param>
        public FilteredStackTrace(StackFrame frame)
            : base(frame)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class using the provided exception object and skipping the specified number of frames.
        /// </summary>
        /// <param name="e">The exception object from which to construct the stack trace.</param>
        /// <param name="skipFrames">The number of frames up the stack from which to start the trace.</param>
        /// <exception cref="System.ArgumentNullException">The parameter e is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The skipFrames parameter is negative.</exception>
        public FilteredStackTrace(Exception e, int skipFrames)
            : base(e, skipFrames)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Gets the number of frames in the stack trace.
        /// </summary>
        public override int FrameCount { get { return _filteredFrames.Value.Length; } }
        /// <summary>
        /// Checks to see if the specified object is equal to this one.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns><b>true</b> if the objects are equal, <b>false</b> if they are not.</returns>
        public override bool Equals(object? obj)
        {
            if (!(obj is FilteredStackTrace)) return false;
            return base.Equals((StackTrace)obj);
        }
        /// <summary>
        /// Computes a 32-bit hash code for the object.
        /// </summary>
        /// <returns>A 32-bit hash code for the object.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        /// <summary>
        /// Builds a readable representation of the stack trace.
        /// </summary>
        /// <returns>A readable representation of the stack trace.</returns>
        public override string ToString()
        {
            return ToString(_filteredFrames.Value);
        }
        /// <summary>
        /// Filters the specified enumeration of <see cref="StackFrame"/>s, removing all System and Microsoft
        /// stack frames except the first one.
        /// </summary>
        /// <param name="frames">The <see cref="StackFrame"/>s to filter.</param>
        /// <returns>A filtered enumeration of <see cref="StackFrame"/>s.</returns>
        public static IEnumerable<StackFrame> FilterSystemAndMicrosoftFrames(IEnumerable<StackFrame> frames)
        {
            if (frames != null)
            {
                bool first = true;
                foreach (StackFrame frame in frames)
                {
                    string fullMethodName = frame!.GetMethod()!.DeclaringType!.FullName!;  // I don't think the method of a stack frame or its declaring type or its name can be null
                    if (
                        !fullMethodName.StartsWith("AmbientServices.Async.", StringComparison.Ordinal) && (
                            first || (
                                    !fullMethodName.StartsWith("Microsoft.", StringComparison.Ordinal)
                                    && !fullMethodName.StartsWith("System.", StringComparison.Ordinal)
                                    && !fullMethodName.StartsWith("Amazon.", StringComparison.Ordinal)
                            )
                        )
                    )
                    {
                        yield return frame;
                    }
                    first = false;
                }
            }
        }
        internal static string FilterFilename(string filename)
        {
            if (filename == null) return string.Empty;
            return Path.GetFileName(filename);
        }
        /// <summary>
        /// Builds a readable representation of the stack trace.
        /// </summary>
        /// <param name="frames">An array of stack frames to build a string for.</param>
        /// <returns>A readable representation of the stack trace.</returns>
        /// <remarks>NOTE: Does NOT filter the stack frames--wrap the enumerator in <see cref="FilterFrames"/> if you want to filter out all System and Microsoft frames except the first one.</remarks>
        internal static string ToString(IEnumerable<StackFrame> frames)
        {
            StringBuilder output = new StringBuilder();
            foreach (StackFrame frame in frames)
            {
                string line = Invariant($" at {frame!.GetMethod()!.DeclaringType!.Name!}.{frame!.GetMethod()!.Name!} in {FilterFilename(frame!.GetFileName() ?? "<unknown>")}:{frame!.GetFileLineNumber()}.{frame!.GetFileColumnNumber()}");
                output.AppendLine(line);
            }
            return output.ToString();
        }
    }
}
