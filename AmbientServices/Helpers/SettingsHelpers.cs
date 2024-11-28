using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AmbientServices;

/// <summary>
/// An static class that utilizes the <see cref="IAmbientClock"/> if one is registered, or the system clock if not.
/// </summary>
public static class AmbientSettings
{
    /// <summary>
    /// Gets an enumeration of <see cref="IAmbientSettingInfo"/> with descriptions and the last time used for all ambient settings.
    /// </summary>
    public static IEnumerable<IAmbientSettingInfo> AmbientSettingsInfo => SettingsRegistry.DefaultRegistry.Settings;
    /// <summary>
    /// Construct a setting instance.  
    /// If a non-null settings set is specified, the setting will be attached to that, otherwise the ambient settings set will be used.
    /// Never returns a setting whose value is always the default value.
    /// </summary>
    /// <param name="settingsSet">The <see cref="IAmbientSettingsSet"/> to get the setting value from.  If null, returns an setting attached to the ambient settings set.</param>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    /// <param name="defaultValueString">The default string value for the setting.  This will be converted for the current value if the setting is not set.  Defaults to empty string.</param>
    public static IAmbientSetting<T> GetSetting<T>(IAmbientSettingsSet? settingsSet, string key, string description, Func<string, T> convert, string defaultValueString = "")
    {
        return (settingsSet == null) ? GetAmbientSetting<T>(key, description, convert, defaultValueString) : GetSettingsSetSetting<T>(settingsSet, key, description, convert, defaultValueString);
    }
    /// <summary>
    /// Construct a setting instance.  
    /// If a non-null settings set is specified, the setting will be attached to that, otherwise the ambient settings set will be used.
    /// Never returns a setting whose value is always the default value.
    /// </summary>
    /// <param name="settingsSet">The <see cref="IAmbientSettingsSet"/> to get the setting value from.  If null, returns an setting attached to the ambient settings set.</param>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    public static IAmbientSetting<T> GetSetting<T>(IAmbientSettingsSet? settingsSet, string key, string description, T defaultValue, Func<string, T> convert)
    {
        return (settingsSet == null) ? GetAmbientSetting<T>(key, description, defaultValue, convert) : GetSettingsSetSetting<T>(settingsSet, key, description, defaultValue, convert);
    }
    /// <summary>
    /// Construct a setting instance that uses a specific settings set and caches the setting with the specified key, converting it from a string using the specified delegate.
    /// </summary>
    /// <param name="settingsSet">The <see cref="IAmbientSettingsSet"/> to get the setting value from.  If null, the setting will always contain the default value.</param>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    /// <param name="defaultValueString">The default string value for the setting.  This will be converted for the current value if the setting is not set.  Defaults to empty string.</param>
    public static IAmbientSetting<T> GetSettingsSetSetting<T>(IAmbientSettingsSet? settingsSet, string key, string description, Func<string, T> convert, string defaultValueString = "")
    {
        return new SettingsSetSetting<T>(settingsSet, key, description, convert, defaultValueString);
    }
    /// <summary>
    /// Construct a setting instance that uses a specific settings set and caches the setting with the specified key, converting it from a string using the specified delegate.
    /// </summary>
    /// <param name="settingsSet">The <see cref="IAmbientSettingsSet"/> to get the setting value from.  If null, the setting will always contain the default value.</param>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    public static IAmbientSetting<T> GetSettingsSetSetting<T>(IAmbientSettingsSet? settingsSet, string key, string description, T defaultValue, Func<string, T> convert)
    {
        return new SettingsSetSetting<T>(settingsSet, key, description, defaultValue, convert);
    }
    /// <summary>
    /// Construct an ambient setting instance that caches the setting with the specified key, converting it from a string using the specified delegate.
    /// Settings will be gathered from the ambient local settings set, if one exists.
    /// </summary>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    /// <param name="defaultValueString">The default string value for the setting.  This will be converted for the current value if the setting is not set.  Defaults to empty string.</param>
    public static IAmbientSetting<T> GetAmbientSetting<T>(string key, string description, Func<string, T> convert, string defaultValueString = "")
    {
        return new AmbientSetting<T>(key, description, convert, defaultValueString);
    }
    /// <summary>
    /// Construct an ambient setting instance that caches the setting with the specified key, converting it from a string using the specified delegate.
    /// Settings will be gathered from the ambient local settings set, if one exists.
    /// </summary>
    /// <param name="key">A key string identifying the setting.</param>
    /// <param name="description">A description of the setting.</param>
    /// <param name="defaultValue">The default value for the setting.  This will be used as the current value if the setting is not set.</param>
    /// <param name="convert">A delegate that takes a string and returns the type.</param>
    public static IAmbientSetting<T> GetAmbientSetting<T>(string key, string description, T defaultValue, Func<string, T> convert)
    {
        return new AmbientSetting<T>(key, description, defaultValue, convert);
    }
}

/// <summary>
/// An abstraction of a setting that holds a strongly-typed value and provides an event to notify the user of changes to the setting value.
/// </summary>
/// <typeparam name="T">The type represented by the the setting.</typeparam>
public interface IAmbientSetting<T>
{
    /// <summary>
    /// Gets the key of the setting, which may be useful in providing an override without having to match the name with a magic value.
    /// </summary>
    string Key { get; }
    /// <summary>
    /// Gets the current value of the setting (cached from the value given by the settings set).
    /// </summary>
    T Value { get; }
    /// <summary>
    /// Gets the current value of the setting along with the name of the set that the value came from (or null if the default value was used).
    /// </summary>
    /// <returns>The current value of the setting along with the name of the set that the value came from (or null if the default value was used).</returns>
    (T, string) GetValueWithSetName();
}
/// <summary>
/// An abstraction that provides access to a setting's description and last time used.
/// </summary>
public interface IAmbientSettingInfo
{
    /// <summary>
    /// Gets the setting key.
    /// </summary>
    string Key { get; }
    /// <summary>
    /// Gets the default value for the setting.
    /// </summary>
    string DefaultValueString { get; }
    /// <summary>
    /// Gets a description of the setting.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// Gets the last time the setting's value was retrieved.
    /// </summary>
    DateTime LastUsed { get; }
    /// <summary>
    /// Gets the default value that is used when the value is not found in the settings set.
    /// </summary>
    object? DefaultValue { get; }
    /// <summary>
    /// Converts the setting value from the specified string to a typed value.
    /// The implementor may cache the value being generated if the settings set is the global settings set.
    /// </summary>
    /// <param name="settingsSet">The settings set asking for the setting value.</param>
    /// <param name="value">The typed value for the setting.</param>
    /// <returns>The typed value for the setting.</returns>
    object Convert(IAmbientSettingsSet settingsSet, string value);
}
/// <summary>
/// A global registry for settings that includes their keys, descriptions, last used time, conversion functions, and default values.
/// </summary>
public class SettingsRegistry
{
    private static readonly SettingsRegistry _DefaultRegistry = new();
    /// <summary>
    /// Gets the default registry.
    /// </summary>
    public static SettingsRegistry DefaultRegistry => _DefaultRegistry;


    private readonly ConcurrentDictionary<string, WeakReference<IAmbientSettingInfo>> _settings = new();
    /// <summary>
    /// Registers an <see cref="IAmbientSettingInfo"/> in the global registry.
    /// </summary>
    /// <param name="setting">The <see cref="IAmbientSettingInfo"/> to register.</param>
    public void Register(IAmbientSettingInfo setting)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(setting);
#else
        if (setting is null) throw new ArgumentNullException(nameof(setting));
#endif
        WeakReference<IAmbientSettingInfo> newReference = new(setting);
        WeakReference<IAmbientSettingInfo> existingReference;
        int loopCount = 0;
        do
        {
            IAmbientSettingInfo? existingSetting;
            existingReference = _settings.GetOrAdd(setting.Key, newReference);
            // did we NOT succeed?
            if (newReference != existingReference)
            {
                // is the old one gone?
                if (!existingReference.TryGetTarget(out existingSetting))
                {
                    // overwrite that one--were we NOT able to overwrite it?
                    if (!_settings.TryUpdate(setting.Key, newReference, existingReference))
                    { // Coverage note: the inside of this loop is nearly impossible to cover in tests
                        // wait a bit and try again
                        System.Threading.Thread.Sleep((int)Math.Pow(2, loopCount + 1));
                        continue;
                    } // else we successfully overwrote it and there is no need to look for a conflict
                    existingSetting = setting;
                }
                // the old one is still there, so we need to check for a conflict
                string existingDescription = existingSetting.Description ?? "<null>";
                string description = setting.Description ?? "<null>";
                string existingDefaultValueString = existingSetting.DefaultValueString;
                string defaultValueString = setting.DefaultValueString;
                if (!string.Equals(existingDescription, description, StringComparison.Ordinal) || !string.Equals(existingDefaultValueString, defaultValueString, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"A setting with the key {setting.Key} has already been registered ({existingDescription} vs {description} or {existingDefaultValueString} vs {defaultValueString})!");
                } // else we didn't succeed, but it doesn't look like there is a conflict anyway, so we're probably fine
            } // else we succeeded
            // raise the registered event
            SettingRegistered?.Invoke(null, setting);
            return;
            // Coverage note: the loop and exception is nearly impossible to cover in tests
        } while (loopCount++ < 10);
        throw new TimeoutException("Timeout attempting to register setting!");
    }
    /// <summary>
    /// Gets an enumeration of all the settings in the system.
    /// </summary>
    public IEnumerable<IAmbientSettingInfo> Settings
    {
        get
        {
            foreach (KeyValuePair<string, WeakReference<IAmbientSettingInfo>> s in _settings)
            {
                IAmbientSettingInfo? setting;
                if (s.Value.TryGetTarget(out setting))
                {
                    yield return setting;
                }
                else
                {
                    _settings.TryRemove(s.Key, out _);
                }
            }
        }
    }
    /// <summary>
    /// Attempts to get the <see cref="IAmbientSettingInfo"/> for the specified settings key.
    /// </summary>
    /// <param name="key">The key for the setting.</param>
    /// <returns>An <see cref="IAmbientSettingInfo"/> for the specified setting.</returns>
    public IAmbientSettingInfo? TryGetSetting(string key)
    {
        WeakReference<IAmbientSettingInfo>? wrSetting;
        IAmbientSettingInfo? setting;
        return _settings.TryGetValue(key, out wrSetting) ? (wrSetting.TryGetTarget(out setting) ? setting : null) : null;
    }
    /// <summary>
    /// An event the is raised when a new setting is registered, allowing settings sets to call the setting's conversion function to get a strongly-typed value.
    /// </summary>
#pragma warning disable CA1003  // this event is performance critical
    public event EventHandler<IAmbientSettingInfo>? SettingRegistered;
#pragma warning restore CA1003
}

/// <summary>
/// An immutable class that contains a typed setting value and the settings set it came from.
/// </summary>
/// <typeparam name="T">The type for the setting.</typeparam>
internal class SettingsSetSettingValue<T>
{
    public T Value { get; private set; }
    public IAmbientSettingsSet SettingsSet { get; private set; }

    public SettingsSetSettingValue(T value, IAmbientSettingsSet settingsSet)
    {
        Value = value;
        SettingsSet = settingsSet;
    }
}
/// <summary>
/// The <see cref="IAmbientSettingsSet"/> that is attached to default values.
/// Note that this settings set does NOT actually contain any settings, it returns null for all keys.
/// Default values are stored with their individual <see cref="IAmbientSettingInfo"/> instance.
/// </summary>
public class DefaultSettingsSet : IAmbientSettingsSet
{
    private static readonly DefaultSettingsSet _Instance = new();
    /// <summary>
    /// Gets the singleton instance of <see cref="DefaultSettingsSet"/>.
    /// </summary>
    public static DefaultSettingsSet Instance => _Instance;

    private DefaultSettingsSet()
    {
    }
    /// <summary>
    /// Gets the name of the set of settings so that a settings consumer can know where a changed setting value came from.
    /// </summary>
    public string SetName => "DefaultSettingsValues";
    /// <summary>
    /// Gets the current raw value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public string? GetRawValue(string key)
    {
        return null;
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public object? GetTypedValue(string key)
    {
        return null;
    }
}

internal class SettingInfo<T> : IAmbientSettingInfo 
{
    protected static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = AmbientService<IAmbientSettingsSet>.Instance;

    private readonly string _key;
    private readonly string _description;
    private readonly string _defaultValueString;
    private readonly T _defaultValue;
    private readonly Func<string, T> _convert;
    private long _lastUsedTicks = DateTime.MinValue.Ticks;       // interlocked
    private SettingsSetSettingValue<T>? _globalSetAndValue;      // interlocked

    public SettingInfo(string key, string description, T defaultValue, Func<string, T>? convert)
    {
        if (convert == null)
        {
            if (typeof(T) != typeof(string)) throw new ArgumentNullException(nameof(convert));
            convert = s => ((T)(object)s)!; // this should be okay because we've just tested the type above and we only get here it T is string and since the input to convert is non-null, the output will be too!
        }
        _key = key;
        _description = description;
        _convert = convert;
        _defaultValue = defaultValue;
        _defaultValueString = defaultValue?.ToString() ?? "";
        SettingsRegistry.DefaultRegistry.Register(this);
    }

    public SettingInfo(string key, string description, Func<string, T>? convert, string defaultValue = "")
    {
        if (convert == null)
        {
            if (typeof(T) != typeof(string)) throw new ArgumentNullException(nameof(convert));
            convert = s => (T)(object)s;
        }
        _key = key;
        _description = description;
        _convert = convert;
        _defaultValueString = defaultValue;
        _defaultValue = convert(defaultValue);
        SettingsRegistry.DefaultRegistry.Register(this);
    }

    /// <summary>
    /// Gets the setting key.
    /// </summary>
    public string Key => _key;
    /// <summary>
    /// Gets a description of the setting.
    /// </summary>
    public string Description => _description;
    /// <summary>
    /// Gets the last time the setting's value was retrieved.
    /// </summary>
    public DateTime LastUsed => new(_lastUsedTicks);
    /// <summary>
    /// Gets the default value for the setting.
    /// </summary>
    public string DefaultValueString => _defaultValueString;
    /// <summary>
    /// Gets the typed default value.
    /// </summary>
    public T DefaultValue => _defaultValue;
    /// <summary>
    /// Gets the untyped default value.
    /// </summary>
    object? IAmbientSettingInfo.DefaultValue => _defaultValue;

    /// <summary>
    /// Converts the specified value for the specified settings set.
    /// </summary>
    /// <param name="settingsSet">The <see cref="IAmbientSettingsSet"/> for the implementation doing the conversion.</param>
    /// <param name="value">The string value for the setting.</param>
    /// <returns>A typed value created from the string value.</returns>
    public object Convert(IAmbientSettingsSet settingsSet, string value)
    {
        T ret;
        try
        {
            ret = _convert(value);
        }
#pragma warning disable CA1031 // this is a "do your best" kind of function, so we really do want to cactch all exceptions here
        catch
#pragma warning restore CA1031 
        {
            ret = _defaultValue;
        }
        // is this settings set the one for this setting or is it the global settings set?
        if (settingsSet == _SettingsSet.Global)
        {
            System.Threading.Interlocked.Exchange(ref _globalSetAndValue, new SettingsSetSettingValue<T>(ret, settingsSet));
        }
        return ret!;
    }
    /// <summary>
    /// Updates the last used time for this setting to the current time.
    /// </summary>
    public void UpdateLastUsed()
    {
        long accessTime = DateTime.UtcNow.Ticks;
        long oldValue = _lastUsedTicks;
        // loop attempting to put it in until we win the race
        while (accessTime > oldValue)
        {
            // Coverate note: this loop is nondeterministic when running with multiple threads, so code coverage may not cover these lines, and it's not possible to force this condition
            // try to put in our value--did we win the race?
            if (oldValue == System.Threading.Interlocked.CompareExchange(ref _lastUsedTicks, accessTime, oldValue))
            {
                // we're done and we were the new max
                break;
            }
            // update our value
            oldValue = _lastUsedTicks;
        }
        // if we didn't break, we're done but the existing value is the max, not this one
    }
    /// <summary>
    /// Gets the current value of the setting (cached from the value given by the settings set).
    /// </summary>
    public T GlobalValue
    {
        get
        {
            UpdateLastUsed();
            return (_globalSetAndValue != null) ? _globalSetAndValue.Value : _defaultValue;
        }
    }
    /// <summary>
    /// Gets the current value of the setting and the settings set it came from (cached from the value given by the settings set).
    /// </summary>
    public SettingsSetSettingValue<T> GlobalSetAndValue
    {
        get
        {
            UpdateLastUsed();
            return _globalSetAndValue ?? new SettingsSetSettingValue<T>(_defaultValue, DefaultSettingsSet.Instance);
        }
    }
}

internal class SettingsSetSetting<T> : IAmbientSetting<T>
{
    protected static readonly AmbientService<IAmbientSettingsSet> _AmbientSettingsSet = AmbientService<IAmbientSettingsSet>.Instance;

    protected readonly AmbientService<IAmbientSettingsSet>? _settingsSet;
    protected readonly SettingInfo<T> _settingInfo;
    private readonly IAmbientSettingsSet? _fixedSettingsSet;

    public SettingsSetSetting(IAmbientSettingsSet? fixedSettingsSet, string key, string description, Func<string, T>? convert, string defaultValueString = "")
    {
        _settingInfo = new SettingInfo<T>(key, description, convert, defaultValueString);
        _fixedSettingsSet = fixedSettingsSet;
    }

    public SettingsSetSetting(IAmbientSettingsSet? fixedSettingsSet, string key, string description, T defaultValue, Func<string, T>? convert)
    {
        _settingInfo = new SettingInfo<T>(key, description, defaultValue, convert);
        _fixedSettingsSet = fixedSettingsSet;
    }

    internal SettingsSetSetting(AmbientService<IAmbientSettingsSet> settings, string key, string description, Func<string, T>? convert, string defaultValueString = "")
    {
        _settingInfo = new SettingInfo<T>(key, description, convert, defaultValueString);
        _settingsSet = settings;
    }
#if NEEDED
    internal SettingsSetSetting(AmbientService<IAmbientSettingsSet> settings, string key, string description, T defaultValue, Func<string, T>? convert)
    {
        _settingInfo = new SettingInfo<T>(key, description, defaultValue, convert);
        _settingsSet = settings;
    }
#endif
    protected IAmbientSettingsSet? GetValueSet()
    {
        _settingInfo.UpdateLastUsed();
        if (_settingsSet != null)
        {
            return _settingsSet.Local;
        }
        else if (_fixedSettingsSet != null)
        {
            return _fixedSettingsSet;
        }
        return null;
    }
    protected T GetValueFromSet(IAmbientSettingsSet set)
    {
        object? value = set.GetTypedValue(_settingInfo.Key);
        return (value == null) ? _settingInfo.DefaultValue : (T)value;
    }
    protected (T, string) GetValueAndSet(IAmbientSettingsSet set)
    {
        object? value = set.GetTypedValue(_settingInfo.Key);
        return (value == null) ? (_settingInfo.DefaultValue, DefaultSettingsSet.Instance.SetName) : ((T)value, set.SetName);
    }
    /// <summary>
    /// Gets the key of the setting, which may be useful in providing an override without having to match the name with a magic value.
    /// </summary>
    public string Key => _settingInfo.Key;
    /// <summary>
    /// Gets the current value of the setting (cached from the value given by the settings set).
    /// </summary>
    public virtual T Value
    {
        get
        {
            IAmbientSettingsSet? set = GetValueSet();
            return (set != null)
                ? GetValueFromSet(set)
                : _settingInfo.GlobalValue;
        }
    }
    /// <summary>
    /// Gets the current value of the setting along with the name of the set that the value came from.
    /// Note that this function may be significantly slower than <see cref="Value"/>.
    /// </summary>
    /// <returns>The current value of the setting along with the name of the set that the value came from.</returns>
    public virtual (T, string) GetValueWithSetName()
    {
        IAmbientSettingsSet? set = GetValueSet();
        return (set != null)
            ? GetValueAndSet(set)
            : (_settingInfo.GlobalValue, DefaultSettingsSet.Instance.SetName);
    }
}

internal class AmbientSetting<T> : SettingsSetSetting<T>
{
    public AmbientSetting(string key, string description, Func<string, T> convert, string defaultValueString = "")
        : base((IAmbientSettingsSet?)null, key, description, convert, defaultValueString)
    {
    }

    public AmbientSetting(string key, string description, T defaultValue, Func<string, T> convert)
        : base(null, key, description, defaultValue, convert)
    {
    }

    internal AmbientSetting(AmbientService<IAmbientSettingsSet> settingsSet, string key, string description, Func<string, T> convert, string defaultValueString = "")
        : base(settingsSet, key, description, convert, defaultValueString)
    {
    }
#if NEEDED // Add this one if needed--it should work fine, but isn't currently used
    internal AmbientSetting(AmbientService<IAmbientSettingsSet> settingsSet, string key, string description, T defaultValue, Func<string, T> convert)
        : base(settingsSet, key, description, defaultValue, convert)
    {
    }
#endif
    private IAmbientSettingsSet? GetAmbientValueSet()
    {
        _settingInfo.UpdateLastUsed();
        AmbientService<IAmbientSettingsSet> settingsSet = _settingsSet ?? _AmbientSettingsSet;
        // is there a local settings set override?
        IAmbientSettingsSet? localSettingsSetOverride = settingsSet.Override;
        if (localSettingsSetOverride != null) return localSettingsSetOverride;
        // is there a local settings set suppression?
        IAmbientSettingsSet? localSettingsSet = settingsSet.Local;
        return (localSettingsSet != null)
            ? GetValueSet() // fall through to the base (global settings set)
            : null;  // use the default value
    }

    /// <summary>
    /// Gets the current value of the setting (cached from the value given by the settings set).
    /// </summary>
    public override T Value
    {
        get
        {
            IAmbientSettingsSet? set = GetAmbientValueSet();
            return (set != null)
                ? GetValueFromSet(set)
                : _settingInfo.GlobalValue;
        }
    }
    /// <summary>
    /// Gets the current value of the setting along with the name of the set that the value came from (or null if the default value was used).
    /// Note that this function may be significantly slower than <see cref="Value"/>.
    /// </summary>
    /// <returns>The current value of the setting along with the name of the set that the value came from (or null if the default value was used).</returns>
    public override (T, string) GetValueWithSetName()
    {
        IAmbientSettingsSet? set = GetAmbientValueSet();
        if (set != null) return GetValueAndSet(set);
        SettingsSetSettingValue<T> setAndValue = _settingInfo.GlobalSetAndValue;
        return (setAndValue.Value, setAndValue.SettingsSet.SetName);
    }
}
