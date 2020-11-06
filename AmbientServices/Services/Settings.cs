using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
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
        /// Gets the current raw value for the setting with the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>
        string GetRawValue(string key);
        /// <summary>
        /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
        /// </summary>
        /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
        /// <returns>The setting value, or null if the setting is not set.</returns>
        object GetTypedValue(string key);
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
        /// <returns>Whether or not the setting actually changed.</returns>
        bool ChangeSetting(string key, string value);
    }
}
