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
    /// A class that holds tests for <see cref="IAmbientSettings"/>.
    /// </summary>
    [TestClass]
    public class TestSettings
    {
        private static object _lock = new object();

        IAmbientSettings AmbientSettings = ServiceBroker<IAmbientSettings>.Implementation;
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettings"/>.
        /// </summary>
        [TestMethod]
        public void Settings()
        {
            ISetting<int> value;
            value = AmbientSettings.GetSetting<int>("int-setting", s => Int32.Parse(s));
            Assert.AreEqual(0, value.Value);
            value = AmbientSettings.GetSetting<int>("int-setting", s => Int32.Parse(s), 1);
            Assert.AreEqual(1, value.Value);
            value = AmbientSettings.GetSetting<int>("int-setting", s => Int32.Parse(s), 1);
            Assert.AreEqual(1, value.Value);
        }

        /// <summary>
        /// Performs tests on <see cref="IAmbientSettings"/>.
        /// </summary>
        [TestMethod]
        public void SettingChangeNotification()
        {
            lock (_lock)
            {
                string testSettingKey = "testNotificationSetting";
                BasicAmbientSettings testSettings = new BasicAmbientSettings();
                testSettings.ChangeSetting(testSettingKey, "serviceChanged");
                ISetting<string> temporarySetting = AmbientSettings.GetSetting<string>(testSettingKey, s => s, "initialValue");
                string value = temporarySetting.Value;
                temporarySetting.ValueChanged += (s, e) => value = e.NewValue;
                AmbientServices.ServiceBroker<IAmbientSettings>.Implementation = testSettings;
                Assert.AreEqual("serviceChanged", value);
                testSettings.ChangeSetting(testSettingKey, "valueChanged");
                Assert.AreEqual("valueChanged", value);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientSettings"/>.
        /// </summary>
        [TestMethod]
        public void SettingsGarbageCollection()
        {
            lock (_lock)
            {
                string testSettingKey = "temporarySetting";
                BasicAmbientSettings testSettings = new BasicAmbientSettings();
                testSettings.ChangeSetting(testSettingKey, "serviceChanged");
                SettingThatGoesAway(testSettingKey, testSettings);
                GC.Collect();   // this should collect the temporary Setting created in the function below
                testSettings.ChangeSetting(testSettingKey, "valueChanged");  // this should trigger the weak proxy to get removed
            // not sure how to verify this progamatically, but if you step into the above function, you can see the weak proxy's unsubscribe get called
            }
        }
        private void SettingThatGoesAway(string testSettingKey, BasicAmbientSettings testSettings)
        {
            ISetting<string> temporarySetting = AmbientSettings.GetSetting<string>(testSettingKey, s => s, "initialValue");
            string value = temporarySetting.Value;
            temporarySetting.ValueChanged += (s, e) =>
            {
                Assert.AreEqual(temporarySetting, e.Setting);
                value = e.NewValue;
            };
            AmbientServices.ServiceBroker<IAmbientSettings>.Implementation = testSettings;
            Assert.AreEqual("serviceChanged", value);
        }
    }
}
