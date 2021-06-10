using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A static class that holds extensions to the system <see cref="Enum"/> class.
    /// </summary>
    internal static class EnumExtensions
    {
        /// <summary>
        /// Returns the highest possible value for an enum.
        /// </summary>
        /// <typeparam name="T">The enum to get the maximum value for.</typeparam>
        /// <returns>The highest enum value.</returns>
        public static T MaxEnumValue<T>() where T : System.Enum
        {
            return EnumMax<T>.Max;  
        }
    }
    /// <summary>
    /// A static class that holds onto the computed max enum value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    static class EnumMax<T> where T : System.Enum
    {
        private static T _Max = Init();
        static T Init()
        {
            Array a = Enum.GetValues(typeof(T))!;   // I don't think it's possible to have a System.Enum for which Enum.GetValues returns null
            return (a.Length == 0)
                ? default!                          // apparently the compiler isn't smart enough to know that even though System.Enum is a class, any derived types are value types
                : a.Cast<T>().Max()!;
        }
        public static T Max {  get { return _Max; } }
    }
}
