using AmbientServices.Utilities;
using System;

namespace AmbientServices.Extensions;

/// <summary>
/// A static class that extends <see cref="System.Array"/>.
/// </summary>
/// <remarks>
/// <pitch>Value semantics for arrays: compare two arrays by their contents and hash an array by its contents, for use in equality implementations or as dictionary keys where reference identity is the wrong notion of sameness.</pitch>
/// <pledge>Equality is deep: element order is significant, and elements that are themselves arrays are compared by value recursively (so jagged arrays compare by content).  The hash code mixes each element's own hash with its position (so arrays with the same elements in a different order generally hash differently) and recurses into nested arrays exactly as <see cref="ValueEquals{TYPE}(TYPE[], TYPE[])"/> does, so value-equal arrays — including jagged ones — produce equal hash codes, as long as the leaf (non-array) element type's own <see cref="object.GetHashCode"/> is value-based.</pledge>
/// </remarks>
public static class ArrayExtensions
{
    /// <summary>
    /// Compares two arrays of arbitrary type to see if the contents are the same.
    /// </summary>
    /// <typeparam name="TYPE">The type of item in the array.</typeparam>
    /// <param name="array1">The first array to compare.</param>
    /// <param name="array2">The second array to compare.</param>
    /// <returns><b>true</b> if the contents of the arrays are the same, <b>false</b> if the contents of the arrays are different.</returns>
    public static bool ValueEquals<TYPE>(this TYPE[] array1, TYPE[] array2)
    {
        return ArrayUtilities.ValueEquals(typeof(TYPE), array1, array2);
    }
    /// <summary>
    /// Gets a hash code for the array based on the value of the array.
    /// </summary>
    /// <typeparam name="TYPE">The type of item in the array.</typeparam>
    /// <param name="array">The array to get the value hash code for.</param>
    /// <returns>A hash code based on the values in the array.</returns>
    public static int ValueHashCode<TYPE>(this TYPE[] array)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(array);
#else
        if (array is null) throw new ArgumentNullException(nameof(array));
#endif
        // delegate to the shared engine so the hash recurses into nested arrays exactly where ValueEquals does (jagged arrays hash by content)
        return ArrayUtilities.ValueHashCode(typeof(TYPE), array);
    }
}
