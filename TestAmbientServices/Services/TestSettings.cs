﻿using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientSettingsSet"/>.
    /// </summary>
    [TestClass]
    public class TestSettings
    {
        private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();

        private static object _lock = new object();

        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void AmbientSettingInt()
        {
            // use a local override in case we ran another test that left the global or local settings set set to something else on this thread
            BasicAmbientSettingsSet settings = new BasicAmbientSettingsSet(nameof(AmbientSettingInt));
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> localOverrideTest = new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
            {
                AmbientSetting<int> value;
                value = new AmbientSetting<int>("int-setting", "", s => s == null ? 0 : Int32.Parse(s));
                Assert.AreEqual(0, value.Value);
                value = new AmbientSetting<int>("int-setting-2", "", s => Int32.Parse(s), "1");
                Assert.AreEqual(1, value.Value);
                value = new AmbientSetting<int>("int-setting-3", "", s => Int32.Parse(s), "1");
                Assert.AreEqual(1, value.Value);

                // test changing the setting without an event listener
                settings.ChangeSetting("int-setting-3", "5");
                // test changing the setting to the same value without an event listener
                settings.ChangeSetting("int-setting-3", "5");
                // test changing the setting to null so we fall through to the global settings set
                settings.ChangeSetting("int-setting-3", null);
                int settingValue = value.Value;
                Assert.AreEqual(1, value.Value);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void NoAmbientSetting()
        {
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> localOverrideTest = new ScopedLocalServiceOverride<IAmbientSettingsSet>(null))
            {
                AmbientSetting<int> value;
                value = new AmbientSetting<int>("int-setting", "", s => s == null ? 0 : Int32.Parse(s));
                Assert.AreEqual(0, value.Value);
                value = new AmbientSetting<int>("int-setting-2", "", s => Int32.Parse(s), "1");
                Assert.AreEqual(1, value.Value);
                value = new AmbientSetting<int>("int-setting-4", "", s => Int32.Parse(s), "-1");
                Assert.AreEqual(-1, value.Value);
                SettingsSetSetting<int> settingsSetSetting = new SettingsSetSetting<int>(_SettingsSet, nameof(NoAmbientSetting) + "4", "", s => Int32.Parse(s), "4");
                Assert.AreEqual(4, settingsSetSetting.Value);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetWithExtraSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(SettingsSetWithExtraSettings) + "1", null }, { nameof(SettingsSetWithExtraSettings) + "2", "test" }, };
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetWithExtraSettings), settings);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetGetRawValue()
        {
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(SettingsSetGetRawValue), "1" }, };
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetGetRawValue), settings);
            Assert.AreEqual(null, settingsSet.GetRawValue(nameof(SettingsSetGetRawValue) + "-notfound"));
            Assert.AreEqual("1", settingsSet.GetRawValue(nameof(SettingsSetGetRawValue)));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetWithPreregisteredSettings()
        {
            SettingInfo<string> base1 = new SettingInfo<string>(nameof(SettingsSetWithPreregisteredSettings) + "1", "", null);
            SettingInfo<int> base2 = new SettingInfo<int>(nameof(SettingsSetWithPreregisteredSettings) + "2", "", s => (s == null) ? 2 : Int32.Parse(s));
            SettingInfo<int> base3 = new SettingInfo<int>(nameof(SettingsSetWithPreregisteredSettings) + "3", "", s => Int32.Parse(s), "3");
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(SettingsSetWithPreregisteredSettings) + "1", null }, { nameof(SettingsSetWithPreregisteredSettings) + "2", "2" }, { nameof(SettingsSetWithPreregisteredSettings) + "3", null }, };
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetWithPreregisteredSettings), settings);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetSettingChangeNotification()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetSettingChangeNotification));
            string testSettingKey = nameof(SettingsSetSettingChangeNotification);
            string initialValue = "initialValue";
            string notificationNewValue = "";
            SettingsSetSetting<string> testSetting = new SettingsSetSetting<string>(settingsSet, testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
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
        public void SettingsSetSettingChangeNoNotification()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetSettingChangeNoNotification));
            string testSettingKey = nameof(SettingsSetSettingChangeNoNotification);
            string initialValue = "initialValue";
            SettingsSetSetting<string> testSetting = new SettingsSetSetting<string>(settingsSet, testSettingKey, "", s => s, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            settingsSet.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetSettingNullConvert()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetSettingNullConvert));
            string testSettingKey = nameof(SettingsSetSettingNullConvert);
            string initialValue = "initialValue";
            SettingsSetSetting<string> testSetting = new SettingsSetSetting<string>(settingsSet, testSettingKey, "", null, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            settingsSet.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetSettingNonStringNullConvert()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsSetSettingNonStringNullConvert));
            string testSettingKey = nameof(SettingsSetSettingNonStringNullConvert);
            Assert.ThrowsException<ArgumentNullException>(() => new SettingsSetSetting<int>(settingsSet, testSettingKey, "", null, "1"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingMisc()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>() { { "key", "value" } };
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingMisc), settings);
            Assert.IsTrue(settingsSet.ToString().Contains(nameof(SettingMisc)));
            Assert.AreEqual("SettingMisc", settingsSet.SetName);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsGarbageCollection()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingsGarbageCollection));
            string testSettingKey = nameof(SettingsGarbageCollection);
            WeakReference<SettingsSetSetting<string>> wr = FinalizableSetting(testSettingKey, settingsSet);
            GC.Collect();   // this should collect the temporary Setting created in the function below
            settingsSet.ChangeSetting(testSettingKey, nameof(SettingsGarbageCollection) + "-CauseCollect");  // this should cause the weak proxy to get removed and the instance to be destroyed
            SettingsSetSetting<string> alive;
            Assert.IsFalse(wr.TryGetTarget(out alive));
        }
        private WeakReference<SettingsSetSetting<string>> FinalizableSetting(string testSettingKey, IMutableAmbientSettingsSet settingsSet)
        {
            bool valueChanged = false;
            string value = null;
            SettingsSetSetting<string> temporarySetting = new SettingsSetSetting<string>(settingsSet, testSettingKey, "", s => { valueChanged = true; value = s; return s; }, nameof(SettingsGarbageCollection) + "-InitialValue");
            WeakReference<SettingsSetSetting<string>> wr = new WeakReference<SettingsSetSetting<string>>(temporarySetting);
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-InitialValue", value);
            // change the setting to be sure we are actually hooked into the settings set's notification event
            settingsSet.ChangeSetting(testSettingKey, nameof(SettingsGarbageCollection) + "-ValueChanged");
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", temporarySetting.Value);
            Assert.IsTrue(valueChanged);
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", value);
            return wr;
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalValueChangeNotification()
        {
            string testSettingKey = nameof(SettingGlobalValueChangeNotification);
            string initialValue = "initialValue";
            string notificationNewValue = "";
            AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            Assert.AreEqual(initialValue, notificationNewValue);
            IAmbientSettingsSet settingsReader = _SettingsSet.Global;
            IMutableAmbientSettingsSet settingsMutator = settingsReader as IMutableAmbientSettingsSet;
            if (settingsMutator != null)
            {
                settingsMutator.ChangeSetting(testSettingKey, secondValue);
                Assert.AreEqual(secondValue, testSetting.Value);
                Assert.AreEqual(secondValue, notificationNewValue);
                // now change it to the same value it already has (this should not do anything)
                notificationNewValue = "";
                bool changed = settingsMutator.ChangeSetting(testSettingKey, secondValue);
                Assert.AreEqual(secondValue, testSetting.Value);
                Assert.AreEqual("", notificationNewValue, changed.ToString());
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalSettingsSetChangeNotification()
        {
            AmbientService<IAmbientSettingsSet> pretendGlobalSettings = new AmbientService<IAmbientSettingsSet>();

            string testSettingKey = nameof(SettingGlobalSettingsSetChangeNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideSettingsSetValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            string notificationNewValue = "";
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalSettings, testSettingKey, "", s => { notificationNewValue = s; return s; }, defaultValue);

            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsImplementation = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNotification), null, pretendGlobalSettings);
            pretendGlobalSettings.Global = pretendGlobalSettingsImplementation;

            Assert.AreEqual(overrideValue, testSetting.Value);
            Assert.AreEqual(overrideValue, notificationNewValue);

            IMutableAmbientSettingsSet globalSettingsSet = pretendGlobalSettings.Global as IMutableAmbientSettingsSet;
            if (globalSettingsSet != null)
            {
                string valueChangeValue = "valueChange";
                globalSettingsSet.ChangeSetting(testSettingKey, valueChangeValue);
                Assert.AreEqual(valueChangeValue, testSetting.Value);
                Assert.AreEqual(valueChangeValue, notificationNewValue);
            }

            overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSettingsOverride pretendGlobalSettingsSet2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNotification), null, pretendGlobalSettings);
            pretendGlobalSettings.Global = pretendGlobalSettingsSet2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsSet3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNotification), null, pretendGlobalSettings);
            pretendGlobalSettings.Global = pretendGlobalSettingsSet3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalSettingsSetChangeNoNotification()
        {
            AmbientService<IAmbientSettingsSet> pretendGlobalService = new AmbientService<IAmbientSettingsSet>();

            string testSettingKey = nameof(SettingGlobalSettingsSetChangeNoNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideSettingsSetValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalService, testSettingKey, "", s => s, defaultValue);

            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsImplementation = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNoNotification), null, pretendGlobalService);
            pretendGlobalService.Global = pretendGlobalSettingsImplementation;

            Assert.AreEqual(overrideValue, testSetting.Value);

            IMutableAmbientSettingsSet globalSettingsSet = pretendGlobalService.Global as IMutableAmbientSettingsSet;
            if (globalSettingsSet != null)
            {
                string valueChangeValue = "valueChange";
                globalSettingsSet.ChangeSetting(testSettingKey, valueChangeValue);
                Assert.AreEqual(valueChangeValue, testSetting.Value);
            }

            overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSettingsOverride pretendGlobalSettingsSettingsSet2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNoNotification), null, pretendGlobalService);
            pretendGlobalService.Global = pretendGlobalSettingsSettingsSet2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsSettingsSet3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalSettingsSetChangeNoNotification), null, pretendGlobalService);
            pretendGlobalService.Global = pretendGlobalSettingsSettingsSet3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingLocalValueChangeNotification()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingLocalValueChangeNotification));
            using (new ScopedLocalServiceOverride<IMutableAmbientSettingsSet>(settingsSet))
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settingsSet))
            {
                string testSettingKey = nameof(SettingLocalValueChangeNotification);
                string initialValue = "initialValue";
                string notificationNewValue = "";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
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
        public void SettingLocalChangeSettingsSet()
        {
            IMutableAmbientSettingsSet settingsSet = new BasicAmbientSettingsSet(nameof(SettingLocalChangeSettingsSet));
            using (new ScopedLocalServiceOverride<IMutableAmbientSettingsSet>(settingsSet))
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settingsSet))
            {
                string testSettingKey = nameof(SettingLocalChangeSettingsSet);
                string initialValue = "initialValue";
                string notificationNewValue = "";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
                Assert.AreEqual(initialValue, testSetting.Value);
                Assert.AreEqual(initialValue, notificationNewValue);

                // now switch local settings set and try again
                string secondValue = "change1";
                IMutableAmbientSettingsSet settingsSet2 = new BasicAmbientSettingsSet(nameof(SettingLocalChangeSettingsSet) + "_2");
                using (new ScopedLocalServiceOverride<IMutableAmbientSettingsSet>(settingsSet2))
                using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settingsSet2))
                {
                    settingsSet2.ChangeSetting(testSettingKey, secondValue);
                    Assert.AreEqual(secondValue, testSetting.Value);
                    Assert.AreEqual(secondValue, notificationNewValue);
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsInfo()
        {
            string settingName = nameof(SettingsInfo);
            DateTime start = AmbientClock.UtcNow;
            AmbientSetting<string> testSetting = new AmbientSetting<string>(settingName, "", s => s, "default value");
            foreach (IAmbientSettingInfo info in SettingsRegistry.DefaultRegistry.Settings)
            {
                if (String.Equals(settingName, info.Key))
                {
                    Assert.IsTrue(info.LastUsed == DateTime.MinValue);
                    Assert.IsTrue(String.IsNullOrEmpty(info.Description));
                    Assert.AreEqual("default value", info.DefaultValueString);
                    Assert.AreEqual("default value", (string)info.DefaultValue);
                }
            }
            Assert.AreEqual("default value", testSetting.Value);
            foreach (IAmbientSettingInfo info in SettingsRegistry.DefaultRegistry.Settings)
            {
                if (String.Equals(settingName, info.Key))
                {
                    Assert.IsTrue(info.LastUsed >= start);
                    Assert.IsTrue(String.IsNullOrEmpty(info.Description));
                    Assert.AreEqual("default value", info.DefaultValueString);
                    Assert.AreEqual("default value", (string)info.DefaultValue);
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void AmbientSettingsInfo()
        {
            AmbientLogFilter filter = AmbientLogFilter.Default;
            filter.IsBlocked(AmbientLogLevel.Critical, "TestType", "TestCategory");
            int ambientLogFilterSettings = 0;
            string settingsDescriptions = "";
            foreach (IAmbientSettingInfo info in AmbientSettings.AmbientSettingsInfo)
            {
                if (info.Key.StartsWith(filter.Name + "-" + nameof(AmbientLogFilter))) ++ambientLogFilterSettings;
                settingsDescriptions += $"{info.Key}:{info.Description} @{info.LastUsed.ToShortDateString()} {info.LastUsed.ToShortTimeString()}" + Environment.NewLine;
            }
            Assert.AreEqual(5, ambientLogFilterSettings);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsRegistryTest()
        {
            SettingsRegistry registry = new SettingsRegistry();
            registry.SettingRegistered += Registry_SettingRegistered;
            TestSettingsSetSetting setting1 = new TestSettingsSetSetting();
            registry.Register(setting1);
            registry.SettingRegistered -= Registry_SettingRegistered;
            TempRegister(registry);
            TempRegister(registry, "key2", "description");
            GC.Collect(2, GCCollectionMode.Forced, false, false);
            TempRegister(registry);
            TempRegister(registry, "key2", "description");
            Assert.ThrowsException<ArgumentException>(() => TempRegister(registry, description: "different description"));

            Assert.ThrowsException<ArgumentNullException>(() => registry.Register(null));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void AmbientSettingsWithSetName()
        {
            string settingName = nameof(AmbientSettingsWithSetName) + "-int-setting";
            AmbientSetting<int> setting3 = new AmbientSetting<int>(settingName, "", s => Int32.Parse(s), "1");
            Assert.AreEqual(1, setting3.Value);
            Assert.IsNull(setting3.GetValueWithSetName().Item2);
            IMutableAmbientSettingsSet settingsSet = _SettingsSet.Global as IMutableAmbientSettingsSet;
            if (settingsSet != null)
            {
                settingsSet.ChangeSetting(settingName, "2");
                Assert.AreEqual(2, setting3.Value);
                Assert.AreEqual(BasicAmbientSettingsSet.DefaultSetName, setting3.GetValueWithSetName().Item2);
            }
            // use a local override in case we ran another test that left the global or local settings set set to something else on this thread
            BasicAmbientSettingsSet settings = new BasicAmbientSettingsSet(nameof(AmbientSettingsWithSetName));
            using (ScopedLocalServiceOverride<IAmbientSettingsSet> localOverrideTest = new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
            {
                AmbientSetting<int> setting;
                setting = new AmbientSetting<int>(nameof(AmbientSettingsWithSetName) + "-int-setting-1", "", s => s == null ? 0 : Int32.Parse(s));
                Assert.AreEqual(0, setting.Value);
                Assert.IsNull(setting.GetValueWithSetName().Item2);
                setting = new AmbientSetting<int>(nameof(AmbientSettingsWithSetName) + "-int-setting-2", "", s => Int32.Parse(s), "1");
                settings.ChangeSetting(nameof(AmbientSettingsWithSetName) + "-int-setting-2", "1");
                Assert.AreEqual(1, setting.Value);
                Assert.AreEqual(nameof(AmbientSettingsWithSetName), setting.GetValueWithSetName().Item2);
                setting = new AmbientSetting<int>(settingName, "", s => Int32.Parse(s), "1");
                settings.ChangeSetting(settingName, "3");
                Assert.AreEqual(3, setting3.Value);
                Assert.AreEqual(nameof(AmbientSettingsWithSetName), setting3.GetValueWithSetName().Item2);

                // test changing the setting to null so we fall through to the global settings set
                settings.ChangeSetting(settingName, null);
                int settingValue = setting.Value;
                Assert.AreEqual(1, setting.Value);
                Assert.IsNull(setting3.GetValueWithSetName().Item2);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsSet"/>.
        /// </summary>
        [TestMethod]
        public void SettingsSetSettingsWithSetName()
        {
            string settingName = nameof(SettingsSetSettingsWithSetName) + "-int-setting";

            IAmbientSetting<int> setting = AmbientSettings.GetSettingsSetSetting(null, settingName, "", s => Int32.Parse(s), "1");
            Assert.AreEqual(1, setting.Value);
            Assert.IsNull(setting.GetValueWithSetName().Item2);

            BasicAmbientSettingsSet settings = new BasicAmbientSettingsSet(nameof(SettingsSetSettingsWithSetName));
            setting = AmbientSettings.GetSettingsSetSetting(settings, settingName, "", s => Int32.Parse(s), "1");
            Assert.AreEqual(1, setting.Value);
            Assert.IsNull(setting.GetValueWithSetName().Item2);

            settings.ChangeSetting(settingName, "2");
            Assert.AreEqual(2, setting.Value);
            Assert.AreEqual(nameof(SettingsSetSettingsWithSetName), setting.GetValueWithSetName().Item2);
        }
        private void Registry_SettingRegistered(object sender, IAmbientSettingInfo e)
        {
        }
        private void TempRegister(SettingsRegistry registry, string key = null, string description = null)
        {
            TestSettingsSetSetting setting2 = new TestSettingsSetSetting(key ?? "key", "defaultValue", description);
            registry.Register(setting2);
        }

        class TestSettingsSetSetting : IAmbientSettingInfo
        {
            public TestSettingsSetSetting(string key = "", string defaultValueString = null, string description = null)
            {
                this.Key = key;
                this.DefaultValueString = defaultValueString;
                this.Description = description;
            }

            public object DefaultValue { get { return DefaultValueString; } }

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
}
