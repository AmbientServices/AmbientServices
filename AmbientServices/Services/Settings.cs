using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An event args class that is sent when one or more ambient settings are changed.
    /// </summary>
    public class AmbientSettingsChangedEventArgs
    {
        /// <summary>
        /// A enumeration of the ambient settings keys that have changed, or null if the recipient must re-read all settings to discover which ones have changed.
        /// </summary>
        public IEnumerable<string> Keys { get; set; }
    }
    /// <summary>
    /// A interface that abstracts an ambient settings provider.
    /// </summary>
    public interface IAmbientSettingsProvider
    {
        /// <summary>
        /// Gets the name of the settings provider so that a settings consumer can know where a changed setting value came from.
        /// </summary>
        string ProviderName { get; }
        /// <summary>
        /// Gets the current setting for the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>
        string GetSetting(string key);
        /// <summary>
        /// An event that will notify subscribers when one or more settings values change.  
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// Note that due to the multithreaded nature of this event, the setting value may be updated before the subscriber is notified of the change.
        /// </summary>
        event EventHandler<AmbientSettingsChangedEventArgs> SettingsChanged;
    }
    /// <summary>
    /// An interface that may or may not also be implemented by an ambient settings provider.
    /// </summary>
    public interface IMutableAmbientSettingsProvider : IAmbientSettingsProvider
    {
        /// <summary>
        /// Changes the specified setting.
        /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
        /// </summary>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="value">The new string value for the setting.</param>
        /// <param name="defaultValue">The default value for the setting.</param>
        /// <returns>A <see cref="IAmbientSetting{T}"/> instance that is updatedt every time the setting gets updated.</returns>
        void ChangeSetting(string key, string value);
    }
}
