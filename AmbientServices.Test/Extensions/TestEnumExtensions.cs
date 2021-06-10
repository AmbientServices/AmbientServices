using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for enum extension methods.
    /// </summary>
    [TestClass]
    public class TestEnumExtensions
    {
        enum TestEmptyEnum
        {
        }
        enum TestOneValueEnum
        {
            ValueOne
        }
        enum TestTwoValueEnum
        {
            ValueOne,
            ValueTwo
        }
        enum TestOverrideEnum
        {
            ValueOne = -1,
            ValueTwo = 5890
        }
        /// <summary>
        /// Performs basic tests on date time extension methods.
        /// </summary>
        [TestMethod]
        public void EnumMaxEnumValue()
        {
            Assert.AreEqual(default(TestEmptyEnum), EnumExtensions.MaxEnumValue<TestEmptyEnum>());
            Assert.AreEqual(TestOneValueEnum.ValueOne, EnumExtensions.MaxEnumValue<TestOneValueEnum>());
            Assert.AreEqual(TestTwoValueEnum.ValueTwo, EnumExtensions.MaxEnumValue<TestTwoValueEnum>());
            Assert.AreEqual(TestOverrideEnum.ValueTwo, EnumExtensions.MaxEnumValue<TestOverrideEnum>());
        }
    }
}
