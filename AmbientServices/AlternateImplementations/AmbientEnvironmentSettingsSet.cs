﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AmbientServices
{
    /// <summary>
    /// A settings set that uses the process environment.
    /// Note that since the framework does not provide an event for when environment variables change, changes after initialization will not be propagated unless those changes are made through <see cref="ChangeSetting(string, string?)"/>.
    /// </summary>
    public class AmbientEnvironmentSettingsSet : IMutableAmbientSettingsSet
    {
        private static readonly AmbientEnvironmentSettingsSet _Instance = new();
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static AmbientEnvironmentSettingsSet Instance => _Instance;

        private readonly LazyUnsubscribeWeakEventListenerProxy<AmbientEnvironmentSettingsSet, object?, IAmbientSettingInfo> _weakSettingRegistered;
        private readonly ConcurrentDictionary<string, string> _rawValues;
        private readonly ConcurrentDictionary<string, object> _typedValues;

        /// <summary>
        /// Constructs the ambient environment settings set.
        /// </summary>
        private AmbientEnvironmentSettingsSet()
        {
            _rawValues = new ConcurrentDictionary<string, string>();
            Dictionary<string, string> newEnvironment = GetFromEnvironment();
            _typedValues = new ConcurrentDictionary<string, object>();
            if (newEnvironment.Count > 0)
            {
                foreach (KeyValuePair<string, string> kvp in newEnvironment)
                {
                    _rawValues.AddOrUpdate(kvp.Key, kvp.Value, (k, v) => kvp.Value);
                }
                foreach (KeyValuePair<string, string> kvp in newEnvironment)
                {
                    IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(kvp.Key);
                    _typedValues[kvp.Key] = (ps != null) ? ps.Convert(this, kvp.Value) : kvp.Value;
                }
            }
            _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<AmbientEnvironmentSettingsSet, object?, IAmbientSettingInfo>(
                    this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
            SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
        }

        private static void NewSettingRegistered(AmbientEnvironmentSettingsSet settingsSet, object? sender, IAmbientSettingInfo setting)
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
        /// Refreshes the settings manually by re-reading the environment variables.
        /// </summary>
        public void Refresh()
        {
            Dictionary<string, string> newEnvironment = GetFromEnvironment();
            // loop through all the old values
            foreach (string key in _rawValues.Keys)
            {
                // not in the new set?
                if (!newEnvironment.ContainsKey(key))
                {
                    // remove this one now because it's not longer set in the environment
                    InternalChangeSetting(key, null);
                }
            }
            // loop through all the new values
            foreach (KeyValuePair<string, string> kvp in newEnvironment)
            {
                // is there *not* a value for this key or the value has changed?
                if (!_rawValues.TryGetValue(kvp.Key, out string? value) || value != kvp.Value)
                {
                    InternalChangeSetting(kvp.Key, kvp.Value);
                }
                // else ignore this environment variable, because it's the same as the value we already have
            }
        }

        private static Dictionary<string, string> GetFromEnvironment()
        {
            IDictionary values = Environment.GetEnvironmentVariables();
            Dictionary<string, string> newEnvironment = new();
            foreach (object? obj in values)
            {
                if (obj is not DictionaryEntry entry) continue;
                string? key = (string?)entry.Key;
                string? value = (string?)entry.Value;
                if (key == null || value == null) continue;
                newEnvironment[key] = value;
            }
            return newEnvironment;
        }
        /// <summary>
        /// Gets the name of the settings set so that a settings consumer can know where a changed setting value came from.
        /// </summary>
        public string SetName => "Environment";
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
        /// Changes the specified setting.
        /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
        /// </summary>
        /// <param name="key">A string that uniquely identifies the setting.</param>
        /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
        /// <returns>Whether or not the setting actually changed.</returns>
        public bool ChangeSetting(string key, string? value)
        {
            Environment.SetEnvironmentVariable(key, value);
            return InternalChangeSetting(key, value);
        }
        private bool InternalChangeSetting(string key, string? value)
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
            return "Settings: Environment";
        }
    }
}
