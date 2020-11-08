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
    /// A static class that provides accessors for ambient services.
    /// </summary>
    public static class Service
    {
        /// <summary>
        /// Gets the <see cref="ServiceReference{T}"/> for the indicated type.
        /// </summary>
        /// <typeparam name="T">The type whose service accessor is needed.</typeparam>
        /// <returns>The <see cref="ServiceReference{T}"/> instance.  This should never be null.</returns>
        public static ServiceReference<T> GetReference<T>() where T : class
        {
            return ServiceReference<T>.Instance;
        }
        /// <summary>
        /// Gets the <see cref="ServiceReference{T}"/> for the indicated type.
        /// </summary>
        /// <typeparam name="T">The type whose service accessor is needed.</typeparam>
        /// <param name="accessor">[OUT] Receives the service accessor.</param>
        /// <returns>The <see cref="ServiceReference{T}"/> instance.  This should never be null.</returns>
        public static ServiceReference<T> GetReference<T>(out ServiceReference<T> accessor) where T : class
        {
            accessor = ServiceReference<T>.Instance;
            return accessor;
        }
        /// <summary>
        /// Overrides the local override provider for the indicated type for the current call context.
        /// </summary>
        /// <typeparam name="T">The type whose local provider should be overridden.</typeparam>
        /// <param name="localOverride">The new local override.  If null, blocks access to the global provider, making accesses think there is no provider.</param>
        public static void SetLocalProvider<T>(T localOverride) where T : class
        {
            ServiceReference<T>.Instance.Provider = localOverride;
        }
        /// <summary>
        /// Reverts the local call context to the global provider.
        /// Note that if the local provider override was set in a higher call context rather than the calling one, the local context and lower ones will be affected, but that context will *not* be affected.
        /// </summary>
        /// <typeparam name="T">The type whose local provider override should be removed.</typeparam>
        public static void RevertToGlobalProvider<T>() where T : class
        {
            ServiceReference<T>.Instance.ProviderOverride = null;
        }
    }
    /// <summary>
    /// Provides a class that provides access to the global and local providers for a service.
    /// Must be accessed through <see cref="ServiceReference{T}.Instance"/>.
    /// </summary>
    /// <remarks>
    /// Note that accessing the provider requires two accesses, one AsyncLocal access to get the local provider override followed by a fallback to the global provider.
    /// Attempting to optimize this to only do one access by caching the global provider in the AsyncLocal as it changes doesn't appear to be possible
    /// because this would still require checking the AsyncLocal to see if it is initialized, so conditional and dereferencing costs would be the same,
    /// but such a system would require allocating an async-local object for every call context, which greatly degrades AsyncLocal performance and has higher memory requirements.
    /// In addition, such a system would require either allocating another async-local object for any local overrides in subcontexts, or requires keeping track of
    /// the old local value to restore at the end of the subcontext override.  
    /// Using a straight async-local implementation makes subcontext overrides naturally rollback as the stack is unwound.
    /// </remarks>
    /// <typeparam name="T">The interface implemented by the service being accessed.</typeparam>
    public class ServiceReference<T> where T : class
    {
        private static readonly ServiceReference<T> _Instance = new ServiceReference<T>();
        /// <summary>
        /// Gets the <see cref="ServiceReference{T}"/> for the indicated specified type.
        /// </summary>
        internal static ServiceReference<T> Instance
        {
            get
            {
                return _Instance;
            }
        }


        /// <summary>
        /// The call-context-local singleton accessor (non-singleton accessors can be used for unit testing).
        /// </summary>
        private AsyncLocal<LocalServiceReference<T>> _localReference = new AsyncLocal<LocalServiceReference<T>>();
        /// <summary>
        /// The global reference accessor.
        /// </summary>
        private GlobalServiceReference<T> _globalReference = new GlobalServiceReference<T>();

        /// <summary>
        /// Gets the local <see cref="LocalServiceReference{T}"/> for the indicated specified type.
        /// This must be used internally in order to ensure local initialization.
        /// </summary>
        /// <returns>The <see cref="LocalServiceReference{T}"/> instance.</returns>
        internal LocalServiceReference<T> LocalReference
        {
            get
            {
                LocalServiceReference<T> ret = _localReference.Value;
                if (ret == null) ret = _localReference.Value = new LocalServiceReference<T>();    // initialize if needed
                return ret;
            }
        }

        internal ServiceReference()
        {
        }
        /// <summary>
        /// Gets or sets the global provider.
        /// If set to null, suppresses the default provider (so that the getter returns null).
        /// When setting the provider, overwrites any previous provider and raises the <see cref="GlobalProviderChanged"/> event either on this thread or another thread asynchronously.
        /// Thread-safe.
        /// </summary>
        public T GlobalProvider
        {
            get
            {
                return _globalReference.Provider;
            }
            set
            {
                _globalReference.Provider = value;
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local override provider.
        /// If set to null, reverts to the global provider and begins watching changes to that.
        /// Otherwise sets to the value specified.
        /// Thread-safe.
        /// </summary>
        public T ProviderOverride
        {
            get
            {
                return LocalReference.RawOverride as T;
            }
            set
            {
                LocalReference.RawOverride = value;
                LocalReference.RaiseProviderChanged();
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local provider.
        /// If set to null, suppresses any local or global provider and ignores updates to the global provider.
        /// Otherwise sets to the value specified.
        /// Thread-safe.
        /// </summary>
        public T Provider
        {
            get
            {
                return (LocalReference.RawOverride ?? _globalReference.Provider) as T;
            }
            set
            {
                LocalReference.Provider = value;
                LocalReference.RaiseProviderChanged();
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a new global provider is registered.  
        /// The notification may happen on any arbitrary thread.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service reference lives forever.
        /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), 
        /// and each notification may proceed at a different pcae, notifications may appear to come in a different order than the changes actually occurred.
        /// Subscribers should query the latest value if needed when they receive the event notification.
        /// This way if multiple changes happen, they will always end up with the latest value.
        /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs> GlobalProviderChanged
        {
            add
            {
                _globalReference.ProviderChanged += value;
            }
            remove
            {
                _globalReference.ProviderChanged -= value;
            }
        }
    }
    /// <summary>
    /// A scoping class that overrides the local service provider during its scope.
    /// Note that call context variables can survive returning from a function and calling into another function, so it is important to reset a local override before returning from the function where the override is used.
    /// As a result, restoring the original value is rarely needed, but especially in unit tests, the call context is carried between tests, so it needs to at least be reset when a test that overrides it is complete, just in case another test subsequently runs using the same call context.
    /// </summary>
    /// <typeparam name="T">The service interface type.</typeparam>
    public sealed class LocalProviderScopedOverride<T> : IDisposable where T : class
    {
        private static readonly ServiceReference<T> _Reference = Service.GetReference<T>();

        private T _oldGlobalProvider;
        private T _oldLocalProviderOverride;

        /// <summary>
        /// Constructs a scoped override that changes the serrvice for this call context until this instance is disposed.
        /// </summary>
        /// <param name="temporaryLocalProvider">The temporary service provider.</param>
        public LocalProviderScopedOverride(T temporaryLocalProvider)
        {
            _oldGlobalProvider = _Reference.GlobalProvider;
            _oldLocalProviderOverride = _Reference.ProviderOverride;
            _Reference.Provider = temporaryLocalProvider;
        }
        /// <summary>
        /// Gets the old value in case it is needed by the overriding provider.
        /// </summary>
        public T OldOverride { get { return _oldLocalProviderOverride; } }
        /// <summary>
        /// Gets the old value in case it is needed by the overriding provider.
        /// </summary>
        public T OldGlobal { get { return _oldGlobalProvider; } }

        #region IDisposable Support
        private bool _disposed = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _Reference.ProviderOverride = _oldLocalProviderOverride;
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
    /// A generic class used to serialize the instantiation of the default provider.
    /// </summary>
    /// <typeparam name="T">The concrete type of the service.</typeparam>
    class DefaultServiceProvider<T> where T : class
    {
        private static T _Provider = Activator.CreateInstance<T>();
        public static T GetProvider() { return _Provider; }
    }
    /// <summary>
    /// A class that manages a global service reference.
    /// </summary>
    /// <typeparam name="T">The interface type for the service being managed.</typeparam>
    public class GlobalServiceReference<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default provider has been suppressed.
        /// </summary>
        private static readonly object SuppressedProvider = new object();

        /// <summary>
        /// The current provider.  Null if not yet initialized.  <see cref="SuppressedProvider"/> if the provider has been explicitly suppressed.
        /// </summary>
        private object _provider;

        internal GlobalServiceReference()
        {
            _provider = DefaultProvider();
        }

        private static T DefaultProvider()
        {
            Type impType = DefaultAmbientServices.TryFind(typeof(T));
            if (impType == null) return null;       // there is no default provider (yet)
            Type type = typeof(DefaultServiceProvider<>).MakeGenericType(impType);
            MethodInfo mi = type.GetMethod("GetProvider");
            T provider = (T)mi.Invoke(null, Array.Empty<object>());
            return provider;
        }
        private T LateAssignedDefaultProvider()
        {
            T newDefaultProvider = DefaultProvider();
            // still no default provider registered?  try again later
            if (newDefaultProvider == null) return null;
            T oldDefaultProvider = System.Threading.Interlocked.CompareExchange(ref _provider, newDefaultProvider, null) as T;
            // we should almost always get a null back here, but it's theoretically possible if two attempts to retrieve the provider happen at the same time, but even in this case, the only way we would get back instances of different types would be if the default ambient service changed, which shouldn't be possible given the current implementation
            // as a result, the non-null case below is unlikely to get covered by tests
            return (oldDefaultProvider == null)
                ? newDefaultProvider
                : oldDefaultProvider;
        }

        /// <summary>
        /// Gets or sets the provider.
        /// If set to null, suppresses the default provider (so that the getter returns null).
        /// When setting the provider, overwrites any previous provider and raises the <see cref="ProviderChanged"/> event.
        /// Thread-safe.
        /// </summary>
        public T Provider
        {
            get
            {
                return (_provider ?? LateAssignedDefaultProvider()) as T;
            }
            set
            {
                T oldProvider = System.Threading.Interlocked.Exchange(ref _provider, value ?? SuppressedProvider) as T;
                ProviderChanged?.Invoke(typeof(LocalServiceReference<T>), EventArgs.Empty);
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a new provider is registered.  
        /// The notification will happen on the thread and within the call context from which the change is initiated.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service reference lives forever.
        /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), 
        /// and each notification may proceed at a different pcae, notifications may appear to come in a different order than the changes actually occurred.
        /// Subscribers should query the latest value if needed when they receive the event notification.
        /// This way if multiple changes happen, they will always end up with the latest value.
        /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs> ProviderChanged;
    }
    /// <summary>
    /// A class that manages a call-context-local service reference.
    /// </summary>
    /// <typeparam name="T">The interface type for the service being managed.</typeparam>
    public class LocalServiceReference<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default provider has been suppressed.
        /// </summary>
        internal static readonly object SuppressedProvider = new object();

        /// <summary>
        /// The call-context-local override.
        /// </summary>
        private object _localOverride;

        internal LocalServiceReference()
        {
            _localOverride = null;
        }
        internal object RawOverride
        {
            get
            {
                return _localOverride;
            }
            set
            {
                _localOverride = value;
                RaiseProviderChanged();
            }
        }
        internal void RaiseProviderChanged()
        {
            ProviderChanged?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Gets or sets the provider.
        /// If set to null, suppresses the default provider (so that the getter returns null).
        /// When setting the provider, overwrites any previous provider and raises the <see cref="ProviderChanged"/> event either on this thread or another thread asynchronously.
        /// Thread-safe.
        /// </summary>
        public T Provider
        {
            get
            {
                return _localOverride as T;
            }
            set
            {
                _localOverride = value ?? SuppressedProvider;
                RaiseProviderChanged();
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a new local provider is registered.  
        /// The notification will happen on the thread and within the call context from which the change is initiated.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service reference lives forever.
        /// Because the event might be raised simultaneously on other threads or call contexts, 
        /// and each notification may proceed at a different pcae, notifications may appear to come in a different order than the changes actually occurred.
        /// Subscribers should query the latest value if needed when they receive the event notification.
        /// Subscriber must take care to avoid race conditions that may be caused by out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs> ProviderChanged;
    }
}
