using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace ReflectionTypeLoadExceptionAssembly
{
    public class ImpossibleToLoadClass
    {
#pragma warning disable CS0626  // this function is specifically to CAUSE the issue being warned about
        public static extern void ImpossibleToLoadFunction();
#pragma warning restore CS0626 
    }
}
