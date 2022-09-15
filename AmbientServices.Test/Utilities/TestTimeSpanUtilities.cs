using AmbientServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Text;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestTimeSpanUtilities
    {
        [TestMethod]
        public void GCD()
        {
            Assert.AreEqual(1UL, TimeSpanUtilities.GCD(53, 7));
            Assert.AreEqual(5UL, TimeSpanUtilities.GCD(60, 5));
            Assert.AreEqual(5UL, TimeSpanUtilities.GCD(35, 15));
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientClock"/>.
        /// </summary>
        [TestMethod]
        public void TimeSpanTicksToStopwatchTicks()
        {
            foreach (long millisecondsToSleep in new long[] { 100, 1000, 10000, 100000 })
            {
                StringBuilder s = new();
                s.AppendLine($"millisecondsToSleep = {millisecondsToSleep}");
                s.AppendLine($"Stopwatch.Frequency = {Stopwatch.Frequency}");
                s.AppendLine($"TimeSpan.TicksPerSecond = {TimeSpan.TicksPerSecond}");
                s.AppendLine($"TimeSpanExtensions.TimeSpanToStopwatchMultiplier = {TimeSpanUtilities.TimeSpanToStopwatchMultiplier}");
                s.AppendLine($"TimeSpanExtensions.TimeSpanToStopwatchDivisor = {TimeSpanUtilities.TimeSpanToStopwatchDivisor}");
                s.AppendLine($"TimeSpan.FromMilliseconds(millisecondsToSleep).Ticks = {TimeSpan.FromMilliseconds(millisecondsToSleep).Ticks}");
                s.AppendLine($"TimeSpanExtensions.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(millisecondsToSleep).Ticks) = {TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(millisecondsToSleep).Ticks)}");
                Assert.AreEqual(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpan.FromMilliseconds(millisecondsToSleep).Ticks), millisecondsToSleep * Stopwatch.Frequency / 1000, s.ToString());
            }
        }
        [TestMethod]
        public void TimeSpanStopwatchTicksRoundTrip()
        {
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 1, TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 1)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100, TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000, TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000, TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000)));

            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 1, TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 1)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100, TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000, TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000)));
            Assert.AreEqual(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000, TimeSpanUtilities.StopwatchTicksToTimeSpanTicks(TimeSpanUtilities.TimeSpanTicksToStopwatchTicks(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000)));
        }
        [TestMethod]
        public void TimeSpanDateTimeStopwatchTicksRoundTrip()
        {
            long diff;

            diff = DateTimeTicksDifference(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100, TimeSpanUtilities.DateTimeToStopwatchTimestamp(TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000, TimeSpanUtilities.DateTimeToStopwatchTimestamp(TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000, TimeSpanUtilities.DateTimeToStopwatchTimestamp(TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000000000000, TimeSpanUtilities.DateTimeToStopwatchTimestamp(TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000000000000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);

            long baseTicks = DateTime.UtcNow.Ticks;

            diff = DateTimeTicksDifference(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100, TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.DateTimeToStopwatchTimestamp(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000, TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.DateTimeToStopwatchTimestamp(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000, TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.DateTimeToStopwatchTimestamp(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 10000000000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
            diff = DateTimeTicksDifference(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000000000000, TimeSpanUtilities.StopwatchTimestampToDateTime(TimeSpanUtilities.DateTimeToStopwatchTimestamp(baseTicks + TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple * 100000000000000)));
            Assert.IsTrue(diff < TimeSpanUtilities.TimeSpanStopwatchConversionLeastCommonMultiple);
        }
        private static long DateTimeTicksDifference(long ticksA, long ticksB)
        {
            return Math.Abs(ticksA - ticksB);
        }
    }
}
