using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An static class that utilizes the <see cref="IAmbientClockProvider"/> if one is registered, or the system clock if not.
    /// </summary>
    public static class AmbientSettings
    {
        /// <summary>
        /// Gets an enumeration of <see cref="IAmbientSettingInfo"/> with descriptions and the last time used for all ambient settings.
        /// </summary>
        public static IEnumerable<IAmbientSettingInfo> AmbientSettingsInfo { get { return SettingsRegistry.DefaultRegistry.Settings; } }
        /// <summary>
        /// Construct a setting instance.  
        /// If a non-null provider is specified, the setting will be attached to that, otherwise the ambient settings provider will be used.
        /// Never returns a setting whose value is always the default value.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> to get the setting value from.  If null, returns an setting attached to the ambient settings provider.</param>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="description">A description of the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValueString">The default string value for the setting.  This will be used as the current value if the setting is not set.  Defaults to null.</param>
        public static IAmbientSetting<T> GetSetting<T>(IAmbientSettingsProvider provider, string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            return (provider == null) ? (IAmbientSetting<T>)GetAmbientSetting<T>(key, description, convert, defaultValueString) : (IAmbientSetting<T>)GetProviderSetting<T>(provider, key, description, convert, defaultValueString);
        }
        /// <summary>
        /// Construct a setting instance that uses a specific settings provider and caches the setting with the specified key, converting it from a string using the specified delegate.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> to get the setting value from.  If null, the setting will always contain the default value.</param>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="description">A description of the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValueString">The default string value for the setting.  This will be used as the current value if the setting is not set.  Defaults to null.</param>
        public static IAmbientSetting<T> GetProviderSetting<T>(IAmbientSettingsProvider provider, string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            return new ProviderSetting<T>(provider, key, description, convert, defaultValueString);
        }
        /// <summary>
        /// Construct an ambient setting instance that caches the setting with the specified key, converting it from a string using the specified delegate.
        /// Settings will be gathered from the ambient local provider, if one exists.
        /// </summary>
        /// <param name="key">A key string identifying the setting.</param>
        /// <param name="description">A description of the setting.</param>
        /// <param name="convert">A delegate that takes a string and returns the type.</param>
        /// <param name="defaultValueString">The default string value for the setting.  This will be used as the current value if the setting is not set.  Defaults to null.</param>
        public static IAmbientSetting<T> GetAmbientSetting<T>(string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            return new AmbientSetting<T>(key, description, convert, defaultValueString);
        }
    }

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
    }
    /// <summary>
    /// An abstraction that provides access to a setting's description and last time used.
    /// </summary>
    public interface IAmbientSettingInfo
    {
        /// <summary>
        /// Gets the setting key.
        /// </summary>
        string Key { get; }
        /// <summary>
        /// Gets the default value for the setting.
        /// </summary>
        string DefaultValueString { get; }
        /// <summary>
        /// Gets a description of the setting.
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Gets the last time the setting's value was retrieved.
        /// </summary>
        DateTime LastUsed { get; }
    }
    /// <summary>
    /// An interface that adds private information about a setting.
    /// </summary>
    public interface IProviderSetting : IAmbientSettingInfo
    {
        /// <summary>
        /// Gets the default value that is used when the value is not found in the settings provider.
        /// </summary>
        object DefaultValue { get; }
        /// <summary>
        /// Converts the setting value from the specified string to a typed value.
        /// The implementor may cache the value being generated if the provider is the global provider.
        /// </summary>
        /// <param name="provider">The provider asking for the setting value.</param>
        /// <param name="value">The typed value for the setting.</param>
        /// <returns>The typed value for the setting.</returns>
        object Convert(IAmbientSettingsProvider provider, string value);
    }
    /// <summary>
    /// A global registry for settings that includes their keys, descriptions, last used time, conversion functions, and default values.
    /// </summary>
    public class SettingsRegistry
    {
        private static SettingsRegistry _DefaultRegistry = new SettingsRegistry();
        /// <summary>
        /// Gets the default registry.
        /// </summary>
        public static SettingsRegistry DefaultRegistry { get { return _DefaultRegistry; } }


        private ConcurrentDictionary<string, WeakReference<IProviderSetting>> _settings = new ConcurrentDictionary<string, WeakReference<IProviderSetting>>();
        /// <summary>
        /// Registers an <see cref="IProviderSetting"/> in the global registry.
        /// </summary>
        /// <param name="setting">The <see cref="IProviderSetting"/> to register.</param>
        public void Register(IProviderSetting setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            WeakReference<IProviderSetting> newReference = new WeakReference<IProviderSetting>(setting);
            WeakReference<IProviderSetting> existingReference;
            int loopCount = 0;
            do
            {
                IProviderSetting existingSetting;
                existingReference = _settings.GetOrAdd(setting.Key, newReference);
                // did we NOT succeed?
                if (newReference != existingReference)
                {
                    // is the old one gone?
                    if (!existingReference.TryGetTarget(out existingSetting))
                    {
                        // overwrite that one--were we NOT able to overwrite it?
                        if (!_settings.TryUpdate(setting.Key, newReference, existingReference))
                        { // the inside of this loop is nearly impossible to cover in tests
                            // wait a bit and try again
                            System.Threading.Thread.Sleep((int)Math.Pow(2, loopCount + 1));
                            continue;
                        } // else we successfully overwrote it and there is no need to look for a conflict
                        existingSetting = setting;
                    }
                    // the old one is still there, so we need to check for a conflict
                    string existingDescription = existingSetting.Description ?? "<null>";
                    string description = setting.Description ?? "<null>";
                    string existingDefaultValueString = existingSetting.DefaultValueString ?? "<null>";
                    string defaultValueString = setting.DefaultValueString ?? "<null>";
                    if (!String.Equals(existingDescription, description, StringComparison.Ordinal) || !String.Equals(existingDefaultValueString, defaultValueString, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"A setting with the key {setting.Key} has already been registered ({existingDescription} vs {description} or {existingDefaultValueString} vs {defaultValueString})!");
                    } // else we didn't succeed, but it doesn't look like there is a conflict anyway, so we're probably fine
                } // else we succeeded
                // raise the registered event
                SettingRegistered?.Invoke(null, setting);
                return;
            // the loop and exception is nearly impossible to cover in tests
            } while (loopCount++ < 10);
            throw new TimeoutException("Timeout attempting to register setting!");
        }
        /// <summary>
        /// Gets an enumeration of all the settings in the system.
        /// </summary>
        public IEnumerable<IProviderSetting> Settings
        {
            get
            {
                foreach (KeyValuePair<string, WeakReference<IProviderSetting>> s in _settings)
                {
                    IProviderSetting setting;
                    if (s.Value.TryGetTarget(out setting))
                    {
                        yield return setting;
                    }
                    else
                    {
                        _settings.TryRemove(s.Key, out _);
                    }
                }
            }
        }
        /// <summary>
        /// Attempts to get the <see cref="IProviderSetting"/> for the specified settings key.
        /// </summary>
        /// <param name="key">The key for the setting.</param>
        /// <returns>An <see cref="IProviderSetting"/> for the specified setting.</returns>
        public IProviderSetting TryGetSetting(string key)
        {
            WeakReference<IProviderSetting> wrSetting;
            IProviderSetting setting;
            return _settings.TryGetValue(key, out wrSetting) ? (wrSetting.TryGetTarget(out setting) ? setting : null) : null;
        }
        /// <summary>
        /// An event the is raised when a new setting is registered, allowing settings providers to call the setting's conversion function to get a strongly-typed value.
        /// </summary>
        public event EventHandler<IProviderSetting> SettingRegistered;
    }
    class SettingBase<T> : IProviderSetting
    {
        protected static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsAccessor = ServiceAccessor<IAmbientSettingsProvider>.Accessor;

        private readonly string _key;
        private readonly string _description;
        private readonly string _defaultValueString;
        private readonly T _defaultValue;
        private readonly Func<string, T> _convert;
        private long _lastUsedTicks;      // interlocked
        private object _globalValue;      // interlocked

        public SettingBase(string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            if (convert == null)
            {
                if (typeof(T) != typeof(string)) throw new ArgumentNullException(nameof(convert));
                convert = s => (T)(object)s;
            }
            _key = key;
            _description = description;
            _convert = convert;
            _defaultValueString = defaultValueString;
            _defaultValue = convert(defaultValueString);
            SettingsRegistry.DefaultRegistry.Register(this);
        }

        /// <summary>
        /// Gets the setting key.
        /// </summary>
        public string Key { get { return _key; } }
        /// <summary>
        /// Gets a description of the setting.
        /// </summary>
        public string Description { get { return _description; } }
        /// <summary>
        /// Gets the last time the setting's value was retrieved.
        /// </summary>
        public DateTime LastUsed { get { return new DateTime(_lastUsedTicks); } }
        /// <summary>
        /// Gets the default value for the setting.
        /// </summary>
        public string DefaultValueString => _defaultValueString;
        /// <summary>
        /// Gets the typed default value.
        /// </summary>
        public T DefaultValue => _defaultValue;
        /// <summary>
        /// Gets the untyped default value.
        /// </summary>
        object IProviderSetting.DefaultValue => _defaultValue;

        /// <summary>
        /// Converts the specified value for the specified provider.
        /// </summary>
        /// <param name="provider">The <see cref="IAmbientSettingsProvider"/> for the provider making the conversion.</param>
        /// <param name="value">The string value for the setting.</param>
        /// <returns>A typed value created from the string value.</returns>
        public object Convert(IAmbientSettingsProvider provider, string value)
        {
            T ret;
            try
            {
                ret = _convert(value);
            }
#pragma warning disable CA1031 
            catch
            {
                ret = _defaultValue;
            }
#pragma warning restore CA1031 
            // is this provider this setting's provider or the global provider
            if (provider == _SettingsAccessor.GlobalProvider)
            {
                System.Threading.Interlocked.Exchange(ref _globalValue, ret);
            }
            return ret;
        }
        /// <summary>
        /// Updates the last used time for this setting to the current time.
        /// </summary>
        public void UpdateLastUsed()
        {
            long accessTime = DateTime.UtcNow.Ticks;
            long oldValue = _lastUsedTicks;
            // loop attempting to put it in until we win the race
            while (accessTime > oldValue)
            {
                // try to put in our value--did we win the race?
                if (oldValue == System.Threading.Interlocked.CompareExchange(ref _lastUsedTicks, accessTime, oldValue))
                {
                    // we're done and we were the new max
                    return;
                }
                // NOTE: this loop is nondeterministic when running with multiple threads, so code coverage may not cover these lines, and it's not possible to force this condition
                // update our value
                oldValue = _lastUsedTicks;
            }
            // we're done and we were not the max value
        }
        /// <summary>
        /// Gets the current value of the setting (cached from the value given by the provider).
        /// </summary>
        public object GlobalValue
        {
            get
            {
                UpdateLastUsed();
                return _globalValue;
            }
        }
    }
    class ProviderSetting<T> : IAmbientSetting<T>
    {
        protected static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsAccessor = ServiceAccessor<IAmbientSettingsProvider>.Accessor;

        protected readonly ServiceAccessor<IAmbientSettingsProvider> _accessor;
        protected readonly SettingBase<T> _settingBase;
        private readonly IAmbientSettingsProvider _fixedProvider;

        public ProviderSetting(IAmbientSettingsProvider fixedProvider, string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            _settingBase = new SettingBase<T>(key, description, convert, defaultValueString);
            _fixedProvider = fixedProvider;
        }

        internal ProviderSetting(ServiceAccessor<IAmbientSettingsProvider> accessor, string key, string description, Func<string, T> convert, string defaultValueString = null)
        {
            _settingBase = new SettingBase<T>(key, description, convert, defaultValueString);
            _accessor = accessor;
        }
        /// <summary>
        /// Gets the current value of the setting (cached from the value given by the provider).
        /// </summary>
        public virtual T Value
        {
            get
            {
                _settingBase.UpdateLastUsed();
                if (_accessor != null)
                {
                    object value = _accessor.Provider?.GetTypedValue(_settingBase.Key);
                    return (value == null) ? _settingBase.DefaultValue : (T)value;
                }
                else if (_fixedProvider != null)
                {
                    object value = _fixedProvider.GetTypedValue(_settingBase.Key);
                    return (value == null) ? _settingBase.DefaultValue : (T)value;
                }
                else
                {
                    return (_settingBase.GlobalValue == null) ? _settingBase.DefaultValue : (T)_settingBase.GlobalValue;
                }
            }
        }
    }
    class AmbientSetting<T> : ProviderSetting<T>
    {
        public AmbientSetting(string key, string description, Func<string, T> convert, string defaultValueString = null)
            : base((IAmbientSettingsProvider)null, key, description, convert, defaultValueString)
        {
        }

        internal AmbientSetting(ServiceAccessor<IAmbientSettingsProvider> accessor, string key, string description, Func<string, T> convert, string defaultValueString = null)
            : base(accessor, key, description, convert, defaultValueString)
        {
        }

        /// <summary>
        /// Gets the current value of the setting, either from the call-context-local settings provider overide, or the cached value from the global provider.
        /// </summary>
        public override T Value
        {
            get
            {
                ServiceAccessor<IAmbientSettingsProvider> accessor = _accessor ?? _SettingsAccessor;
                // is there a local provider override?
                IAmbientSettingsProvider localProviderOverride = accessor.ProviderOverride;
                if (localProviderOverride != null)
                {
                    _settingBase.UpdateLastUsed();
                    object value = localProviderOverride.GetTypedValue(_settingBase.Key);
                    return (value == null) ? _settingBase.DefaultValue : (T)value;
                }
                // is there a local provider suppression?
                IAmbientSettingsProvider localProvider = accessor.Provider;
                if (localProvider == null) return _settingBase.DefaultValue;
                // fall through to the base (global provider)
                return base.Value;
            }
        }
    }
}
