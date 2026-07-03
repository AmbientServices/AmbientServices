using System.Collections.Generic;
using System.Linq;

namespace AmbientServices.Extensions;

/// <summary>
/// A class with extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
/// <remarks>
/// <pitch>Nullable-annotation ergonomics for LINQ: filter the nulls out of an enumeration of nullable items and get back an enumeration typed as non-nullable, without a cast or a warning suppression at every call site.</pitch>
/// </remarks>
public static class EnumerableExtensions
{
    /// <summary>
    /// Filters null items out of an enumerable.
    /// </summary>
    /// <typeparam name="T">The type of item in the enumerable.</typeparam>
    /// <param name="nullableEnum">The nullable enumeration.</param>
    /// <returns>An enumeration with all the null values removed.</returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> nullableEnum)
    {
        return nullableEnum.Where(v => v != null)!; // we've explicitly checked for nulls, so we can suppress the warning
    }
}
