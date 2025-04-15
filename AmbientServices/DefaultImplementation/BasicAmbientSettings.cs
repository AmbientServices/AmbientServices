using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AmbientServices;

/// <summary>
/// A basic settings set implementation that may be used for unit test dependency injection.
/// </summary>
[DefaultAmbientService(typeof(IAmbientSettingsSet))]
public class BasicAmbientSettingsSet : IAmbientSettingsSet
{
    /// <summary>
    /// The set name of the default settings set.
    /// </summary>
    public const string DefaultSetName = "Default";
    private readonly LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsSet, object?, IAmbientSettingInfo> _weakSettingRegistered;
    private readonly ConcurrentDictionary<string, string> _rawValues;
    private readonly ConcurrentDictionary<string, object> _typedValues;

    /// <summary>
    /// Constructs the default ambient settings set.
    /// </summary>
    internal BasicAmbientSettingsSet()
        : this (DefaultSetName)
    {
    }
    /// <summary>
    /// Constructs a new ambient settings set with the specified values.
    /// </summary>
    /// <param name="name">The name of the set.</param>
    public BasicAmbientSettingsSet(string name)
    {
        SetName = name;
        _rawValues = new ConcurrentDictionary<string, string>();
        _typedValues = new ConcurrentDictionary<string, object>();
        _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsSet, object?, IAmbientSettingInfo>(
                this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
        SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
    }
    /// <summary>
    /// Constructs a new ambient settings set with the specified values.
    /// </summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="values">A set of name-value pairs to use for the initial values for the set.</param>
    public BasicAmbientSettingsSet(string name, IDictionary<string, string> values)
    {
        SetName = name;
        _rawValues = new ConcurrentDictionary<string, string>(values);
        _typedValues = new ConcurrentDictionary<string, object>();
        if (values != null)
        {
            foreach (string key in values.Keys)
            {
                IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
                _typedValues[key] = (ps != null) ? ps.Convert(this, values[key]) : values[key];
            }
        }
        _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<BasicAmbientSettingsSet, object?, IAmbientSettingInfo>(
                this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
        SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
    }

    private static void NewSettingRegistered(BasicAmbientSettingsSet settingsSet, object? sender, IAmbientSettingInfo setting)
    {
        // is there a value for this setting?
        string? value;
        if (settingsSet._rawValues.TryGetValue(setting.Key, out value))
        {
            // get the typed value
            settingsSet._typedValues[setting.Key] = setting.Convert(settingsSet, value);
        }
    }

    /// <summary>
    /// Gets the name of the settings set so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    public string SetName { get; }
    /// <summary>
    /// Gets the current raw (string) value for the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>

    public string? GetRawValue(string key)
    {
        string? value;
        return _rawValues.TryGetValue(key, out value) ? value : null;
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public object? GetTypedValue(string key)
    {
        object? value;
        return _typedValues.TryGetValue(key, out value) ? value : null;
    }
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => true;
    /// <summary>
    /// Changes the specified setting.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed.</returns>
    public bool ChangeSetting(string key, string? value)
    {
        if (value == null)
        {
            string? oldValue;
            _rawValues.TryRemove(key, out oldValue);
            _typedValues.TryRemove(key, out _);
            // did the value *not* change?  bail out early
            if (oldValue == null) return false;
        }
        else
        {
            string? oldValue = null;
            _rawValues.AddOrUpdate(key, value, (k, v) => { oldValue = v; return value; });
            // did the value *not* change?  bail out early
            if (string.Equals(value, oldValue, StringComparison.Ordinal)) return false;
            IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
            _typedValues[key] = (ps != null) ? ps.Convert(this, value) : value;
        }
        return true;
    }
    /// <summary>
    /// Gets a string representing the settings instance.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return "Settings: " + SetName;
    }
}
