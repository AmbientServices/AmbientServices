using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An attribute to identify classes implementing an ambient service default implementation.
    /// </summary>
    /// <remarks>
    /// When applied to a class with a public empty constructor in any assembly, causes each interface implemented by that class to be registered as the default ambient service implementation, unless one already exists.
    /// If another implementation has already been registered, the new one will be ignored.
    /// The class instance implementing the service implementation will be constructed the first time it is requested.  
    /// In some rare situations where multiple threads attempt the initialization simultaneously, the constructor may be called more than once.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultAmbientServiceAttribute : Attribute
    {
        private IEnumerable<Type> _registrationInterfaces;

        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute.
        /// </summary>
        public DefaultAmbientServiceAttribute()
        {
        }
        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute that is limited to one specific interface, even if multiple interfaces are directly implemented.
        /// </summary>
        /// <param name="registrationInterface">An interface type to use for the registration instead of all the interfaces implemented by the class.</param>
        public DefaultAmbientServiceAttribute(Type registrationInterface)
        {
            _registrationInterfaces = new Type[] { registrationInterface };
        }
        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute that is limited to the listed interfaces, even if other interfaces are directly implemented.
        /// </summary>
        /// <param name="registrationInterfaces">A params array of interface types to use for the registration instead of all the interfaces implemented by the class.</param>
        public DefaultAmbientServiceAttribute(params Type[] registrationInterfaces)
        {
            _registrationInterfaces = registrationInterfaces;
        }
        /// <summary>
        /// Gets the interface types indicating which services are implemented by the class the attribute is applied to.  
        /// If null, all interfaces that are directly implemented by the class should be used.
        /// </summary>
        public IEnumerable<Type> RegistrationTypes { get { return _registrationInterfaces; } }
    }
    /// <summary>
    /// An internal static class that collects default ambient service implementations in every currently and subsequently loaded assembly.
    /// </summary>
    static class DefaultAmbientServices
    {
        private static readonly ConcurrentDictionary<Type, Type> _DefaultImplementations = new ConcurrentDictionary<Type, Type>();
        private static Assembly _ThisAssembly = Assembly.GetExecutingAssembly();

        static DefaultAmbientServices()
        {
            foreach (Type type in AllLoadedReferringTypes())
            {
                AddDefaultImplementation(type);
            }
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        }

        private static void AddDefaultImplementation(Type type)
        {
            DefaultAmbientServiceAttribute attribute = type.GetCustomAttribute<DefaultAmbientServiceAttribute>();
            if (attribute != null && type.GetConstructor(Type.EmptyTypes) != null)
            {
                IEnumerable<Type> registrationInterfaces = attribute.RegistrationTypes ?? type.GetInterfaces();
                foreach (Type iface in registrationInterfaces)
                {
                    _DefaultImplementations.TryAdd(iface, type);
                }
            }
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            Assembly assembly = args.LoadedAssembly;
            AssemblyLoader.OnLoad(assembly);
            // does this assembly reference THIS assembly?
            if (assembly.DoesAssemblyReferToAssembly(_ThisAssembly))
            {
                // check every type in this assembly to see if the type indicates a default service implementation
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    AddDefaultImplementation(type);
                }
            }
        }

        /// <summary>
        /// Tries to find the default implementation of the specified interface, if one exists.
        /// Thread-safe.
        /// </summary>
        /// <param name="iface">The <see cref="Type"/> of interface whose implementation is wanted.</param>
        /// <returns>The <see cref="Type"/> that implements that interface, or null if no implementation could be found.</returns>
        public static Type TryFind(Type iface)
        {
            if (!iface.IsInterface) throw new ArgumentException("The specified type is not an interface type!", nameof(iface));
            Type impType;
            if (_DefaultImplementations.TryGetValue(iface, out impType))
            {
                System.Diagnostics.Debug.Assert(iface.IsAssignableFrom(impType));
                return impType;
            }
            return null;
        }
        /// <summary>
        /// Enuemrates all the types in all currently loaded assemblies that refer to this assembly (they can't possibly have the appropriate attribute without referencing this assembly).
        /// </summary>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
        private static IEnumerable<Type> AllLoadedReferringTypes()
        {
            // loop through all the assemblies loaded in our AppDomain
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // is this assembly us or does it reference us?
                if (assembly == _ThisAssembly || assembly.DoesAssemblyReferToAssembly(_ThisAssembly))
                {
                    foreach (Type type in assembly.GetLoadableTypes())
                    {
                        yield return type;
                    }
                }
            }
        }
    }
    /// <summary>
    /// A class that loads assemblies and logs information about the loading.
    /// </summary>
    class AssemblyLoader
    {
        private static readonly AmbientLogger<AssemblyLoader> Logger = new AmbientLogger<AssemblyLoader>();

        internal static void OnLoad(Assembly assembly)
        {
            Logger.Log(assembly.GetName().Name, "AssemblyLoad", AmbientLogLevel.Error);
        }
    }
    /// <summary>
    /// A static class with extension functions for <see cref="System.Reflection.Assembly"/>.
    /// </summary>
    static class AssemblyExtensions
    {
        /// <summary>
        /// Enumerates loadable types from the assembly, even if referencing one or more of the types throws a <see cref="ReflectionTypeLoadException"/> exception.
        /// </summary>
        /// <param name="assembly">The assembly to attempt to enumerate types from.</param>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
        [DebuggerNonUserCode]
        internal static IEnumerable<Type> GetLoadableTypes(this System.Reflection.Assembly assembly)
        {
            try
            {
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
            return ex.Types.Where(t => t != null);
        }
        /// <summary>
        /// Checks to see if this assembly refers to the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <param name="referredToAssembly">The referred to assembly to look for.</param>
        /// <returns><b>true</b> if <paramref name="assembly"/> refers to <paramref name="referredToAssembly"/>.</returns>
        internal static bool DoesAssemblyReferToAssembly(this System.Reflection.Assembly assembly, Assembly referredToAssembly)
        {
            return (assembly == referredToAssembly || assembly.GetReferencedAssemblies().FirstOrDefault(a => a.FullName == referredToAssembly.FullName) != null);
        }
    }
    /// <summary>
    /// An empty interface that needs to be in this assembly in order to get tested properly because the interface will be registered before the assembly that implements it is loaded.
    /// </summary>
    internal interface ILateAssignmentTest
    { }
}
