using AmbientServices.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices;

/// <summary>
/// An implementation of <see cref="IAmbientSettingsSet"/> that treats multiple settings sets as a single set.
/// </summary>
public class AmbientSettingsLayers : IMutableAmbientSettingsSet
{
    private readonly List<IAmbientSettingsSet> _setsInLowPriorityOrder = new();

    /// <summary>
    /// Create a settings layer with just an in-memory settings set.
    /// </summary>
    public AmbientSettingsLayers()
    {
        // add a mutable set at the top level
        _setsInLowPriorityOrder.Add(new BasicAmbientSettingsSet());
    }
    /// <summary>
    /// Create a settings layer with a specified set of settings sets.
    /// If the last (highest priority) set is not mutable, a mutable set will be added so that all settings can be mutated.
    /// </summary>
    /// <param name="sets">An enumeration of settings sets to add, with the last one being the highest priority, hiding same-named settings in any previous sets.  null values are ignored.</param>
    public AmbientSettingsLayers(IEnumerable<IAmbientSettingsSet?> sets)
    {
        if (sets == null) throw new ArgumentNullException(nameof(sets));
        _setsInLowPriorityOrder.AddRange(sets.WhereNotNull());
        // if the last set is not mutable, add a mutable set at the top level
        if (_setsInLowPriorityOrder[_setsInLowPriorityOrder.Count-1] is not IMutableAmbientSettingsSet)
        {
            _setsInLowPriorityOrder.Add(new BasicAmbientSettingsSet());
        }
    }
    /// <summary>
    /// Create a settings layer with a specified set of settings sets.
    /// If the last (highest priority) set is not mutable, a mutable set will be added so that all settings can be mutated.
    /// </summary>
    /// <param name="sets">An enumeration of settings sets to add, with the last one being the highest priority, hiding same-named settings in previous sets.  null values are ignored.</param>
    public AmbientSettingsLayers(params IAmbientSettingsSet[] sets) : this((IEnumerable<IAmbientSettingsSet>)sets)
    {
    }

    /// <summary>
    /// Gets the name of the set of settings so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    public string SetName => $"Layers[{string.Join(",", _setsInLowPriorityOrder.Select(s => s.SetName))}]";
    /// <summary>
    /// Changes the specified setting.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed.</returns>
    public bool ChangeSetting(string key, string? value)
    {
        IMutableAmbientSettingsSet mutableSet = (IMutableAmbientSettingsSet)_setsInLowPriorityOrder[_setsInLowPriorityOrder.Count-1];
        return mutableSet.ChangeSetting(key, value);
    }
    /// <summary>
    /// Gets the current raw value for the setting with the specified key from the first settings set in which it is found, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set in any of the sets.</returns>
    public string? GetRawValue(string key)
    {
        for (int offset = _setsInLowPriorityOrder.Count - 1; offset >= 0; --offset)
        {
            IAmbientSettingsSet set = _setsInLowPriorityOrder[offset];
            string? rawValue = set.GetRawValue(key);
            if (rawValue != null) return rawValue;
        }
        return null;
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key from the first settings set in which it is found, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set in any of the sets.</returns>
    public object? GetTypedValue(string key)
    {
        for (int offset = _setsInLowPriorityOrder.Count - 1; offset >= 0; --offset)
        {
            IAmbientSettingsSet set = _setsInLowPriorityOrder[offset];
            object? typedValue = set.GetTypedValue(key);
            if (typedValue != null) return typedValue;
        }
        return null;
    }
}
