using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace AmbientServices
{
    /// <summary>
    /// An attribute to identify the default implementation types.
    /// </summary>
    class DefaultImplementationAttribute : Attribute
    {
    }
    /// <summary>
    /// A static class that discovers local default implementations and registers them.
    /// </summary>
    static class DefaultImplementations
    {
        private static readonly Dictionary<Type, Type> _DefaultImplementations = new Dictionary<Type, Type>();

        static DefaultImplementations()
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetLoadableTypes())
            {
                if (type.GetCustomAttribute<DefaultImplementationAttribute>() != null)
                {
                    foreach (Type iface in type.GetInterfaces())
                    {
                        _DefaultImplementations.Add(iface, type);
                    }
                }
            }
        }
        /// <summary>
        /// Tries to find the default implementation of the specified interface, if one exists.
        /// </summary>
        /// <param name="iface">The <see cref="Type"/> of interface whose implementation is wanted.</param>
        /// <returns>The <see cref="Type"/> that implements that interface.</returns>
        public static Type TryFind(Type iface)
        {
            if (!iface.IsInterface) throw new ArgumentException("The specified type is not an interface type!", "iface");
            Type impType;
            if (_DefaultImplementations.TryGetValue(iface, out impType))
            {
                System.Diagnostics.Debug.Assert(impType.ImplementsInterface(iface));
                return impType;
            }
            return null;
        }
        /// <summary>
        /// Returns whether or not the specified class or interface type implements the specified interface.
        /// </summary>
        /// <param name="implementor">The class or interface that might implement the interface.</param>
        /// <param name="interfaceType">The interface to look for.</param>
        /// <returns><b>true</b> if the interface is supported, <b>false</b> if it is not.</returns>
        public static bool ImplementsInterface(this Type implementor, Type interfaceType)
        {
            if (interfaceType.IsGenericTypeDefinition)
            {
                return (implementor.IsGenericType && implementor.GetGenericTypeDefinition() == interfaceType) ||
                    (implementor.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType));
            }
            else return interfaceType.IsAssignableFrom(implementor);
        }
        /// <summary>
        /// Enumerates loadable types from the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to attempt to enumerate types from.</param>
        /// <returns>An enumeration of <see cref="Type"/>s.</returns>
        [DebuggerNonUserCode]
        public static IEnumerable<Type> GetLoadableTypes(this System.Reflection.Assembly assembly)
        {
            Type[] types = new Type[0];
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                yield return type;
            }
        }
    }
    /// <summary>
    /// Provides a global registry for the implementation of one specific ambient service implementation.
    /// </summary>
    /// <typeparam name="T">The ambient service interface.</typeparam>
    public static class Registry<T> where T : class
    {
        private static T _implementation;
        /// <summary>
        /// Gets or sets the implementation.  When setting the implementation, overwrites any previous implementation.
        /// </summary>
        public static T Implementation
        {
            get
            {
                T implementation = _implementation;
                if (implementation == null)
                {
                    Type impType = DefaultImplementations.TryFind(typeof(T));
                    implementation = Activator.CreateInstance(impType) as T;
                }
                return implementation;
            }
            set
            {
                System.Threading.Interlocked.Exchange(ref _implementation, value);
            }
        }
    }
}
