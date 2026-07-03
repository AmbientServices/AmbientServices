using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// Registry of per-caller regex filters used to mask sensitive field names in structured log output.
/// Multiple unrelated assemblies may each register filters; all active filters are applied when rendering log data.
/// </summary>
/// <remarks>
/// <pitch>Process-wide scrubbing of secrets from structured logs: register a field-name pattern ("password", "token", …) once and every structured entry rendered anywhere in the process masks matching values — each assembly can protect its own fields without coordinating with the others.</pitch>
/// <pledge>
/// Filters match field names, never values, and a match replaces the value with a fixed mask string.  Registrations are independent: disposing one never affects filters registered by other callers, and disposal is idempotent.  Null or empty field names never match.
/// Registration, disposal, and mask checks are thread-safe and take effect immediately; each check consults the set of filters registered at that moment.
/// </pledge>
/// <plan>A <see cref="ConcurrentDictionary{TKey, TValue}"/> of <see cref="Regex"/>es keyed by an <see cref="Interlocked"/>-incremented id, with each registration's <see cref="IDisposable"/> removing only its own id.  A mask check scans every registered regex linearly, so per-field cost grows with the number of registered filters — fine for the expected handful; callers with many patterns should combine them into one regex.</plan>
/// </remarks>
public static class AmbientLogSensitiveFieldFilters
{
    /// <summary>
    /// The string substituted for values whose field names match a registered sensitive-field pattern.
    /// </summary>
    public const string MaskedValue = "***";

    private static int _nextRegistrationId;
    private static readonly ConcurrentDictionary<int, Regex> _filters = new();

    /// <summary>
    /// Registers a regex that matches sensitive log field names (property or dictionary keys).
    /// </summary>
    /// <remarks>
    /// Unregister by disposing the returned <see cref="IDisposable"/> (for example with <c>using</c> or by calling <see cref="IDisposable.Dispose"/>).
    /// Each caller should keep and dispose its own registration; disposing one registration does not affect filters registered by other callers.
    /// </remarks>
    /// <param name="fieldNamePattern">A regex matched against field names (not values).</param>
    /// <returns>An <see cref="IDisposable"/> that removes this filter when disposed.</returns>
    public static IDisposable RegisterFieldNameFilter(Regex fieldNamePattern)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fieldNamePattern);
#else
        if (fieldNamePattern is null) throw new ArgumentNullException(nameof(fieldNamePattern));
#endif
        int id = Interlocked.Increment(ref _nextRegistrationId);
        _filters[id] = fieldNamePattern;
        return new Registration(id);
    }

    /// <summary>
    /// Registers a regex that matches sensitive log field names (property or dictionary keys). Dispose the returned instance to unregister.
    /// </summary>
    /// <param name="pattern">The regex pattern string.</param>
    /// <param name="options">Regex options. Defaults to <see cref="RegexOptions.IgnoreCase"/> | <see cref="RegexOptions.CultureInvariant"/>.</param>
    /// <returns>An <see cref="IDisposable"/> that removes this filter when disposed.</returns>
    public static IDisposable RegisterFieldNameFilter(string pattern, RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(pattern);
#else
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
#endif
        return RegisterFieldNameFilter(new Regex(pattern, options));
    }

    /// <summary>
    /// Returns whether <paramref name="fieldName"/> matches any currently registered sensitive-field filter.
    /// </summary>
    public static bool ShouldMaskFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;
        foreach (Regex filter in _filters.Values)
        {
            if (filter.IsMatch(fieldName)) return true;
        }
        return false;
    }

    /// <summary>
    /// Masks <paramref name="value"/> when <paramref name="fieldName"/> matches a registered filter; otherwise returns the original value.
    /// </summary>
    public static object? MaskValueIfSensitive(string fieldName, object? value)
    {
        return ShouldMaskFieldName(fieldName) ? MaskedValue : value;
    }

    /// <summary>
    /// Returns a snapshot of the currently registered filters (for diagnostics and tests).
    /// </summary>
    public static IReadOnlyCollection<Regex> GetRegisteredFilters()
    {
        return _filters.Values.ToArray();
    }

    private sealed class Registration : IDisposable
    {
        private readonly int _id;
        private int _disposed;

        public Registration(int id) => _id = id;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _ = _filters.TryRemove(_id, out _);
        }
    }
}
