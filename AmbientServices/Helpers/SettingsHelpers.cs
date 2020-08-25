using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An abstraction of a setting that holds a strongly-typed value and provides an event to notify the user of changes to the setting value.
    /// </summary>
    /// <typeparam name="T">The type represented by the the setting.</typeparam>
    public interface IAmbientSetting<T>
    {
        /// <summary>
        /// Gets the current value of the setting (cached from the value given by the provider).
        /// </summary>
        T Value { get; }
        /// <summary>
        /// An event that will notify subscribers when a setting value changes.  
        /// Note that <see cref="Value"/> will be updated regardless of whether or not this event is utilized.
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// </summary>
        /// <remarks>
        /// Users MUST NOT rely on this event getting triggered before (or even after) updated values are returned by <see cref="Value"/>.
        /// Because this event may be triggered asynchronously, multiple triggers of the event might be received out-of-order.
        /// As a result, the new value is not sent.  The subscriber should get the latest value and use that (and handle any resulting race conditions).
        /// </remarks>
        event EventHandler<EventArgs> ValueChanged;
    }
    /// <summary>
    /// An static class that utilizes the <see cref="IAmbientClockProvider"/> if one is registered, or the system clock if not.
    /// </summary>
    public static class AmbientSettings
    {
        /// <summary>
        /// Construct a setting instance.  
        /// If a non-null provider is specified, the setting will be attached to that, otherwise the ambient settings provider will be used.
        /// Never returns a setting whose value is always the default value.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> to get the setting value from.  If null, returns an setting attached to the ambient settings provider.</param>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
        public static IAmbientSetting<T> GetSetting<T>(IAmbientSettingsProvider provider, string key, Func<string, T> convert, T defaultValue = default(T))
        {
            return (provider == null) ? (IAmbientSetting<T>)GetAmbientSetting<T>(key, convert, defaultValue) : (IAmbientSetting<T>)GetProviderSetting<T>(provider, key, convert, defaultValue);
        }
        /// <summary>
        /// Construct a setting instance that uses a specific settings provider and caches the setting with the specified key, converting it from a string using the specified delegate.
        /// Settings changes caused by a setting value change within the specified provider will trigger the <see cref="ValueChanged"/> event and in <see cref="Value"/>, 
        /// but no provider changes or overrides will.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> to get the setting value from.  If null, the setting will always contain the default value.</param>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
        public static IAmbientSetting<T> GetProviderSetting<T>(IAmbientSettingsProvider provider, string key, Func<string, T> convert, T defaultValue = default(T))
        {
            return new ProviderSetting<T>(provider, key, convert, defaultValue);
        }
        /// <summary>
        /// Construct an ambient setting instance that caches the setting with the specified key, converting it from a string using the specified delegate.
        /// Settings will be gathered from the ambient local provider, even if it changes after construction.
        /// The <see cref="ValueChanged"/> event will be triggered if the setting changes due to the global provider changing or the value within the global provider changing, 
        /// but will not be triggered if a local override is applied or a setting changes there.  
        /// This is due to the fact that the settings instance isn't owned by any particular call context.
        /// In these cases, the value returned by <see cref="Value"/> will be different from the most recent value received by event subscribers.
        /// </summary>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.  Defaults to default(T).</param>
        public static IAmbientSetting<T> GetAmbientSetting<T>(string key, Func<string, T> convert, T defaultValue = default(T))
        {
            return new AmbientSetting<T>(key, convert, defaultValue);
        }
    }

    /// <summary>
    /// A class used to cache and access a strongly-typed setting and provide notification when the setting changes.
    /// </summary>
    /// <typeparam name="T">The type represented by the the setting.</typeparam>
    class ProviderSetting<T> : IAmbientSetting<T>
    {
        private readonly string _key;
        private readonly T _defaultValue;
        private readonly Func<string, T> _convert;
        private readonly IAmbientSettingsProvider _provider;
        private readonly LazyUnsubscribeWeakEventListenerProxy<ProviderSetting<T>, object, AmbientSettingsChangedEventArgs> _weakValueChanged;

        private string _currentStringValue;                 // interlocked
        private object _currentValue;                       // interlocked


        internal string Key { get { return _key; } }
        internal T DefaultValue { get { return _defaultValue; } }
        internal Func<string, T> Convert { get { return _convert; } }
        internal string StringValue { get { return _currentStringValue; } }
        internal IAmbientSettingsProvider Provider { get { return _provider; } }

        /// <summary>
        /// Construct a setting instance that uses a specific settings provider and caches the setting with the specified key, converting it from a string using the specified delegate.
        /// Settings changes caused by a setting value change within the specified provider will trigger the <see cref="ValueChanged"/> event and in <see cref="Value"/>, 
        /// but no provider changes or overrides will.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> to get the setting value from.</param>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
        public ProviderSetting(IAmbientSettingsProvider provider, string key, Func<string, T> convert, T defaultValue = default(T))
        {
            if (convert == null)
            {
                if (typeof(T) != typeof(string)) throw new ArgumentNullException(nameof(convert));
                convert = s => (T)(object)s;
            }
            _provider = provider;
            _key = key;
            _defaultValue = defaultValue;
            _convert = convert;
            string initialValue = provider?.GetSetting(key);
            _currentStringValue = initialValue;
            _currentValue = string.IsNullOrEmpty(initialValue) ? defaultValue : convert(initialValue);
            if (provider != null)
            {
                IAmbientSettingsProvider tempProvider = provider; // without using this, the unsubscribe delegate below causes the instance to leak
                Action<ProviderSetting<T>, object, AmbientSettingsChangedEventArgs> staticUnsubscribe = StaticUnderlyingSettingValueChanged;
                _weakValueChanged = new LazyUnsubscribeWeakEventListenerProxy<ProviderSetting<T>, object, AmbientSettingsChangedEventArgs>(
                        this, StaticUnderlyingSettingValueChanged, wvc => tempProvider.SettingsChanged -= wvc.WeakEventHandler);
                _provider.SettingsChanged += _weakValueChanged.WeakEventHandler;
            }
        }
        static void StaticUnderlyingSettingValueChanged(ProviderSetting<T> setting, object sender, AmbientSettingsChangedEventArgs e)
        {
            // is the provider notifying us *not* our *current* provider? (it may be a former provider and still be attached)
            if (!Object.ReferenceEquals(sender, setting._provider)) return;
            setting.UnderlyingSettingValueChanged(sender, e);
        }
        private void UnderlyingSettingValueChanged(object _, AmbientSettingsChangedEventArgs e)
        {
            // was either *this* setting changed or *all* settings changed?
            if (e.Keys == null || e.Keys.Contains(_key))
            {
                // get the new value
                string newStringValue = _provider.GetSetting(_key);
                // did the value really change?
                if (newStringValue != _currentStringValue)
                {
                    // update the value and notify subscribers
                    System.Threading.Interlocked.Exchange(ref _currentStringValue, newStringValue);
                    T newValue = (newStringValue == null) ? _defaultValue : _convert(newStringValue);
                    System.Threading.Interlocked.Exchange(ref _currentValue, newValue);
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        /// <summary>
        /// Gets the current value of the setting (cached from the value given by the provider).
        /// </summary>
        public T Value
        {
            get
            {
                return (T)_currentValue;
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a setting value changes.  
        /// Note that <see cref="Value"/> will be updated regardless of whether or not this event is utilized.
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// </summary>
        /// <remarks>
        /// Users MUST NOT rely on this event getting triggered before (or even after) updated values are returned by <see cref="Value"/>.
        /// Because this event may be triggered asynchronously, multiple triggers of the event might be received out-of-order.
        /// As a result, the new value is not sent.  The subscriber should get the latest value and use that (and handle any resulting race conditions).
        /// </remarks>
        public event EventHandler<EventArgs> ValueChanged;
    }
    /// <summary>
    /// A class used to cache and access a strongly-typed ambient setting and provide notification when the setting changes.
    /// </summary>
    /// <typeparam name="T">The type contained in the setting.</typeparam>
    /// <remarks>
    /// An ambient setting is one that may be updated within global the provider, or may come from a local service override.
    /// Change notifications through <see cref=""/>
    /// </remarks>
    class AmbientSetting<T> : IAmbientSetting<T>
    {
        private readonly ServiceAccessor<IAmbientSettingsProvider> _ambientSettings;
        private readonly LazyUnsubscribeWeakEventListenerProxy<AmbientSetting<T>, object, EventArgs> _weakAmbientProviderChanged;

        private readonly object _changeNotificationLock = new object();
        private ProviderSetting<T> _providerSetting;        // interlocked
        private LazyUnsubscribeWeakEventListenerProxy<AmbientSetting<T>, object, EventArgs> _weakValueChanged;      // interlocked

        internal AmbientSetting(ServiceAccessor<IAmbientSettingsProvider> ambientSettings, string key, Func<string, T> convert, T defaultValue = default(T))
        {
            _ambientSettings = ambientSettings;
            _providerSetting = new ProviderSetting<T>(_ambientSettings.LocalProvider, key, convert, defaultValue);
            SubscribeToProviderSettingChanges(_providerSetting);
            _weakAmbientProviderChanged = new LazyUnsubscribeWeakEventListenerProxy<AmbientSetting<T>, object, EventArgs>(
                    this, GlobalProviderChanged, luwf => _ambientSettings.GlobalProviderChanged -= luwf.WeakEventHandler);
            _ambientSettings.GlobalProviderChanged += _weakAmbientProviderChanged.WeakEventHandler;
        }
        private void SubscribeToProviderSettingChanges(ProviderSetting<T> providerSetting)
        {
            System.Threading.Interlocked.Exchange(ref _weakValueChanged, new LazyUnsubscribeWeakEventListenerProxy<AmbientSetting<T>, object, EventArgs>(
                    this, UnderlyingSettingValueChanged, wvc => providerSetting.ValueChanged -= wvc.WeakEventHandler));
            providerSetting.ValueChanged += _weakValueChanged.WeakEventHandler;
        }
        /// <summary>
        /// Construct an ambient setting instance that caches the setting with the specified key, converting it from a string using the specified delegate.
        /// Settings will be gathered from the ambient local provider, even if it changes after construction.
        /// The <see cref="ValueChanged"/> event will be triggered if the setting changes due to the global provider changing or the value within the global provider changing, 
        /// but will not be triggered if a local override is applied or a setting changes there.  
        /// This is due to the fact that the settings instance isn't owned by any particular call context.
        /// In these cases, the value returned by <see cref="Value"/> will be different from the most recent value received by event subscribers.
        /// </summary>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.  Defaults to default(T).</param>
        public AmbientSetting(string key, Func<string, T> convert, T defaultValue = default(T))
            : this (Service.GetAccessor<IAmbientSettingsProvider>(), key, convert, defaultValue)
        {
        }
        static void UnderlyingSettingValueChanged(AmbientSetting<T> setting, object sender, EventArgs e)
        {
            // is the provider notifying us *not* our *current* provider? (it may be a former provider and still be attached)
            if (!Object.ReferenceEquals(sender, setting._providerSetting)) return;
            setting.ValueChanged?.Invoke(setting, e);
        }
        private static void GlobalProviderChanged(AmbientSetting<T> setting, object sender, EventArgs e)
        {
            setting.GlobalProviderChanged(sender, e);
        }
        private void GlobalProviderChanged(object _, EventArgs e)
        {
            bool notify = false;
            // I generally prefer lock-free constructs whenever possible, but in this case we need to
            // lock here to make sure we get the latest settings value to compare to
            // without the lock this could get called twice when two updates happen very quickly or when the system is under heavy load
            // and the two threads could execute in such a way that the first updated global provider is retreived on one thread,
            // but then the thread is interrupted and a second change is made and goes through the entire notification process.
            // Sometime later, when the first thread resumes and without the lock, it would overwrite the current setting values with the first version,
            // leaving things in an incorrect state
            // note that we are careful here to avoid calling out to outside code that might lock (the event subscribers) while the lock is held
            lock (_changeNotificationLock)
            {
                IAmbientSettingsProvider newProvider = _ambientSettings.GlobalProvider;
                // is the provider different from our current provider?
                if (newProvider != _providerSetting.Provider)
                {
                    // get the current setting value
                    string oldStringValue = _providerSetting.StringValue;
                    // make a new setting attached to the new settings provider
                    ProviderSetting<T> newSetting = new ProviderSetting<T>(newProvider, _providerSetting.Key, _providerSetting.Convert, _providerSetting.DefaultValue);
                    System.Threading.Interlocked.Exchange(ref _providerSetting, newSetting);
                    // subscribe to value changes to this
                    SubscribeToProviderSettingChanges(_providerSetting);
                    // has the value changed?
                    string newStringValue = newSetting.StringValue;
                    if (oldStringValue != newStringValue) notify = true;
                }
            }
            if (notify) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Gets the current value of the setting.
        /// If using 
        /// </summary>
        public T Value
        {
            get
            {
                IAmbientSettingsProvider local = _ambientSettings.LocalProvider;
                // local override eliminates the settings provider?
                if (local == null) return _providerSetting.DefaultValue;
                IAmbientSettingsProvider localProvider = _ambientSettings.LocalProvider;
                // is the local override the same provider we already have?
                if (localProvider == _providerSetting.Provider) return _providerSetting.Value;
                // use the value from the local service override
                string providerValue = localProvider.GetSetting(_providerSetting.Key);
                if (providerValue == null) return _providerSetting.DefaultValue;
                return _providerSetting.Convert(providerValue);
            }
        }
        /// <summary>
        /// An event that will notify subscribers when a setting value changes.  
        /// Note that <see cref="Value"/> will be updated regardless of whether or not this event is utilized.
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// </summary>
        /// <remarks>
        /// Users MUST NOT rely on this event getting triggered before (or even after) updated values are returned by <see cref="Value"/>.
        /// Because this event may be triggered asynchronously, multiple triggers of the event might be received out-of-order.
        /// As a result, the new value is not sent.  The subscriber should get the latest value from the settings provider and use that, handling any race conditions in the process.
        /// </remarks>
        public event EventHandler<EventArgs> ValueChanged;
    }
}
