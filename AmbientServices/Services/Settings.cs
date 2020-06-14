using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// An interface that abstracts an ambient settings service.
    /// As used here, this is intended for *ambient* settings, but the interface is equally useful for any collection of settings.
    /// The basic settings implementation is always available and just returns the default value unless a new ambient settings service is registered.
    /// </summary>
    /// <remarks>
    /// Note to implementors:
    /// Individual settings values may be lazily retrieved, but should be not be recomputed or requeried every time <see cref="ISetting{T}.Value"/> is used.
    /// Individual settings values may be updated asynchronously using interlocked functions, but <see cref="ISetting{T}.ValueChanged"/> should only be triggered when the value actually changes.
    /// If all settings are requeried, <see cref="ISetting{T}.ValueChanged"/> should not be triggered for all settings, only those whose values actually changed.
    /// Whether or not a settings value is considered changed depends on the <see cref="Object.Equals"/> implementation for the type associated with the setting.
    /// </remarks>
    public interface IAmbientSettings
    {
        /// <summary>
        /// Gets a wrapper interface that will track the value for the setting with the specified key, converting it from a string to the specified type if needed. See <see cref="ISetting{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the setting.</typeparam>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="convert">A function that converts the non-null-but-possibly-empty string value for the setting into the desired runtime type (for settings that are actually strings, this may be a function that simply returns the input).</param>
        /// <param name="defaultValue">The default value for the setting.</param>
        /// <returns>A <see cref="ISetting{T}"/> instance that is updated every time the setting gets updated.</returns>
        ISetting<T> GetSetting<T>(string key, Func<string, T> convert, T defaultValue = default(T));
    }
    /// <summary>
    /// An interface that may or may not also be implemented by an ambient settings service.
    /// </summary>
    public interface IMutableAmbientSettings
    { 
        /// <summary>
        /// Changes the specified setting.
        /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
        /// </summary>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="value">The new string value for the setting.</param>
        /// <param name="defaultValue">The default value for the setting.</param>
        /// <returns>A <see cref="ISetting{T}"/> instance that is updated every time the setting gets updated.</returns>
        void ChangeSetting(string key, string value);
    }
    /// <summary>
    /// An event args class that is sent when a setting value is changed.
    /// </summary>
    /// <typeparam name="T">The type for the setting.</typeparam>
    public class SettingValueChangedEventArgs<T>
    {
        /// <summary>
        /// The <see cref="ISetting{T}"/> for the setting that changed.
        /// </summary>
        public ISetting<T> Setting { get; set; }
        /// <summary>
        /// The new setting value (which will be the same as <see cref="Setting"/>.<see cref="ISetting{T}.Value"/>.
        /// </summary>
        public T NewValue { get; set; }
    }
    /// <summary>
    /// An interface representing one setting containing a particular type and providing cached access to its strongly-typed value and notification when it changes.
    /// </summary>
    /// <typeparam name="T">The type contained in the setting.</typeparam>
    /// <remarks>
    /// Implementors need to be careful to be sure that the implementation behaves properly when the settings provider changes.
    /// A single instance should be able to seamlessly switch when the backend settings provider changes, giving the user access to the latest setting value from the new provider, and notifying the user if the value changed when the provider changed, or if the value changes later.
    /// Please see the reference implementation, BasicAmbientSettings.Setting.
    /// </remarks>
    public interface ISetting<T>
    {
        /// <summary>
        /// Gets the current value of the setting.
        /// </summary>
        T Value { get; }
        /// <summary>
        /// An event that will notify subscribers when a setting value changes.  
        /// Note that <see cref="Value"/> will be updated regardless of whether or not this event is utilized.
        /// This event is so that the subscriber *knows* when the value changes, in case something else needs to be done as a result of the change.
        /// </summary>
        event EventHandler<SettingValueChangedEventArgs<T>> ValueChanged;
    }
}
