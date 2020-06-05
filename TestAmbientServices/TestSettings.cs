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
    /// A class that holds tests for <see cref="ISettings"/>.
    /// </summary>
    [TestClass]
    public class TestSettings
    {
        /// <summary>
        /// Performs tests on <see cref="ISettings"/>.
        /// </summary>
        [TestMethod]
        public void Settings()
        {
            ISetting<int> value;
            ISettings settings = Registry<ISettings>.Implementation;
            value = settings.GetSetting<int>("int-setting");
            Assert.AreEqual(0, value.Value);
            value = settings.GetSetting<int>("int-setting", 1);
            Assert.AreEqual(1, value.Value);
            value = settings.GetSetting<int>("int-setting", 1, s => (s == null) ? 0 : int.Parse(s));
            Assert.AreEqual(1, value.Value);
        }
    }
}
