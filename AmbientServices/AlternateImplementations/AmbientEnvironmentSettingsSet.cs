using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AmbientServices;

/// <summary>
/// A settings set that uses the process environment.
/// Note that since the framework does not provide an event for when environment variables change, changes after initialization will not be propagated unless those changes are made through <see cref="ChangeSetting(string, string?)"/> or <see cref="Refresh"/>.
/// </summary>
/// <remarks>
/// <para><b>Security:</b> At construction time, only environment variables whose keys are already registered in <see cref="SettingsRegistry"/> are copied into memory.
/// Settings registered later are imported when <see cref="SettingsRegistry.SettingRegistered"/> fires (see <c>NewSettingRegistered</c>).
/// Other variables are read lazily from the process environment when requested via <see cref="GetRawValue(string)"/> or <see cref="GetTypedValue(string)"/>.
/// This reduces the risk that unrelated secrets in the environment (database passwords, API keys, tokens, etc.) are held in this object's dictionaries for the lifetime of the process.</para>
/// <para><b>Security:</b> <see cref="ChangeSetting(string, string?)"/> calls <see cref="Environment.SetEnvironmentVariable(string, string?)"/> and can affect child processes and other components in the same process that read the environment.
/// Callers should treat mutable environment settings as privileged operations.</para>
/// <para><b>Security:</b> Any value retrieved through this settings set may contain sensitive data. Avoid logging raw setting values unless they are known to be non-sensitive.</para>
/// </remarks>
public class AmbientEnvironmentSettingsSet : IAmbientSettingsSet
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static AmbientEnvironmentSettingsSet Instance { get; } = new();

    private readonly LazyUnsubscribeWeakEventListenerProxy<AmbientEnvironmentSettingsSet, object?, IAmbientSettingInfo> _weakSettingRegistered;
    private readonly ConcurrentDictionary<string, string> _rawValues;
    private readonly ConcurrentDictionary<string, object> _typedValues;
    private readonly ConcurrentDictionary<string, byte> _observedKeys = new();
    private readonly object _lock = new();

    /// <summary>
    /// Constructs the ambient environment settings set.
    /// </summary>
    internal AmbientEnvironmentSettingsSet()
    {
        _rawValues = new ConcurrentDictionary<string, string>();
        _typedValues = new ConcurrentDictionary<string, object>();
        ImportRegisteredEnvironmentVariables();
        _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<AmbientEnvironmentSettingsSet, object?, IAmbientSettingInfo>(
                this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
        SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
    }

    private void ImportRegisteredEnvironmentVariables()
    {
        foreach (IAmbientSettingInfo registered in SettingsRegistry.DefaultRegistry.Settings)
        {
            string? value = Environment.GetEnvironmentVariable(registered.Key);
            if (value == null) continue;
            _ = _rawValues.TryAdd(registered.Key, value);
            _typedValues[registered.Key] = registered.Convert(this, value);
        }
    }

    private static void NewSettingRegistered(AmbientEnvironmentSettingsSet settingsSet, object? sender, IAmbientSettingInfo setting)
    {
        string? value = settingsSet._rawValues.TryGetValue(setting.Key, out string? cached)
            ? cached
            : Environment.GetEnvironmentVariable(setting.Key);
        if (value != null)
        {
            _ = settingsSet._rawValues.TryAdd(setting.Key, value);
            settingsSet._typedValues[setting.Key] = setting.Convert(settingsSet, value);
        }
    }
    /// <summary>
    /// Refreshes the settings manually by re-reading the environment variables for registered keys, keys previously observed via getters, and keys changed through this instance.
    /// If another thread attempts to refresh while a refresh is happening, all threads will wait until all refreshes are complete.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)    // this maybe could be improved, but we need to ensure that the first entry to this loop gets processed before subsequent entries do--otherwise the entries get mixed up
        {
            HashSet<string> keysToSync = new(StringComparer.Ordinal);
            foreach (string key in _rawValues.Keys)
            {
                keysToSync.Add(key);
            }
            foreach (string key in _observedKeys.Keys)
            {
                keysToSync.Add(key);
            }
            foreach (IAmbientSettingInfo registered in SettingsRegistry.DefaultRegistry.Settings)
            {
                keysToSync.Add(registered.Key);
            }

            Dictionary<string, string?> updates = new();
            foreach (string key in keysToSync)
            {
                string? envValue = Environment.GetEnvironmentVariable(key);
                if (!_rawValues.TryGetValue(key, out string? cached) || !string.Equals(cached, envValue, StringComparison.Ordinal))
                {
                    updates[key] = envValue;
                }
            }

            foreach (KeyValuePair<string, string?> kvp in updates)
            {
                _ = InternalChangeSetting(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// Gets the name of the settings set so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    public string SetName => "Environment";
    /// <summary>
    /// Gets the current raw (string) value for the specified key, or null if the setting is not set.
    /// Values changed in the environment after initialization are visible here without calling <see cref="Refresh"/> unless the value was previously cached by <see cref="Refresh"/> or <see cref="ChangeSetting(string, string?)"/>.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public string? GetRawValue(string key)
    {
        _ = _observedKeys.TryAdd(key, 0);
        if (_rawValues.TryGetValue(key, out string? cached))
        {
            return cached;
        }
        return Environment.GetEnvironmentVariable(key);
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public object? GetTypedValue(string key)
    {
        _ = _observedKeys.TryAdd(key, 0);
        if (_typedValues.TryGetValue(key, out object? typed))
        {
            return typed;
        }
        string? raw = _rawValues.TryGetValue(key, out string? cached) ? cached : Environment.GetEnvironmentVariable(key);
        if (raw == null) return null;
        IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, raw) : raw;
    }

    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => true;

    /// <summary>
    /// Changes the specified setting in the process environment and in this settings set's in-memory caches.
    /// </summary>
    /// <remarks>
    /// <b>Security:</b> This updates the process environment via <see cref="Environment.SetEnvironmentVariable(string, string?)"/>, which may expose the new value to child processes and to other code that reads environment variables directly.
    /// Use only for settings that are safe to propagate at the process level.
    /// </remarks>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed.</returns>
    public bool ChangeSetting(string key, string? value)
    {
        Environment.SetEnvironmentVariable(key, value);
        bool ret = InternalChangeSetting(key, value);
        return ret;
    }
    private bool InternalChangeSetting(string key, string? value)
    {
        _ = _observedKeys.TryAdd(key, 0);
        if (value == null)
        {
            _ = _rawValues.TryRemove(key, out string? oldValue);
            _ = _typedValues.TryRemove(key, out _);
            // did the value *not* change?  return that fact
            if (oldValue == null) return false;
        }
        else
        {
            string? oldValue = null;
            _ = _rawValues.AddOrUpdate(key, value, (k, v) => { System.Threading.Interlocked.CompareExchange(ref oldValue, v, null); return value; });          // note that according to the documentation, the delegate here might be called more than once, so we need this CompareExchange to be sure we only get the old value here
            // did the value *not* change?
            if (string.Equals(value, oldValue, StringComparison.Ordinal))
            {
                return false;
            }
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
        return "Settings: Environment";
    }
}
