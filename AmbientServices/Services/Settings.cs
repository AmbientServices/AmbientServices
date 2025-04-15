namespace AmbientServices;

/// <summary>
/// A interface that abstracts a set of ambient settings.
/// </summary>
public interface IAmbientSettingsSet
{
    /// <summary>
    /// Gets the name of the set of settings so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    string SetName { get; }
    /// <summary>
    /// Gets the current raw value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    string? GetRawValue(string key);
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    object? GetTypedValue(string key);
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    bool SettingsAreMutable { get; }
    /// <summary>
    /// Changes the specified setting, if possible.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed (it may have had already the same value).</returns>
    bool ChangeSetting(string key, string? value);
}
