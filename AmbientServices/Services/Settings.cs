namespace AmbientServices;

/// <summary>
/// An interface that abstracts a set of ambient settings.
/// </summary>
/// <remarks>
/// <pitch>Implement this to expose a configuration source (in-memory values, environment variables, a configuration file or service) as a named set of string-keyed settings that every AmbientServices consumer reads through.  By convention settings affect behavior only in ways callers are not concerned about, so sources can be swapped or layered without changing the outputs callers depend on.</pitch>
/// <pledge>
/// Every setting value has a raw string form; its typed form is that raw value converted by the conversion registered in <see cref="SettingsRegistry"/> for the key, or the raw string itself when no conversion is registered — the raw and typed getters must describe the same underlying value.  Null means "not set": a set never stores a null value, and a null return tells the consumer to fall back to another set or to the setting's declared default.
/// A set advertises whether it accepts changes; attempting to change a setting on an immutable set is an invalid call.  A change reports whether the stored value actually changed (writing an identical value reports false), and changing a setting to null removes it.  The set's name identifies the provenance of values it supplies.  Reads and writes may arrive concurrently from any thread.
/// </pledge>
/// </remarks>
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
