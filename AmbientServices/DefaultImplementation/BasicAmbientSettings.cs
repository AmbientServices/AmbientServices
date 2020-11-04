using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A basic settings provider that may be used for unit test dependency injection.
    /// </summary>
    [DefaultAmbientServiceProvider(typeof(IAmbientSettingsProvider))]
    public class BasicAmbientSettingsProvider : IMutableAmbientSettingsProvider
    {
        private readonly string _name;
        private readonly LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsProvider, object, IProviderSetting> _weakSettingRegistered;
        private ConcurrentDictionary<string, string> _rawValues;
        private ConcurrentDictionary<string, object> _typedValues;

        /// <summary>
        /// Constructs a new empty ambient settings provider.
        /// </summary>
        public BasicAmbientSettingsProvider()
            : this (nameof(BasicAmbientSettingsProvider))
        {
        }
        /// <summary>
        /// Constructs a new ambient settings provider with the specified values.
        /// </summary>
        /// <param name="name">The name of the provider.</param>
        public BasicAmbientSettingsProvider(string name)
        {
            _name = name;
            _rawValues = new ConcurrentDictionary<string, string>();
            _typedValues = new ConcurrentDictionary<string, object>();
            _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsProvider, object, IProviderSetting>(
                    this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
            SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
        }
        /// <summary>
        /// Constructs a new ambient settings provider with the specified values.
        /// </summary>
        /// <param name="name">The name of the provider.</param>
        /// <param name="values">A set of name-value pairs to use for the initial values for the provider.</param>
        public BasicAmbientSettingsProvider(string name, IDictionary<string, string> values)
        {
            _name = name;
            _rawValues = new ConcurrentDictionary<string, string>(values);
            _typedValues = new ConcurrentDictionary<string, object>();
            if (values != null)
            {
                foreach (string key in values.Keys)
                {
                    IProviderSetting ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
                    _typedValues[key] = (ps != null) ? ps.Convert(this, values[key]) : values[key];
                }
            }
            _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsProvider, object, IProviderSetting>(
                    this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
            SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
        }
        static void NewSettingRegistered(BasicAmbientSettingsProvider settingsProvider, object sender, IProviderSetting setting)
        {
            // is there a value for this setting?
            string value;
            if (settingsProvider._rawValues.TryGetValue(setting.Key, out value))
            {
                // get the typed value
                settingsProvider._typedValues[setting.Key] = setting.Convert(settingsProvider, value);
            }
        }

        /// <summary>
        /// Gets the name of the settings provider so that a settings consumer can know where a changed setting value came from.
        /// </summary>
        public string ProviderName => _name;
        /// <summary>
        /// Gets the current raw (string) value for the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>

        public string GetRawValue(string key)
        {
            string value;
            return _rawValues.TryGetValue(key, out value) ? value : null;
        }
        /// <summary>
        /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>
        public object GetTypedValue(string key)
        {
            object value;
            return _typedValues.TryGetValue(key, out value) ? value : null;
        }
        /// <summary>
        /// An event that will notify subscribers when one or more settings values change.  
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// Note that due to the multithreaded nature of this event, the setting value may be updated before the subscriber is notified of the change.
        /// </summary>
        public event EventHandler<AmbientSettingsChangedEventArgs> SettingsChanged;

        /// <summary>
        /// Changes the specified setting.
        /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
        /// </summary>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="value">The new string value for the setting, or null to remove the setting and revert to the .</param>
        /// <returns>Whether or not the setting actually changed.</returns>
        public bool ChangeSetting(string key, string value)
        {
            if (value == null)
            {
                string oldValue;
                _rawValues.TryRemove(key, out oldValue);
                _typedValues.TryRemove(key, out _);
                // did the value *not* change?  bail out early
                if (oldValue == null) return false;
            }
            else
            {
                string oldValue = null;
                _rawValues.AddOrUpdate(key, value, (k, v) => { oldValue = v; return value; });
                // did the value *not* change?  bail out early
                if (String.Equals(value, oldValue, StringComparison.Ordinal)) return false;
                IProviderSetting ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
                _typedValues[key] = (ps != null) ? ps.Convert(this, value) : value;
            }
            SettingsChanged?.Invoke(this, new AmbientSettingsChangedEventArgs { Keys = new string[] { key } });
            return true;
        }
        /// <summary>
        /// Gets a string representing the settings instance.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Settings: " + _name;
        }
    }
}
