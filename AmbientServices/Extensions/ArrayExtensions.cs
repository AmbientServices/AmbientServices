using AmbientServices.Utilities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace AmbientServices.Extensions
{
    /// <summary>
    /// A static class that extends <see cref="System.Array"/>.
    /// </summary>
    internal static class ArrayExtensions
    {
        /// <summary>
        /// Compares two arrays of arbitrary type to see if the contents are the same.
        /// </summary>
        /// <typeparam name="TYPE">The type of item in the arary.</typeparam>
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
            if (array == null) throw new ArgumentNullException(nameof(array));
            int code = array.Length;
            // loop through each element and compare
            for (int offset = 0; offset < array.Length; ++offset)
            {
                int elemhashcode = array[offset]?.GetHashCode() ?? 0;   // Note here that even though TYPE is not TYPE?, it is nullable because we haven't added a notnull generic type contraint (where TYPE: notnull)
                code ^= (elemhashcode >> (32 - (offset % 32)) ^ (elemhashcode << (offset % 32)) ^ 0x1A7FCA3B);
            }
            return code;
        }
    }
}
