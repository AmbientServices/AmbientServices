using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for <see cref="IAmbientSettingsSet"/>.
/// </summary>
[TestClass]
public class TestEnvironmentSettings
{
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();

    private static readonly object _lock = new();

    /// <summary>
    /// Performs tests on <see cref="AmbientEnvironmentSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingInt()
    {
        // use a local override so we don't interfere with other tests
        AmbientEnvironmentSettingsSet settingsSet = new();
        using ScopedLocalServiceOverride<IAmbientSettingsSet> localOverrideTest = new(settingsSet);
        AmbientSetting<int> setting1 = new(nameof(EnvironmentSettingInt) + "-int-setting", "", s => string.IsNullOrEmpty(s) ? 0 : Int32.Parse(s));
        Assert.AreEqual(0, setting1.Value);
        AmbientSetting<int> setting2 = new(nameof(EnvironmentSettingInt) + "-int-setting-2", "", s => Int32.Parse(s), "1");
        Assert.AreEqual(1, setting2.Value);
        AmbientSetting<int> setting3 = new(nameof(EnvironmentSettingInt) + "-int-setting-3", "", s => Int32.Parse(s), "1");
        Assert.AreEqual(1, setting3.Value);

        // test changing the setting without an event listener
        settingsSet.ChangeSetting(setting3.Key, "5");
        // test changing the setting to the same value without an event listener
        settingsSet.ChangeSetting(setting3.Key, "5");
        // test changing the setting to null so we fall through to the global settings set
        settingsSet.ChangeSetting(setting3.Key, null);
        int settingValue = setting3.Value;
        Assert.AreEqual(1, setting3.Value);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetGetRawValue()
    {
        AmbientEnvironmentSettingsSet settingsSet = new();
        string testString = nameof(EnvironmentSettingsSetGetRawValue) + Guid.NewGuid().ToString();
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsSetGetRawValue) + "_testValue", testString);
        settingsSet.Refresh();
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetGetRawValue) + "-notfound"));
        Assert.AreEqual(testString, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetGetRawValue) + "_testValue"));
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsRefresh()
    {
        string testString = nameof(EnvironmentSettingsRefresh) + Guid.NewGuid().ToString();
        AmbientEnvironmentSettingsSet settingsSet = new();
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsRefresh) + "_testValue", testString);
        settingsSet.Refresh();
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "-notfound"));
        Assert.AreEqual(testString, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "_testValue"));
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsRefresh) + "_testValue", null);
        Assert.IsNull(Environment.GetEnvironmentVariable(nameof(EnvironmentSettingsRefresh) + "_testValue"));
        settingsSet.Refresh();
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "-notfound"));
// The following line seems to fail frequently with: Assert.AreEqual failed. Expected:<(null)>. Actual:<EnvironmentSettingsRefresh687fdfa2-0d32-46ab-be96-f06af6866048>.
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "_testValue"));
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsRefresh) + "_testValue", testString);
        settingsSet.Refresh();
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "-notfound"));
        Assert.AreEqual(testString, settingsSet.GetRawValue(nameof(EnvironmentSettingsRefresh) + "_testValue"));
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetNullToNull()
    {
        string testString = nameof(EnvironmentSettingsSetNullToNull) + Guid.NewGuid().ToString();
        AmbientEnvironmentSettingsSet settingsSet = new();
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsSetNullToNull) + "_testValue", testString);
        settingsSet.Refresh();
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetNullToNull) + "-notfound"));
        Assert.AreEqual(testString, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetNullToNull) + "_testValue"));
        Environment.SetEnvironmentVariable(nameof(EnvironmentSettingsSetNullToNull) + "_testValue", null);
        settingsSet.Refresh();
        settingsSet.ChangeSetting(nameof(EnvironmentSettingsSetNullToNull) + "_testValue", null);
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetNullToNull) + "-notfound"));
        Assert.AreEqual(null, settingsSet.GetRawValue(nameof(EnvironmentSettingsSetNullToNull) + "_testValue"));
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetSettingChangeNotification()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        string testSettingKey = nameof(EnvironmentSettingsSetSettingChangeNotification);
        string initialValue = "initialValue";
        string notificationNewValue = "";
        SettingsSetSetting<string> testSetting = new(settingsSet, testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
        Assert.AreEqual(initialValue, testSetting.Value);
        string secondValue = "change1";
        Assert.AreEqual(initialValue, notificationNewValue);
        settingsSet.ChangeSetting(testSettingKey, secondValue);
        Assert.AreEqual(secondValue, testSetting.Value);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetSettingChangeNoNotification()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        string testSettingKey = nameof(EnvironmentSettingsSetSettingChangeNoNotification);
        string initialValue = "initialValue";
        SettingsSetSetting<string> testSetting = new(settingsSet, testSettingKey, "", s => s, initialValue);
        Assert.AreEqual(initialValue, testSetting.Value);
        string secondValue = "change1";
        settingsSet.ChangeSetting(testSettingKey, secondValue);
        Assert.AreEqual(secondValue, testSetting.Value);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetSettingNullConvert()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        string testSettingKey = nameof(EnvironmentSettingsSetSettingNullConvert);
        string initialValue = "initialValue";
        string secondValue = "change1";
        SettingsSetSetting<string> testSetting = new(settingsSet, testSettingKey, "", null, initialValue);
        Assert.AreEqual(initialValue, testSetting.Value);
        settingsSet.ChangeSetting(testSettingKey, secondValue);
        Assert.AreEqual(secondValue, testSetting.Value);
        settingsSet.ChangeSetting(testSettingKey, initialValue);
        Assert.AreEqual(initialValue, settingsSet.GetTypedValue(testSettingKey));// somehow this fails sometimes
        Assert.IsTrue(true);
        Assert.IsTrue(true);
        Assert.IsTrue(true);
        Assert.AreEqual(initialValue, testSetting.Value);       // somehow this fails even when the above doesn't
        testSetting = new SettingsSetSetting<string>(settingsSet, testSettingKey, "", initialValue, null);
        Assert.AreEqual(initialValue, settingsSet.GetTypedValue(testSettingKey));// somehow this fails sometimes--not sure how this can fail, especially when the same check above succeeded
        Assert.AreEqual(initialValue, testSetting.Value);       // somehow this fails sometimes
        settingsSet.ChangeSetting(testSettingKey, secondValue);
        Assert.AreEqual(secondValue, testSetting.Value);
        settingsSet.ChangeSetting(testSettingKey, initialValue);
        Assert.AreEqual(initialValue, testSetting.Value);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetSettingNonStringNullConvert()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        string testSettingKey = nameof(EnvironmentSettingsSetSettingNonStringNullConvert);
        Assert.ThrowsException<ArgumentNullException>(() => new SettingsSetSetting<int>(settingsSet, testSettingKey, "", null, "1"));
        Assert.ThrowsException<ArgumentNullException>(() => new SettingsSetSetting<int>(settingsSet, testSettingKey, "", 1, null));
        Assert.ThrowsException<ArgumentNullException>(() => new SettingsSetSetting<int?>(settingsSet, testSettingKey, "", (int?)null, null));
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingMisc()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        Assert.IsTrue(settingsSet!.ToString()!.Contains("Environment"));
        Assert.AreEqual("Environment", settingsSet.SetName);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsGarbageCollection()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        string testSettingKey = Guid.NewGuid().ToString();
        WeakReference<SettingsSetSetting<string>> wr = FinalizableSetting(testSettingKey, settingsSet);
        GC.Collect();   // this should collect the temporary Setting created in the function below
        settingsSet.ChangeSetting(testSettingKey, $"{testSettingKey}-CauseCollect");  // this should cause the weak proxy to get removed and the instance to be destroyed
        Assert.IsFalse(wr.TryGetTarget(out SettingsSetSetting<string> alive));
    }
    private WeakReference<SettingsSetSetting<string>> FinalizableSetting(string testSettingKey, IAmbientSettingsSet settingsSet)
    {
        bool valueChanged = false;
        string value = null;
        SettingsSetSetting<string> temporarySetting = new(settingsSet, testSettingKey, "", s => { valueChanged = true; value = s; return s; }, $"{testSettingKey}-InitialValue");
        WeakReference<SettingsSetSetting<string>> wr = new(temporarySetting);
        Assert.AreEqual($"{testSettingKey}-InitialValue", value);
        // change the setting to be sure we are actually hooked into the settings set's notification event
        settingsSet.ChangeSetting(testSettingKey, $"{testSettingKey}-ValueChanged");
        Assert.AreEqual($"{testSettingKey}-ValueChanged", temporarySetting.Value);      // this one occasionally fails saying the value is still the initial value
        Assert.IsTrue(valueChanged);
        Assert.AreEqual($"{testSettingKey}-ValueChanged", value);
        return wr;
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingLocalValueChangeNotification()
    {
        IAmbientSettingsSet settingsSet = new AmbientEnvironmentSettingsSet();
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settingsSet))
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settingsSet))
        {
            string testSettingKey = nameof(EnvironmentSettingLocalValueChangeNotification);
            string initialValue = "initialValue";
            string notificationNewValue = "";
            AmbientSetting<string> testSetting = new(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            Assert.AreEqual(initialValue, notificationNewValue);
            settingsSet.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
            Assert.AreEqual(secondValue, notificationNewValue);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestMethod]
    public void EnvironmentSettingsSetSettingsWithSetName()
    {
        string settingName = nameof(EnvironmentSettingsSetSettingsWithSetName) + "-int-setting";

        IAmbientSetting<int> setting = AmbientSettings.GetSettingsSetSetting(null, settingName, "", s => Int32.Parse(s), "1");
        Assert.AreEqual(1, setting.Value);
        Assert.IsNotNull(setting.GetValueWithSetName().Item2);

        AmbientEnvironmentSettingsSet settings = AmbientEnvironmentSettingsSet.Instance;
        setting = AmbientSettings.GetSettingsSetSetting(settings, settingName, "", s => Int32.Parse(s), "1");
        Assert.AreEqual(1, setting.Value);
        Assert.IsNotNull(setting.GetValueWithSetName().Item2);

        settings.ChangeSetting(settingName, "2");
        Assert.AreEqual(2, setting.Value);
        Assert.AreEqual("Environment", setting.GetValueWithSetName().Item2);
    }
    private void Registry_SettingRegistered(object sender, IAmbientSettingInfo e)
    {
    }
    private void TempRegister(SettingsRegistry registry, string key = null, string description = null)
    {
        TestSettingsSetSetting setting2 = new(key ?? "key", "defaultValue", description);
        registry.Register(setting2);
    }

    class TestSettingsSetSetting : IAmbientSettingInfo
    {
        public TestSettingsSetSetting(string key = "", string defaultValueString = null, string description = null)
        {
            Key = key;
            DefaultValueString = defaultValueString;
            Description = description;
        }

        public object DefaultValue => DefaultValueString;

        public string Key { get; private set; }

        public string DefaultValueString { get; private set; }

        public string Description { get; private set; }

        public DateTime LastUsed => DateTime.MinValue;

        public object Convert(IAmbientSettingsSet settingsSet, string value)
        {
            return DefaultValueString;
        }
    }
}
