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
    /// A static class that provides access to <see cref="AmbientService{T}"/>s.
    /// </summary>
    public static class Ambient
    {
        /// <summary>
        /// Gets the <see cref="AmbientService{T}"/> for the indicated type.
        /// </summary>
        /// <typeparam name="T">The type of service that is needed.</typeparam>
        /// <returns>The <see cref="AmbientService{T}"/> instance.  This should never be null.</returns>
        public static AmbientService<T> GetService<T>() where T : class
        {
            return AmbientService<T>.Instance;
        }
        /// <summary>
        /// Gets the <see cref="AmbientService{T}"/> for the indicated type.
        /// </summary>
        /// <typeparam name="T">The type of service that is needed.</typeparam>
        /// <param name="service">[OUT] Receives the ambient service.</param>
        /// <returns>The <see cref="AmbientService{T}"/> instance.  This should never be null.</returns>
        public static AmbientService<T> GetService<T>(out AmbientService<T> service) where T : class
        {
            service = AmbientService<T>.Instance;
            return service;
        }
    }
    /// <summary>
    /// A generic class that provides access to an ambient service implementation.
    /// Must be accessed through <see cref="Ambient.GetService{T}()"/> or <see cref="Ambient.GetService{T}(out AmbientService{T})"/>.
    /// </summary>
    /// <remarks>
    /// Note that accessing an ambient service usually requires two dereferences, one to check for a local override, and one to get the global service.
    /// Attempting to optimize this to only do one access by caching the appropriate service in an AsyncLocal as it changes doesn't appear to be possible
    /// because this would still require checking the AsyncLocal to see if it is initialized, so conditional and dereferencing costs would be the same,
    /// but such a system would require allocating an AsyncLocal object for every call context, which greatly degrades AsyncLocal performance and has higher memory requirements.
    /// In addition, such a system would require either allocating another AsyncLocal refernce-type object for any local overrides in subcontexts, or 
    /// requires keeping track of the old local value to restore at the end of the subcontext override.  
    /// Using an implementation where a single AsyncLocal for the local override is checked on every access makes subcontext overrides naturally rollback as the stack is unwound.
    /// </remarks>
    /// <typeparam name="T">The interface for the service.</typeparam>
    public class AmbientService<T> where T : class
    {
        private static readonly AmbientService<T> _Instance = new();
        /// <summary>
        /// Gets the <see cref="AmbientService{T}"/> for the service indicated by the type.
        /// </summary>
        internal static AmbientService<T> Instance => _Instance;
        /// <summary>
        /// The singleton call-context-local service reference (non-singleton reference can be used for unit testing).
        /// </summary>
        private readonly AsyncLocal<LocalServiceReference<T>?> _localReference = new();
        /// <summary>
        /// The global service reference.
        /// </summary>
        private readonly GlobalServiceReference<T> _globalReference = new();


        // this is only internal instead of private so that we can diagnose issues in test cases
        internal GlobalServiceReference<T> GlobalReference => _globalReference;

        /// <summary>
        /// Gets the <see cref="LocalServiceReference{T}"/> for the service indicated by the type.
        /// This property must be used internally in order to ensure local initialization of the <see cref="AsyncLocal{T}"/>.
        /// </summary>
        /// <returns>The <see cref="LocalServiceReference{T}"/> instance.</returns>
        internal LocalServiceReference<T> LocalReference
        {
            get
            {
                LocalServiceReference<T>? ret = _localReference.Value;
                if (ret == null) ret = _localReference.Value = new LocalServiceReference<T>();    // initialize if needed
                return ret;
            }
        }

        /// <summary>
        /// Overrides the service implementation locally and temporarily.
        /// </summary>
        /// <param name="newLocalServiceImplementation">The new local service implementation to use until the returned object is disposed.</param>
        /// <returns>An <see cref="IDisposable"/> instance that, when disposed, will return the local service implementation to what it was before this call.</returns>
        public IDisposable ScopedLocalOverride(T newLocalServiceImplementation)
        {
            return new ScopedLocalServiceOverride<T>(newLocalServiceImplementation);
        }

        internal AmbientService()
        {
        }
        /// <summary>
        /// Gets or sets the global service implementation, or null if there is no implementation or it has been suppressed.
        /// If set to null, suppresses the global service.
        /// When setting the service, overwrites any previous implementation and raises the <see cref="GlobalChanged"/> event either on this thread or another thread asynchronously.
        /// Thread-safe.
        /// </summary>
        public T? Global
        {
            get
            {
                return _globalReference.Service;
            }
            set
            {
                _globalReference.Service = value;
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local override implementation for the service, or null if there is no override implementation.
        /// If set to null, reverts to the global service implementation and begins watching changes to that.
        /// Otherwise sets to the specified implementation.
        /// Thread-safe.
        /// </summary>
        public T? Override
        {
            get
            {
                return LocalReference.RawOverride as T;
            }
            set
            {
                LocalReference.RawOverride = value;
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local service impelementation.
        /// If set to null, suppresses any local or global service (and begins ignoring changes to the global service).
        /// Otherwise sets the local override to the specified implementation.
        /// Thread-safe.
        /// </summary>
        public T? Local
        {
            get
            {
                return (LocalReference.RawOverride ?? _globalReference.Service) as T;
            }
            set
            {
                LocalReference.Service = value;
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a global service implementation is changed.  
        /// The notification may happen on any arbitrary thread.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because this instance lives forever.
        /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), and
        /// the fact that each notification may proceed at a different pace, notifications may appear to come in a different order than the changes actually occurred.
        /// As a result, subscribers should query the latest value if needed when they receive the event notification.
        /// This way if multiple changes happen, they will always end up with the latest value.
        /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs> GlobalChanged
        {
            add
            {
                _globalReference.ServiceChanged += value;
            }
            remove
            {
                _globalReference.ServiceChanged -= value;
            }
        }
    }
    /// <summary>
    /// A scoping class that overrides the global service implementation with a specified local one during its scope.
    /// Note that call context variables can sometimes survive returning from a function and calling into another function, 
    /// so it is important to reset a local override before returning from the function where the override is used.
    /// As a result, depending on how contexts are reused, restoring the original may be needed.
    /// For example, in unit tests, the same call context is used for multiple unit tests, so any overrides need
    /// to be undone when the test is complete just in case another test subsequently runs using the same call context.
    /// </summary>
    /// <typeparam name="T">The service interface type.</typeparam>
    public sealed class ScopedLocalServiceOverride<T> : IDisposable where T : class
    {
        private static readonly AmbientService<T> _Reference = Ambient.GetService<T>();

        private readonly T? _oldGlobalService;
        private readonly T? _oldLocalServiceOverride;

        /// <summary>
        /// Constructs a scoped override that changes the service implementation for this call context until this instance is disposed.
        /// </summary>
        /// <param name="temporaryLocalService">The service to temporarily use in this call context.</param>
        public ScopedLocalServiceOverride(T? temporaryLocalService)
        {
            _oldGlobalService = _Reference.Global;
            _oldLocalServiceOverride = _Reference.Override;
            _Reference.Local = temporaryLocalService;
        }
        /// <summary>
        /// Gets the old local override in case it is needed by the overriding implementation.
        /// </summary>
        public T? OldOverride => _oldLocalServiceOverride;
        /// <summary>
        /// Gets the old global implementation in case it is needed by the overriding implementation.
        /// </summary>
        public T? OldGlobal => _oldGlobalService;

        #region IDisposable Support
        private bool _disposed; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _Reference.Override = _oldLocalServiceOverride;
                }
                _disposed = true;
            }
        }
        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }

    /// <summary>
    /// A generic class used to ensure that only one instance of the default service implementation gets created.
    /// </summary>
    /// <typeparam name="T">The concrete type of the service.</typeparam>
    internal class DefaultServiceImplementation<T> where T : class
    {
        private static readonly T _ImplementationSingleton = CreateInstance();
        private static T CreateInstance()
        {
            ConstructorInfo? ci = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ci == null) throw new InvalidOperationException("The type must have a default constructor!");
            return (T)ci.Invoke(Array.Empty<object>());
        }
        public static T GetImplementation() { return _ImplementationSingleton; }
    }
    /// <summary>
    /// A class that manages a global service reference.
    /// </summary>
    /// <typeparam name="T">The interface type for the service being managed.</typeparam>
    internal class GlobalServiceReference<T> where T : class
    {
        /// <summary>
        /// A generic object whose instance is used to indicate that the default service implementation has been suppressed.
        /// </summary>
        private static readonly object SuppressedService = new();

        /// <summary>
        /// A reference to the current service implementation.  Null if not yet initialized.  <see cref="SuppressedService"/> if the service has been explicitly suppressed.
        /// </summary>
        private object? _service;

        internal GlobalServiceReference()
        {
            _service = DefaultImplementation();
        }
        private static T? DefaultImplementation()
        {
            Type? impType = DefaultAmbientServices.TryFind(typeof(T));
            if (impType == null) return null;       // there is no default implementation (yet)
            Type type = typeof(DefaultServiceImplementation<>).MakeGenericType(impType);
            MethodInfo mi = type.GetMethod(nameof(DefaultServiceImplementation<T>.GetImplementation))!; // DefaultServiceImplementation<T> has a public GetImplementation method, so this should always succeed
            T implementation = (T)mi.Invoke(null, Array.Empty<object>())!;  // DefaultServiceImplementation<T> returns a non-null T
            return implementation;
        }
        private T? LateAssignedDefaultServiceImplementation()
        {
            T? newDefaultImplementation = DefaultImplementation();
            // still no default implementation registered?  try again later
            if (newDefaultImplementation == null) return null;
            // we should almost always get a null back here below, but it's theoretically possible if two attempts to retrieve the implementation happen at the same time, but even in this case, the only way we would get back instances of different types would be if the default ambient service changed, which shouldn't be possible given the current implementation
            // as a result, the non-null case below is unlikely to get covered by tests
            return (Interlocked.CompareExchange(ref _service, newDefaultImplementation, null) is not T oldDefaultImplementation)
                ? newDefaultImplementation
                : oldDefaultImplementation;
        }

        /// <summary>
        /// Gets or sets the service.
        /// If set to null, suppresses the default service (so that the getter returns null).
        /// When setting the service, overwrites any previous service and raises the <see cref="ServiceChanged"/> event.
        /// Thread-safe.
        /// </summary>
        public T? Service
        {
            get
            {
                return (_service ?? LateAssignedDefaultServiceImplementation()) as T;
            }
            set
            {
                T? oldImplementation = Interlocked.Exchange(ref _service, value ?? SuppressedService) as T;
                ServiceChanged?.Invoke(typeof(LocalServiceReference<T>), EventArgs.Empty);
            }
        }
        /// <summary>
        /// An event that will notify subscribers when the global service implementation is changed.  
        /// The notification will happen on the thread and within the call context from which the change is initiated.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service reference lives forever.
        /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), 
        /// and each notification may proceed at a different pace, notifications may appear to come in a different order than the changes actually occurred.
        /// Subscribers should query the latest value if needed when they receive the event notification.
        /// This way if multiple changes happen, they will always end up with the latest value.
        /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs>? ServiceChanged;
    }
    /// <summary>
    /// A class that manages a call-context-local service reference.
    /// </summary>
    /// <typeparam name="T">The interface type for the service being managed.</typeparam>
    internal class LocalServiceReference<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default implementation has been suppressed.
        /// </summary>
        internal static readonly object SuppressedImplementation = new();

        /// <summary>
        /// The call-context-local override.
        /// </summary>
        private object? _localOverride;

        internal LocalServiceReference()
        {
            _localOverride = null;
        }
        internal object? RawOverride
        {
            get
            {
                return _localOverride;
            }
            set
            {
                _localOverride = value;
            }
        }
        /// <summary>
        /// Sets the implementation.
        /// If set to null, suppresses the default implementation (so that the getter returns null).
        /// Thread-safe.
        /// </summary>
        public T? Service
        {
            // this isn't currently needed, but if it is, just uncomment this code
            //get
            //{
            //    return _localOverride as T;
            //}
            set
            {
                _localOverride = value ?? SuppressedImplementation;
            }
        }
    }
}
