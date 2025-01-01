using AmbientServices.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace AmbientServices.Extensions;

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
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(assembly);
#else
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
#endif
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
        return types ?? Array.Empty<Type>();
    }
    /// <summary>
    /// Checks to see if this assembly refers directly to the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <param name="referredToAssembly">The referred to assembly to look for.</param>
    /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
    public static bool DoesAssemblyReferDirectlyToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(assembly);
#else
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
#endif
        return (assembly == referredToAssembly || assembly.GetReferencedAssemblies().FirstOrDefault(a => a.FullName == referredToAssembly.FullName) != null);
    }
    /// <summary>
    /// Checks to see if this assembly refers to the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <param name="referredToAssembly">The referred to assembly to look for.</param>
    /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
    public static bool DoesAssemblyReferToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
    {
        // I attempted to implement a recursive algorithm, but it ended up causing hangs in the tests, even when I added a HashSet to prevent loops,
        // and I'm beginning to doubt whether a recursive algorithm is needed to list indirectly-referenced assemblies
        return DoesAssemblyReferDirectlyToAssembly(assembly, referredToAssembly);
    }
}
