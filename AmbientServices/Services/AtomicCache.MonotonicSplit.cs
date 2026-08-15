using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// Extension-based monotonic split-cache API for <see cref="IAmbientAtomicCache"/> on all target frameworks
/// (versioned head + unversioned payload per revision). Implemented as extensions so netstandard2.0 and similar
/// targets get the same API as modern runtimes without relying on default interface members.
/// </summary>
/// <remarks>
/// <pitch>
/// The monotonic split-cache pattern packaged as extension methods: publish a tiny versioned head plus a separately-keyed payload per revision, so readers can enforce a staleness floor by checking only the cheap head and fetch (or rebuild) the large payload at most once per revision.
/// </pitch>
/// <pledge>
/// Head and payload keys are derived deterministically from a base logical key using a reserved separator character (<see cref="MonotonicSplitCacheKeySeparator"/>), which the base key must not contain.
/// The head travels through the versioned operation family and the payload through the unversioned family keyed by the head's revision, so a payload written for one revision is never returned for another.
/// A combined read resolves the head first and touches the payload only when the head is present, unexpired, and at least the requested minimum revision; a publish writes the head first and hands back the new revision for the caller to key the payload with.
/// These methods add no storage or synchronization of their own — every behavioral guarantee is inherited from the underlying <see cref="IAmbientAtomicCache"/>.
/// </pledge>
/// <plan>
/// Pure key composition and delegation: keys are concatenated from the base key, <see cref="MonotonicSplitCacheKeySeparator"/>, and fixed marker segments, revision numbers are formatted with the invariant culture, and each method forwards to the corresponding <see cref="IAmbientAtomicCache"/> member after argument validation.
/// No state, no I/O, no locking — cost and durability are exactly those of the underlying cache, plus one extra cache round trip for the head on combined reads.
/// </plan>
/// <priority>
/// 1. Cheap staleness checks over a single round trip: a combined read resolves the small head first and touches the large payload only once the head proves present, unexpired, and recent enough — one extra round trip on every read.  A sibling storing one mutable entry would read in a single trip and is rejected because a reader could then only bound staleness by fetching the entire payload, which is the cost this pattern exists to avoid. (public)
/// 2. Adding nothing of its own over doing more: these methods compose keys and delegate — no storage, no synchronization, no retry — so every guarantee is exactly the underlying <see cref="IAmbientAtomicCache"/>'s and the pattern works on any realization of it.  The cost is that any weakness in the underlying cache passes straight through to the caller. (public)
/// </priority>
/// </remarks>
public static class AmbientAtomicSplitCacheExtensions
{
    /// <summary>
    /// Separator (ASCII unit separator) reserved for split-cache key composition. It must not appear in <c>baseLogicalKey</c>.
    /// </summary>
    public const char MonotonicSplitCacheKeySeparator = '\u001f';

    /// <summary>
    /// Logical key passed to <see cref="IAmbientAtomicCache.VersionedGet{T}"/> / <see cref="IAmbientAtomicCache.VersionedPut{T}"/> for the split-cache head.
    /// </summary>
    public static string GetMonotonicSplitCacheHeadKey(string baseLogicalKey)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(baseLogicalKey);
#else
        if (baseLogicalKey is null) throw new ArgumentNullException(nameof(baseLogicalKey));
#endif
        return baseLogicalKey + MonotonicSplitCacheKeySeparator + "ambient.split.head" + MonotonicSplitCacheKeySeparator;
    }

    /// <summary>
    /// Logical key passed to <see cref="IAmbientAtomicCache.GetOrAdd{T}"/> / <see cref="IAmbientAtomicCache.Remove{T}"/> for the split-cache payload at <paramref name="headVersion"/>.
    /// </summary>
    public static string GetMonotonicSplitCachePayloadKey(string baseLogicalKey, long headVersion)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(baseLogicalKey);
#else
        if (baseLogicalKey is null) throw new ArgumentNullException(nameof(baseLogicalKey));
#endif
        return baseLogicalKey + MonotonicSplitCacheKeySeparator + "ambient.split.payload" + MonotonicSplitCacheKeySeparator + headVersion.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the split-cache versioned head only (step one of a two-step read).
    /// </summary>
    public static ValueTask<(THead? Head, long HeadVersion)> MonotonicSplitCacheGetHeadAsync<THead>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long minHeadVersion = -1,
        TimeSpan? headRefresh = null,
        TimeSpan? headTimeout = null,
        CancellationToken cancel = default)
        where THead : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
#endif
        return cache.VersionedGet<THead>(GetMonotonicSplitCacheHeadKey(baseLogicalKey), minHeadVersion, headRefresh, headTimeout, cancel);
    }

    /// <summary>
    /// Writes a new split-cache head revision (step one of a publish). Returns the new monotonic version to use for <see cref="GetMonotonicSplitCachePayloadKey"/>.
    /// </summary>
    public static ValueTask<long> MonotonicSplitCachePutHeadAsync<THead>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        THead head,
        TimeSpan? maxCacheDuration = null,
        DateTime? expiration = null,
        TimeSpan? timeout = null,
        CancellationToken cancel = default)
        where THead : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(head);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (head is null) throw new ArgumentNullException(nameof(head));
#endif
        return cache.VersionedPut(GetMonotonicSplitCacheHeadKey(baseLogicalKey), head, maxCacheDuration, expiration, timeout, cancel);
    }

    /// <summary>
    /// Resolves or creates the split-cache payload for a known <paramref name="headVersion"/> (step two of a read/write).
    /// </summary>
    public static ValueTask<TPayload> MonotonicSplitCacheGetOrAddPayloadAsync<TPayload>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long headVersion,
        Func<ValueTask<(TPayload Item, DateTime? Expires)>> create,
        TimeSpan? payloadRefresh = null,
        TimeSpan? payloadTimeout = null,
        CancellationToken cancel = default)
        where TPayload : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(create);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (create is null) throw new ArgumentNullException(nameof(create));
#endif
        return cache.GetOrAdd(GetMonotonicSplitCachePayloadKey(baseLogicalKey, headVersion), create, payloadRefresh, payloadTimeout, cancel);
    }

    /// <summary>
    /// Two-step read: versioned head (staleness via <paramref name="minHeadVersion"/>), then unversioned payload for that head revision.
    /// When the head is missing, expired, or rejected as too old for <paramref name="minHeadVersion"/>, the payload is not loaded and <c>Head</c> is null.
    /// </summary>
    public static async ValueTask<(THead? Head, TPayload? Payload, long HeadVersion)> MonotonicSplitCacheGetHeadAndPayloadAsync<THead, TPayload>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long minHeadVersion,
        Func<ValueTask<(TPayload Item, DateTime? Expires)>> getOrCreatePayload,
        TimeSpan? headRefresh = null,
        TimeSpan? headTimeout = null,
        TimeSpan? payloadRefresh = null,
        TimeSpan? payloadTimeout = null,
        CancellationToken cancel = default)
        where THead : class
        where TPayload : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(getOrCreatePayload);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (getOrCreatePayload is null) throw new ArgumentNullException(nameof(getOrCreatePayload));
#endif
        (THead? head, long v) = await cache.MonotonicSplitCacheGetHeadAsync<THead>(baseLogicalKey, minHeadVersion, headRefresh, headTimeout, cancel);
        if (head is null)
        {
            return (null, null, v);
        }

        TPayload payload = await cache.MonotonicSplitCacheGetOrAddPayloadAsync(baseLogicalKey, v, getOrCreatePayload, payloadRefresh, payloadTimeout, cancel);
        return (head, payload, v);
    }
}
