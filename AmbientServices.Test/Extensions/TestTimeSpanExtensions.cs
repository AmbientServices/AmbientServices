using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestTimeSpanExtensions
    {
        [TestMethod]
        public void TimeSpanRenderAndParse()
        {
            Assert.AreEqual((TimeSpan?)null, "".TryParseTimeSpan());
            foreach (TimeSpan ts in new TimeSpan[] {    // NOTE: These numbers were carefully chosen to avoid rounding issues and avoid issues with the short string using only two significant digits
                    TimeSpan.FromMilliseconds(1.3), TimeSpan.FromMilliseconds(-1.3),
                    TimeSpan.FromMilliseconds(2.3), TimeSpan.FromMilliseconds(-2.3),
                    TimeSpan.FromMilliseconds(230), TimeSpan.FromMilliseconds(-230),
                    TimeSpan.FromMilliseconds(2300), TimeSpan.FromMilliseconds(-2300),
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1),
                    TimeSpan.FromSeconds(23), TimeSpan.FromSeconds(-23),
                    TimeSpan.FromSeconds(228), TimeSpan.FromSeconds(-228),
                    TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(-1),
                    TimeSpan.FromMinutes(23), TimeSpan.FromMinutes(-23),
                    TimeSpan.FromMinutes(228), TimeSpan.FromMinutes(-228),
                    TimeSpan.FromHours(1), TimeSpan.FromHours(-1),
                    TimeSpan.FromHours(23), TimeSpan.FromHours(-23),
                    TimeSpan.FromHours(182.4), TimeSpan.FromHours(-182.4),
                    TimeSpan.FromDays(1), TimeSpan.FromDays(-1),
                    TimeSpan.FromDays(23), TimeSpan.FromDays(-23),
                    TimeSpan.FromDays(37), TimeSpan.FromDays(-37),
                    TimeSpan.FromDays(73.05), TimeSpan.FromDays(-73.05),
                    TimeSpan.FromDays(167.40625), TimeSpan.FromDays(-167.40625),
                    TimeSpan.FromDays(365.25), TimeSpan.FromDays(-365.25),
                    TimeSpan.FromDays(8400.75), TimeSpan.FromDays(-8400.75),
                    TimeSpan.FromDays(317767.5), TimeSpan.FromDays(-317767.5),
                })
            {
                Assert.AreEqual((TimeSpan?)ts, ts.ToLongHumanReadableString().TryParseTimeSpan());
                Assert.AreEqual((TimeSpan?)ts, ts.ToShortHumanReadableString().TryParseTimeSpan());
            }
        }
        [TestMethod]
        public void TimeSpanNormalParse()
        {
            string s;
            TimeSpan? t;

            s = TimeSpan.FromMinutes(1).ToString();
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMinutes(1), t);

            s = TimeSpan.FromMilliseconds(1).ToString();
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMilliseconds(1), t);

            s = TimeSpan.FromDays(1).ToString();
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(1), t);

            s = TimeSpan.FromMinutes(137).ToString();
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMinutes(137), t);
        }
        [TestMethod]
        public void TimeSpanParseSingular()
        {
            TimeSpan? t;

            t = "1 MS".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMilliseconds(1), t);

            t = "1 MILLISECOND".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMilliseconds(1), t);

            t = "1 S".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromSeconds(1), t);

            t = "1 SECOND".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromSeconds(1), t);

            t = "1 m".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMinutes(1), t);

            t = "1 MINUTE".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromMinutes(1), t);

            t = "1 H".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromHours(1), t);

            t = "1 HOUR".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromHours(1), t);

            t = "1 D".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(1), t);

            t = "1 DAY".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(1), t);

            t = "1 M".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(30.4375), t);

            t = "1 MONTH".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(30.4375), t);

            t = "1 Y".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(365.25), t);

            t = "1 YEAR".TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(365.25), t);

        }
        [TestMethod]
        public void TimeSpanParseMisc()
        {
            string s;
            TimeSpan? t;

            s = "15t";
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromTicks(15), t);

            s = "3Y";
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromDays(365.25 * 3), t);

            s = "Ω";
            t = s.TryParseTimeSpan();
            Assert.IsNull(t);

            s = "3.287E+3thisisjunk";
            t = s.TryParseTimeSpan();
            Assert.AreEqual(TimeSpan.FromTicks(3287), t);
        }
    }
}
