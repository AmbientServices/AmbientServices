using AmbientServices.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace AmbientServices;

/// <summary>
/// An attribute to identify classes implementing an ambient service default implementation.
/// </summary>
/// <remarks>
/// <pitch>The zero-registration way to supply the fallback implementation for a service interface: mark the class and every consumer of the interface picks it up automatically, even when the marked assembly loads after the first use of the service.</pitch>
/// <pledge>
/// Applying the attribute to a class with a parameterless constructor registers that class as the default implementation of each interface it directly implements (or only the interfaces listed in the attribute, when given), in every assembly that references AmbientServices.  First registration per interface wins; later-discovered candidates for an already-claimed interface are ignored.  Discovery is passive — registration does not construct the class; construction happens lazily on first request and the same instance is shared thereafter.
/// A default is only a fallback: an explicit assignment to <see cref="AmbientService{T}.Global"/> always takes precedence, and suppression hides the default entirely.
/// </pledge>
/// When applied to a class with a public empty constructor in any assembly, causes each interface implemented by that class to be registered as the default ambient service implementation, unless one already exists.
/// If another implementation has already been registered, the new one will be ignored.
/// The class instance implementing the service implementation will be constructed the first time it is requested.
/// In some rare situations where multiple threads attempt the initialization simultaneously, the constructor may be called more than once.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DefaultAmbientServiceAttribute : Attribute
{

    /// <summary>
    /// Constructs a DefaultAmbientServiceAttribute.
    /// </summary>
    public DefaultAmbientServiceAttribute()
    {
    }
    /// <summary>
    /// Constructs a DefaultAmbientServiceAttribute that is limited to the specified interface, even if other interfaces are directly implemented.
    /// </summary>
    /// <param name="registrationInterface">A single registration interface (for CLS compliance).</param>
#pragma warning disable CA1019  // this constructor is only for CLS compliance--this attribute is accessible through the RegistrationInterfaces property
    public DefaultAmbientServiceAttribute(Type registrationInterface)
#pragma warning restore CA1019
    {
        RegistrationInterfaces = ImmutableArray<Type>.Empty.Add(registrationInterface);
    }
    /// <summary>
    /// Constructs a DefaultAmbientServiceAttribute that is limited to the listed interfaces, even if other interfaces are directly implemented.
    /// </summary>
    /// <param name="registrationInterfaces">A params array of interface types to use for the registration instead of all the interfaces implemented by the class.</param>
    public DefaultAmbientServiceAttribute(params Type[] registrationInterfaces)
    {
        RegistrationInterfaces = ImmutableArray<Type>.Empty.AddRange(registrationInterfaces);
    }
    /// <summary>
    /// Gets the interface types indicating which services are implemented by the class the attribute is applied to.  
    /// If null, all interfaces that are directly implemented by the class should be used.
    /// </summary>
    public IReadOnlyList<Type>? RegistrationInterfaces { get; }
}

/// <summary>
/// An internal static class that collects default ambient service implementations in every currently and subsequently loaded assembly.
/// </summary>
/// <remarks>
/// <pitch>The discovery registry behind <see cref="DefaultAmbientServiceAttribute"/>: the one place that answers "which type is the default implementation of this interface?"</pitch>
/// <pledge><see cref="TryFind"/> maps an interface type to its default implementation type or null, is thread-safe, and reflects assemblies loaded at any time before the call; it throws if handed a non-interface type.  Registration is first-wins per interface and never replaces an existing entry.</pledge>
/// <plan>A static <see cref="ConcurrentDictionary{TKey, TValue}"/> from interface type to implementation type, seeded by scanning every already-loaded assembly and kept current by an <see cref="AppDomain.AssemblyLoad"/> hook installed exactly once by the static constructor.  Scanning is pruned to assemblies that reference this assembly (others cannot carry the attribute) and tolerates partially loadable assemblies via <c>GetLoadableTypes</c>, so a single bad type cannot poison discovery.  Entries are added with <c>TryAdd</c>, making the first-wins rule atomic under concurrent loads.</plan>
/// </remarks>
internal static class DefaultAmbientServices
{
    private static readonly Assembly _ThisAssembly;
    private static readonly ConcurrentDictionary<Type, Type> _DefaultImplementations;

    static DefaultAmbientServices()
    {
        _ThisAssembly = Assembly.GetExecutingAssembly();
        _DefaultImplementations = InitializeAlreadyLoadedDefaultAmbientServices();
        // start hooking into assembly loading now, but only do this ONCE
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
    }
    private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        Assembly assembly = args.LoadedAssembly;
        OnAssemblyLoad(assembly);
    }

    private static ConcurrentDictionary<Type, Type> InitializeAlreadyLoadedDefaultAmbientServices()
    {
        ConcurrentDictionary<Type, Type> dictionary = new();
        foreach (Type type in AllLoadedReferringTypes())
        {
            AddDefaultImplementation(dictionary, type);
        }
        return dictionary;
    }
    /// <summary>
    /// Enumerates all the types in all currently loaded assemblies that refer to this assembly (they can't possibly have the appropriate attribute without referencing this assembly).
    /// </summary>
    /// <returns>An enumeration of <see cref="Type"/>s.</returns>
    private static IEnumerable<Type> AllLoadedReferringTypes()
    {
        List<Assembly> checkedAssemblies = new();
        // loop through all the assemblies loaded in our AppDomain
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // is this assembly us or does it reference us?
            if (assembly == _ThisAssembly || assembly.DoesAssemblyReferToAssembly(_ThisAssembly))
            {
                checkedAssemblies.Add(assembly);
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    yield return type;
                }
            }
        }
        System.Diagnostics.Trace.WriteLine($"Loading Ambient Types From {string.Join(",", checkedAssemblies.Select(a => a.FullName))}");
    }
    /// <summary>
    /// Adds the default implementation for the specified interface type.
    /// </summary>
    /// <param name="dictionary">The dictionary to add to (usually <see cref="_DefaultImplementations"/>).</param>
    /// <param name="type">The interface type whose default implementation type is to be added.</param>
    private static void AddDefaultImplementation(ConcurrentDictionary<Type, Type> dictionary, Type type)
    {
        DefaultAmbientServiceAttribute? attribute = type.GetCustomAttribute<DefaultAmbientServiceAttribute>();
        if (attribute != null)
        {
            IReadOnlyList<Type>? registrationInterfaces = attribute.RegistrationInterfaces;
            if ((registrationInterfaces?.Count ?? 0) == 0)
            {
                registrationInterfaces = type.GetInterfaces();   // this could be null if the specified type doesn't support *any* interfaces
            }
            if (registrationInterfaces != null)
            {
                foreach (Type iface in registrationInterfaces)
                {
                    dictionary.TryAdd(iface, type);
                }
            }
        }
    }
    /// <summary>
    /// Loads the default implementations in the specified assembly and notifies subscribers that the assembly has been loaded.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> whose default implementations are to be found and registered.</param>
    internal static void OnAssemblyLoad(Assembly assembly)
    {
        // does the being-loaded assembly reference THIS assembly?
        if (assembly.DoesAssemblyReferToAssembly(_ThisAssembly))
        {
            System.Diagnostics.Trace.WriteLine($"Late Loading Ambient Types From {assembly.FullName}");
            // check every type in the being-loaded assembly to see if the type indicates a default service implementation
            foreach (Type type in assembly.GetLoadableTypes())
            {
                AddDefaultImplementation(_DefaultImplementations, type);
            }
        }
    }

    /// <summary>
    /// Tries to find the default implementation of the specified interface, if one exists.
    /// Thread-safe.
    /// </summary>
    /// <param name="iface">The <see cref="Type"/> of interface whose implementation is wanted.</param>
    /// <returns>The <see cref="Type"/> that implements that interface, or null if no implementation could be found.</returns>
    public static Type? TryFind(
#if NETCOREAPP3_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        Type iface)
    {
        if (!iface.IsInterface) throw new ArgumentException("The specified type is not an interface type!", nameof(iface));
        Type? impType;
        if (_DefaultImplementations.TryGetValue(iface, out impType))
        {
            Debug.Assert(iface.IsAssignableFrom(impType));
            return impType;
        }
        return null;
    }
}
/// <summary>
/// An empty interface that needs to be in this assembly in order to get tested properly because the interface will be registered before the assembly that implements it is loaded.
/// </summary>
internal interface ILateAssignmentTest
{ }
