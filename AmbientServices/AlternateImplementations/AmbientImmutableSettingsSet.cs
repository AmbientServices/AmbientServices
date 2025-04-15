using System;
using System.Collections.Generic;

namespace AmbientServices;

/// <summary>
/// A settings set implementation that contains settings that are assigned at construction and cannot be changed.
/// </summary>
public class AmbientImmutableSettingsSet : IAmbientSettingsSet
{
    /// <summary>
    /// The set name of the default settings set.
    /// </summary>
    public const string DefaultSetName = "Immutable";
    private readonly Dictionary<string, string> _rawValues;
    private readonly Dictionary<string, object> _typedValues;

    /// <summary>
    /// Constructs the default ambient settings set.
    /// </summary>
    internal AmbientImmutableSettingsSet()
        : this(DefaultSetName)
    {
    }
    /// <summary>
    /// Constructs a new immutable ambient settings set with the specified values.
    /// </summary>
    /// <param name="name">The name of the set.</param>
    public AmbientImmutableSettingsSet(string name)
    {
        SetName = name;
        _rawValues = new Dictionary<string, string>();
        _typedValues = new Dictionary<string, object>();
    }
    /// <summary>
    /// Constructs a new immutable ambient settings set with the specified values.
    /// </summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="values">A set of name-value pairs to use for the initial values for the set.</param>
    public AmbientImmutableSettingsSet(string name, IDictionary<string, string> values)
    {
        SetName = name;
        _rawValues = new Dictionary<string, string>(values);
        _typedValues = new Dictionary<string, object>();
        if (values != null)
        {
            foreach (string key in values.Keys)
            {
                IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
                _typedValues[key] = (ps != null) ? ps.Convert(this, values[key]) : values[key];
            }
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
        return _rawValues.TryGetValue(key, out string? value) ? value : null;
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public object? GetTypedValue(string key)
    {
        return _typedValues.TryGetValue(key, out object? value) ? value : null;
    }
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => false;
    /// <summary>
    /// Changes the specified setting, if possible.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed (it may have had already the same value).</returns>
    public bool ChangeSetting(string key, string? value) => throw new InvalidOperationException($"{nameof(AmbientImmutableSettingsSet)} is not mutable.");

    /// <summary>
    /// Gets a string representing the settings instance.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return "ImmutableSettings: " + SetName;
    }
}
