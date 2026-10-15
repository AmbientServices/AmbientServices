using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Tests that a setting registered while a settings set is being built, or after a set has already typed its values, is still read as its declared type.
/// </summary>
[TestClass]
public class TestSettingsRegistrationRace
{
    private const int RaceIterations = 2000;
    private const int FillerKeys = 64;
    private const int SpinLimit = 1_000_000;

    private static int ParseInt(string s) => string.IsNullOrEmpty(s) ? 0 : int.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// Races the registration of a setting against the construction of a <see cref="BasicAmbientSettingsSet"/> holding a value for it, and checks that the set never ends up holding the raw string and that a typed read of it never throws.
    /// </summary>
    [TestMethod]
    public async Task BasicSetRegistrationRacesConstruction()
    {
        string prefix = nameof(BasicSetRegistrationRacesConstruction) + "-" + Guid.NewGuid().ToString("N") + "-";
        int stringsLeft = 0;
        for (int iteration = 0; iteration < RaceIterations; ++iteration)
        {
            string key = prefix + iteration.ToString(CultureInfo.InvariantCulture);
            Dictionary<string, string> values = new(StringComparer.Ordinal) { [key] = "42" };
            for (int filler = 0; filler < FillerKeys; ++filler) values[key + "-filler-" + filler.ToString(CultureInfo.InvariantCulture)] = "x";
            int started = 0;
            int registerDelay = iteration % 64;
            Task<BasicAmbientSettingsSet> build = Task.Run(() =>
            {
                Interlocked.Increment(ref started);
                for (int spin = 0; Volatile.Read(ref started) < 2 && spin < SpinLimit; ++spin) { }
                return new BasicAmbientSettingsSet(key, values);
            });
            Task<SettingInfo<int>> register = Task.Run(() =>
            {
                Interlocked.Increment(ref started);
                for (int spin = 0; Volatile.Read(ref started) < 2 && spin < SpinLimit; ++spin) { }
                Thread.SpinWait(registerDelay * 8);
                return new SettingInfo<int>(key, "", ParseInt, "0");
            });
            BasicAmbientSettingsSet set = await build;
            SettingInfo<int> info = await register;
            if (set.GetTypedValue(key) is not int) ++stringsLeft;
            SettingsSetSetting<int> setting = new(set, key, "", ParseInt, "0");
            Assert.AreEqual(42, setting.Value);
            Assert.AreEqual(42, setting.GetValueWithSetName().Item1);
            GC.KeepAlive(info);
        }
        Assert.AreEqual(0, stringsLeft, "the set kept the raw string for a setting registered while it was being built");
    }
    /// <summary>
    /// Checks that a typed read converts the raw string an immutable set keeps for a setting registered after the set was built, rather than throwing <see cref="InvalidCastException"/>.
    /// </summary>
    [TestMethod]
    public void ImmutableSetLateRegistrationReadsTyped()
    {
        string key = nameof(ImmutableSetLateRegistrationReadsTyped) + "-" + Guid.NewGuid().ToString("N");
        AmbientImmutableSettingsSet set = new(key, new Dictionary<string, string>(StringComparer.Ordinal) { [key] = "17" });
        // by that set's pledge, the typed value stays the raw string
        Assert.AreEqual("17", set.GetTypedValue(key));
        SettingsSetSetting<int> setting = new(set, key, "", ParseInt, "0");
        Assert.AreEqual(17, setting.Value);
        (int value, string setName) = setting.GetValueWithSetName();
        Assert.AreEqual(17, value);
        Assert.AreEqual(key, setName);
    }
    /// <summary>
    /// Checks that a typed read through an ambient setting converts a raw string held by the local settings set rather than throwing <see cref="InvalidCastException"/>.
    /// </summary>
    [TestMethod]
    public void AmbientSettingReadsRawStringTyped()
    {
        string key = nameof(AmbientSettingReadsRawStringTyped) + "-" + Guid.NewGuid().ToString("N");
        AmbientImmutableSettingsSet set = new(key, new Dictionary<string, string>(StringComparer.Ordinal) { [key] = "23" });
        using ScopedLocalServiceOverride<IAmbientSettingsSet> localOverride = new(set);
        AmbientSetting<int> setting = new(key, "", ParseInt, "0");
        Assert.AreEqual(23, setting.Value);
        Assert.AreEqual(23, setting.GetValueWithSetName().Item1);
    }
}
