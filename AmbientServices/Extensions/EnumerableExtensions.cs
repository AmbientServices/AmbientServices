using System.Collections.Generic;
using System.Linq;

namespace AmbientServices.Extensions;

/// <summary>
/// A class with extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
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
