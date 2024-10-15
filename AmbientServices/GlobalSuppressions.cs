using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.HighPerformanceFifoTaskScheduler.GetProcessorCount~System.Int32")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.TraceBuffer.TraceBufferBackgoundFlusher")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.GlobalServiceReference`1.DefaultImplementation~`0")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.ConsoleBuffer.ConsoleBufferBackgoundFlusher")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.AmbientFileLogger.#ctor(System.String,System.String,System.Int32,System.Int32)")]

// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
