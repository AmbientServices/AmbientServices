using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AmbientServices
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
        public static IEnumerable<Type> GetLoadableTypes(this System.Reflection.Assembly assembly)
        {
            try
            {
                if (assembly == null) throw new ArgumentNullException(nameof(assembly));
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // can't figure out how to force this exception for the moment, but this code is from several popular posts on the internet
                return TypesFromException(ex);
            }
        }
        /// <summary>
        /// Gets the loadable types from the <see cref="ReflectionTypeLoadException"/>.
        /// </summary>
        /// <param name="ex">The <see cref="ReflectionTypeLoadException"/> that was thrown.</param>
        /// <returns>The list of types that *are* loadable, as obtained from the exception.</returns>
        internal static IEnumerable<Type> TypesFromException(ReflectionTypeLoadException ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return ex.Types.Where(t => t != null);
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
    }
}
