using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
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
    /// An interface representing one setting containing a particular type.
    /// </summary>
    /// <typeparam name="T">The type contained in the setting.</typeparam>
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
        event EventHandler<SettingValueChangedEventArgs<T>> SettingValueChanged;
    }
    /// <summary>
    /// An interface that abstracts a service that manages settings.
    /// As used here, this is intended for *ambient* settings, but the interface is equally useful for any collection of settings.
    /// The default setting is always available.
    /// </summary>
    /// <remarks>
    /// Note to implementors:
    /// Individual settings values may be lazily retrieved, but should be not be recomputed or requeried every time <see cref="GetSetting"/> is called.
    /// Individual settings values may be updated asynchronously using interlocked functions, but <see cref="ISetting{T}.SettingValueChanged"/> should only be triggered when the value actually changes.
    /// If all settings are requeried, <see cref="ISetting{T}.SettingValueChanged"/> should not be triggered for all settings, only those whose values actually changed.
    /// </remarks>
    public interface ISettings
    {
        /// <summary>
        /// Gets the value for the setting with the specified key, converting it from a string to the specified type if needed.
        /// </summary>
        /// <typeparam name="T">The type of the setting.</typeparam>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="defaultValue">The default value for the setting.</param>
        /// <param name="convert">A function that converts the string value for the setting into the desired runtime type.</param>
        /// <returns>A <see cref="ISetting{T}"/> instance that is updated every time the setting gets updated.</returns>
        ISetting<T> GetSetting<T>(string key, T defaultValue = default(T), Func<string, T> convert = null);
    }

    [DefaultImplementation]
    class DefaultSettings : ISettings
    {
        class Setting<T> : ISetting<T>
        {
            private T _value;

            public Setting(T constantValue)
            {
                _value = constantValue;
            }

            public T Value => _value;
#pragma warning disable CS0067
            public event EventHandler<SettingValueChangedEventArgs<T>> SettingValueChanged;
#pragma warning restore CS0067
#if MAYBE_LATER
            void ChangeShape()
            {
                OnSettingValueChangedEvent(new SettingValueChangedEventArgs<T> { Setting = this, NewValue = _value });
            }
            protected virtual void OnSettingValueChangedEvent(SettingValueChangedEventArgs<T> e)
            {
                SettingValueChanged?.Invoke(this, e);
            }
#endif
        }
        public ISetting<T> GetSetting<T>(string key, T defaultValue = default(T), Func<string, T> convert = null)
        {
            return new Setting<T>(defaultValue);
        }
    }
}
