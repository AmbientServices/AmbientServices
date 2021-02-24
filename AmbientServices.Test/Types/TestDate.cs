using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds unit tests for <see cref="Date"/>.
    /// </summary>
    [TestClass]
    public class TestDate
    {
        [TestMethod]
        public void DateCtor()
        {
            Assert.AreEqual(DateTime.Parse("1984-01-01"), new Date(1984, 1, 1).ToDateTime());
            Assert.AreEqual(DateTime.Parse("1984-01-01"), Date.FromDateTime(DateTime.Parse("1984-01-01")).ToDateTime());
        }

        [TestMethod]
        public void DateOperators()
        {
            Assert.AreEqual(1, (new Date(1984, 1, 2) - new Date(1984, 1, 1)).TotalDays);
            Assert.AreEqual(new Date(1984, 1, 1), (new Date(1984, 1, 2) - new TimeSpan(1, 0, 0, 0)));
            Assert.AreEqual(new Date(1984, 1, 2), (new Date(1984, 1, 1) + new TimeSpan(1, 0, 0, 0)));
            Assert.IsTrue(new Date(1984, 1, 1) != new Date(1984, 1, 2));
            Assert.IsTrue(new Date(1984, 1, 1) == new Date(1984, 1, 1));
            Assert.IsTrue(new Date(1984, 1, 1) < new Date(1984, 1, 2));
            Assert.IsTrue(new Date(1984, 1, 2) > new Date(1984, 1, 1));
            Assert.IsTrue(new Date(1984, 1, 2) <= new Date(1984, 1, 2));
            Assert.IsTrue(new Date(1984, 1, 2) >= new Date(1984, 1, 2));

            Assert.AreEqual(new Date(1984, 1, 2), (new Date(1984, 1, 1)).Add(new TimeSpan(1, 0, 0, 0)));
            Assert.AreEqual(new Date(1984, 1, 1), (new Date(1984, 1, 2)).Subtract(new TimeSpan(1, 0, 0, 0)));
            Assert.AreEqual(TimeSpan.FromDays(1), (new Date(1984, 1, 2)).Subtract(new Date(1984, 1, 1)));

            Assert.AreEqual(new Date(1984, 1, 2), (new Date(1984, 1, 1)).AddDays(1));
            Assert.AreEqual(new Date(1984, 2, 1), (new Date(1984, 1, 1)).AddMonths(1));
            Assert.AreEqual(new Date(1985, 1, 1), (new Date(1984, 1, 1)).AddYears(1));
        }

        [TestMethod]
        public void DateProperties()
        {
            Date date = new Date(1970, 5, 20);
            Assert.AreEqual(1970, date.Year);
            Assert.AreEqual(5, date.Month);
            Assert.AreEqual(20, date.Day);
            Assert.AreEqual(DayOfWeek.Wednesday, date.DayOfWeek);
            Assert.AreEqual(140, date.DayOfYear);

            Assert.AreEqual(28, Date.DaysInMonth(1970, 2));
            Assert.AreEqual(29, Date.DaysInMonth(2016, 2));

            Assert.IsFalse(Date.IsLeapYear(1900));      // 100-year exception
            Assert.IsFalse(Date.IsLeapYear(1970));
            Assert.IsTrue(Date.IsLeapYear(1996));
            Assert.IsFalse(Date.IsLeapYear(1997));
            Assert.IsFalse(Date.IsLeapYear(1998));
            Assert.IsFalse(Date.IsLeapYear(1999));
            Assert.IsTrue(Date.IsLeapYear(2000));       // 400-year exception
            Assert.IsTrue(Date.IsLeapYear(2016));

            date = new Date(1900, 5, 20);
            Assert.AreEqual(1900, date.Year);
            Assert.AreEqual(5, date.Month);
            Assert.AreEqual(20, date.Day);
            Assert.AreEqual(140, date.DayOfYear);

            date = new Date(1999, 5, 20);
            Assert.AreEqual(1999, date.Year);
            Assert.AreEqual(5, date.Month);
            Assert.AreEqual(20, date.Day);
            Assert.AreEqual(140, date.DayOfYear);

            date = new Date(2000, 5, 20);
            Assert.AreEqual(2000, date.Year);
            Assert.AreEqual(5, date.Month);
            Assert.AreEqual(20, date.Day);
            Assert.AreEqual(141, date.DayOfYear);
        }

        [TestMethod]
        public void DateParse()
        {
            Assert.AreEqual(DateTime.Parse("1984-01-01"), Date.Parse("1984-01-01").ToDateTime());
            Assert.AreEqual(DateTime.Parse("1984-01-01", System.Globalization.CultureInfo.InvariantCulture), Date.Parse("1984-01-01", System.Globalization.CultureInfo.InvariantCulture).ToDateTime());
            Assert.AreEqual(DateTime.Parse("1984-01-01", System.Globalization.CultureInfo.InvariantCulture, DateTimeStyles.None), Date.Parse("1984-01-01", System.Globalization.CultureInfo.InvariantCulture, DateTimeStyles.None).ToDateTime());
            Assert.AreEqual(DateTime.ParseExact("1984-01-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Date.ParseExact("1984-01-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).ToDateTime());
            Assert.AreEqual(DateTime.ParseExact("1984-01-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal), Date.ParseExact("1984-01-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal).ToDateTime());
            Assert.AreEqual(DateTime.ParseExact("1984-01-01", new string[] { "MM dd yyyy", "yyyy-MM-dd" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal), Date.ParseExact("1984-01-01", new string[] { "MM dd yyyy", "yyyy-MM-dd" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal).ToDateTime());
        }

        [TestMethod]
        public void DateTryParse()
        {
            Date actualResult;
            DateTime expectedResult;

            Assert.IsFalse(Date.TryParse("x", out actualResult));

            Assert.IsTrue(DateTime.TryParse("1984-01-01", out expectedResult));
            Assert.IsTrue(Date.TryParse("1984-01-01", out actualResult));
            Assert.AreEqual(expectedResult.ToString("d"), actualResult.ToString());
            Assert.IsTrue(Date.TryParse("1984-01-01", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));
            Assert.AreEqual(expectedResult.ToString("d"), actualResult.ToString());
            Assert.IsFalse(Date.TryParse("fdsjklvjklxc", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));


            Assert.IsFalse(Date.TryParseExact("x", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));

            Assert.IsTrue(DateTime.TryParse("1984-01-01", out expectedResult));
            Assert.IsTrue(Date.TryParseExact("1984-01-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));
            Assert.AreEqual(expectedResult.ToString("d"), actualResult.ToString());
            Assert.IsTrue(Date.TryParseExact("1984-01-01", new string[] { "MM dd yyyy", "yyyy-MM-dd" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));
            Assert.AreEqual(expectedResult.ToString("d"), actualResult.ToString());
            Assert.IsFalse(Date.TryParseExact("fdsjklvjklxc", new string[] { "MM dd yyyy", "yyyy-MM-dd" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out actualResult));
        }
        [TestMethod]
        public void DateMonthGuess()
        {
            Date date = new Date(31);
            DateTime dt = new DateTime(1, 1, 1).AddDays(31);
            Assert.AreEqual(date.Month, dt.Month);
        }

        [TestMethod]
        public void DateToString()
        {
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToLongDateString(), Date.Parse("1984-01-01").ToLongDateString());
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToShortDateString(), Date.Parse("1984-01-01").ToShortDateString());
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToString("d"), Date.Parse("1984-01-01").ToString("d"));
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToString(System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10), Date.Parse("1984-01-01").ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToString("d", System.Globalization.CultureInfo.InvariantCulture), Date.Parse("1984-01-01").ToString("d", System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual(DateTime.Parse("1984-01-01").ToString("o", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10), Date.Parse("1984-01-01").ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                Date.Parse("1984-01-01").ToString("!", System.Globalization.CultureInfo.InvariantCulture);
                Assert.Fail("Invalid date format didn't throw an exception!");
            }
            catch { }
        }
        [TestMethod]
        public void DateCompare()
        {
            Date now = DateTime.UtcNow.GetDate();
            Date earlier = now.AddDays(-1);
            Date later = now.AddDays(1);

            Assert.IsTrue(now.CompareTo(earlier) > 0);
            Assert.IsTrue(now.CompareTo(later) < 0);
            Assert.IsTrue(now.CompareTo(now) == 0);
            Assert.IsFalse(now.Equals(earlier));
            Assert.IsFalse(now.Equals(later));
            Assert.IsTrue(now.Equals(now));
            Assert.IsTrue(Date.Compare(now, earlier) > 0);
            Assert.IsTrue(Date.Compare(now, later) < 0);
            Assert.IsTrue(Date.Compare(now, now) == 0);
            Assert.IsFalse(Date.Equals(now, earlier));
            Assert.IsFalse(Date.Equals(now, later));
            Assert.IsTrue(Date.Equals(now, now));
            Assert.IsTrue(now.CompareTo((object)earlier) > 0);
            Assert.IsTrue(now.CompareTo((object)later) < 0);
            Assert.IsTrue(now.CompareTo((object)now) == 0);
        }
        [TestMethod]
        public void DateHash()
        {
            Date now = Date.FromDateTime(DateTime.Now);
            Date earlier = now.AddDays(-1);
            Date later = now.AddDays(1);
            // just make sure that GetHashCode doesn't crash
            int hash = now.GetHashCode();
            hash = earlier.GetHashCode();
            hash = later.GetHashCode();

            now = Date.FromDateTime(DateTime.UtcNow);
            earlier = now.AddDays(-1);
            later = now.AddDays(1);
            // just make sure that GetHashCode doesn't crash
            hash = now.GetHashCode();
            hash = earlier.GetHashCode();
            hash = later.GetHashCode();
        }
        [TestMethod]
        public void DateToday()
        {
            Assert.AreEqual(Date.LocalToday, Date.FromDateTime(DateTime.Now.Date));
            Assert.AreEqual(Date.UtcToday, Date.FromDateTime(DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified)));
        }
        [TestMethod]
        public void DateAdd()
        {
            DateTime dtNow = DateTime.Now;
            DateTime dtThirteenMonthsFromNow = dtNow.AddMonths(13);
            DateTime dtThirteenMonthsAgo = dtNow.AddMonths(-13);
            Date dToday = dtNow.GetDate();
            Date dThirteenMonthsFromNow = dToday.AddMonths(13);
            Date dThirteenMonthsAgo = dToday.AddMonths(-13);
            Assert.AreEqual(dtThirteenMonthsFromNow.GetDate(), dThirteenMonthsFromNow);
            Assert.AreEqual(dtThirteenMonthsAgo.GetDate(), dThirteenMonthsAgo);
        }
        [TestMethod]
        public void DateConvert()
        {
            DateTime now = DateTime.Now;
            int days = (int)(now.Date - new DateTime(0L, DateTimeKind.Utc).Date).TotalDays;
            Date today = now.GetDate();
            IConvertible dateTimeConvertible = days;
            IConvertible dateConvertible = today;
            Assert.AreEqual(dateTimeConvertible.GetTypeCode(), dateConvertible.GetTypeCode());
            TestConversion(dateTimeConvertible.ToBoolean, dateConvertible.ToBoolean);
            TestConversion(dateTimeConvertible.ToByte, dateConvertible.ToByte);
            TestConversion(dateTimeConvertible.ToChar, dateConvertible.ToChar);
            TestConversion(dateTimeConvertible.ToDateTime, dateConvertible.ToDateTime);
            TestConversion(dateTimeConvertible.ToDecimal, dateConvertible.ToDecimal);
            TestConversion(dateTimeConvertible.ToDouble, dateConvertible.ToDouble);
            TestConversion(dateTimeConvertible.ToInt16, dateConvertible.ToInt16);
            TestConversion(dateTimeConvertible.ToInt32, dateConvertible.ToInt32);
            TestConversion(dateTimeConvertible.ToInt64, dateConvertible.ToInt64);
            TestConversion(dateTimeConvertible.ToSByte, dateConvertible.ToSByte);
            TestConversion(dateTimeConvertible.ToSingle, dateConvertible.ToSingle);
            TestConversion(dateTimeConvertible.ToString, dateConvertible.ToString);
            TestConversion(dateTimeConvertible.ToUInt16, dateConvertible.ToUInt16);
            TestConversion(dateTimeConvertible.ToUInt32, dateConvertible.ToUInt32);
            TestConversion(dateTimeConvertible.ToUInt64, dateConvertible.ToUInt64);
            TestConversion(p => dateTimeConvertible.ToType(typeof(double), p), p => dateConvertible.ToType(typeof(double), p));
        }

        private void TestConversion<T>(Func<IFormatProvider, T> dateTimeConvert, Func<IFormatProvider, T> dateConvert)
        {
            bool dateTimeException = false;
            T dateTimeResult = default(T);
            try
            {
                dateTimeResult = dateTimeConvert(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                dateTimeException = true;
            }
            bool dateException = false;
            T dateResult = default(T);
            try
            {
                dateResult = dateConvert(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                dateException = true;
            }
            Assert.AreEqual(dateTimeException, dateException);
            Assert.AreEqual(dateTimeResult, dateResult);
        }
        [TestMethod]
        public void DateFeb31()
        {
            Date d = new Date(2000, 1, 31).AddMonths(1);
            Assert.AreEqual(2000, d.Year);
            Assert.AreEqual(2, d.Month);
            Assert.AreEqual(29, d.Day);
            d = new Date(2001, 1, 31).AddMonths(1);
            Assert.AreEqual(2001, d.Year);
            Assert.AreEqual(2, d.Month);
            Assert.AreEqual(28, d.Day);
        }
        [TestMethod]
        public void DateExceptions()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { Date d = new Date(0, 1, 1); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { Date d = new Date(2000, -1, 1); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => { Date d = new Date(2000, 1, 32); });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Date.IsLeapYear(-1));
            Assert.ThrowsException<ArgumentNullException>(() => new Date(2000, 1, 15).ToString((string)null));
        }
    }
}
