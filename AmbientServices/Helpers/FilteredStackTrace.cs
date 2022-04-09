using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AmbientServices.Utility;
using static System.FormattableString;

namespace AmbientServices
{
    /// <summary>
    /// A class that is a stack trace that always filters the output to hide file paths and not-very-helpful system stack frames.
    /// </summary>
    public class FilteredStackTrace : StackTrace
    {
        private static readonly Regex NamespaceFilter = new(@"([^. ]*\.)*", RegexOptions.Compiled);

        private readonly Lazy<StackFrame[]> _filteredFrames;

        private Lazy<StackFrame[]> FilterFrames()
        {
            return new Lazy<StackFrame[]>(() =>
            {
                List<StackFrame> filteredFrames = new();
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NET5_0_OR_GREATER
                StackFrame?[] baseFrames = base.GetFrames();
#else
                StackFrame[] baseFrames = base.GetFrames();
#endif
                if (baseFrames != null)
                {
                    filteredFrames.AddRange(FilterFrames(baseFrames.Where(f => f != null)!));
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
        /// Initializes a new instance of the System.Diagnostics.StackTrace class from the caller's frame, optionally capturing source information.
        /// </summary>
        /// <param name="needFileInfo">true to capture the file name, line number, and column number; otherwise, false.</param>
        public FilteredStackTrace(bool needFileInfo)
            : base(needFileInfo)
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
        /// Initializes a new instance of the System.Diagnostics.StackTrace class, using the provided exception object and optionally capturing source information.
        /// </summary>
        /// <param name="e">The exception object from which to construct the stack trace.</param>
        /// <param name="needFileInfo">true to capture the file name, line number, and column number; otherwise, false.</param>
        /// <exception cref="System.ArgumentNullException">The parameter e is null.</exception>
        public FilteredStackTrace(Exception e, bool needFileInfo)
            : base(e, needFileInfo)
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
        /// Initializes a new instance of the System.Diagnostics.StackTrace class from the caller's frame, skipping the specified number of frames and optionally capturing source information.
        /// </summary>
        /// <param name="skipFrames">The number of frames up the stack from which to start the trace.</param>
        /// <param name="needFileInfo">true to capture the file name, line number, and column number; otherwise, false.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">The skipFrames parameter is negative.</exception>
        public FilteredStackTrace(int skipFrames, bool needFileInfo)
            : base(skipFrames, needFileInfo)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Initializes a new instance of the System.Diagnostics.StackTrace class using the provided exception object, skipping the specified number of frames and optionally capturing source information.
        /// </summary>
        /// <param name="e">The exception object from which to construct the stack trace.</param>
        /// <param name="skipFrames">The number of frames up the stack from which to start the trace.</param>
        /// <param name="needFileInfo">true to capture the file name, line number, and column number; otherwise, false.</param>
        /// <exception cref="System.ArgumentNullException">The parameter e is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The skipFrames parameter is negative.</exception>
        public FilteredStackTrace(Exception e, int skipFrames, bool needFileInfo)
            : base(e, skipFrames, needFileInfo)
        {
            _filteredFrames = FilterFrames();
        }
        /// <summary>
        /// Gets the number of frames in the stack trace.
        /// </summary>
        public override int FrameCount { get { return _filteredFrames.Value.Length; } }
        /// <summary>
        /// Gets the specified stack frame.
        /// </summary>
        /// <param name="index">The index of the stack frame requested.</param>
        /// <returns>The specified stack frame.</returns>
        public override StackFrame? GetFrame(int index)
        {
            // there seems to be a bug in the base implementation where it calls this derivative class function without bounding it with the corresponding derivative class limit
            if (index >= _filteredFrames.Value.Length) return base.GetFrame(index);
            return _filteredFrames.Value[index];
        }
        /// <summary>
        /// Returns a copy of all stack frames in the current stack trace.
        /// </summary>
        /// <returns>An array of type System.Diagnostics.StackFrame representing the function calls in the stack trace.</returns>
        public override StackFrame[] GetFrames()
        {
            return _filteredFrames.Value;
        }
        /// <summary>
        /// Filters the specified enumeration of <see cref="StackFrame"/>s, removing all System and Microsoft
        /// stack frames except the first one.
        /// </summary>
        /// <param name="frames">The <see cref="StackFrame"/>s to filter.</param>
        /// <returns>A filtered enumeration of <see cref="StackFrame"/>s.</returns>
        public static IEnumerable<StackFrame> FilterFrames(IEnumerable<StackFrame?> frames)
        {
            if (frames != null)
            {
                bool first = true;
                foreach (StackFrame? frame in frames)
                {
                    if (frame == null) continue;
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
            foreach (string sourcePathToSuppress in SourcePathsToErase)
            {
                if (filename.StartsWith(sourcePathToSuppress, StringComparison.Ordinal))
                {
                    filename = filename.Substring(sourcePathToSuppress.Length + 1);
                }
            }
            return filename.TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        private static readonly ConcurrentHashSet<string> SourcePathsToErase = InitializeSourcePathsToWrase();
        private static ConcurrentHashSet<string> InitializeSourcePathsToWrase()
        {
            ConcurrentHashSet<string> dict = new();
            string? projectPath = AssemblyExtensions.GetCallingCodeSourceFolder(1, 1);
            if (projectPath != null && !string.IsNullOrEmpty(projectPath) && (projectPath.Contains(System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || projectPath.Contains(System.IO.Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
            {
                // suppress the folder for the project folder so we get the project folder name in the output
                dict.Add(projectPath);
            }
            return dict;
        }
        /// <summary>
        /// Suppresses the calling code's source path folders from stack traces.
        /// </summary>
        /// <param name="subfolders">The number of subfolders the calling code's source module is in, with zero meaning the calling source module is in the root of the project.</param>
        public static void SuppressCallingSourcePath(int subfolders = 0)
        {
            string? projectPath = AssemblyExtensions.GetCallingCodeSourceFolder(subfolders, 1)?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (projectPath != null && !string.IsNullOrEmpty(projectPath)) SourcePathsToErase.Add(projectPath);
        }
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
        /// Builds a readable representation of the stack trace.
        /// </summary>
        /// <param name="frames">An array of stack frames to build a string for.</param>
        /// <returns>A readable representation of the stack trace.</returns>
        /// <remarks>
        /// Note that this function does not filter system frames from the list given.  
        /// If the caller wants to filter system frames, they need to filter the input frames using <see cref="FilterFrames(IEnumerable{StackFrame})"/>.
        /// </remarks>
        public static string ToString(IEnumerable<StackFrame> frames)
        {
            try
            {
                StringBuilder output = new();
                if (frames != null)
                {
                    foreach (StackFrame frame in FilterFrames(frames))
                    {
                        if (frame == null) continue;
                        string line = Invariant($" at {NamespaceFilter.Replace(frame.GetMethod()?.DeclaringType?.Name ?? "<unknwown>", "")}.{frame.GetMethod()?.Name ?? "<unknwown>"} in {FilterFilename(frame.GetFileName() ?? "<unknown>")}:{frame.GetFileLineNumber()}.{frame.GetFileColumnNumber()}");
                        output.AppendLine(line);
                    }
                }
                return output.ToString();
            }
#pragma warning disable CA1031 // Do not catch general exception types--this string will be preferable to *any* exception
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return "Error generating stack trace string: " + ex.ToString();
            }
        }
    }
}
