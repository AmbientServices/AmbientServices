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
        private ConcurrentDictionary<string, string> _rawValues;

        /// <summary>
        /// Constructs a new empty ambient settings provider.
        /// </summary>
        public BasicAmbientSettingsProvider()
        {
            _name = nameof(BasicAmbientSettingsProvider);
            _rawValues = new ConcurrentDictionary<string, string>();
        }
        /// <summary>
        /// Constructs a new ambient settings provider with the specified values.
        /// </summary>
        /// <param name="name">The name of the provider.</param>
        public BasicAmbientSettingsProvider(string name)
        {
            _name = name;
            _rawValues = new ConcurrentDictionary<string, string>();
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
        }

        /// <summary>
        /// Gets the name of the settings provider so that a settings consumer can know where a changed setting value came from.
        /// </summary>
        public string ProviderName => _name;
        /// <summary>
        /// Gets the current setting for the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>

        public string GetSetting(string key)
        {
            string value;
            return _rawValues.TryGetValue(key, out value) ? value : null;
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
        /// <param name="value">The new string value for the setting.</param>
        /// <param name="defaultValue">The default value for the setting.</param>
        /// <returns>A <see cref="IAmbientSetting{T}"/> instance that is updatedt every time the setting gets updated.</returns>
        public void ChangeSetting(string key, string value)
        {
            _rawValues.AddOrUpdate(key, value, (k,v) => value);
            SettingsChanged?.Invoke(this, new AmbientSettingsChangedEventArgs { Keys = new string[] { key } });
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
