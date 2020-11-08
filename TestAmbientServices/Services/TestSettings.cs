using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientSettingsProvider"/>.
    /// </summary>
    [TestClass]
    public class TestSettings
    {
        private static readonly ServiceReference<IAmbientSettingsProvider> _SettingsProvider = Service.GetReference<IAmbientSettingsProvider>();

        private static object _lock = new object();

        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void AmbientSettingInt()
        {
            // use a local override in case we ran another test that left the global or local settings provider set to something else on this thread
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider(nameof(AmbientSettingInt));
            using (LocalProviderScopedOverride<IAmbientSettingsProvider> LocalOverrideTest = new LocalProviderScopedOverride<IAmbientSettingsProvider>(settings))
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
                // test changing the setting to null so we fall through to the global provider
                settings.ChangeSetting("int-setting-3", null);
                int settingValue = value.Value;
                Assert.AreEqual(1, value.Value);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void NoAmbientSetting()
        {
            using (LocalProviderScopedOverride<IAmbientSettingsProvider> LocalOverrideTest = new LocalProviderScopedOverride<IAmbientSettingsProvider>(null))
            {
                AmbientSetting<int> value;
                value = new AmbientSetting<int>("int-setting", "", s => s == null ? 0 : Int32.Parse(s));
                Assert.AreEqual(0, value.Value);
                value = new AmbientSetting<int>("int-setting-2", "", s => Int32.Parse(s), "1");
                Assert.AreEqual(1, value.Value);
                value = new AmbientSetting<int>("int-setting-4", "", s => Int32.Parse(s), "-1");
                Assert.AreEqual(-1, value.Value);
                ProviderSetting<int> providerValue = new ProviderSetting<int>(_SettingsProvider, nameof(NoAmbientSetting) + "4", "", s => Int32.Parse(s), "4");
                Assert.AreEqual(4, providerValue.Value);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderWithExtraSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(ProviderWithExtraSettings) + "1", null }, { nameof(ProviderWithExtraSettings) + "2", "test" }, };
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderWithExtraSettings), settings);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderGetRawValue()
        {
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(ProviderGetRawValue), "1" }, };
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderGetRawValue), settings);
            Assert.AreEqual(null, settingsProvider.GetRawValue(nameof(ProviderGetRawValue) + "-notfound"));
            Assert.AreEqual("1", settingsProvider.GetRawValue(nameof(ProviderGetRawValue)));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderWithPreregisteredSettings()
        {
            SettingInfo<string> base1 = new SettingInfo<string>(nameof(ProviderWithPreregisteredSettings) + "1", "", null);
            SettingInfo<int> base2 = new SettingInfo<int>(nameof(ProviderWithPreregisteredSettings) + "2", "", s => (s == null) ? 2 : Int32.Parse(s));
            SettingInfo<int> base3 = new SettingInfo<int>(nameof(ProviderWithPreregisteredSettings) + "3", "", s => Int32.Parse(s), "3");
            Dictionary<string, string> settings = new Dictionary<string, string> { { nameof(ProviderWithPreregisteredSettings) + "1", null }, { nameof(ProviderWithPreregisteredSettings) + "2", "2" }, { nameof(ProviderWithPreregisteredSettings) + "3", null }, };
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderWithPreregisteredSettings), settings);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderSettingChangeNotification()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderSettingChangeNotification));
            string testSettingKey = nameof(ProviderSettingChangeNotification);
            string initialValue = "initialValue";
            string notificationNewValue = "";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            Assert.AreEqual(initialValue, notificationNewValue);
            settingsProvider.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderSettingChangeNoNotification()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderSettingChangeNoNotification));
            string testSettingKey = nameof(ProviderSettingChangeNoNotification);
            string initialValue = "initialValue";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, "", s => s, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            settingsProvider.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderSettingNullConvert()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderSettingNullConvert));
            string testSettingKey = nameof(ProviderSettingNullConvert);
            string initialValue = "initialValue";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, "", null, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string secondValue = "change1";
            settingsProvider.ChangeSetting(testSettingKey, secondValue);
            Assert.AreEqual(secondValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderSettingNonStringNullConvert()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderSettingNonStringNullConvert));
            string testSettingKey = nameof(ProviderSettingNonStringNullConvert);
            Assert.ThrowsException<ArgumentNullException>(() => new ProviderSetting<int>(settingsProvider, testSettingKey, "", null, "1"));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingMisc()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>() { { "key", "value" } };
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(SettingMisc), settings);
            Assert.IsTrue(settingsProvider.ToString().Contains(nameof(SettingMisc)));
            Assert.AreEqual("SettingMisc", settingsProvider.ProviderName);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingsGarbageCollection()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(SettingsGarbageCollection));
            string testSettingKey = nameof(SettingsGarbageCollection);
            WeakReference<ProviderSetting<string>> wr = FinalizableSetting(testSettingKey, settingsProvider);
            GC.Collect();   // this should collect the temporary Setting created in the function below
            settingsProvider.ChangeSetting(testSettingKey, nameof(SettingsGarbageCollection) + "-CauseCollect");  // this should cause the weak proxy to get removed and the instance to be destroyed
            ProviderSetting<string> alive;
            Assert.IsFalse(wr.TryGetTarget(out alive));
        }
        private WeakReference<ProviderSetting<string>> FinalizableSetting(string testSettingKey, IMutableAmbientSettingsProvider settingsProvider)
        {
            bool valueChanged = false;
            string value = null;
            ProviderSetting<string> temporarySetting = new ProviderSetting<string>(settingsProvider, testSettingKey, "", s => { valueChanged = true; value = s; return s; }, nameof(SettingsGarbageCollection) + "-InitialValue");
            WeakReference<ProviderSetting<string>> wr = new WeakReference<ProviderSetting<string>>(temporarySetting);
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-InitialValue", value);
            // change the setting to be sure we are actually hooked into the provider's notification event
            settingsProvider.ChangeSetting(testSettingKey, nameof(SettingsGarbageCollection) + "-ValueChanged");
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", temporarySetting.Value);
            Assert.IsTrue(valueChanged);
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", value);
            return wr;
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
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
            IAmbientSettingsProvider settingsReader = _SettingsProvider.GlobalProvider;
            IMutableAmbientSettingsProvider settingsMutator = settingsReader as IMutableAmbientSettingsProvider;
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
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalProviderChangeNotification()
        {
            ServiceReference<IAmbientSettingsProvider> pretendGlobalAccessor = new ServiceReference<IAmbientSettingsProvider>();

            string testSettingKey = nameof(SettingGlobalProviderChangeNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideProviderValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            string notificationNewValue = "";
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalAccessor, testSettingKey, "", s => { notificationNewValue = s; return s; }, defaultValue);

            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsProvider = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider;

            Assert.AreEqual(overrideValue, testSetting.Value);
            Assert.AreEqual(overrideValue, notificationNewValue);

            IMutableAmbientSettingsProvider globalProvider = pretendGlobalAccessor.GlobalProvider as IMutableAmbientSettingsProvider;
            if (globalProvider != null)
            {
                string valueChangeValue = "valueChange";
                globalProvider.ChangeSetting(testSettingKey, valueChangeValue);
                Assert.AreEqual(valueChangeValue, testSetting.Value);
                Assert.AreEqual(valueChangeValue, notificationNewValue);
            }

            overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSettingsOverride pretendGlobalSettingsProvider2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsProvider3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalProviderChangeNoNotification()
        {
            ServiceReference<IAmbientSettingsProvider> pretendGlobalAccessor = new ServiceReference<IAmbientSettingsProvider>();

            string testSettingKey = nameof(SettingGlobalProviderChangeNoNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideProviderValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalAccessor, testSettingKey, "", s => s, defaultValue);

            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsProvider = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNoNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider;

            Assert.AreEqual(overrideValue, testSetting.Value);

            IMutableAmbientSettingsProvider globalProvider = pretendGlobalAccessor.GlobalProvider as IMutableAmbientSettingsProvider;
            if (globalProvider != null)
            {
                string valueChangeValue = "valueChange";
                globalProvider.ChangeSetting(testSettingKey, valueChangeValue);
                Assert.AreEqual(valueChangeValue, testSetting.Value);
            }

            overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSettingsOverride pretendGlobalSettingsProvider2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNoNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsProvider3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNoNotification), null, pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingLocalValueChangeNotification()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(SettingLocalValueChangeNotification));
            using (new LocalProviderScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider))
            using (new LocalProviderScopedOverride<IAmbientSettingsProvider>(settingsProvider))
            {
                string testSettingKey = nameof(SettingLocalValueChangeNotification);
                string initialValue = "initialValue";
                string notificationNewValue = "";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
                Assert.AreEqual(initialValue, testSetting.Value);
                string secondValue = "change1";
                Assert.AreEqual(initialValue, notificationNewValue);
                settingsProvider.ChangeSetting(testSettingKey, secondValue);
                Assert.AreEqual(secondValue, testSetting.Value);
                Assert.AreEqual(secondValue, notificationNewValue);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingLocalChangeProvider()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(SettingLocalChangeProvider));
            using (new LocalProviderScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider))
            using (new LocalProviderScopedOverride<IAmbientSettingsProvider>(settingsProvider))
            {
                string testSettingKey = nameof(SettingLocalChangeProvider);
                string initialValue = "initialValue";
                string notificationNewValue = "";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, "", s => { notificationNewValue = s; return s; }, initialValue);
                Assert.AreEqual(initialValue, testSetting.Value);
                Assert.AreEqual(initialValue, notificationNewValue);

                // now switch local providers and try again
                string secondValue = "change1";
                IMutableAmbientSettingsProvider settingsProvider2 = new BasicAmbientSettingsProvider(nameof(SettingLocalChangeProvider) + "_2");
                using (new LocalProviderScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider2))
                using (new LocalProviderScopedOverride<IAmbientSettingsProvider>(settingsProvider2))
                {
                    settingsProvider2.ChangeSetting(testSettingKey, secondValue);
                    Assert.AreEqual(secondValue, testSetting.Value);
                    Assert.AreEqual(secondValue, notificationNewValue);
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
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
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
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
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingsRegistryTest()
        {
            SettingsRegistry registry = new SettingsRegistry();
            registry.SettingRegistered += Registry_SettingRegistered;
            TestProviderSetting setting1 = new TestProviderSetting();
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

        private void Registry_SettingRegistered(object sender, IAmbientSettingInfo e)
        {
        }
        private void TempRegister(SettingsRegistry registry, string key = null, string description = null)
        {
            TestProviderSetting setting2 = new TestProviderSetting(key ?? "key", "defaultValue", description);
            registry.Register(setting2);
        }

        class TestProviderSetting : IAmbientSettingInfo
        {
            public TestProviderSetting(string key = "", string defaultValueString = null, string description = null)
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

            public object Convert(IAmbientSettingsProvider provider, string value)
            {
                return DefaultValueString;
            }
        }
    }
}
