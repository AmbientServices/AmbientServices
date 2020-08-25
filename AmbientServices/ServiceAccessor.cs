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
        /// Gets the <see cref="ServiceAccessor{T}"/> for the indicated specified type.
        /// </summary>
        /// <typeparam name="T">The type whose service accessor is needed.</typeparam>
        /// <returns>The <see cref="ServiceAccessor{T}"/> instance.</returns>
        public static ServiceAccessor<T> GetAccessor<T>() where T : class
        {
            return ServiceAccessor<T>.GetAccessor();
        }
        /// <summary>
        /// Gets the <see cref="ServiceAccessor{T}"/> for the indicated specified type.
        /// </summary>
        /// <typeparam name="T">The type whose service accessor is needed.</typeparam>
        /// <param name="accessor">[OUT] Receives the service accessor.</param>
        /// <returns>The <see cref="ServiceAccessor{T}"/> instance.</returns>
        public static ServiceAccessor<T> GetAccessor<T>(out ServiceAccessor<T> accessor) where T : class
        {
            accessor = ServiceAccessor<T>.GetAccessor();
            return accessor;
        }
    }
    /// <summary>
    /// Provides a class that provides access to the global and local providers for a service.
    /// </summary>
    /// <typeparam name="T">The interface implemented by the service being accessed.</typeparam>
    public class ServiceAccessor<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default provider has been suppressed.
        /// </summary>
        private static readonly object SuppressedProvider = new object();

        /// <summary>
        /// The singleton accessor (non-singleton accessors can be used for unit testing).
        /// </summary>
        private static ServiceAccessor<T> _Accessor = new ServiceAccessor<T>();

        /// <summary>
        /// The current provider.  Null if not yet initialized.  <see cref="SuppressedProvider"/> if the provider has been explicitly suppressed.
        /// </summary>
        private object _provider;
        /// <summary>
        /// The call-context-local service accesor (caches the current value and updates automatically when the global provider changes).
        /// </summary>
        private AsyncLocal<CallContextServiceAccessor<T>> _callContextServiceAccesor;

        internal ServiceAccessor()
        {
            _provider = DefaultProvider();
            _callContextServiceAccesor = new AsyncLocal<CallContextServiceAccessor<T>>();
        }

        private void InitializeCallContext()
        {
            if (_callContextServiceAccesor.Value == null)
            {
                _callContextServiceAccesor.Value = new CallContextServiceAccessor<T>(this);
            }
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
            return (oldDefaultProvider == null)
                ? newDefaultProvider
                : oldDefaultProvider;
        }

        /// <summary>
        /// Gets the <see cref="ServiceAccessor{T}"/> for the indicated specified type.
        /// </summary>
        /// <typeparam name="T">The type whose service accessor is needed.</typeparam>
        /// <returns>The <see cref="ServiceAccessor{T}"/> instance.</returns>
        internal static ServiceAccessor<T> GetAccessor()
        {
            return _Accessor;
        }

        /// <summary>
        /// Gets or sets the global provider.
        /// If set to null, suppresses the default provider (so that the getter returns null).
        /// When setting the provider, overwrites any previous provider and triggers the <see cref="GlobalProviderChanged"/> event either on this thread or another thread asynchronously.
        /// Thread-safe.
        /// </summary>
        public T GlobalProvider
        {
            get
            {
                return (_provider ?? LateAssignedDefaultProvider()) as T;
            }
            set
            {
                T oldProvider = System.Threading.Interlocked.Exchange(ref _provider, value ?? SuppressedProvider) as T;
                GlobalProviderChanged?.Invoke(typeof(ServiceAccessor<T>), EventArgs.Empty);
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local override provider.
        /// If set to null, reverts to the global provider and begins watching changes to that.
        /// Otherwise sets to the value specified.
        /// Thread-safe.
        /// </summary>
        public T LocalProviderOverride
        {
            get
            {
                InitializeCallContext();
                return _callContextServiceAccesor.Value.ProviderOverride;
            }
            set
            {
                InitializeCallContext();
                _callContextServiceAccesor.Value.ProviderOverride = value;
            }
        }
        /// <summary>
        /// Gets or sets the call-context-local provider.
        /// If set to null, suppresses any local or global provider and ignores updates to the global provider.
        /// Otherwise sets to the value specified.
        /// Thread-safe.
        /// </summary>
        public T LocalProvider
        {
            get
            {
                InitializeCallContext();
                return _callContextServiceAccesor.Value.Provider;
            }
            set
            {
                InitializeCallContext();
                _callContextServiceAccesor.Value.Provider = value;
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a new global provider is registered.  
        /// The notification may happen on any arbitrary thread.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
        /// because the service broker lives forever.
        /// Because the event can be triggered asynchronously, notifications may not come in the order the changes occur.
        /// Subscribers should query the latest value if needed when they received the event notification and must take care to avoid race conditions that may be caused by out-of-order notifications.
        /// </remarks>
        public event EventHandler<EventArgs> GlobalProviderChanged;
    }

    /// <summary>
    /// A scoping class that overrides the local service provider during its scope.
    /// </summary>
    /// <typeparam name="T">The service interface type.</typeparam>
    public sealed class LocalServiceScopedOverride<T> : IDisposable where T : class
    {
        private static readonly ServiceAccessor<T> _Accessor = Service.GetAccessor<T>();

        private T _oldGlobalProvider;
        private T _oldLocalProviderOverride;

        /// <summary>
        /// Constructs a service ServiceOverride to override the service during the scope of the instance.
        /// </summary>
        /// <param name="temporaryProvider">The temporary service provider.</param>
        public LocalServiceScopedOverride(T temporaryProvider)
        {
            _oldGlobalProvider = _Accessor.GlobalProvider;
            _oldLocalProviderOverride = _Accessor.LocalProviderOverride;
            _Accessor.LocalProvider = temporaryProvider;
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
                    _Accessor.LocalProviderOverride = _oldLocalProviderOverride;
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
    /// A class that hooks into the global service provider and allows a call-context-local override.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class CallContextServiceAccessor<T> where T : class
    {
        /// <summary>
        /// An object whose instance is used to indicate that the default provider has been suppressed.
        /// </summary>
        private static readonly object SuppressedProvider = new object();

        private readonly object _changeNotificationLock = new object();
        /// <summary>
        /// The main service accessor.
        /// </summary>
        private readonly ServiceAccessor<T> _accessor;
        /// <summary>
        /// A weak subscriber to the global provider changed event.
        /// </summary>
        private LazyUnsubscribeWeakEventListenerProxy<CallContextServiceAccessor<T>, object, EventArgs> _weakGlobalProviderChanged;
        /// <summary>
        /// The provider override (null, <see cref="SuppressedProvider"/>, or the provider).
        /// </summary>
        private object _providerOverride;
        /// <summary>
        /// The cached (and asynchronously updated) global provider.
        /// </summary>
        private object _currentGlobalProvider;

        /// <summary>
        /// Constructs a call-context service accessor.
        /// </summary>
        /// <param name="accessor">The <see cref="ServiceAccessor{T}"/> this call context service accessor uses to access and refresh the global provider.</param>
        internal CallContextServiceAccessor(ServiceAccessor<T> accessor)
        {
            _accessor = accessor;
            UpdateSubscription();
        }
        private void GlobalProviderChanged(CallContextServiceAccessor<T> broker, object sender, EventArgs e)
        {
            // is there NOT an override?
            if (Object.ReferenceEquals(_providerOverride as T, null))
            {
                // I generally prefer lock-free constructs whenever possible, but in this case we need to
                // lock here to make sure we get the latest provider and set it into _currentGlobalProvider 
                // without the lock this function could get called twice when two updates happen very quickly or when the system is under heavy load
                // and the two threads could execute in such a way that the first updated global provider is retreived on one thread,
                // but then the thread is interrupted and a second change is made and goes through the entire notification process.
                // Sometime later, when the first thread resumes and without the lock, it would overwrite _currentGlobalProvider with the first version,
                // leaving it in an incorrect state
                // note that we are careful here to avoid calling out to outside code that might lock (the event subscribers) while the lock is held
                lock (_changeNotificationLock)
                {
                    // the new provider affects this call context, so keep it
                    System.Threading.Interlocked.Exchange(ref _currentGlobalProvider, _accessor.GlobalProvider);
                }
            }
        }
        private void UpdateSubscription()
        {
            // is there now *not* an override?
            if (_providerOverride == null)
            {
                // use the global provider
                _currentGlobalProvider = _accessor.GlobalProvider;
                // subscribe to changes in the global provider
                if (_weakGlobalProviderChanged == null)
                {
                    _weakGlobalProviderChanged = new LazyUnsubscribeWeakEventListenerProxy<CallContextServiceAccessor<T>, object, EventArgs>(
                        this, GlobalProviderChanged, wgic => _accessor.GlobalProviderChanged -= wgic.WeakEventHandler
                        );
                    _accessor.GlobalProviderChanged += _weakGlobalProviderChanged.WeakEventHandler;
                }
            }
            else // there is now an override
            {
                // have we not unsubscribed yet?
                if (_weakGlobalProviderChanged != null)
                {
                    // unsubscribe now and stop listening to provider changed event notifications
                    _weakGlobalProviderChanged.Unsubscribe();
                    _weakGlobalProviderChanged = null;
                }
                _currentGlobalProvider = _providerOverride;
            }
        }
        /// <summary>
        /// Gets or sets the provider.
        /// If set to null, suppresses any local or global provider and ignores updates to the global provider.
        /// </summary>
        public T Provider
        {
            get
            {
                return _currentGlobalProvider as T;
            }
            set
            {
                _providerOverride = value ?? SuppressedProvider;
                UpdateSubscription();
            }
        }
        /// <summary>
        /// Gets or sets the override provider, which will be null if there is no local override or if it is suppressed.
        /// If set to null, <see cref="Provider"/> reverts to the global provider.
        /// </summary>
        public T ProviderOverride
        {
            get
            {
                return _providerOverride as T;
            }
            set
            {
                _providerOverride = value;
                UpdateSubscription();
            }
        }
    }
}
