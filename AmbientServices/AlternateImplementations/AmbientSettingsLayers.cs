using AmbientServices.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices;

/// <summary>
/// An implementation of <see cref="IAmbientSettingsSet"/> that treats multiple settings sets as a single set.
/// </summary>
/// <remarks>
/// <pitch>Composes several settings sets into one with override semantics — later (higher-priority) sets hide same-named settings in earlier ones — so a fixed base configuration can be selectively overridden (for example, environment over file over defaults) while consumers see a single set.  Always writable: a mutable top layer is guaranteed.</pitch>
/// <pledge><see cref="IAmbientSettingsSet"/></pledge>
/// <pledge>
/// Reads probe the layers from highest priority to lowest and return the first value found.  Writes go only to the highest-priority layer (a mutable one is appended at construction when the given layers end with an immutable one), so a change shadows lower layers rather than modifying them, and removing a setting from the top layer merely unmasks whatever a lower layer holds — it cannot remove a value owned by a lower layer.  The set's name is composed from the layer names so provenance remains visible.
/// </pledge>
/// <plan>An ordered <see cref="List{T}"/> of the composed sets, fixed at construction (which is what makes unsynchronized concurrent reads of the layer list safe); each get walks the list in reverse until a layer returns non-null, so read cost grows with the number of layers above the owning one, and nothing is cached or merged — every read reflects the layers' live values.</plan>
/// </remarks>
public class AmbientSettingsLayers : IAmbientSettingsSet
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
        if (!_setsInLowPriorityOrder[_setsInLowPriorityOrder.Count-1].SettingsAreMutable)
        {
            _setsInLowPriorityOrder.Add(new BasicAmbientSettingsSet());
        }
    }
    /// <summary>
    /// Create a settings layer with a specified set of settings sets.
    /// If the last (highest priority) set is not mutable, a mutable set will be added so that all settings can be mutated.
    /// </summary>
    /// <param name="sets">An enumeration of settings sets to add, with the last one being the highest priority, hiding same-named settings in previous sets.  null values are ignored.</param>
    public AmbientSettingsLayers(params IAmbientSettingsSet?[] sets) : this((IEnumerable<IAmbientSettingsSet?>)sets)
    {
    }

    /// <summary>
    /// Gets the name of the set of settings so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    public string SetName => $"Layers[{string.Join(",", _setsInLowPriorityOrder.Select(s => s.SetName))}]";
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
        IAmbientSettingsSet mutableSet = (IAmbientSettingsSet)_setsInLowPriorityOrder[_setsInLowPriorityOrder.Count-1];
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
