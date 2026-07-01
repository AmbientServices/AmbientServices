using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 20, Scope = ExecutionScope.MethodLevel)]
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
