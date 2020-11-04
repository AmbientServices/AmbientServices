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
    /// An attribute to identify classes implementing ambient service default provider.
    /// </summary>
    /// <remarks>
    /// When applied to a class with a public empty constructor in any assembly, causes each interface implemented by that class to be registered with the ambient service broker as the default implementation, unless one already exists.
    /// The service will be constructed the first time it is requested.  In some rare situations, the constructor may be called more than once.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultAmbientServiceProviderAttribute : Attribute
    {
        private IEnumerable<Type> _registrationInterfaces;

        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute.
        /// </summary>
        public DefaultAmbientServiceProviderAttribute()
        {
        }
        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute.
        /// </summary>
        /// <param name="registrationInterface">An interface type to use for the registration instead of all the interfaces implemented by the class.</param>
        public DefaultAmbientServiceProviderAttribute(Type registrationInterface)
        {
            _registrationInterfaces = new Type[] { registrationInterface };
        }
        /// <summary>
        /// Constructs a DefaultAmbientServiceAttribute.
        /// </summary>
        /// <param name="registrationInterfaces">A params array of interface types to use for the registration instead of all the interfaces implemented by the class.</param>
        public DefaultAmbientServiceProviderAttribute(params Type[] registrationInterfaces)
        {
            _registrationInterfaces = registrationInterfaces;
        }
        /// <summary>
        /// Gets the registration types (if any).
        /// </summary>
        public IEnumerable<Type> RegistrationTypes { get { return _registrationInterfaces; } }
    }
    /// <summary>
    /// An internal static class that discovers local default implementations and registers them.
    /// </summary>
    class DefaultAmbientServices
    {
        private static readonly ConcurrentDictionary<Type, Type> _DefaultProviders = new ConcurrentDictionary<Type, Type>();
        private static Assembly _ThisAssembly = Assembly.GetExecutingAssembly();

        static DefaultAmbientServices()
        {
            foreach (Type type in AllLoadedReferringTypes())
            {
                AddDefaultProvider(type);
            }
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        }

        private static void AddDefaultProvider(Type type)
        {
            DefaultAmbientServiceProviderAttribute attribute = type.GetCustomAttribute<DefaultAmbientServiceProviderAttribute>();
            if (attribute != null && type.GetConstructor(Type.EmptyTypes) != null)
            {
                IEnumerable<Type> registrationInterfaces = attribute.RegistrationTypes ?? type.GetInterfaces();
                foreach (Type iface in registrationInterfaces)
                {
                    _DefaultProviders.TryAdd(iface, type);
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
                // add any default implementations in the assembly
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    AddDefaultProvider(type);
                }
            }
        }

        /// <summary>
        /// Tries to find the default implementation of the specified interface, if one exists.
        /// Thread-safe.
        /// </summary>
        /// <param name="iface">The <see cref="Type"/> of interface whose implementation is wanted.</param>
        /// <returns>The <see cref="Type"/> that implements that interface.</returns>
        public static Type TryFind(Type iface)
        {
            if (!iface.IsInterface) throw new ArgumentException("The specified type is not an interface type!", nameof(iface));
            Type impType;
            if (_DefaultProviders.TryGetValue(iface, out impType))
            {
                System.Diagnostics.Debug.Assert(iface.IsAssignableFrom(impType));
                return impType;
            }
            return null;
        }
        /// <summary>
        /// Enuemrates all the types in this assembly and all loaded assemblies that refer to it.
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
    class AssemblyLoader
    {
        private static readonly AmbientLogger<AssemblyLoader> Logger = new AmbientLogger<AssemblyLoader>();

        internal static void OnLoad(Assembly assembly)
        {
            Logger.Log(assembly.GetName().Name, "AssemblyLoad", AmbientLogLevel.Error);
            Service.GetAccessor<IAmbientLoggerProvider>().Provider?.Flush();
        }
    }
    static class AssemblyExtensions
    {
        /// <summary>
        /// Enumerates loadable types from the assembly.
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
    internal interface ILateAssignmentTest
    { }
}
