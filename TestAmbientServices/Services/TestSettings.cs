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
        private static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsProvider = Service.GetAccessor<IAmbientSettingsProvider>();

        private static object _lock = new object();

        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void AmbientSettingInt()
        {
            // use a local override in case we ran another test that left the global or local settings provider set to something else on this thread
            BasicAmbientSettingsProvider settings = new BasicAmbientSettingsProvider(nameof(AmbientSettingInt));
            using (LocalServiceScopedOverride<IAmbientSettingsProvider> LocalOverrideTest = new LocalServiceScopedOverride<IAmbientSettingsProvider>(settings))
            {
                AmbientSetting<int> value;
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s));
                Assert.AreEqual(0, value.Value);
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s), 1);
                Assert.AreEqual(1, value.Value);
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s), 1);
                Assert.AreEqual(1, value.Value);

                // test changing the setting without an event listener
                settings.ChangeSetting("int-setting", "5");
                // test changing the setting to the same value without an event listener
                settings.ChangeSetting("int-setting", "5");
                // test changing the setting to null so we fall through to the global provider
                settings.ChangeSetting("int-setting", null);
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
            // use a local override in case we ran another test that left the global or local settings provider set to something else on this thread
            using (LocalServiceScopedOverride<IAmbientSettingsProvider> LocalOverrideTest = new LocalServiceScopedOverride<IAmbientSettingsProvider>(null))
            {
                AmbientSetting<int> value;
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s));
                Assert.AreEqual(0, value.Value);
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s), 1);
                Assert.AreEqual(1, value.Value);
                value = new AmbientSetting<int>("int-setting", s => Int32.Parse(s), -1);
                Assert.AreEqual(-1, value.Value);
            }
        }

        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void ProviderSettingChangeNotification()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(ProviderSettingChangeNotification));
            string testSettingKey = "testNotification";
            string initialValue = "initialValue";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, s => s, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string notificationNewValue = "";
            testSetting.ValueChanged += (s, e) => notificationNewValue = testSetting.Value;
            string secondValue = "change1";
            Assert.AreEqual("", notificationNewValue);
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
            string testSettingKey = "testNotification";
            string initialValue = "initialValue";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, s => s, initialValue);
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
            string testSettingKey = "testNotification";
            string initialValue = "initialValue";
            ProviderSetting<string> testSetting = new ProviderSetting<string>(settingsProvider, testSettingKey, null, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string notificationNewValue = "";
            testSetting.ValueChanged += (s, e) => notificationNewValue = testSetting.Value;
            string secondValue = "change1";
            Assert.AreEqual("", notificationNewValue);
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
            string testSettingKey = "testNotification";
            Assert.ThrowsException<ArgumentNullException>(() => new ProviderSetting<int>(settingsProvider, testSettingKey, null, 1));
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
            ProviderSetting<string> temporarySetting = new ProviderSetting<string>(settingsProvider, testSettingKey, s => s, nameof(SettingsGarbageCollection) + "-InitialValue");
            WeakReference<ProviderSetting<string>> wr = new WeakReference<ProviderSetting<string>>(temporarySetting);
            bool valueChanged = false;
            string value = temporarySetting.Value;
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-InitialValue", value);
            EventHandler<EventArgs> valueChangedLambda = (s, e) =>
            {
                valueChanged = true;
                Assert.AreEqual(settingsProvider, temporarySetting.Provider);
                value = temporarySetting.Value;
            };
            temporarySetting.ValueChanged += valueChangedLambda;
            // change the setting to be sure we are actually hooked into the provider's notification event
            settingsProvider.ChangeSetting(testSettingKey, nameof(SettingsGarbageCollection) + "-ValueChanged");
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", temporarySetting.Value);
            Assert.IsTrue(valueChanged);
            Assert.AreEqual(nameof(SettingsGarbageCollection) + "-ValueChanged", value);
            temporarySetting.ValueChanged -= valueChangedLambda;
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
            AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, s => s, initialValue);
            Assert.AreEqual(initialValue, testSetting.Value);
            string notificationNewValue = "";
            testSetting.ValueChanged += (s, e) => { string newValue = testSetting.Value;  if (!string.IsNullOrEmpty(newValue)) { notificationNewValue = newValue; } };
            string secondValue = "change1";
            Assert.AreEqual("", notificationNewValue);
            IAmbientSettingsProvider settingsReader = _SettingsProvider.GlobalProvider;
            IMutableAmbientSettingsProvider settingsMutator = settingsReader as IMutableAmbientSettingsProvider;
            if (settingsMutator != null)
            {
                settingsMutator.ChangeSetting(testSettingKey, secondValue);
                Assert.AreEqual(secondValue, testSetting.Value);
                Assert.AreEqual(secondValue, notificationNewValue);
                // now change it to the same value it already has (the event should *not* be called)
                notificationNewValue = "";
                settingsMutator.ChangeSetting(testSettingKey, secondValue);
                Assert.AreEqual(secondValue, testSetting.Value);
                Assert.AreEqual("", notificationNewValue);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalProviderChangeNotification()
        {
            ServiceAccessor<IAmbientSettingsProvider> pretendGlobalAccessor = new ServiceAccessor<IAmbientSettingsProvider>();

            string testSettingKey = nameof(SettingGlobalProviderChangeNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideProviderValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalAccessor, testSettingKey, s => s, defaultValue);

            string notificationNewValue = "";
            testSetting.ValueChanged += (s, e) => notificationNewValue = testSetting.Value;
            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsProvider = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
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
            AmbientSettingsOverride pretendGlobalSettingsProvider2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsProvider3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingGlobalProviderChangeNoNotification()
        {
            ServiceAccessor<IAmbientSettingsProvider> pretendGlobalAccessor = new ServiceAccessor<IAmbientSettingsProvider>();

            string testSettingKey = nameof(SettingGlobalProviderChangeNotification);
            string defaultValue = "defaultValue";
            string overrideValue = "overrideProviderValue";
            Dictionary<string, string> overrides = new Dictionary<string, string>() { { testSettingKey, overrideValue } };
            AmbientSetting<string> testSetting = new AmbientSetting<string>(pretendGlobalAccessor, testSettingKey, s => s, defaultValue);

            Assert.AreEqual(defaultValue, testSetting.Value);

            AmbientSettingsOverride pretendGlobalSettingsProvider = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
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
            AmbientSettingsOverride pretendGlobalSettingsProvider2 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider2;
            Assert.AreEqual(overrideValue, testSetting.Value);

            overrides[testSettingKey] = null;
            AmbientSettingsOverride pretendGlobalSettingsProvider3 = new AmbientSettingsOverride(overrides, nameof(SettingGlobalProviderChangeNotification), pretendGlobalAccessor);
            pretendGlobalAccessor.GlobalProvider = pretendGlobalSettingsProvider3;
            Assert.AreEqual(defaultValue, testSetting.Value);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettingsProvider"/>.
        /// </summary>
        [TestMethod]
        public void SettingLocalChangeNotification()
        {
            IMutableAmbientSettingsProvider settingsProvider = new BasicAmbientSettingsProvider(nameof(SettingLocalChangeNotification));
            using (new LocalServiceScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider))
            using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(settingsProvider))
            {
                string testSettingKey = nameof(SettingLocalChangeNotification);
                string initialValue = "initialValue";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, s => s, initialValue);
                Assert.AreEqual(initialValue, testSetting.Value);
                string notificationNewValue = "";
                testSetting.ValueChanged += (s, e) => notificationNewValue = testSetting.Value;
                string secondValue = "change1";
                Assert.AreEqual("", notificationNewValue);
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
            using (new LocalServiceScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider))
            using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(settingsProvider))
            {
                string testSettingKey = nameof(SettingLocalChangeNotification);
                string initialValue = "initialValue";
                AmbientSetting<string> testSetting = new AmbientSetting<string>(testSettingKey, s => s, initialValue);
                Assert.AreEqual(initialValue, testSetting.Value);
                string notificationNewValue = "";
                testSetting.ValueChanged += (s, e) => notificationNewValue = testSetting.Value;
                Assert.AreEqual("", notificationNewValue);

                // now switch local providers and try again
                string secondValue = "change1";
                IMutableAmbientSettingsProvider settingsProvider2 = new BasicAmbientSettingsProvider(nameof(SettingLocalChangeProvider) + "_2");
                using (new LocalServiceScopedOverride<IMutableAmbientSettingsProvider>(settingsProvider2))
                using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(settingsProvider2))
                {
                    settingsProvider2.ChangeSetting(testSettingKey, secondValue);
                    Assert.AreEqual(secondValue, testSetting.Value);
                    // we don't get notified of this type of change (change due to change in local provider or changes to settings within local provider)
                    Assert.AreEqual("", notificationNewValue);
                }
            }
        }
    }
}
