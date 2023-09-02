using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AmbientServices.Extensions
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
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            Type[] types;
            //int loop = 0;
            //do
            //{
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // can't figure out how to force this exception for the moment, but this code is from several popular posts on the internet
                types = AssemblyUtilities.TypesFromException(ex);
            }
            //    if ((types?.Length ?? 0) > 0) break;
            //    System.Threading.Thread.Sleep(100);
            //} while (loop++ < 5);
            if (types != null) return types;
            return Array.Empty<Type>();
        }
        /// <summary>
        /// Checks to see if this assembly refers directly to the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <param name="referredToAssembly">The referred to assembly to look for.</param>
        /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
        public static bool DoesAssemblyReferDirectlyToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            return (assembly == referredToAssembly || assembly.GetReferencedAssemblies().FirstOrDefault(a => a.FullName == referredToAssembly.FullName) != null);
        }
        /// <summary>
        /// Checks to see if this assembly refers to the specified assembly (directly or indirectly).
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <param name="referredToAssembly">The referred to assembly to look for.</param>
        /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
        public static bool DoesAssemblyReferToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (referredToAssembly == null) throw new ArgumentNullException(nameof(referredToAssembly));
            if (assembly == referredToAssembly) return true;
            foreach (AssemblyName referringAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (referringAssemblyName.FullName == referredToAssembly.FullName) return true;
                Assembly a = Assembly.ReflectionOnlyLoad(referringAssemblyName.FullName);
                if (DoesAssemblyReferToAssembly(a, referredToAssembly)) return true;
            }
            return false;
        }
    }
}
