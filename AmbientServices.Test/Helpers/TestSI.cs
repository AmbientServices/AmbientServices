using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestSI
    {
        [TestMethod]
        public void SISpecials()
        {
            string result;

            result = Double.NaN.ToSi();
            Assert.AreEqual("NaN", result);

            result = Double.MaxValue.ToSi();
            Assert.AreEqual("MAX", result);
            result = Double.MaxValue.ToSi(positiveSign: true);
            Assert.AreEqual("+MAX", result);
            result = Double.MinValue.ToSi();
            Assert.AreEqual("-MAX", result);
            result = Double.MinValue.ToSi(positiveSign: true);
            Assert.AreEqual("-MAX", result);

            result = Double.PositiveInfinity.ToSi();
            Assert.AreEqual("INF", result);
            result = Double.PositiveInfinity.ToSi(positiveSign: true);
            Assert.AreEqual("+INF", result);
            result = Double.NegativeInfinity.ToSi();
            Assert.AreEqual("-INF", result);
            result = Double.NegativeInfinity.ToSi(positiveSign: true);
            Assert.AreEqual("-INF", result);

            result = Double.Epsilon.ToSi();
            Assert.AreEqual("EPS", result);
            result = Double.Epsilon.ToSi(positiveSign: true);
            Assert.AreEqual("+EPS", result);
            result = (-Double.Epsilon).ToSi();
            Assert.AreEqual("-EPS", result);
            result = (-Double.Epsilon).ToSi(positiveSign: true);
            Assert.AreEqual("-EPS", result);
        }
        [TestMethod]
        public void SIMagnitude()
        {
            string result;

            result = 0.0.ToSi();
            Assert.AreEqual("0.00", result);
            result = 1.0.ToSi();
            Assert.AreEqual("1.00", result);
            result = 1.0.ToSi(positiveSign: true);
            Assert.AreEqual("+1.00", result);
            result = (-1.0).ToSi();
            Assert.AreEqual("-1.00", result);
            result = 999.0.ToSi();
            Assert.AreEqual("999", result);
            result = (-999.0).ToSi();
            Assert.AreEqual("-999", result);
            result = 1000.0.ToSi();
            Assert.AreEqual("1.00k", result);
            result = (-1000.0).ToSi();
            Assert.AreEqual("-1.00k", result);

            result = 1.0.ToSi(postfix: "b");
            Assert.AreEqual("1.00b", result);

            result = 1.0.ToSi(longName: true, postfix: "byte");
            Assert.AreEqual("1.00 byte", result);

            result = 1000.0.ToSi(postfix: "b");
            Assert.AreEqual("1.00kb", result);
            result = (-1000.0).ToSi(postfix: "b");
            Assert.AreEqual("-1.00kb", result);

            result = 1000.0.ToSi(longName: true);
            Assert.AreEqual("1.00 kilo", result);
            result = (-1000.0).ToSi(longName: true);
            Assert.AreEqual("-1.00 kilo", result);

            result = 1000.0.ToSi(longName: true, postfix: "byte");
            Assert.AreEqual("1.00 kilobyte", result);
            result = (-1000.0).ToSi(longName: true, postfix: "byte");
            Assert.AreEqual("-1.00 kilobyte", result);

            result = (AmbientServices.Utility.SI.Yotta * 1000).ToSi();
            Assert.AreEqual("1.00kY", result);
            result = (AmbientServices.Utility.SI.Yotta * 1000).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00kY", result);
            result = (-AmbientServices.Utility.SI.Yotta * 1000).ToSi();
            Assert.AreEqual("-1.00kY", result);

            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi();
            Assert.AreEqual("1.00YY", result);
            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00YY", result);
            result = (-AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi();
            Assert.AreEqual("-1.00YY", result);

            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi();
            Assert.AreEqual("1.00YYY", result);
            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00YYY", result);
            result = (-AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi();
            Assert.AreEqual("-1.00YYY", result);
            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi(longName: true);
            Assert.AreEqual("1.00 yottayottayotta", result);
            result = (AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta * AmbientServices.Utility.SI.Yotta).ToSi(longName: true, positiveSign: true);
            Assert.AreEqual("+1.00 yottayottayotta", result);

            result = 0.001.ToSi();
            Assert.AreEqual("1.00m", result);
            result = 0.001.ToSi(positiveSign: true);
            Assert.AreEqual("+1.00m", result);
            result = (-0.001).ToSi();
            Assert.AreEqual("-1.00m", result);

            result = (AmbientServices.Utility.SI.Yocto / 1000.0).ToSi();
            Assert.AreEqual("1.00my", result);
            result = (AmbientServices.Utility.SI.Yocto / 1000.0).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00my", result);
            result = (-AmbientServices.Utility.SI.Yocto / 1000.0).ToSi();
            Assert.AreEqual("-1.00my", result);

            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi();
            Assert.AreEqual("1.00yy", result);
            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00yy", result);
            result = (-AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi();
            Assert.AreEqual("-1.00yy", result);

            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi();
            Assert.AreEqual("1.00yyy", result);
            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi(positiveSign: true);
            Assert.AreEqual("+1.00yyy", result);
            result = (-AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi();
            Assert.AreEqual("-1.00yyy", result);
            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi(longName: true);
            Assert.AreEqual("1.00 yoctoyoctoyocto", result);
            result = (AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto * AmbientServices.Utility.SI.Yocto).ToSi(longName: true, positiveSign: true);
            Assert.AreEqual("+1.00 yoctoyoctoyocto", result);
        }
        [TestMethod]
        public void SIRounding()
        {
            string result;

            result = 999.4.ToSi();
            Assert.AreEqual("999", result);
            result = (-999.4).ToSi();
            Assert.AreEqual("-999", result);

            result = 999.5.ToSi();
            Assert.AreEqual("1.00k", result);
            result = (-999.5).ToSi();
            Assert.AreEqual("-1.00k", result);

            result = 0.9994.ToSi();
            Assert.AreEqual("999m", result);
            result = (-0.9994).ToSi();
            Assert.AreEqual("-999m", result);

            result = 0.9995.ToSi();
            Assert.AreEqual("1.00", result);
            result = (-0.9995).ToSi();
            Assert.AreEqual("-1.00", result);


            result = 999400.0.ToSi();
            Assert.AreEqual("999k", result);
            result = (-999400.0).ToSi();
            Assert.AreEqual("-999k", result);

            result = 999500.0.ToSi();
            Assert.AreEqual("1.00M", result);
            result = (-999500.0).ToSi();
            Assert.AreEqual("-1.00M", result);

            result = 0.0009994.ToSi();
            Assert.AreEqual("999μ", result);
            result = (-0.0009994).ToSi();
            Assert.AreEqual("-999μ", result);

            result = 0.0009995.ToSi();
            Assert.AreEqual("1.00m", result);
            result = (-0.0009995).ToSi();
            Assert.AreEqual("-1.00m", result);


            result = 999400000.0.ToSi();
            Assert.AreEqual("999M", result);
            result = (-999400000.0).ToSi();
            Assert.AreEqual("-999M", result);

            result = 999500000.0.ToSi();
            Assert.AreEqual("1.00G", result);
            result = (-999500000.0).ToSi();
            Assert.AreEqual("-1.00G", result);

            result = 0.0000009994.ToSi();
            Assert.AreEqual("999n", result);
            result = (-0.0000009994).ToSi();
            Assert.AreEqual("-999n", result);

            result = 0.0000009995.ToSi();
            Assert.AreEqual("1.00μ", result);
            result = (-0.0000009995).ToSi();
            Assert.AreEqual("-1.00μ", result);


            result = 999400000000.0.ToSi();
            Assert.AreEqual("999G", result);
            result = (-999400000000.0).ToSi();
            Assert.AreEqual("-999G", result);

            result = 999500000000.0.ToSi();
            Assert.AreEqual("1.00T", result);
            result = (-999500000000.0).ToSi();
            Assert.AreEqual("-1.00T", result);

            result = 0.0000000009994.ToSi();
            Assert.AreEqual("999p", result);
            result = (-0.0000000009994).ToSi();
            Assert.AreEqual("-999p", result);

            result = 0.0000000009995.ToSi();
            Assert.AreEqual("1.00n", result);
            result = (-0.0000000009995).ToSi();
            Assert.AreEqual("-1.00n", result);


            result = 999400000000000.0.ToSi();
            Assert.AreEqual("999T", result);
            result = (-999400000000000.0).ToSi();
            Assert.AreEqual("-999T", result);

            result = 999500000000000.0.ToSi();
            Assert.AreEqual("1.00P", result);
            result = (-999500000000000.0).ToSi();
            Assert.AreEqual("-1.00P", result);

            result = 0.0000000000009994.ToSi();
            Assert.AreEqual("999f", result);
            result = (-0.0000000000009994).ToSi();
            Assert.AreEqual("-999f", result);

            result = 0.0000000000009995.ToSi();
            Assert.AreEqual("1.00p", result);
            result = (-0.0000000000009995).ToSi();
            Assert.AreEqual("-1.00p", result);


            result = 999400000000000000.0.ToSi();
            Assert.AreEqual("999P", result);
            result = (-999400000000000000.0).ToSi();
            Assert.AreEqual("-999P", result);

            result = 999500000000000000.0.ToSi();
            Assert.AreEqual("1.00E", result);
            result = (-999500000000000000.0).ToSi();
            Assert.AreEqual("-1.00E", result);

            result = 0.0000000000000009994.ToSi();
            Assert.AreEqual("999a", result);
            result = (-0.0000000000000009994).ToSi();
            Assert.AreEqual("-999a", result);

            result = 0.0000000000000009995.ToSi();
            Assert.AreEqual("1.00f", result);
            result = (-0.0000000000000009995).ToSi();
            Assert.AreEqual("-1.00f", result);


            result = 999400000000000000000.0.ToSi();
            Assert.AreEqual("999E", result);
            result = (-999400000000000000000.0).ToSi();
            Assert.AreEqual("-999E", result);

            result = 999500000000000000000.0.ToSi();
            Assert.AreEqual("1.00Z", result);
            result = (-999500000000000000000.0).ToSi();
            Assert.AreEqual("-1.00Z", result);

            result = 0.0000000000000000009994.ToSi();
            Assert.AreEqual("999z", result);
            result = (-0.0000000000000000009994).ToSi();
            Assert.AreEqual("-999z", result);

            result = 0.0000000000000000009995.ToSi();
            Assert.AreEqual("1.00a", result);
            result = (-0.0000000000000000009995).ToSi();
            Assert.AreEqual("-1.00a", result);


            result = 999400000000000000000000.0.ToSi();
            Assert.AreEqual("999Z", result);
            result = (-999400000000000000000000.0).ToSi();
            Assert.AreEqual("-999Z", result);

            result = 999500000000000000000000.0.ToSi();
            Assert.AreEqual("1.00Y", result);
            result = (-999500000000000000000000.0).ToSi();
            Assert.AreEqual("-1.00Y", result);

            result = 0.0000000000000000000009994.ToSi();
            Assert.AreEqual("999y", result);
            result = (-0.0000000000000000000009994).ToSi();
            Assert.AreEqual("-999y", result);

            result = 0.0000000000000000000009995.ToSi();
            Assert.AreEqual("1.00z", result);
            result = (-0.0000000000000000000009995).ToSi();
            Assert.AreEqual("-1.00z", result);
        }
        [TestMethod]
        public void SIMaxCharacters()
        {
            string result;

            result = 1.8235892345890.ToSi(1);
            Assert.AreEqual("2", result);
            result = (-1.8235892345890).ToSi(1);
            Assert.AreEqual("-2", result);

            result = 1.8235892345890.ToSi(2);
            Assert.AreEqual("2", result);
            result = (-1.8235892345890).ToSi(2);
            Assert.AreEqual("-2", result);

            result = 1.8235892345890.ToSi(3);
            Assert.AreEqual("1.8", result);
            result = (-1.8235892345890).ToSi(3);
            Assert.AreEqual("-1.8", result);

            result = 1.8235892345890.ToSi(4);
            Assert.AreEqual("1.82", result);
            result = (-1.8235892345890).ToSi(4);
            Assert.AreEqual("-1.82", result);

            result = 1.8235892345890.ToSi(5);
            Assert.AreEqual("1.824", result);
            result = (-1.8235892345890).ToSi(5);
            Assert.AreEqual("-1.824", result);

            result = 1.8235892345890.ToSi(6);
            Assert.AreEqual("1.8236", result);
            result = (-1.8235892345890).ToSi(6);
            Assert.AreEqual("-1.8236", result);

            result = 1.8235892345890.ToSi(7);
            Assert.AreEqual("1.82359", result);
            result = (-1.8235892345890).ToSi(7);
            Assert.AreEqual("-1.82359", result);

            result = 1.8235892345890.ToSi(8);
            Assert.AreEqual("1.823589", result);
            result = (-1.8235892345890).ToSi(8);
            Assert.AreEqual("-1.823589", result);

            result = 1.8235892345890.ToSi(9);
            Assert.AreEqual("1.8235892", result);
            result = (-1.8235892345890).ToSi(9);
            Assert.AreEqual("-1.8235892", result);

            result = 1.8235892345890.ToSi(10);
            Assert.AreEqual("1.82358923", result);
            result = (-1.8235892345890).ToSi(10);
            Assert.AreEqual("-1.82358923", result);

            result = 1.8235892345890.ToSi(11);
            Assert.AreEqual("1.823589235", result);
            result = (-1.8235892345890).ToSi(11);
            Assert.AreEqual("-1.823589235", result);

            result = 1.8235892345890.ToSi(12);
            Assert.AreEqual("1.8235892346", result);
            result = (-1.8235892345890).ToSi(12);
            Assert.AreEqual("-1.8235892346", result);

            result = 1.8235892345890.ToSi(13);
            Assert.AreEqual("1.82358923459", result);
            result = (-1.8235892345890).ToSi(13);
            Assert.AreEqual("-1.82358923459", result);

            result = 1.8235892345890.ToSi(14);
            Assert.AreEqual("1.823589234589", result);
            result = (-1.8235892345890).ToSi(14);
            Assert.AreEqual("-1.823589234589", result);

            result = 1.8235892345890.ToSi(15);
            Assert.AreEqual("1.8235892345890", result);
            result = (-1.8235892345890).ToSi(15);
            Assert.AreEqual("-1.8235892345890", result);


            result = 18.235892345890.ToSi(1);
            Assert.AreEqual("18", result);
            result = (-18.235892345890).ToSi(1);
            Assert.AreEqual("-18", result);

            result = 18.235892345890.ToSi(2);
            Assert.AreEqual("18", result);
            result = (-18.235892345890).ToSi(2);
            Assert.AreEqual("-18", result);

            result = 18.235892345890.ToSi(3);
            Assert.AreEqual("18", result);
            result = (-18.235892345890).ToSi(3);
            Assert.AreEqual("-18", result);

            result = 18.235892345890.ToSi(4);
            Assert.AreEqual("18.2", result);
            result = (-18.235892345890).ToSi(4);
            Assert.AreEqual("-18.2", result);

            result = 18.235892345890.ToSi(5);
            Assert.AreEqual("18.24", result);
            result = (-18.235892345890).ToSi(5);
            Assert.AreEqual("-18.24", result);

            result = 18.235892345890.ToSi(6);
            Assert.AreEqual("18.236", result);
            result = (-18.235892345890).ToSi(6);
            Assert.AreEqual("-18.236", result);

            result = 18.235892345890.ToSi(7);
            Assert.AreEqual("18.2359", result);
            result = (-18.235892345890).ToSi(7);
            Assert.AreEqual("-18.2359", result);

            result = 18.235892345890.ToSi(8);
            Assert.AreEqual("18.23589", result);
            result = (-18.235892345890).ToSi(8);
            Assert.AreEqual("-18.23589", result);

            result = 18.235892345890.ToSi(9);
            Assert.AreEqual("18.235892", result);
            result = (-18.235892345890).ToSi(9);
            Assert.AreEqual("-18.235892", result);

            result = 18.235892345890.ToSi(10);
            Assert.AreEqual("18.2358923", result);
            result = (-18.235892345890).ToSi(10);
            Assert.AreEqual("-18.2358923", result);

            result = 18.235892345890.ToSi(11);
            Assert.AreEqual("18.23589235", result);
            result = (-18.235892345890).ToSi(11);
            Assert.AreEqual("-18.23589235", result);

            result = 18.235892345890.ToSi(12);
            Assert.AreEqual("18.235892346", result);
            result = (-18.235892345890).ToSi(12);
            Assert.AreEqual("-18.235892346", result);

            result = 18.235892345890.ToSi(13);
            Assert.AreEqual("18.2358923459", result);
            result = (-18.235892345890).ToSi(13);
            Assert.AreEqual("-18.2358923459", result);

            result = 18.235892345890.ToSi(14);
            Assert.AreEqual("18.23589234589", result);
            result = (-18.235892345890).ToSi(14);
            Assert.AreEqual("-18.23589234589", result);

            result = 18.235892345890.ToSi(15);
            Assert.AreEqual("18.235892345890", result);
            result = (-18.235892345890).ToSi(15);
            Assert.AreEqual("-18.235892345890", result);


            result = 182.35892345890.ToSi(1);
            Assert.AreEqual("182", result);
            result = (-182.35892345890).ToSi(1);
            Assert.AreEqual("-182", result);

            result = 182.35892345890.ToSi(2);
            Assert.AreEqual("182", result);
            result = (-182.35892345890).ToSi(2);
            Assert.AreEqual("-182", result);

            result = 182.35892345890.ToSi(3);
            Assert.AreEqual("182", result);
            result = (-182.35892345890).ToSi(3);
            Assert.AreEqual("-182", result);

            result = 182.35892345890.ToSi(4);
            Assert.AreEqual("182", result);
            result = (-182.35892345890).ToSi(4);
            Assert.AreEqual("-182", result);

            result = 182.35892345890.ToSi(5);
            Assert.AreEqual("182.4", result);
            result = (-182.35892345890).ToSi(5);
            Assert.AreEqual("-182.4", result);

            result = 182.35892345890.ToSi(6);
            Assert.AreEqual("182.36", result);
            result = (-182.35892345890).ToSi(6);
            Assert.AreEqual("-182.36", result);

            result = 182.35892345890.ToSi(7);
            Assert.AreEqual("182.359", result);
            result = (-182.35892345890).ToSi(7);
            Assert.AreEqual("-182.359", result);

            result = 182.35892345890.ToSi(8);
            Assert.AreEqual("182.3589", result);
            result = (-182.35892345890).ToSi(8);
            Assert.AreEqual("-182.3589", result);

            result = 182.35892345890.ToSi(9);
            Assert.AreEqual("182.35892", result);
            result = (-182.35892345890).ToSi(9);
            Assert.AreEqual("-182.35892", result);

            result = 182.35892345890.ToSi(10);
            Assert.AreEqual("182.358923", result);
            result = (-182.35892345890).ToSi(10);
            Assert.AreEqual("-182.358923", result);

            result = 182.35892345890.ToSi(11);
            Assert.AreEqual("182.3589235", result);
            result = (-182.35892345890).ToSi(11);
            Assert.AreEqual("-182.3589235", result);

            result = 182.35892345890.ToSi(12);
            Assert.AreEqual("182.35892346", result);
            result = (-182.35892345890).ToSi(12);
            Assert.AreEqual("-182.35892346", result);

            result = 182.35892345890.ToSi(13);
            Assert.AreEqual("182.358923459", result);
            result = (-182.35892345890).ToSi(13);
            Assert.AreEqual("-182.358923459", result);

            result = 182.35892345890.ToSi(14);
            Assert.AreEqual("182.3589234589", result);
            result = (-182.35892345890).ToSi(14);
            Assert.AreEqual("-182.3589234589", result);

            result = 182.35892345890.ToSi(15);
            Assert.AreEqual("182.35892345890", result);
            result = (-182.35892345890).ToSi(15);
            Assert.AreEqual("-182.35892345890", result);

        }
    }
}

