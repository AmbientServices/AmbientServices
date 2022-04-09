using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A static class with extension functions for <see cref="System.Reflection.Assembly"/>.
    /// </summary>
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Enumerates loadable types from the assembly, even if referencing one or more of the types throws a <see cref="ReflectionTypeLoadException"/> exception.
        /// </summary>
        /// <param name="assembly">The assembly to attempt to enumerate types from.</param>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
        [DebuggerNonUserCode]
        public static Type[] GetLoadableTypes(this System.Reflection.Assembly assembly)
        {
            Type[] types;
            //int loop = 0;
            //do
            //{
                try
                {
                    if (assembly == null) throw new ArgumentNullException(nameof(assembly));
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // can't figure out how to force this exception for the moment, but this code is from several popular posts on the internet
                    types = TypesFromException(ex);
                }
            //    if ((types?.Length ?? 0) > 0) break;
            //    System.Threading.Thread.Sleep(100);
            //} while (loop++ < 5);
            return types ?? Array.Empty<Type>();
        }
        /// <summary>
        /// Gets the loadable types from the <see cref="ReflectionTypeLoadException"/>.
        /// </summary>
        /// <param name="ex">The <see cref="ReflectionTypeLoadException"/> that was thrown.</param>
        /// <returns>The list of types that *are* loadable, as obtained from the exception.</returns>
        internal static Type[] TypesFromException(ReflectionTypeLoadException ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return ex.Types.Where(t => t != null).ToArray()!; // the where condition filters out null values!
        }
        /// <summary>
        /// Checks to see if this assembly refers to the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <param name="referredToAssembly">The referred to assembly to look for.</param>
        /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
        public static bool DoesAssemblyReferToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            return (assembly == referredToAssembly || assembly.GetReferencedAssemblies().FirstOrDefault(a => a.FullName == referredToAssembly.FullName) != null);
        }
        /// <summary>
        /// Gets the folder where the calling source code's project is located so that it can be stripped from stack trace dumps, but any subpath and file name can be retained.
        /// </summary>
        /// <param name="subfolders">The number of subfolders to remove from the path because the calling source is in a project subfolder.  Use zero if the calling source is in the folder you want to get, one if the calling source is in a source module in a project subfolder and you want the main project folder, and higher numbers if the source module is in a deeper folder.</param>
        /// <param name="skipFrames">The number of stack frames to skip to get to the "calling code".</param>
        /// <returns>The calling code's project's root folder path, if it could be determined, or null if it could not.</returns>
        public static string? GetCallingCodeSourceFolder(int subfolders = 0, int skipFrames = 0)
        {
            string? foldername = Path.GetDirectoryName(new System.Diagnostics.StackFrame(skipFrames + 1, true).GetFileName());
            while (foldername != null && subfolders-- > 0 && (foldername.Contains(System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || foldername.Contains(System.IO.Path.AltDirectorySeparatorChar, StringComparison.Ordinal))) foldername = Path.GetDirectoryName(foldername);
            return foldername;
        }
    }
}
