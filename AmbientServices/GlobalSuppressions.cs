using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.HighPerformanceFifoTaskScheduler.GetProcessorCount~System.Int32")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.TraceBuffer.TraceBufferBackgoundFlusher")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.GlobalServiceReference`1.DefaultImplementation~`0")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.ConsoleBuffer.ConsoleBufferBackgoundFlusher")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is terrible advice, especially for systems that need to run persistently", Scope = "member", Target = "~M:AmbientServices.AmbientFileLogger.#ctor(System.String,System.String,System.Int32,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "Primary constructors are great for records.  For classes, not so much.  One tiny little change and you're typing for 5 minutes to recover.", Scope = "member", Target = "~M:AmbientServices.BasicAmbientAtomicCache.CacheEntry.#ctor(System.String,System.Nullable{System.DateTime},System.Object,System.Boolean,System.Int64)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "Primary constructors are great for records.  For classes, not so much.  One tiny little change and you're typing for 5 minutes to recover.", Scope = "member", Target = "~M:AmbientServices.BasicAmbientAtomicCache.#ctor(AmbientServices.IAmbientSettingsSet)")]

// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
