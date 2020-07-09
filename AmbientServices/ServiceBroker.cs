using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// A generic class used to serialize the instantiation of the default implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class DefaultServiceImplementation<T> where T : class
    {
        private static T _Implementation = Activator.CreateInstance<T>();
        public static T GetImplementation() { return _Implementation; }
    }
    /// <summary>
    /// Provides a global service broker that callers can use to retrieve (or register) the system-wide implementation of an ambient service interface (indicated using the generic type parameter).
    /// </summary>
    /// <typeparam name="T">The ambient service interface.</typeparam>
    public static class ServiceBroker<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default implementation has been suppressed.
        /// </summary>
        private static object SuppressedImplementation = new object();
        /// <summary>
        /// The current implementation.  Null if not yet initialized.  <see cref="SuppressedImplementation"/> if the implementation has been explicitly suppressed.
        /// </summary>
        private static object _implementation = DefaultImplementation();
        /// <summary>
        /// The call-context-local override (if any).
        /// </summary>
        private static AsyncLocal<object> _callContextLocalImplementation = new AsyncLocal<object>();

        private static T DefaultImplementation()
        {
            Type impType = DefaultAmbientServices.TryFind(typeof(T));
            if (impType == null) return null;       // there is no default implementation (yet)
            Type type = typeof(DefaultServiceImplementation<>).MakeGenericType(impType);
            MethodInfo mi = type.GetMethod("GetImplementation");
            T implementation = (T)mi.Invoke(null, new object[0]);
            return implementation;
        }
        private static T LateAssignedDefaultImplementation()
        {
            T newDefaultImplementation = DefaultImplementation();
            // still no default implementation registered?  try again later
            if (newDefaultImplementation == null) return null;
            T oldDefaultImplementation = System.Threading.Interlocked.CompareExchange(ref _implementation, newDefaultImplementation, null) as T;
            return (oldDefaultImplementation == null)
                ? newDefaultImplementation
                : oldDefaultImplementation;
        }
        /// <summary>
        /// Gets or sets the global implementation.
        /// If set to null, suppresses the default implementation (so that the getter returns null).
        /// When setting the implementation, overwrites any previous implementation and triggers the <see cref="GlobalImplementationChanged"/> event either on this thread or another thread asynchronously.
        /// Thread-safe.
        /// </summary>
        public static T GlobalImplementation
        {
            get
            {
                return (_implementation ?? LateAssignedDefaultImplementation()) as T;
            }
            set
            {
                T oldImplementation = System.Threading.Interlocked.Exchange(ref _implementation, value ?? SuppressedImplementation) as T;
                GlobalImplementationChanged?.Invoke(typeof(ServiceBroker<T>), new ImplementationChangedEventArgs<T> { OldImplementation = oldImplementation, NewImplementation = value });
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local implementation.
        /// When setting the implementation, if set to <see cref="GlobalImplementation"/>, reverts to the global implementation (even if it changes later).  
        /// If set to null, suppresses the implementation in the local call context (so that the getter returns null).
        /// Otherwise sets to the value specified.
        /// Thread-safe.
        /// </summary>
        public static T LocalImplementation
        {
            get
            {
                return (_callContextLocalImplementation.Value ?? _implementation ?? LateAssignedDefaultImplementation()) as T;
            }
            set
            {
                _callContextLocalImplementation.Value = (value == GlobalImplementation) ? null : value ?? SuppressedImplementation;
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a new implementation is registered.  
        /// The notification may happen on any arbitrary thread.
        /// Note that in order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service broker lives forever.
        /// Thread-safe.
        /// </summary>
        public static event EventHandler<ImplementationChangedEventArgs<T>> GlobalImplementationChanged;

        /// <summary>
        /// An event args class that is sent when a setting value is changed.
        /// </summary>
        /// <typeparam name="T">The type for the setting.</typeparam>
        public class ImplementationChangedEventArgs<ET>
        {
            /// <summary>
            /// The old service implementation.
            /// </summary>
            public ET OldImplementation { get; set; }
            /// <summary>
            /// The new service implementation.
            /// </summary>
            public ET NewImplementation { get; set; }
        }
    }
    /// <summary>
    /// An attribute to identify classes implementing ambient service default implementations.
    /// </summary>
    /// <remarks>
    /// When applied to a class with a public empty constructor in any assembly, causes each interface implemented by that class to be registered with the ambient service broker as the default implementation, unless one already exists.
    /// The service will be constructed the first time it is requested.  In some rare situations, the constructor may be called more than once.
    /// </remarks>
    public class DefaultAmbientServiceAttribute : Attribute
    {
    }
    /// <summary>
    /// An internal static class that discovers local default implementations and registers them.
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
            if (type.GetCustomAttribute<DefaultAmbientServiceAttribute>() != null && type.GetConstructor(Type.EmptyTypes) != null)
            {
                foreach (Type iface in type.GetInterfaces())
                {
                    _DefaultImplementations.TryAdd(iface, type);
                }
            }
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            Assembly assembly = args.LoadedAssembly;
            // does this assembly reference THIS assembly?
            if (DoesAssemblyReferToAssembly(assembly, _ThisAssembly))
            {
                // add any default implementations in the assembly
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
        /// <returns>The <see cref="Type"/> that implements that interface.</returns>
        public static Type TryFind(Type iface)
        {
            if (!iface.IsInterface) throw new ArgumentException("The specified type is not an interface type!", "iface");
            Type impType;
            if (_DefaultImplementations.TryGetValue(iface, out impType))
            {
                System.Diagnostics.Debug.Assert(iface.IsAssignableFrom(impType));
                return impType;
            }
            return null;
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
        /// <summary>
        /// Enumerates loadable types from the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to attempt to enumerate types from.</param>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
//        [DebuggerNonUserCode]
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
        /// Enuemrates all the types in this assembly and all loaded assemblies that refer to it.
        /// </summary>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
        private static IEnumerable<Type> AllLoadedReferringTypes()
        {
            // loop through all the assemblies loaded in our AppDomain
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // is this assembly us or does it reference us?
                if (assembly == _ThisAssembly || DoesAssemblyReferToAssembly(assembly, _ThisAssembly))
                {
                    foreach (Type type in assembly.GetLoadableTypes())
                    {
                        yield return type;
                    }
                }
            }
        }
    }
    internal interface ILateAssignmentTest
    { }
}
