using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Tests for <see cref="IAmbientAtomicCache"/> / <see cref="BasicAmbientAtomicCache"/>.
/// </summary>
[TestClass]
public sealed class TestAtomicCache
{
    private static readonly Dictionary<string, string> TestAtomicCacheSettingsDictionary = new()
    {
        { nameof(BasicAmbientAtomicCache) + "-EjectFrequency", "10" },
        { nameof(BasicAmbientAtomicCache) + "-MaximumItemCount", "20" },
        { nameof(BasicAmbientAtomicCache) + "-MinimumItemCount", "1" },
    };

    /// <summary>Low queue ceiling and <c>MinimumItemCount = -1</c> so <see cref="BasicAmbientAtomicCache"/> untimed ejection is not blocked by the minimum guard.</summary>
    private static readonly Dictionary<string, string> AtomicUntimedEjectSettingsDictionary = new()
    {
        { nameof(BasicAmbientAtomicCache) + "-EjectFrequency", "2" },
        { nameof(BasicAmbientAtomicCache) + "-MaximumItemCount", "8" },
        { nameof(BasicAmbientAtomicCache) + "-MinimumItemCount", "-1" },
    };

    private static readonly DateTime FarPastUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task Eject(IAmbientAtomicCache cache, int count)
    {
        const int freq = 10;
        for (int ejection = 0; ejection < count; ++ejection)
        {
            for (int i = 0; i < freq; ++i)
            {
                _ = await cache.GetOrAdd<string>("__atomic_eject_probe__", async () => ("", null));
            }
        }
    }

    private static async Task EjectWithFrequency(IAmbientAtomicCache cache, int ejectFrequency, int count)
    {
        for (int ejection = 0; ejection < count; ++ejection)
        {
            for (int i = 0; i < ejectFrequency; ++i)
            {
                _ = await cache.GetOrAdd<string>("__atomic_eject_probe__" + ejection + "_" + i, async () => ("", null));
            }
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_MaxDurationAndFixedExpiration_PicksEarlier()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_MaxDurationAndFixedExpiration_PicksEarlier));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedPut_MaxDurationAndFixedExpiration_PicksEarlier);
            DateTime early = AmbientClock.UtcNow.AddMinutes(5);
            long rev = await cache.VersionedPut(key, new AtomicRefBox(1), maxCacheDuration: TimeSpan.FromHours(1), expiration: early);
            Assert.IsTrue(rev >= 1);
            AmbientClock.SkipAhead(TimeSpan.FromMinutes(6));
            (AtomicRefBox? gone, _) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.IsNull(gone);
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_ExpiresUtcKind()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_ExpiresUtcKind));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            DateTime utc = DateTime.SpecifyKind(AmbientClock.UtcNow.AddMinutes(10), DateTimeKind.Utc);
            await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_ExpiresUtcKind), async () => (new AtomicRefBox(1), utc));
            AmbientClock.SkipAhead(TimeSpan.FromMinutes(11));
            AtomicRefBox replaced = await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_ExpiresUtcKind), async () => (new AtomicRefBox(2), null));
            Assert.AreEqual(2, replaced.Id);
        }
    }

    [TestMethod]
    public void AtomicCache_DefaultConstructor()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_DefaultConstructor));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            BasicAmbientAtomicCache cache = new();
            Assert.IsNotNull(cache);
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_HitAndCreate_Untimed()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_HitAndCreate_Untimed));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            int creates = 0;
            AtomicRefBox a = await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_HitAndCreate_Untimed), async () =>
            {
                Interlocked.Increment(ref creates);
                return (new AtomicRefBox(1), null);
            });
            Assert.AreEqual(1, creates);
            AtomicRefBox b = await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_HitAndCreate_Untimed), async () =>
            {
                Interlocked.Increment(ref creates);
                return (new AtomicRefBox(2), null);
            });
            Assert.AreEqual(1, creates);
            Assert.AreSame(a, b);
            Assert.AreEqual(1, a.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_ExpiresKindsAndRefresh()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_ExpiresKindsAndRefresh));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            DateTime localExp = DateTime.SpecifyKind(AmbientClock.UtcNow.AddHours(1).ToLocalTime(), DateTimeKind.Local);
            AtomicRefBox viaLocal = await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_ExpiresKindsAndRefresh) + "L", async () => (new AtomicRefBox(1), localExp));
            Assert.IsNotNull(viaLocal);

            DateTime unspec = DateTime.SpecifyKind(AmbientClock.UtcNow.AddHours(1), DateTimeKind.Unspecified);
            AtomicRefBox viaUnspec = await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_ExpiresKindsAndRefresh) + "U", async () => (new AtomicRefBox(2), unspec));
            Assert.IsNotNull(viaUnspec);

            string refreshKey = nameof(AtomicCache_GetOrAdd_ExpiresKindsAndRefresh) + "R";
            await cache.GetOrAdd<AtomicRefBox>(refreshKey, async () => (new AtomicRefBox(3), AmbientClock.UtcNow.AddMilliseconds(50)));
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(40));
            AtomicRefBox still = await cache.GetOrAdd<AtomicRefBox>(refreshKey, async () => (new AtomicRefBox(99), null), refresh: TimeSpan.FromMilliseconds(100));
            Assert.AreEqual(3, still.Id);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(200));
            AtomicRefBox replaced = await cache.GetOrAdd<AtomicRefBox>(refreshKey, async () => (new AtomicRefBox(4), AmbientClock.UtcNow.AddHours(1)));
            Assert.AreEqual(4, replaced.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_DisposeDiscardedWhenCreateAlreadyExpired()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_DisposeDiscardedWhenCreateAlreadyExpired));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            int creates = 0;
            DisposableCacheEntry good = await cache.GetOrAdd<DisposableCacheEntry>(nameof(AtomicCache_GetOrAdd_DisposeDiscardedWhenCreateAlreadyExpired), async () =>
            {
                int n = Interlocked.Increment(ref creates);
                if (n == 1)
                {
                    var dead = new DisposableCacheEntry(-1);
                    return (dead, AmbientClock.UtcNow.AddTicks(-1));
                }
                return (new DisposableCacheEntry(42), AmbientClock.UtcNow.AddHours(1));
            });
            Assert.AreEqual(2, creates);
            Assert.IsFalse(good.Disposed);
            Assert.AreEqual(42, good.GetHashCode());
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_CancelledTokenThrows()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_CancelledTokenThrows));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            using CancellationTokenSource cts = new();
            cts.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            {
                await cache.GetOrAdd<string>("x", async () => ("y", null), cancel: cts.Token);
            });
        }
    }

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_GetOrAdd_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately);
            InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await cache.GetOrAdd<string>(key, async () =>
                {
                    Assert.Fail("create should not run when the ambient-clock retry deadline is already in the past");
                    return ("x", null);
                }, timeout: TimeSpan.FromMilliseconds(-10)));
            StringAssert.Contains(ex.Message, "optimistic");
        }
    }

    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_AddOrUpdate_NegativeTimeout_UnderPausedClock_ExhaustsOptimisticRetryImmediately);
            InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await cache.AddOrUpdate<AtomicRefBox>(key,
                    async () =>
                    {
                        Assert.Fail("create should not run when the ambient-clock retry deadline is already in the past");
                        return (new AtomicRefBox(1), null);
                    },
                    async _ =>
                    {
                        Assert.Fail("update should not run when the ambient-clock retry deadline is already in the past");
                        return (new AtomicRefBox(2), null);
                    },
                    timeout: TimeSpan.FromMilliseconds(-10)));
            StringAssert.Contains(ex.Message, "optimistic");
        }
    }

    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_CreateThenUpdate()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_CreateThenUpdate));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_AddOrUpdate_CreateThenUpdate);
            AtomicRefBox first = await cache.AddOrUpdate<AtomicRefBox>(key,
                async () => (new AtomicRefBox(1), null),
                async _ => throw new InvalidOperationException("no existing"));
            Assert.AreEqual(1, first.Id);
            AtomicRefBox second = await cache.AddOrUpdate<AtomicRefBox>(key,
                async () => throw new InvalidOperationException("should not create"),
                async cur =>
                {
                    Assert.AreSame(first, cur);
                    return (new AtomicRefBox(2), null);
                });
            Assert.AreEqual(2, second.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_TryUpdateFailsAfterRemove_DisposesUpdatedItemThenCreate()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_TryUpdateFailsAfterRemove_DisposesUpdatedItemThenCreate));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_AddOrUpdate_TryUpdateFailsAfterRemove_DisposesUpdatedItemThenCreate);
            await cache.AddOrUpdate<DisposableCacheEntry>(key,
                async () => (new DisposableCacheEntry(1), null),
                async _ => throw new InvalidOperationException());

            DisposableCacheEntry? offeredFromUpdate = null;
            using SemaphoreSlim enteredUpdate = new(0, 1);
            using SemaphoreSlim finishUpdate = new(0, 1);

            Task<DisposableCacheEntry> slow = Task.Run(async () => await cache.AddOrUpdate<DisposableCacheEntry>(key,
                async () => (new DisposableCacheEntry(99), null),
                async cur =>
                {
                    enteredUpdate.Release();
                    await finishUpdate.WaitAsync();
                    var fresh = new DisposableCacheEntry(2);
                    offeredFromUpdate = fresh;
                    return (fresh, null);
                }));

            await enteredUpdate.WaitAsync();
            await cache.Remove<DisposableCacheEntry>(key);
            finishUpdate.Release();
            DisposableCacheEntry end = await slow;
            Assert.IsNotNull(offeredFromUpdate);
            Assert.IsTrue(offeredFromUpdate.Disposed, "TryUpdate discard should dispose the unused updated instance.");
            Assert.AreEqual(99, end.GetHashCode());
        }
    }

    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_UpdateReturnsExpired_EjectsAndRetriesCreate()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_UpdateReturnsExpired_EjectsAndRetriesCreate));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_AddOrUpdate_UpdateReturnsExpired_EjectsAndRetriesCreate);
            await cache.AddOrUpdate<DisposableCacheEntry>(key,
                async () => (new DisposableCacheEntry(1), AmbientClock.UtcNow.AddHours(1)),
                async _ => throw new InvalidOperationException());

            DisposableCacheEntry? doomed = new DisposableCacheEntry(7);
            try
            {
                int creates = 0;
                DisposableCacheEntry result = await cache.AddOrUpdate<DisposableCacheEntry>(key,
                    async () =>
                    {
                        Interlocked.Increment(ref creates);
                        return (new DisposableCacheEntry(50), null);
                    },
                    async cur =>
                    {
                        Assert.AreEqual(1, cur.GetHashCode());
                        return (doomed!, AmbientClock.UtcNow.AddTicks(-1));
                    });
                Assert.AreEqual(1, creates);
                Assert.AreEqual(50, result.GetHashCode());
                Assert.IsTrue(doomed!.Disposed);
                doomed = null;
            }
            finally
            {
                doomed?.Dispose();
            }
        }
    }

    /// <summary>
    /// Covers <see cref="IAmbientAtomicCache.AddOrUpdate{T}"/> when the stored entry is already expired:
    /// the implementation ejects it without invoking <c>update</c>, then runs <c>create</c>.
    /// </summary>
    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_CachedEntryExpiredAfterClockSkip_EjectsWithoutUpdate()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_CachedEntryExpiredAfterClockSkip_EjectsWithoutUpdate));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_AddOrUpdate_CachedEntryExpiredAfterClockSkip_EjectsWithoutUpdate);
            await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(1), AmbientClock.UtcNow.AddHours(1)));
            AmbientClock.SkipAhead(TimeSpan.FromHours(2));
            int creates = 0;
            int updates = 0;
            AtomicRefBox result = await cache.AddOrUpdate<AtomicRefBox>(key,
                async () =>
                {
                    Interlocked.Increment(ref creates);
                    return (new AtomicRefBox(200), AmbientClock.UtcNow.AddHours(1));
                },
                async _ =>
                {
                    Interlocked.Increment(ref updates);
                    return (new AtomicRefBox(999), null);
                });
            Assert.AreEqual(1, creates, "Create should run once after the expired entry is ejected.");
            Assert.AreEqual(0, updates, "Update must not run when the cached entry was already expired.");
            Assert.AreEqual(200, result.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_Remove_Idempotent()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_Remove_Idempotent));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_Remove_Idempotent);
            await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(1), null));
            await cache.Remove<AtomicRefBox>(key);
            await cache.Remove<AtomicRefBox>(key);
            AtomicRefBox again = await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(2), null));
            Assert.AreEqual(2, again.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPutGet_MinVersionAndMissing()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPutGet_MinVersionAndMissing));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedPutGet_MinVersionAndMissing);
            (AtomicRefBox? v0, long ver0) = await cache.VersionedGet<AtomicRefBox>(key);
            Assert.IsNull(v0);
            Assert.AreEqual(0, ver0);

            long r1 = await cache.VersionedPut(key, new AtomicRefBox(1), TimeSpan.FromHours(1));
            Assert.AreEqual(1, r1);
            (AtomicRefBox? v1, long ver1) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.IsNotNull(v1);
            Assert.AreEqual(1, ver1);
            Assert.AreEqual(1, v1!.Id);

            (AtomicRefBox? vTooNew, long verSeen) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: 5);
            Assert.IsNull(vTooNew);
            Assert.AreEqual(1, verSeen);

            long r2 = await cache.VersionedPut(key, new AtomicRefBox(2), TimeSpan.FromHours(1));
            Assert.AreEqual(2, r2);
            (AtomicRefBox? v2, long ver2) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: 2);
            Assert.IsNotNull(v2);
            Assert.AreEqual(2, v2!.Id);
            Assert.AreEqual(2, ver2);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_NegativeMaxDuration_DisposesAndReturnsZero()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_NegativeMaxDuration_DisposesAndReturnsZero));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            using DisposableCacheEntry d = new(1);
            long rev = await cache.VersionedPut(nameof(AtomicCache_VersionedPut_NegativeMaxDuration_DisposesAndReturnsZero), d, maxCacheDuration: TimeSpan.FromSeconds(-1));
            Assert.AreEqual(0, rev);
            Assert.IsTrue(d.Disposed);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_ExpirationInPast_DisposesAndReturnsZero()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_ExpirationInPast_DisposesAndReturnsZero));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            using DisposableCacheEntry d = new(2);
            long rev = await cache.VersionedPut(nameof(AtomicCache_VersionedPut_ExpirationInPast_DisposesAndReturnsZero), d, expiration: AmbientClock.UtcNow.AddTicks(-1));
            Assert.AreEqual(0, rev);
            Assert.IsTrue(d.Disposed);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedGet_ExpiredThenVersionedPutEarlierExpirationWins()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedGet_ExpiredThenVersionedPutEarlierExpirationWins));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedGet_ExpiredThenVersionedPutEarlierExpirationWins);
            await cache.VersionedPut(key, new AtomicRefBox(1), maxCacheDuration: TimeSpan.FromMilliseconds(30));
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            (AtomicRefBox? gone, long z) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.IsNull(gone);
            Assert.AreEqual(0, z);

            long r = await cache.VersionedPut(key, new AtomicRefBox(2), maxCacheDuration: TimeSpan.FromHours(1), expiration: AmbientClock.UtcNow.AddMinutes(10));
            Assert.IsTrue(r >= 1);
            (AtomicRefBox? ok, long ver) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.IsNotNull(ok);
            Assert.AreEqual(2, ok!.Id);
            Assert.IsTrue(ver >= r);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedGet_Refresh()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedGet_Refresh));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedGet_Refresh);
            await cache.VersionedPut(key, new AtomicRefBox(1), maxCacheDuration: TimeSpan.FromMilliseconds(80));
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
            (AtomicRefBox? v, long ver) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1, refresh: TimeSpan.FromMilliseconds(200));
            Assert.IsNotNull(v);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(150));
            (AtomicRefBox? v2, _) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: ver);
            Assert.IsNotNull(v2);
        }
    }

    [TestMethod]
    public async Task AtomicCache_UnversionedAndVersioned_SameLogicalKeyAreIndependent()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_UnversionedAndVersioned_SameLogicalKeyAreIndependent));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_UnversionedAndVersioned_SameLogicalKeyAreIndependent);
            AtomicRefBox u = await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(100), null));
            await cache.VersionedPut(key, new AtomicRefBox(200), TimeSpan.FromHours(1));
            AtomicRefBox u2 = await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(101), null));
            (AtomicRefBox? v, _) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.AreSame(u, u2);
            Assert.AreEqual(100, u.Id);
            Assert.IsNotNull(v);
            Assert.AreEqual(200, v!.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_Clear_RemovesUnversionedAndVersioned()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_Clear_RemovesUnversionedAndVersioned));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_Clear_RemovesUnversionedAndVersioned);
            await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(1), null));
            await cache.VersionedPut(key, new AtomicRefBox(2), TimeSpan.FromHours(1));
            await cache.Clear();
            AtomicRefBox u = await cache.GetOrAdd<AtomicRefBox>(key, async () => (new AtomicRefBox(3), null));
            (AtomicRefBox? v, long ver) = await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1);
            Assert.AreEqual(3, u.Id);
            Assert.IsNull(v);
            Assert.AreEqual(0, ver);
            long r = await cache.VersionedPut(key, new AtomicRefBox(4), TimeSpan.FromHours(1));
            Assert.AreEqual(1, r);
        }
    }

    [TestMethod]
    public async Task AtomicCache_EjectionPressure()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_EjectionPressure));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            for (int i = 0; i < 25; i++)
            {
                string k = nameof(AtomicCache_EjectionPressure) + i;
                await cache.GetOrAdd<AtomicRefBox>(k, async () => (new AtomicRefBox(i), AmbientClock.UtcNow.AddHours(1)));
            }
            await Eject(cache, 3);
            int remaining = 0;
            for (int i = 0; i < 25; i++)
            {
                string k = nameof(AtomicCache_EjectionPressure) + i;
                AtomicRefBox? b = await cache.GetOrAdd<AtomicRefBox>(k, async () => (new AtomicRefBox(-1), AmbientClock.UtcNow.AddHours(1)));
                if (b!.Id != -1) remaining++;
            }
            Assert.IsTrue(remaining < 25, "Ejection should remove some timed entries.");
        }
    }

    [TestMethod]
    public async Task AtomicCache_ConcurrentGetOrAdd_OneSurvivorOthersDisposed()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_ConcurrentGetOrAdd_OneSurvivorOthersDisposed));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_ConcurrentGetOrAdd_OneSurvivorOthersDisposed);
            int createCalls = 0;
            ConcurrentBag<DisposableCacheEntry> allocated = new();
            await Parallel.ForEachAsync(Enumerable.Range(0, 24), async (_, ct) =>
            {
                await cache.GetOrAdd<DisposableCacheEntry>(key, async () =>
                {
                    var d = new DisposableCacheEntry(Interlocked.Increment(ref createCalls));
                    allocated.Add(d);
                    return (d, AmbientClock.UtcNow.AddHours(1));
                });
            });
            int notDisposed = 0;
            foreach (DisposableCacheEntry d in allocated)
            {
                if (!d.Disposed) notDisposed++;
            }
            Assert.AreEqual(1, notDisposed, "Losers from TryAdd races should be disposed.");
            Assert.IsTrue(createCalls >= 1);
        }
    }

    [TestMethod]
    public async Task AtomicCache_Remove_DisposesDisposablePayload()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_Remove_DisposesDisposablePayload));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_Remove_DisposesDisposablePayload);
            DisposableCacheEntry d = await cache.GetOrAdd<DisposableCacheEntry>(key, async () => (new DisposableCacheEntry(77), null));
            Assert.IsFalse(d.Disposed);
            await cache.Remove<DisposableCacheEntry>(key);
            Assert.IsTrue(d.Disposed);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_Replace_DisposesPreviousDisposable()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_Replace_DisposesPreviousDisposable));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedPut_Replace_DisposesPreviousDisposable);
#pragma warning disable CA2000 // Ownership transfers to the cache after VersionedPut.
            DisposableCacheEntry first = new(1);
            await cache.VersionedPut(key, first, TimeSpan.FromHours(1));
            Assert.IsFalse(first.Disposed);
            DisposableCacheEntry second = new(2);
            await cache.VersionedPut(key, second, TimeSpan.FromHours(1));
#pragma warning restore CA2000
            Assert.IsTrue(first.Disposed);
            Assert.IsFalse(second.Disposed);
        }
    }

#if NET5_0_OR_GREATER
    [TestMethod]
    public async Task AtomicCache_VersionedPut_Replace_DisposesPreviousAsyncDisposableOnly()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_Replace_DisposesPreviousAsyncDisposableOnly));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedPut_Replace_DisposesPreviousAsyncDisposableOnly);
#pragma warning disable CA2000 // Ownership transfers to the cache after VersionedPut.
            AsyncDisposableOnly first = new();
            await cache.VersionedPut(key, first, TimeSpan.FromHours(1));
            Assert.IsFalse(first.AsyncDisposed);
            await cache.VersionedPut(key, new AtomicRefBox(1), TimeSpan.FromHours(1));
#pragma warning restore CA2000
            Assert.IsTrue(first.AsyncDisposed);
        }
    }
#endif

    [TestMethod]
    public async Task AtomicCache_GetOrAdd_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_GetOrAdd_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            int expiredCreates = 0;
            Task task = Task.Run(async () => await cache.GetOrAdd<AtomicRefBox>(nameof(AtomicCache_GetOrAdd_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock), async () =>
            {
                Interlocked.Increment(ref expiredCreates);
                return (new AtomicRefBox(1), FarPastUtc);
            }, timeout: TimeSpan.FromMilliseconds(150)));
            await Task.Delay(30);
            AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await task);
            Assert.IsTrue(expiredCreates >= 2);
        }
    }

    [TestMethod]
    public async Task AtomicCache_AddOrUpdate_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_AddOrUpdate_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            int expiredCreates = 0;
            Task task = Task.Run(async () => await cache.AddOrUpdate<AtomicRefBox>(nameof(AtomicCache_AddOrUpdate_TimeoutAfterRepeatExpiredCreate_UsesAmbientClock),
                async () =>
                {
                    Interlocked.Increment(ref expiredCreates);
                    return (new AtomicRefBox(1), FarPastUtc);
                },
                async _ => throw new InvalidOperationException(),
                timeout: TimeSpan.FromMilliseconds(150)));
            await Task.Delay(30);
            AmbientClock.SkipAhead(TimeSpan.FromSeconds(10));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await task);
            Assert.IsTrue(expiredCreates >= 2);
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedGet_WithTimeoutRegistersAmbientCancellation()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedGet_WithTimeoutRegistersAmbientCancellation));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            (AtomicRefBox? v, long ver) = await cache.VersionedGet<AtomicRefBox>(nameof(AtomicCache_VersionedGet_WithTimeoutRegistersAmbientCancellation), minVersion: -1, timeout: TimeSpan.FromMinutes(1));
            Assert.IsNull(v);
            Assert.AreEqual(0, ver);
        }
    }

    /// <summary>
    /// Non-positive <c>timeout</c> uses an already-cancelled CTS; when the caller supplies a cancelable token the implementation links it via
    /// <see cref="CancellationTokenSource.CreateLinkedTokenSource"/> (unlike the default token path, which uses only the immediate CTS).
    /// </summary>
    [TestMethod]
    public async Task AtomicCache_VersionedGet_NegativeTimeout_WithCancelableToken_ThrowsOperationCanceled()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedGet_NegativeTimeout_WithCancelableToken_ThrowsOperationCanceled));
        using (CancellationTokenSource cooperative = new())
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedGet_NegativeTimeout_WithCancelableToken_ThrowsOperationCanceled);
            await cache.VersionedPut(key, new AtomicRefBox(1), TimeSpan.FromHours(1));
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1, timeout: TimeSpan.FromMilliseconds(-1), cancel: cooperative.Token));
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_WithTimeoutRegistersAmbientCancellation()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_WithTimeoutRegistersAmbientCancellation));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            long rev = await cache.VersionedPut(nameof(AtomicCache_VersionedPut_WithTimeoutRegistersAmbientCancellation), new AtomicRefBox(1), TimeSpan.FromHours(1), timeout: TimeSpan.FromMinutes(1));
            Assert.AreEqual(1, rev);
        }
    }

    /// <summary>
    /// With a cancelable <see cref="CancellationToken"/> and a positive <c>timeout</c>, the cache links the caller token to an ambient timeout and
    /// disposes <c>TimeoutCleanup</c> when the operation completes (default <see cref="CancellationToken"/> skips that path).
    /// </summary>
    [TestMethod]
    public async Task AtomicCache_TimeoutWithCancelableToken_DisposesLinkedTimeoutCleanupOnEachPath()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_TimeoutWithCancelableToken_DisposesLinkedTimeoutCleanupOnEachPath));
        using (CancellationTokenSource cooperative = new())
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string baseName = nameof(AtomicCache_TimeoutWithCancelableToken_DisposesLinkedTimeoutCleanupOnEachPath);

            AtomicRefBox added = await cache.GetOrAdd<AtomicRefBox>(baseName + "g", async () => (new AtomicRefBox(1), AmbientClock.UtcNow.AddHours(1)),
                timeout: TimeSpan.FromMinutes(1), cancel: cooperative.Token);
            Assert.AreEqual(1, added.Id);

            (AtomicRefBox? miss, long ver0) = await cache.VersionedGet<AtomicRefBox>(baseName + "vMiss", minVersion: -1, timeout: TimeSpan.FromMinutes(1), cancel: cooperative.Token);
            Assert.IsNull(miss);
            Assert.AreEqual(0, ver0);

            long rev = await cache.VersionedPut(baseName + "vPut", new AtomicRefBox(2), TimeSpan.FromHours(1), timeout: TimeSpan.FromMinutes(1), cancel: cooperative.Token);
            Assert.AreEqual(1, rev);

            AtomicRefBox updated = await cache.AddOrUpdate<AtomicRefBox>(baseName + "a",
                async () => (new AtomicRefBox(3), null),
                async cur => (new AtomicRefBox(cur.Id + 10), null),
                timeout: TimeSpan.FromMinutes(1), cancel: cooperative.Token);
            Assert.AreEqual(3, updated.Id);
        }
    }

    [TestMethod]
    public async Task AtomicCache_EjectOneUntimed_StaleUntimedQueueEntryThenEvictDisposable()
    {
        AmbientSettingsOverride settings = new(AtomicUntimedEjectSettingsDictionary, nameof(AtomicCache_EjectOneUntimed_StaleUntimedQueueEntryThenEvictDisposable));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string suffix = Guid.NewGuid().ToString("N");
            string pivot = nameof(AtomicCache_EjectOneUntimed_StaleUntimedQueueEntryThenEvictDisposable) + "p" + suffix;
            DisposableCacheEntry pivotEntry = await cache.GetOrAdd<DisposableCacheEntry>(pivot, async () => (new DisposableCacheEntry(1), null));
#pragma warning disable CA2000 // On cache miss this factory runs; on hit the factory is not invoked.
            await cache.GetOrAdd<DisposableCacheEntry>(pivot, async () => (new DisposableCacheEntry(999), AmbientClock.UtcNow.AddHours(1)), refresh: TimeSpan.FromHours(1));
#pragma warning restore CA2000
            List<DisposableCacheEntry> extras = new();
            for (int i = 0; i < 14; i++)
            {
                string k = nameof(AtomicCache_EjectOneUntimed_StaleUntimedQueueEntryThenEvictDisposable) + "u" + i + suffix;
                DisposableCacheEntry e = await cache.GetOrAdd<DisposableCacheEntry>(k, async () => (new DisposableCacheEntry(200 + i), null));
                extras.Add(e);
            }
            await EjectWithFrequency(cache, 2, 40);
            int disposedExtras = extras.Count(static x => x.Disposed);
            Assert.IsTrue(disposedExtras >= 1, "Untimed LRU ejection should dispose at least one disposable extra.");
            // Pivot was refreshed to timed storage so EjectOneUntimed skips it via the stale-queue `continue` path; it may still be timed-ejected separately.
            _ = pivotEntry;
        }
    }

    [TestMethod]
    public async Task AtomicCache_EjectOneTimed_StaleTimedQueueEntryAfterRefreshThenEvictDisposable()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_EjectOneTimed_StaleTimedQueueEntryAfterRefreshThenEvictDisposable));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string suffix = Guid.NewGuid().ToString("N");
            string pivot = nameof(AtomicCache_EjectOneTimed_StaleTimedQueueEntryAfterRefreshThenEvictDisposable) + "p" + suffix;
            DateTime firstExpiration = AmbientClock.UtcNow.AddHours(1);
            DisposableCacheEntry pivotEntry = await cache.GetOrAdd<DisposableCacheEntry>(pivot, async () => (new DisposableCacheEntry(1), firstExpiration));
#pragma warning disable CA2000 // On cache miss this factory runs; on hit the factory is not invoked.
            await cache.GetOrAdd<DisposableCacheEntry>(pivot, async () => (new DisposableCacheEntry(999), AmbientClock.UtcNow.AddDays(1)), refresh: TimeSpan.FromHours(1));
#pragma warning restore CA2000
            List<DisposableCacheEntry> extras = new();
            for (int i = 0; i < 14; i++)
            {
                string k = nameof(AtomicCache_EjectOneTimed_StaleTimedQueueEntryAfterRefreshThenEvictDisposable) + "t" + i + suffix;
                DisposableCacheEntry e = await cache.GetOrAdd<DisposableCacheEntry>(k, async () => (new DisposableCacheEntry(400 + i), AmbientClock.UtcNow.AddHours(2)));
                extras.Add(e);
            }

            await Eject(cache, 30);
            int disposedExtras = extras.Count(static x => x.Disposed);
            Assert.IsTrue(disposedExtras >= 1, "Timed ejection should dispose at least one disposable extra after skipping stale timed-queue rows.");
            _ = pivotEntry;
        }
    }

    [TestMethod]
    public async Task AtomicCache_EjectOneTimed_OrphanTimedQueueEntriesAfterRemove()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_EjectOneTimed_OrphanTimedQueueEntriesAfterRemove));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string suffix = Guid.NewGuid().ToString("N");
            string a = nameof(AtomicCache_EjectOneTimed_OrphanTimedQueueEntriesAfterRemove) + "a" + suffix;
            string b = nameof(AtomicCache_EjectOneTimed_OrphanTimedQueueEntriesAfterRemove) + "b" + suffix;
            await cache.GetOrAdd<DisposableCacheEntry>(a, async () => (new DisposableCacheEntry(1), AmbientClock.UtcNow.AddHours(1)));
            await cache.GetOrAdd<DisposableCacheEntry>(b, async () => (new DisposableCacheEntry(2), AmbientClock.UtcNow.AddHours(1)));
            await cache.Remove<DisposableCacheEntry>(a);
            await cache.Remove<DisposableCacheEntry>(b);
            for (int i = 0; i < 12; i++)
            {
                string k = nameof(AtomicCache_EjectOneTimed_OrphanTimedQueueEntriesAfterRemove) + "u" + i + suffix;
                _ = await cache.GetOrAdd<DisposableCacheEntry>(k, async () => (new DisposableCacheEntry(50 + i), null));
            }

            await Eject(cache, 25);
            DisposableCacheEntry fresh = await cache.GetOrAdd<DisposableCacheEntry>(a, async () => (new DisposableCacheEntry(99), AmbientClock.UtcNow.AddHours(1)));
            Assert.AreEqual(99.GetHashCode(), fresh.GetHashCode());
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedGet_PreCancelledToken_ThrowsOperationCanceled()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedGet_PreCancelledToken_ThrowsOperationCanceled));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedGet_PreCancelledToken_ThrowsOperationCanceled);
            await cache.VersionedPut(key, new AtomicRefBox(1), TimeSpan.FromHours(1));
            using CancellationTokenSource cts = new();
            cts.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await cache.VersionedGet<AtomicRefBox>(key, minVersion: -1, cancel: cts.Token).AsTask());
        }
    }

    [TestMethod]
    public async Task AtomicCache_VersionedPut_PreCancelledToken_ThrowsOperationCanceled()
    {
        AmbientSettingsOverride settings = new(TestAtomicCacheSettingsDictionary, nameof(AtomicCache_VersionedPut_PreCancelledToken_ThrowsOperationCanceled));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string key = nameof(AtomicCache_VersionedPut_PreCancelledToken_ThrowsOperationCanceled);
            using CancellationTokenSource cts = new();
            cts.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await cache.VersionedPut(key, new AtomicRefBox(1), TimeSpan.FromHours(1), cancel: cts.Token).AsTask());
        }
    }

    private sealed class AtomicRefBox
    {
        public int Id { get; }
        public AtomicRefBox(int id) => Id = id;
        public override int GetHashCode() => Id;
        public override bool Equals(object? obj) => obj is AtomicRefBox o && o.Id == Id;
    }

#if NET5_0_OR_GREATER
    private sealed class AsyncDisposableOnly : IAsyncDisposable
    {
        public bool AsyncDisposed;
        public ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return default;
        }
    }
#endif
}
