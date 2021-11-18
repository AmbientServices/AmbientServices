# Overview
AmbientServices is a .NET library that provides abstractions for services which are both ubiquitous and optional, allowing assemblies that use it to be used in a variety of systems that provide vastly different implementations (or no implementation) of those services.

## Basic Services
The basic ambient services include caching, clock, logging, progress/cancellation, and settings.  Interfaces for those services are provided here.
By accessing these services through the interfaces provided here, library authors can utilize new basic services as they become available without changing their external interface, and library consumers can use those libraries without having to provide dependencies for systems that they may or may not use.
If consumers want the added benefits provided by a more complicated implementation of one or more of those services, they can provide a bridge to their own implementations of these basic services and register them with the AmbientServices service.
With one simple registration, the services will automatically be utilized by every library that uses AmbientServices.

The well known dependency injection pattern is one possible solution to this problem, but dependency injection requires the code consumer to pass the required dependencies to each object constructor (or worse, each function), which can be cumbersome.  When the functionality is optional anyway, this can be more work than it's worth, especially when you're just trying to get things up and running quickly.
Dependency injection becomes even more cumbersome when the assembly being used adds or removes service dependencies, requiring the consumer to update every constructor invocation with the new dependencies.
Dependency injection still makes sense for services that are required, but when services are optional anyway, AmbientServices is a simpler option.

By convention, AmbientServices should not be used for information that alters the outputs of functions that use it in any way that the caller might care about.  Side-effects should either not alter the relationship between inputs and outputs at all, or should not alter them unexpectedly.  

For example, logging and performance tracking should never alter function outputs at all.  Caching may affect the output, but only by giving results that are slightly stale, and only in cases where there are already hidden inputs (like a database) anyway.  Some functions may measure the passage of time during processing and might record that information or change their outputs based on the duration of time passed, but callers should not be surprised when the passage of time is slower or faster than their expected "normal."  Settings (often stored in a configuration file) can alter the output of a function, but never in a way that the caller is concerned about.  In fact, the very concept of settings is in reality a type of parameter intended to affect functions without requiring the caller to be concerned with their specific values.  Progress tracking and cancellation may be useful for the caller, but never affects the output of the function other than aborting its processing altogether.

## Performance Services
Advanced ambient services provide detailed system performance monitoring.

There are three primary questions that may be answered through performance monitoring.  
    1. How well are the various systems functioning under how much load?  This question may be answered using AmbientStatistics that track the usage, performance, and effectiveness of major system functions.
    2. Why did an operation take as long as it did?  This question may be answered using AmbientServiceProfiler to track which operations delayed the response by how much.
    3. How close is the system to maxing out?  This question may be answered using AmbientBottleneckDetector to track saturation of possible system bottlenecks so you can determine scalability even before load testing.

By using these services, with very little overhead, you can easily track how various parts of your system are performing all the time, not just when you run it with a code profiler.
You can also expose this across the network to roll-up this data throughout the whole system, even up through the client.

## Status
The status system enables periodic background testing of backend systems, with summarization of overall process status and across both heterogenous and homogenous server farms.
Status tests are automatically detected based on class inheritance and constructor signature, but tests only run after the system is explicitly started.


## Getting Started
In Visual Studio, use Manage Nuget Packages and search nuget.org for AmbientServices to add a package reference for this library.

For .NET Core environments, use:
`dotnet add package https://www.nuget.org/packages/AmbientServices/`


# Service Descriptions

## AmbientCache
The ambient cache interface abstracts a very simple cache of the type that is universally applicable.  Some items are cached for a specific amount of time, others are cached indefinitely.  Items cached temporarily may have their expiration time extended or shortened each time they are retrieved or updated.  Both types of items may expire from the cache at any time according to cache limits and/or memory capacity.  Items may be removed from the cache manually at any time.  

In order to prevent unexpected alteration of outputs, care must be taken to ensure that cached items are based entierly on the inputs.  For functions that are not "pure" (database queries for example), the results should always be based entirely on the inputs and either the current state of the database or some previous state (when it uses cached results).  For example, if the cache key does not contain all the inputs identifying the item being cached, completely different results could be obtained depending on the order in which calls to the cache were made.  This is true of all caches and naturally every cache user and implementor understands that this type of usage is erroneous and must be avoided.

### Helpers
The `AmbientCache<TOWNER>` generic class provides a wrapper of the ambient cache that attaches the owner type name as a prefix for each cache key to prevent cross-class cache key conflicts, and ignores calls when there is no ambient cache or it has been suppressed.

### Settings
BasicAmbientCache-EjectFrequency: the number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.  Default is 100.
BasicAmbientCache-ItemCount: the maximum number of both timed and untimed items to allow in the cache before ejecting items.  Default is 1000.

### Sample
[//]: # (AmbientCacheSample)
```csharp
/// <summary>
/// A user manager class that shows how the caching ambient service might be used.
/// </summary>
class UserManager
{
    private static readonly AmbientCache<UserManager> Cache = new AmbientCache<UserManager>();

    /// <summary>
    /// Finds the user with the specified emali address.
    /// </summary>
    /// <param name="email">The emali address for the user.</param>
    /// <returns>The <see cref="User"/>, if one was found, or null if the user was not found.</returns>
    public static async Task<User?> FindUser(string email)
    {
        string userKey = nameof(User) + "-" + email;
        User? user = await Cache.Retrieve<User>(userKey, TimeSpan.FromMinutes(15));
        if (user != null)
        {
            user = User.Find(email);
            if (user != null) await Cache.Store<User>(false, userKey, user, TimeSpan.FromMinutes(15)); else await Cache.Remove<User>(false, userKey);
        }
        return user;
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async Task CreateUser(User user)
    {
        string userKey = nameof(User) + "-" + user.Email;
        user.Create();
        await Cache.Store<User>(false, userKey, user, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async Task UpdateUser(User user)
    {
        string userKey = nameof(User) + "-" + user.Email;
        user.Update();
        await Cache.Store<User>(false, userKey, user, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Deletes the specified user.
    /// </summary>
    /// <param name="email">The email of the user to be deleted.</param>
    public static async Task DeleteUser(string email)
    {
        string userKey = nameof(User) + "-" + email;
        User.Delete(email);
        await Cache.Remove<User>(false, userKey);
    }
}
```
### Default Implementation
The default implementation has a small local-only cache using a very simple implementation.

## AmbientLogger
The ambient logger interface abstracts a simple logging system of the type that is universally applicable.  The logger simply receives strings to log and flushes them when called.

Logging should never be used in a way that affects control flow or results.  The only side-effect should be transparent to the caller.  Every user and implementor should understand this implied part of the logging interface contract.  In order to do this, care should be taken to ensure that when using logging functions that use lambdas to avoid generating log messages until after the logging system checks to see if the message would be filtered, those lambdas must not have any side effects.

### Helpers
The `AmbientLogger<TOWNER>` generic class provides a wrapper of the ambient cache that attaches the owner type, a severity level, and a category to each message and filters them according to settings from the ambient or specified settings.  Overloads that take a message-generating lambda are also provided.  These overloads should be used when generating the log message from the provided input data is expensive and the caller wants to avoid that expense when the message is going to be filtered anyway.

### Settings
`AmbientLogger-Format`: A format string that controls what entries in the log look like where {0} is the entry time, {1} is the level, {2} is the log owner type, {3} is the category, and {4} is the message.  Default is {0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}.
`AmbientLogFilter-LogLevel`: The AmbientLogLevel above which logs will be filtered (entries at this level and below will be logged).  Default is Information.
`AmbientLogFilter-TypeAllow`: A regular expression indicating which type(s) are allowed to be logged.  Default is null, meaning all types are allowed.
`AmbientLogFilter-TypeBlock`: A regular expression indicating which type(s) should be blocked from being logged.  Default is null, meaning no types should be blocked.
`AmbientLogFilter-CategoryAllow`: A regular expression indicating which categorie(s) are allowed to be logged.  Default is null, meaning all categories are allowed.
`AmbientLogFilter-CategoryBlock`: A regular expression indicating which categorie(s) should be blocked from being logged.  Default is null, meaning no categories should be blocked.
Blocking is applied after allowing, so if a type or category matches both expressions, it will be blocked.

### Sample
[//]: # (AmbientLoggerSample)
```csharp
/// <summary>
/// A static class with extensions methods used to log various assembly events.
/// </summary>
public static class AssemblyLoggingExtensions
{
    private static readonly AmbientLogger<Assembly> Logger = new AmbientLogger<Assembly>();

    /// <summary>
    /// Log that the assembly was loaded.
    /// </summary>
    /// <param name="assembly">The assembly that was loaded.</param>
    public static void LogLoaded(this Assembly assembly)
    {
        Logger.Log("AssemblyLoaded: " + assembly.FullName, "Lifetime", AmbientLogLevel.Trace);
    }
    /// <summary>
    /// Logs that there was an assembly load exception.
    /// </summary>
    /// <param name="assembly">The <see cref="AssemblyName"/> for the assembly that failed to load.</param>
    /// <param name="ex">The <see cref="Exception"/> that occured during the failed load.</param>
    /// <param name="operation">The operation that needed the assembly.</param>
    public static void LogLoadException(this AssemblyName assemblyName, Exception ex, string operation)
    {
        Logger.Log("Error loading assembly " + assemblyName.FullName + " while attempting to perform operation " + operation, ex, "Lifetime");
    }
    /// <summary>
    /// Logs that an assembly was scanned.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> that was scanned.</param>
    public static void LogScanned(this Assembly assembly)
    {
        Logger.Log("Assembly " + assembly.FullName + " scanned", "Scan", AmbientLogLevel.Trace);
    }
}
```
### Default Implementation
The default implementation asynchronously buffers the log messages and flushes them in batches out to the System Diagnostics Trace (which would slow code dramatically if each log message was written synchronously).
An alternate implementation, `AmbientFileLogger` logs messages to a daily rotating set of files at a location specified in the constructor.

## AmbientProgress
The ambient progress interface abstracts a simple context-following progress tracker of the type that is universally applicable.  Progress tracking tracks the proportion of an operation that has completed processing and the item currently being processed and provides easy aggregation of subprocess progress.  The ambient context is checked for cancellation each time the progress is updated or parts are started or completed.

Progress tracking should never affect control flow or results, except in the event of a cancellation, in which case there are no functional results.  Naturally both consumers and services should avoid any usage or implementation to the contrary.

### Helpers
The `AmbientProgressService` static class provides easy access to the local and global `IAmbientProgress`.
The `AmbientCancellationTokenSource` class is a superset of the framework's `CancellationTokenSource` that can raise cancellation using an ambient clock.

### Settings
There are no settings for this service.

### Sample
[//]: # (AmbientProgressSample)
```csharp
/// <summary>
/// A class that downloads and unzips a zip package.
/// </summary>
class DownloadAndUnzip
{
    private readonly string _targetFolder;
    private readonly string _downlaodUrl;
    private readonly MemoryStream _package;

    public DownloadAndUnzip(string targetFolder, string downloadUrl)
    {
        _targetFolder = targetFolder;
        _downlaodUrl = downloadUrl;
        _package = new MemoryStream();
    }

    public async Task MainOperation(CancellationToken cancel = default(CancellationToken))
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        using (progress?.TrackPart(0.01f, 0.75f, "Download "))
        {
            await Download();
        }
        using (progress?.TrackPart(0.75f, 0.99f, "Unzip "))
        {
            await Unzip();
        }
    }
    public async Task Download()
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        CancellationToken cancel = progress?.CancellationToken ?? default(CancellationToken);
        HttpWebRequest request = HttpWebRequest.CreateHttp(_downlaodUrl);
        using (WebResponse response = request.GetResponse())
        {
            long totalBytesRead = 0;
            int bytesRead;
            long totalBytes = response.ContentLength;
            byte[] buffer = new byte[8192];
            using (Stream downloadReader = response.GetResponseStream())
            {
                while ((bytesRead = await downloadReader.ReadAsync(buffer, 0, buffer.Length, cancel)) != 0)
                {
                    await _package.WriteAsync(buffer, 0, bytesRead, cancel);
                    totalBytesRead += bytesRead;
                    progress?.Update(totalBytesRead * 1.0f / totalBytes);
                }
            }
        }
    }
    public Task Unzip()
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        CancellationToken cancel = progress?.CancellationToken ?? default(CancellationToken);

        ZipArchive archive = new ZipArchive(_package);
        int entries = archive.Entries.Count;
        for (int entry = 0; entry < entries; ++entry)
        {
            ZipArchiveEntry archiveEntry = archive.Entries[entry];
            // update the progress
            progress?.Update(entry * 1.0f / entries, archiveEntry.FullName);
            archiveEntry.ExtractToFile(Path.Combine(_targetFolder, archiveEntry.FullName));
        }
        return Task.CompletedTask;
    }
}
```
### Default Implementation
The default implementation tracks progress and provides access to the data, but does not output the progress information anywhere.

## AmbientClock

The ambient clock interface abstracts a system clock.  Artificial clock control can be important in testing, especially to efficiently and quickly exercise timeout conditions and to avoid timeouts when tests run under heavy CPU load (as you would ususally want them to run in order to get through them as quickly as possible).  Ideally, the underlying platform would provide some kind of thread or execution-context-specific clock for use by timeout logic, but unfortunatly most platforms do not provide this functionality.  This service provides that missing functionality, at least for the purposes of testing.

Clocks, of course, are generally counter to the goals of purely functional programming, and even in imperative programming, it makes sense that functions that aren't obviously time-dependent should not have their outputs unexpectedly affected by the clock.  One such acceptable usage is logging with timestamps.  Another acceptable usage is timeouts.  For all programs, clocks could indirectly appear to be frozen if the CPU is unexpectedly fast or the system clock has an unexpectedly low resolution.  Correspondingly, clocks could appear to skip ahead if the system CPU is overloaded and the thread doesn't get scheduled or if the system goes to sleep or hibernates and then later resumes.  The artificial clock AmbientClock provides simply allows an upstream service consumer to simulate those conditions for both unit and integration testing purposes.  These are important edge cases to test for systems that need a high degree of reliability and graceful degredation.  

Clocks should never go backwards.  Ambient clock service implementors must ensure this holds true.

### Helpers

The `AmbientClock` static class provides an abstraction that automatically uses the system clock if there is no registered clock service.  It also provides a `Pause` function that allows the caller to temporarily pause time as seen by the ambient clock.  The `SkipAhead` function allows the caller to move the paused clock forward (ignored if the clock is not paused).  `AmbientClock` can also issue an `AmbientCancellationToken` that is cancelled by the ambient clock service.
The `AmbientStopwatch` class provides a time measuring class similar to the framework's `Stopwatch` class, but pauses when the ambient clock is paused.
The `AmbientTimer` class provides a callback similar to the framework's `Timer` class, but follows the ambient clock.

### Usage
Converting a project to use `AmbientClock` begins with changing all references to `DateTime.UtcNow` to `AmbientClock.UtcNow`, `Stopwatch` to `AmbientStopwatch`, and `System.Timers.Timer` to `AmbientTimer`.  This should not affect anything at all.  Next, in unit tests that are sensitive to timing, add the following code:
```
using (AmbientClock.Pause())
{

    // beginning of test here--no time will appear to pass while this code executes

    // move the ambient clock ahead such that it will appear to the system that exactly 100ms has passed
    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));

    // more test code here--no time will appear to pass while this code executes, but 100ms will appear to have elapsed since the first part of the code ran

}
```

### Sample
[//]: # (AmbientClockSample)
```csharp
/// <summary>
/// A test for TimeDependentService.
/// </summary>
[TestClass]
public class TimeDependentServiceTest
{
    [TestMethod]
    public async Task TestCancellation()
    {
        // this first part *should* get cancelled because we're using the system clock
        AmbientCancellationTokenSource cts = new AmbientCancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => AsyncFunctionThatShouldCancelAfterOneSecond(cts.Token));

        // switch the current call context to the artifically-paused ambient clock and try again
        using (AmbientClock.Pause())
        {
            AmbientCancellationTokenSource cts2 = new AmbientCancellationTokenSource(TimeSpan.FromSeconds(1));
            // this should *not* throw because the clock has been paused
            await AsyncFunctionThatShouldCancelAfterOneSecond(cts2.Token);

            // this skips the artifical paused clock ahead, raising the cancellation
            AmbientClock.SkipAhead(TimeSpan.FromSeconds(1));
            // make sure the cancellation got raised
            Assert.ThrowsException<OperationCanceledException>(() => cts2.Token.ThrowIfCancellationRequested());
        }
    }
    private async Task AsyncFunctionThatShouldCancelAfterOneSecond(CancellationToken cancel)
    {
        for (int loop = 0; loop < 20; ++loop)
        {
            await Task.Delay(100);
            cancel.ThrowIfCancellationRequested();
        }
    }
    [TestMethod]
    public async Task TestCodeThatCouldTimeoutUnderHeavyLoad()
    {
        using (AmbientClock.Pause())
        {
            AmbientCancellationTokenSource cts = new AmbientCancellationTokenSource(TimeSpan.FromSeconds(1));
            await AsyncFunctionThatCouldTimeoutUnderHeavyLoad(cts.Token);
        }
    }
    private async Task AsyncFunctionThatCouldTimeoutUnderHeavyLoad(CancellationToken cancel)
    {
        AmbientStopwatch stopwatch = new AmbientStopwatch(true);
        for (int count = 0; count < 9; ++count)
        {
            await Task.Delay(100);
            cancel.ThrowIfCancellationRequested();
        }
        // if we finished before getting cancelled, we must have been scheduled within about 10 milliseconds on average, or we must be running using a paused ambient clock
    }
}
```
### Default Implementation
There is no default implementation, but an implementation intended for use as a local override is provided.  The lack of default implementation causes the helper classes to use the system clock.

## AmbientSettings
The ambient settings interface abstracts a simple string-based settings set.  Each setting has a value identified by a unique string.  The value of the setting is always a string, but each setting may be converted to a desired type by specifying a delegate that converts the string into the desired strongly-typed value.
Often the value for a setting may change on the fly, so the value exposed by the helper class might change after initialization.  Users can also subscribe to an event that notifies them when the value for a setting changes, in case they need to do something more complicated than just parsing the new value.  Value change event notifications may arrive asynchronously on any thread at any time, so users must not depend on the notification occurring before they get an updated value.  
A call-context-specific override can be used for some settings, but of course no change notifications can occur when the value changes due to setting a call-context-local settings set or changes of the value within a call-context-local settings set (where would the notification go?).

Settings set implementations may or may not provide post-initialization settings value updates but if they do, they should also raise the notifications.

Among other things, the ambient settings system is designed to provide sensible access to settings and notification of changes during system startup and shutdown.  
For example, at the beginning of startup, the settings just use default values.  
At some point, the global settings set can be replaced with a settings set implementatoin that reads from a local configuration, and then later on with an implementation that reads settings from a centralized settings store.  
Users of settings don't need to bother with knowing where the settings come from, only that they might change during system startup.  
This is especially useful for things like logging.  
Errors that occur before the location of shared logs is determined (that location might be stored in a central database) can be stored in the event log or local file system as desired.  
Once the centralized settings are hooked up, logging can automatically switch to a remote log store indicated in the centralized settings store.  
No centralized (and often complicated) "startup" code is required for this kind of transition, just a subscription to the change event for a log configuration setting.
Most code can (and usuall should) use the default ambient settings set, which will automatically transition from basic settings sets implementations to more complicated ones as initialization progresses and more complicated implementations become available for use.

Settings by their very nature must be considered inputs for the purposes of functional programming.  
They are by definition not passed on the stack (otherwise, they're just insanely-overpopulated collections of parameters someone decided to call "settings").

### Helpers
The `IAmbientSetting<T>` generic helper interface provides access to a type-converted setting and an event to notify subscribers when the setting value changes.
The `AmbientSettings` static class is used to construct an `IAmbientSetting<T>` for the caller.  
Settings provided by `AmbientSettings` can be "settings set" settings whose value comes from an explicit settings set specified during construction, or "ambient" settings whose value comes from the default ambient settings set (even if there is a local override in the call-context when the value is retrieved).

### Settings
There are no settings for this service.

### Sample
[//]: # (AmbientSettingsSample)
```csharp
/// <summary>
/// A class that manages a pool of buffers.
/// </summary>
class BufferPool
{
    private static readonly IAmbientSetting<int> MaxTotalBufferBytes = AmbientSettings.GetAmbientSetting<int>(nameof(BufferPool) + "-MaxTotalBytes", "The maximum total number of bytes to use for all the allocated buffers.  The default value is 1MB.", s => Int32.Parse(s), "1000000");
    private static readonly IAmbientSetting<int> DefaultBufferBytes = AmbientSettings.GetAmbientSetting<int>(nameof(BufferPool) + "-BufferBytes", "The number of bytes to allocate for each buffer.  The default value is 8000 bytes.", s => Int32.Parse(s), "8000");

    private SizedBufferRecycler _recycler;  // interlocked

    class SizedBufferRecycler
    {
        private readonly int _bufferBytes;
        private readonly ConcurrentBag<byte[]> _bag;

        public SizedBufferRecycler(int bufferBytes)
        {
            _bufferBytes = bufferBytes;
            _bag = new ConcurrentBag<byte[]>();
        }
        public int BufferBytes { get { return _bufferBytes; } }
        public byte[] GetBuffer(int bytes)
        {
            if (bytes < _bufferBytes)
            {
                byte[]? buffer;
                if (_bag.TryTake(out buffer))
                {
                    return buffer;
                }
            }
            return new byte[Math.Max(bytes, _bufferBytes)];
        }
        public void Recycle(byte[] buffer)
        {
            if (buffer.Length == _bufferBytes && _bag.Count * _bufferBytes < MaxTotalBufferBytes.Value)
            {
                _bag.Add(buffer);
            }
            // else just let the garbage collector release this byte array
        }
    }

    /// <summary>
    /// Constructs a buffer pool using the ambient settings.
    /// </summary>
    public BufferPool()
    {
        _recycler = new SizedBufferRecycler(DefaultBufferBytes.Value);
    }

    /// <summary>
    /// Gets a buffer from the recycling pool.
    /// </summary>
    /// <param name="minimumByteSize">The minimum buffer size.</param>
    /// <returns>A buffer with at least as many bytes as specified.</returns>
    public byte[] GetBuffer(int minimumByteSize)
    {
        SizedBufferRecycler recycler = _recycler;
        return recycler.GetBuffer(minimumByteSize);
    }
    /// <summary>
    /// Recycles a previously-retrieved buffer by putting it back into the pool, if that's where it belongs.
    /// </summary>
    /// <param name="buffer">The buffer to return to the pool</param>
    public void RecycleBuffer(byte[] buffer)
    {
        SizedBufferRecycler recycler = _recycler;
        recycler.Recycle(buffer);
    }
}
```
### Default Implementation
The default implementation just uses a local initally-empty ConcurrentDictionary to keep track of settings values, so the default settings values will be used unless the default settings set is altered.  
An alternate interface, `IMutableAmbientSettings`, extends `IAmbientSettingsSet` and adds methods to change the settings values in this implementation.  
Other service implementations may or may not support changing settings values and may or may not support this interface to do so.  
The simplicity of this abstraction is due to the wide variety of settings systems available and the fact that nearly all use cases can be handled using this abstraction.  
Since the interface is only one function, implementing a bridge to Configuration.AppSettings or some other more appropriate settings repository is very simple.

# Customizing Ambient Services

## Implementing A New Ambient Service
[//]: # (CustomAmbientServiceSample)
```csharp
/// <summary>
/// An interface that abstracts a simple ambient call stack tracking service.
/// </summary>
public interface IAmbientCallStack
{
    /// <summary>
    /// Creates a call stack scope for the specified fuction name, keeping it on the stack until it is disposed.
    /// </summary>
    /// <param name="function">The name of the function being executed.</param>
    /// <returns>A disposable object that will remove the function at the end of its execution scope.</returns>
    IDisposable Scope(string function);
    /// <summary>
    /// Gets an enumeration of the call stack entries created by previous calls to <see cref="Scope"/> that haven't yet been disposed.
    /// </summary>
    IEnumerable<string> Entries { get; }
}

/// <summary>
/// A basic implementation of <see cref="IAmbientCallStack"/>.
/// A few enhancements could make these call stacks accessible remotely, which could be very handy for diagnosing what servers are busy doing.
/// </summary>
[DefaultAmbientService]
class BasicAmbientCallStack : IAmbientCallStack
{
    static private AsyncLocal<ImmutableStack<string>> Stack = new AsyncLocal<ImmutableStack<string>>();

    static private ImmutableStack<string> GetStack()
    {
        ImmutableStack<string>? stack = Stack.Value;
        if (stack == null)
        {
            stack = ImmutableStack<string>.Empty;
            Stack.Value = stack;
        }
        return stack;
    }

    public IDisposable Scope(string entry)
    {
        ImmutableStack<string> stack = GetStack();
        stack = stack.Push(entry);
        Stack.Value = stack;
        return new CallStackEntry(stack);
    }

    public IEnumerable<string> Entries { get { return GetStack(); } }

    class CallStackEntry : IDisposable
    {
        private ImmutableStack<string>? _stack;

        public CallStackEntry(ImmutableStack<string> stack)
        {
            _stack = stack;
        }

        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_stack != null)
                    {
                        Stack.Value = _stack.Pop();
                        _stack = null;
                    }
                }
                _disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
```

## Disabling An Ambient Service
[//]: # (DisableAmbientServiceSample)
```csharp
/// <summary>
/// A sample setup class that disables the cache implementation when it is initialized.
/// </summary>
class Setup
{
    private static readonly AmbientService<IAmbientCache> Cache = Ambient.GetService<IAmbientCache>();
    static Setup()
    {
        Cache.Global = null;
    }
}
```

## Overriding An Ambient Service Globally
[//]: # (OverrideAmbientServiceGlobalSample)
```csharp
/// <summary>
/// An application setup class that registers an implementation of <see cref="IAmbientSettingsSet"/> that uses <see cref="Configuration.AppSettings"/> for the settings as the ambient service.
/// </summary>
class SetupApplication
{
    static SetupApplication()
    {
        AmbientService<IAmbientSettingsSet> SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
        SettingsSet.Global = new AppConfigAmbientSettings();
    }
}
/// <summary>
/// An implementation of <see cref="IAmbientSettingsSet"/> that uses <see cref="Configuration.AppSettings"/> as the backend settings store.
/// </summary>
class AppConfigAmbientSettings : IAmbientSettingsSet
{
    public string SetName => "AppConfig";

    public string? GetRawValue(string key)
    {
        return System.Configuration.ConfigurationManager.AppSettings[key];
    }
    public object? GetTypedValue(string key)
    {
        string? rawValue = System.Configuration.ConfigurationManager.AppSettings[key];
        IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue ?? "") : rawValue;
    }
}
```

## Overriding An Ambient Service Locally From A Call Context
[//]: # (OverrideAmbientServiceLocalSample)
```csharp
/// <summary>
/// An implementation of <see cref="IAmbientSettingsSet"/> that overrides specific settings.
/// </summary>
class LocalAmbientSettingsOverride : IAmbientSettingsSet, IDisposable
{
    private static readonly AmbientService<IAmbientSettingsSet> SettingsSet = Ambient.GetService<IAmbientSettingsSet>();

    private readonly IAmbientSettingsSet? _oldSettingsSet;
    private readonly Dictionary<string, string> _overrides;

    /// <summary>
    /// For the life of this instance, overrides the settings in the specified dictionary with their corresponding values.
    /// </summary>
    /// <param name="overrides">A Dictionary containing the key/value pairs to override.</param>
    public LocalAmbientSettingsOverride(Dictionary<string, string> overrides)
    {
        _oldSettingsSet = SettingsSet.Local;
        SettingsSet.Override = this;
        _overrides = new Dictionary<string, string>();
    }

    public string SetName => nameof(LocalAmbientSettingsOverride);

    /// <summary>
    /// Disposes of this instance, returning the ambient settings to their former value.
    /// </summary>
    public void Dispose()
    {
        SettingsSet.Override = _oldSettingsSet;
    }

    public string? GetRawValue(string key)
    {
        string? value;
        if (_overrides.TryGetValue(key, out value))
        {
            return value;
        }
        return _oldSettingsSet?.GetRawValue(key);
    }
    public object? GetTypedValue(string key)
    {
        string? rawValue = GetRawValue(key);
        IAmbientSettingInfo? ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue ?? "") : rawValue;
    }
}
```


## AmbientStatistics
The `IAmbientStatistics` interface abstracts the creation and gathering of statistics.  
Each statistic keeps track of the measurement of one aspect of system performance, using a single number that holds an accumulated, minimum, maximum, or raw value.
Statistics can be used to track memory allocated, time waited, minimum or maximum sizes or times, cache hits and misses, etc.
Each statistic can be incremented or decremented, added-to, set to a new raw value, or conditionally set if it is a new minimum or maximum value.
Ratios of two statistics or their changes can be used to track things like average sizes or times, events per second, bytes per second, cache hit ratios, etc.
A statistic named `"ExecutionTime"` is defined by the system and holds the number of ticks elapsed since the process started.
Ticks are in terms of the standard system Stopwatch.Frequency.  All operations are lock-free.  `Min` and `Max` statistics use an optimistic atomic update loop.

### Helpers
The `IAmbientStatisticReader` interface provides read access to an individual statistic.
The `IAmbientStatistic` interface extends `IAmbientStatisticReader` interface and adds functions to update the value for the statistic.

### Sample
[//]: # (AmbientStatisticsSample)
```csharp
/// <summary>
/// A class that represents a type of request.
/// </summary>
public class RequestType
{
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();

    private readonly IAmbientStatistic? _pendingRequests;
    private readonly IAmbientStatistic? _totalRequests;
    private readonly IAmbientStatistic? _totalProcessingTime;
    private readonly IAmbientStatistic? _retries;
    private readonly IAmbientStatistic? _failures;
    private readonly IAmbientStatistic? _timeouts;

    /// <summary>
    /// Constructs a RequestType with the specified type name.
    /// </summary>
    /// <param name="typeName">The name of the request type.</param>
    public RequestType(string typeName)
    {
        IAmbientStatistics? ambientStatistics = AmbientStatistics.Local;
        _pendingRequests = ambientStatistics?.GetOrAddStatistic(false, typeName + "-RequestsPending", "The number of requests currently executing", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
        _totalRequests = ambientStatistics?.GetOrAddStatistic(false, typeName + "-TotalRequests", "The total number of requests that have finished executing", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
        _totalProcessingTime = ambientStatistics?.GetOrAddStatistic(true, typeName + "-TotalProcessingTime", "The total time spent processing requests (only includes completed requests)", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
        _retries = ambientStatistics?.GetOrAddStatistic(false, typeName + "-Retries", "The total number of retries", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
        _failures = ambientStatistics?.GetOrAddStatistic(false, typeName + "-Failures", "The total number of failures", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
        _timeouts = ambientStatistics?.GetOrAddStatistic(false, typeName + "-Timeouts", "The total number of timeouts", false, 0, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Sum, AggregationTypes.Sum, MissingSampleHandling.LinearEstimation);
    }
    /// <summary>
    /// Tracks a request by creating a <see cref="RequestTracker"/> which automatically counts the request and times its duration and allows the caller to report failures, timeouts, and retries.
    /// </summary>
    /// <returns>A <see cref="RequestTracker"/> instance that should be disposed when the request finishes processing.</returns>
    public RequestTracker TrackRequest()
    {
        return new RequestTracker(this);
    }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the number of pending requests.
    /// </summary>
    public IAmbientStatistic? PendingRequests { get { return _pendingRequests; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of requests.
    /// </summary>
    public IAmbientStatistic? TotalRequests { get { return _totalRequests; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total processing time.
    /// </summary>
    public IAmbientStatistic? TotalProcessingTime { get { return _totalProcessingTime; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of retries.
    /// </summary>
    public IAmbientStatistic? Retries { get { return _retries; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of failures.
    /// </summary>
    public IAmbientStatistic? Failures { get { return _failures; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of timeouts.
    /// </summary>
    public IAmbientStatistic? Timeouts { get { return _timeouts; } }
}
/// <summary>
/// A request tracking object.
/// </summary>
public class RequestTracker : IDisposable
{
    private readonly RequestType _requestType;
    private readonly AmbientStopwatch _stopwatch;
    private bool _disposedValue;

    internal RequestTracker(RequestType requestType)
    {
        _requestType = requestType;
        _stopwatch = new AmbientStopwatch(true);
        requestType.PendingRequests?.Increment();
    }

    /// <summary>
    /// Reports a failure during the processing of the request.
    /// </summary>
    public void ReportFailure()
    {
        _requestType.Failures?.Increment();
    }
    /// <summary>
    /// Reports a timeout during the processing of the request.
    /// </summary>
    public void ReportTimeout()
    {
        _requestType.Timeouts?.Increment();
    }
    /// <summary>
    /// Reports a retry during the processing of the request.
    /// </summary>
    public void ReportRetry()
    {
        _requestType.Retries?.Increment();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _requestType.PendingRequests?.Add(-1);
                _requestType.TotalRequests?.Increment();
                _requestType.TotalProcessingTime?.Add(_stopwatch.ElapsedTicks);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~RequestTracker()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    /// <summary>
    /// Disposes of the RequestTracker, decrementing the pending request count and adding the time to the total time statistic.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// A static class to report statistics in XML format.
/// </summary>
public static class StatisticsReporter
{
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    /// <summary>
    /// Writes all statistics with their current values to the specified <see cref="XmlWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> to write the data to.</param>
    public static void ToXml(XmlWriter writer)
    {
        writer.WriteStartElement("statistics");
        foreach (IAmbientStatisticReader statistic in AmbientStatistics.Local?.Statistics.Values ?? Array.Empty<IAmbientStatisticReader>())
        {
            writer.WriteStartElement("statistic");
            writer.WriteAttributeString("id", statistic.Id);
            writer.WriteAttributeString("value", statistic.SampleValue.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }
}
```

### Default Implementation
The default implementation uses thread-safe lock-free statistics instances, keeping all the information associated with each statistic.

## AmbientServiceProfiler
The `AmbientServiceProfiler` interface abstracts a low-overhead service profiler with performance designed for always-on course-grained profiling.  
This profiling can be used to determine how the time for a request, program, or time window was used.
The code being profiled calls into the `IAmbientServiceProfiler` each time the system that is executing switches (only one system can be active per call context at a time).
A system identifier contains a main system name followed by various subsystem and result identifiers (of course results aren't available until the next system begins executing, so the profiler allows the service to update the system identifier after execution completes).
The consumer of the service profiler may want to ignore some or all of the susbsystem and result parts of the identifier and can do so using the system group transform setting, which is a regular expression that matches only the desired pieces of the identifier, causing statistics from one or more subsystems and/or results to be grouped together.
For example, a system identifier might be `SQL/Database:My-database/Table:User/Result:Failed`.
The fully-detailed system identifier would allow the service profile consumer to distinguish how much time was spent waiting for SQL results that failed from those that timed out or succeeded, and those from one database and/or table from another.
This level of information is usually too-detailed, so the consumer may want to group everything to just the top-level system, in which case, all SQL access, no matter which database or table, and no matter whether the operation was successful, timed out, or threw an exception, would all be grouped into a single profile entry.
When no other system is executing, the service should set the system identifier to the empty string, which will also be tracked.
Some systems may allow tracking of CPU time, so that could be another system identifier.
As of .NET 5, it does not provide any way to track this, so the consumer can assume that the empty string system accounts for any remaining CPU time.
Of course, this estimate will be wildly incorrect if the service, while running under the empty string system, calls something that blocks execution (such as waiting for a mutex or performing IO), or when the system CPU is high enough that available threads don't get scheduled.

### Helpers
The `AmbientServiceProfilerCoordinator` allows users to create service profilers for various contexts, including the current call context, rotating time windows of a given time span, or the process as a whole.
The call context profiler and process-wide profiler implement the `IAmbientServiceProfile` interface, and the time window profiler calls an async delegate with an instance of that interface, each contains the profile for the context it came from.
`IAmbientServiceProfile` provides access to a scope name and and enumeration of `AmbientServiceProfilerAccumulator` instance, each of which has the statistics for a given system or system group.

### Settings
`AmbientServiceProfilerCoordinator-DefaultSystemGroupTransform`: A `Regex` string used to transform the system identifier to a group identifier.
The regular expression will attempt to match the system identifier, with the values for any matching match groups being concatenated into the system group identifier.

### Sample
[//]: # (AmbientServiceProfilerSample)
```csharp
/// <summary>
/// A class that access a SQL database and reports profiling information to the system profiling system.
/// </summary>
class SqlAccessor
{
    private static readonly AmbientService<IAmbientServiceProfiler> ServiceProfiler = Ambient.GetService<IAmbientServiceProfiler>();

    private readonly string _connectionString;
    private readonly SqlConnection _connection;
    private readonly string _systemIdPrefix;

    /// <summary>
    /// Creates a SQL accessor for the specified connection string.
    /// </summary>
    /// <param name="connectionString">A connection string with information on how to connect to a SQL Server database.</param>
    public SqlAccessor(string connectionString)
    {
        _connectionString = connectionString;
        _connection = new SqlConnection(connectionString);
        _systemIdPrefix = $"SQL/Server:{_connection.DataSource}/Database:{_connection.Database}";
    }

    /// <summary>
    /// Creates a <see cref="SqlCommand"/> that uses this connection.
    /// </summary>
    /// <returns>A <see cref="SqlCommand"/> for this connection.</returns>
    public SqlCommand CreateCommand() { return _connection.CreateCommand(); }

    private async Task<T> ExecuteAsync<T>(SqlCommand command, Func<CancellationToken, Task<T>> f, string? table = null, CancellationToken cancel = default(CancellationToken))
    {
        string systemId = _systemIdPrefix + (string.IsNullOrEmpty(table) ? "" : $"/Table:{table}");
        T ret;
        try
        {
            ServiceProfiler.Local?.SwitchSystem(systemId);
            ret = await f(cancel);
            systemId = systemId + $"/Result:Success";
        }
        catch (Exception e)
        {
            if (e.Message.ToUpperInvariant().Contains("TIMEOUT")) systemId = systemId + $"/Result:Timeout";
            else systemId = systemId + $"/Result:Error";
            throw;
        }
        finally
        {
            ServiceProfiler.Local?.SwitchSystem("", systemId);
        }
        return ret;
    }

    public Task<int> ExecuteNonQueryAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string? table = null)
    {
        return ExecuteAsync<int>(command, command.ExecuteNonQueryAsync, table, cancel);
    }
    public Task<SqlDataReader> ExecuteReaderAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string? table = null)
    {
        return ExecuteAsync<SqlDataReader>(command, command.ExecuteReaderAsync, table, cancel);
    }
    public Task<object> ExecuteScalarAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string? table = null)
    {
        return ExecuteAsync<object>(command, command.ExecuteScalarAsync, table, cancel);
    }
    public Task<XmlReader> ExecuteXmlReaderAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string? table = null)
    {
        return ExecuteAsync<XmlReader>(command, command.ExecuteXmlReaderAsync, table, cancel);
    }
}
/// <summary>
/// A class that collects bottleneck statistics and reports on them.
/// </summary>
class ProfileReporter
{
    private AmbientBottleneckSurveyorCoordinator _surveyor = new AmbientBottleneckSurveyorCoordinator();
    private Dictionary<string, long>? _mostRecentWindowServiceProfile;  // interlocked
    private AmbientServiceProfilerCoordinator _coordinator;
    private IDisposable? _timeWindow;
    /// <summary>
    /// Constructs a Bottleneck reporter that holds onto the top ten utilized bottlenecks for the entire process for the previous one-minute window.
    /// </summary>
    public ProfileReporter()
    {
        _coordinator = new AmbientServiceProfilerCoordinator();
        _timeWindow = _coordinator.CreateTimeWindowProfiler(nameof(ProfileReporter), TimeSpan.FromMilliseconds(100), OnMostRecentWindowClosed);
    }

    private Task OnMostRecentWindowClosed(IAmbientServiceProfile profile)
    {
        Dictionary<string, long> serviceProfile = new Dictionary<string, long>();
        foreach (AmbientServiceProfilerAccumulator record in profile.ProfilerStatistics)
        {
            serviceProfile.Add(record.Group, record.TotalStopwatchTicksUsed);
        }
        System.Threading.Interlocked.Exchange(ref _mostRecentWindowServiceProfile, serviceProfile);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a dictionary containing the service profile for the most recent time window.
    /// </summary>
    public Dictionary<string, long>? RecentProfile
    {
        get
        {
            return _mostRecentWindowServiceProfile;
        }
    }
}
```

### Default Implementation
The default implementation uses thread-safe lock-free instances.  
Each system switch is transformed according to the setting and then distributed to all the profilers the switch applies to.

## AmbientBottleneckDetector
The `IAmbientBottleneckDetector` interface provides access to a function to measure access to a bottleneck and events that the are used to track usage over time.  
The gathered data can be used to determine how close that part of the system is to maxing-out, so that scalability limits can be more accurately estimated.  

### Helpers
An instance of the `AmbientBottleneck` class is used to represent each bottleneck in the system.
Each bottleneck has a unique string identifier, a description, an algorithm indicating how blocking occurs, and an optional limit and time window for that limit.
When code enters the bottleneck, it calls `EnterBottleneck` on the `AmbientBottleneck` instance.
This function returns an `AmbientBottleneckAccessor` instance which scopes access to the bottleneck.
The Automatic property on `AmbientBottleneck` identifies whether or not the timing of the scope of the `AmbientBottleneckAccessor` instance automatically sets the bottleneck usage, or whether the usage is set manually using `SetUsage` and/or `AddUsage` on the `AmbientBottleneckAccessor` instance.
Note that bottlenecks will sometimes overlap, such that multiple bottlenecks have been entered at the same time, but users of the system should be sure that if the bottlenecks are associated with exclusive access, such as a mutex, that in order to avoid deadlock, entry to such bottlenecks should be strictly ordered.

The `AmbientBottleneckSurveyorCoordinator` class provides access to surveyors for various contexts such as the current call context, the entire process, the current thread, and/or a rotating time window.
The surveyor coordinator collects the bottleneck usage events and distributes them to each of the applicable surveyors that have been created so they can track access within their context and provide survey results.

### Settings
`AmbientBottleneckSurveyorCoordinator-DefaultAllow`: A `Regex` string used to match bottleneck identifiers that should be tracked.  By default, all bottlenecks are allowed.
`AmbientBottleneckSurveyorCoordinator-DefaultBlock`: A `Regex` string used to match bottleneck identifiers that should NOT be tracked.  By default, no bottlenecks are blocked.
Blocking is applied after allowing, so if a bottleneck matches both expressions, it will be blocked.

### Sample
[//]: # (AmbientBottleneckDetectorSample)
```csharp
/// <summary>
/// A class that holds a thread-safe queue which reports on the associated bottleneck.
/// </summary>
class GlobalQueue
{
    private static Mutex Mutex = new Mutex(false);
    private static Queue<object> Queue = new Queue<object>();
    private static readonly AmbientBottleneck GlobalQueueBottleneck = new AmbientBottleneck("GlobalQueue-Access", AmbientBottleneckUtilizationAlgorithm.Linear, true, "A bottleneck which only allows one accessor at a time.");

    /// <summary>
    /// Adds a new item to the queue.
    /// </summary>
    /// <param name="o">The object to add to the queue.</param>
    public static void Enqueue(object o)
    {
        try
        {
            Mutex.WaitOne();
            using (GlobalQueueBottleneck.EnterBottleneck())
            {
                Queue.Enqueue(o);
            }
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }
    /// <summary>
    /// Removes the oldest item from the queue.
    /// </summary>
    /// <returns>The oldest item in the queue.</returns>
    /// <exception cref="InvalidOperationException">If the queue is empty.</exception>
    public static object Dequeue()
    {
        try
        {
            Mutex.WaitOne();
            using (GlobalQueueBottleneck.EnterBottleneck())
            {
                return Queue.Dequeue();
            }
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }
}
/// <summary>
/// A class that access an EBS volume and reports on the associated bottleneck.
/// </summary>
class EbsAccessor
{
    private const int IopsPageSize = 16 * 1024;

    private readonly string _volumePrefix;
    private readonly AmbientBottleneck _bottleneck = new AmbientBottleneck("Ebs-Iops", AmbientBottleneckUtilizationAlgorithm.Linear, false, "A bottleneck which has a limit, but which is not based on access time.", 1000, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Creates an EBS accessor for the specified volume.
    /// </summary>
    /// <param name="volumePrefix">The volume prefix, which will be prefixed onto <paramref name="ReadBytes.volumePrefix"/>"/> whenever a file is read from this volume.</param>
    public EbsAccessor(string volumePrefix)
    {
        _volumePrefix = volumePrefix;
    }

    /// <summary>
    /// Reads bytes from the specified location in the specified file.
    /// </summary>
    /// <param name="file">The file path (relative to the volume prefix specified in the constructor.</param>
    /// <param name="fileOffset">The byte offset in the file where the read is to start.</param>
    /// <param name="buffer">A buffer to put the data into.</param>
    /// <param name="bufferOffset">The offset within the buffer where the read bytes should be placed.</param>
    /// <param name="bytes">The number of bytes to attempt to read.</param>
    /// <returns>The number of bytes that were actually read.</returns>
    public int ReadBytes(string file, long fileOffset, byte[] buffer, int bufferOffset, int bytes)
    {
        string filePath = Path.Combine(_volumePrefix, file);
        int bytesRead;
        using (AmbientBottleneckAccessor? access = _bottleneck.EnterBottleneck())
        {
            using (FileStream f = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                f.Position = fileOffset;
                bytesRead = f.Read(buffer, bufferOffset, bytes);
            }
            access?.SetUsage(1, (bytesRead + IopsPageSize - 1) / IopsPageSize); // note that this approximation of IOPS won't be correct if the file is fragmented, and the lookup and opening of the file will likely use some IOPS as well--more accurate estmates can be obtained after long-term usage and comparison to AWS metrics
        }
        return bytesRead;
    }
}
/// <summary>
/// A class that collects bottleneck statistics and reports on them.
/// </summary>
class BottleneckReporter
{
    private AmbientBottleneckSurveyorCoordinator _surveyor = new AmbientBottleneckSurveyorCoordinator();
    private Dictionary<string, double>? _mostRecentWindowTopBottlenecks;  // interlocked
    private IDisposable _timeWindow;

    /// <summary>
    /// Constructs a Bottleneck reporter that holds onto the top ten utilized bottlenecks for the entire process for the previous one-minute window.
    /// </summary>
    public BottleneckReporter()
    {
        _surveyor = new AmbientBottleneckSurveyorCoordinator();
        _timeWindow = _surveyor.CreateTimeWindowSurveyor(TimeSpan.FromSeconds(60), OnMostRecentWindowClosed);
    }

    private Task OnMostRecentWindowClosed(IAmbientBottleneckSurvey analysis)
    {
        Dictionary<string, double> mostRecentWindowTopBottlenecks = new Dictionary<string, double>();
        foreach (AmbientBottleneckAccessor record in analysis.GetMostUtilizedBottlenecks(10))
        {
            mostRecentWindowTopBottlenecks.Add(record.Bottleneck.Id, record.Utilization);
        }
        System.Threading.Interlocked.Exchange(ref _mostRecentWindowTopBottlenecks, mostRecentWindowTopBottlenecks);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a dictionary containing the top 10 bottlenecks with their overall utilization for the most recent time window.
    /// </summary>
    public Dictionary<string, double>? RecentBottleneckSummary
    {
        get
        {
            return _mostRecentWindowTopBottlenecks;
        }
    }
}
```

### Default Implementation
The default implementation uses thread-safe lock-free instances.  
In order to effectively users must strike a balance between conservative estimates of bottleneck saturation vs. having only inaccurate top bottlenecks in summaries.


## Status
The `Status` classes enable systems with automated background backend dependency status testing and aggregation of test results to generate concise status summary reports across backend systems and across server farms.
Some backend systems contain static status information which is gathered by a class that inherits from the abstract `StatusChecker` class, others need to be tested periodically.
The status for these systems is gathered by a class that inherits from the abstract `StatusAuditor` class.
StatusChecker and StatusAuditor classes with empty public constructors are automatically detected, constructed, and added to the global list of checkers.
Lazy construction and registration of status checkers is generally not a good idea, as detecting status issues as soon as possible during startup is preferable to detecting them only when the backend systems get initialized.
Even so, such classes with non-empty or non-public constructors can be registered manually if desired.
The status of each system is rated with a `StatusRating` floting-point number.
Systems are rated in one of four ranges, `StatusRating.Fail`, `StatusRating.Alert`, `StatusRating.Okay`, and `StatusRating.Superlative`.
Status rating numbers less than or equal to zero indicate that the corresponding system is in some degree of failure.
Status rating numbers greater than zero and less than or equal to one indicate that the corresponding system is not failing, but is in a state that needs attention such that a system administrator should be alerted.
Status rating numbers greater than one and less than or equal to two indicate that the corresponding system is working and no attention is needed.
Status rating numbers greater than two indicate that the corresponding system is "superlative", ie. better than just okay.
Systems that have not been tested yet are given a value of NaN.
Ratings may be much worse (less) than the threshold for Fail, and much better (higher) than the threshold for Okay, but the status system doesn't distinguish between values less than failure or greater than okay.

System status is indicated by a hierarchy of `StatusResults` objects, each containing the following properties:
1. The source system (a string indicating the system that performed the audit).
2. The target system (a slash-delimited string identifying the subsystem whose status is indicated).
3. A `DateTime` indicating when the information was gathered.
4. A list of key-value properties.
5. Either a list of child nodes, along with a `StatusNatureOfSystem` indicating how the children are related to the parent and/or each other so that the system can automatically aggregate results, or
6. A `StatusAuditReport` containing the following information about the audit:
    1. A `DateTime` indicating when the audit started.
    2. A `TimeSpan` indicating how long the audit took.
    3. An optional `DateTime` indicating when the next audit will happen.
    4. An optional `StatusAuditAlert` containing the following properties about an alert (if there is one):
       1. A status rating number indicating the health of the system.
       2. An alert audit code (a short string that is constant across alerts of this type).
       3. A string containing a terse description of the problem, suitable for a plaintext SMS message.
       4. A string containing a detailed description of the problem, suitable for email, web, or mobile application display.

A summary across backend systems, or a full rollup across an entire server farm may also be generated.
Such a summary will collate results based primarily on the target system (because that is almost always the way problems are detected), secondarily on the alert code.
If some source systems are reporting issues and others are not, the summary will indicate the sources reporting each status range.
When the rating is due to properties falling outside configured thresholds, the reported property value ranges will also be indicated.
The summarization system is designed to provide the relevant information for operations staff as concisely as possible in both SMS and detailed form.
For example, an SMS status alert might look like the following: 
```
🛑 FAIL @2:37 AM
 AWS
  RDS: [2]->Timeout
  S3: [2]->Read Timeout
  S3: [2]->Write Timeout
  S3: [2]->Query Timeout
⚠️ ALERT
 AWS
  ES: [3]->Slow Response
```

Note that the timestamp at the top is to help the receiver know when message delivery was significantly delayed (this happens more than you might think, and can be very disconcerting when alerts come in hours after the actual incident).
In this example, /AWS would be the target name of a node that contains children for each of the systems within AWS (RDS, S3, and ES in this case).
The leading slash indicates that AWS is a top-level target, so targets specified in any parent nodes should be ignored.
RDS would be the target name of the node (for each source) that contains the failure information about RDS.
[2] indicates the number of sources reporting the same alert code.
When only one source reports an issue, the full source is indicated.

The details will contain the corresponding detailed information, and a list of sources instead of just a count.
The source is usually applied not by that system, but by the system gathering the results across the server farm.
This is because when there are multiple levels of servers using the system, the system directly doing the testing may or may not be the source that system operators want to be reported.

The timing of audits is determined algorithmically, but will always be between one tenth and four times the baseline audit frequency specified in the constructor.
The time until the next audit is a function of the baseline audit frequency, the rating, and the duration of the previous audit.
As status ratings degrade and audits speed up, the frequency is increased towards one tenth the baseline frequency.
This algorithm prevents slow audits from consuming too many resources, but also speeds up recovery detection when possible.

### Settings
`StatusChecker-HistoryRetentionMinutes`: The maximum number of minutes to retain old `StatusResults`.  
`StatusChecker-HistoryRetentionEntries`: The maximum number of old `StatusResults` entries to retain.

### Sample
[//]: # (StatusSample)
```csharp
/// <summary>
/// A class that audits a specific drive.
/// </summary>
class DiskAuditor
{
    private readonly DriveInfo _driveInfo;
    private readonly string _testPath;
    private readonly bool _readonly;

    /// <summary>
    /// Constructs a disk auditor.
    /// </summary>
    /// <param name="driveName">The name of the drive to be audited.</param>
    /// <param name="testPath">A path within the drive to be used for testing.</param>
    /// <param name="readOnly">Whether or not the test should be readonly. (The program may not have write access to some paths and still want to audit them for space usage and reading).</param>
    public DiskAuditor(string driveName, string testPath, bool readOnly)
    {
        _driveInfo = new DriveInfo(driveName);
        _testPath = testPath;
        _readonly = readOnly;
    }
    /// <summary>
    /// Performs the disk audit, reporting results into <paramref name="statusBuilder"/>.
    /// </summary>
    /// <param name="statusBuilder">A <see cref="StatusResultsBuilder"/> to write the results into.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public async Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
    {
        statusBuilder.NatureOfSystem = StatusNatureOfSystem.Leaf;
        statusBuilder.AddProperty("_Path", _driveInfo.Name);
        statusBuilder.AddProperty("_VolumeLabel", _driveInfo.VolumeLabel);
        statusBuilder.AddProperty("DriveFormat", _driveInfo.DriveFormat);
        statusBuilder.AddProperty("DriveType", _driveInfo.DriveType);
        statusBuilder.AddProperty("AvailableFreeBytes", _driveInfo.AvailableFreeSpace);
        statusBuilder.AddProperty("TotalFreeBytes", _driveInfo.TotalFreeSpace);
        statusBuilder.AddProperty("TotalBytes", _driveInfo.TotalSize);
        if (!string.IsNullOrEmpty(_testPath))
        {
            if (_readonly)
            {
                StatusResultsBuilder readBuilder = new StatusResultsBuilder("Read");
                statusBuilder.AddChild(readBuilder);
                try
                {
                    int attempt = 0;
                    // attempt to read a file (if one exists).  note that under Linux, some files in the temp path may be inaccessible in such a way as to timeout attempting to open even as few as ten of them.  this is probably a flaw in the .NET Core implementation on Linux.
                    foreach (string file in Directory.EnumerateFiles(Path.Combine(_driveInfo.RootDirectory.FullName, _testPath)))
                    {
                        AmbientStopwatch s = AmbientStopwatch.StartNew();
                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                byte[] b = new byte[1];
                                await fs.ReadAsync(b, 0, 1, cancel);
                                await fs.FlushAsync();
                            }
                            readBuilder.AddProperty("ResponseMs", s.ElapsedMilliseconds);
                            readBuilder.AddOkay("Ok", "Success", "The read operation succeeded.");
                            break;
                        }
                        catch (IOException) // this will be thrown if the file cannot be accessed because it is open exclusively by another process (this happens a lot with temp files)
                        {
                            // only attempt to read up to 10 files
                            if (++attempt > 10) throw;
                            // just move on and try the next file
                            continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    readBuilder.AddException(e);
                }
            }
            else
            {
                StatusResultsBuilder writeBuilder = new StatusResultsBuilder("Write");
                StatusResultsBuilder readBuilder = new StatusResultsBuilder("Read");
                statusBuilder.AddChild(writeBuilder);
                statusBuilder.AddChild(readBuilder);
                // attempt to write a temporary file
                string targetPath = Path.Combine(_driveInfo.RootDirectory.FullName, Guid.NewGuid().ToString("N"));
                try
                {
                    AmbientStopwatch s = AmbientStopwatch.StartNew();
                    try
                    {
                        using (FileStream fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096))
                        {
                            byte[] b = new byte[1];
                            await fs.WriteAsync(b, 0, 1);
                            await fs.FlushAsync();
                            writeBuilder.AddProperty("ResponseMs", s.ElapsedMilliseconds);
                            writeBuilder.AddOkay("Ok", "Success", "The write operation succeeded.");
                        }
                    }
                    catch (Exception e)
                    {
                        writeBuilder.AddException(e);
                    }
                    s.Reset();
                    try
                    {
                        using (FileStream fs = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            byte[] b = new byte[1];
                            await fs.ReadAsync(b, 0, 1, cancel);
                            await fs.FlushAsync();
                        }
                        readBuilder.AddProperty("ResponseMs", s.ElapsedMilliseconds);
                        readBuilder.AddOkay("Ok", "Success", "The read operation succeeded.");
                    }
                    catch (Exception e)
                    {
                        readBuilder.AddException(e);
                    }
                }
                finally
                {
                    File.Delete(targetPath);
                }
            }
        }
    }
}
/// <summary>
/// An auditor for the local disk system.  This class will be automatically instantiated when <see cref="Status.Start"/> is called and disposed when <see cref="Status.Stop"/> is called.
/// </summary>
public sealed class LocalDiskAuditor : StatusAuditor 
{
    private readonly bool _ready;
    private readonly DiskAuditor _tempAuditor;
    private readonly DiskAuditor _systemAuditor;

    /// <summary>
    /// Constructs the local disk auditor instance, which will audit the state of the local disk every 15 minutes (note that this frequency is to prevent the code from slowing down the unit tests; if this were used in the real world, one minute might be a better frequency).
    /// </summary>
    public LocalDiskAuditor() : base ("/LocalDisk", TimeSpan.FromMinutes(15))
    {
        string tempPath = System.IO.Path.GetTempPath()!;
        string tempDrive = Path.GetPathRoot(tempPath) ?? "/";
        if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(tempDrive)) tempDrive = tempPath = "/";
        if (tempPath?[0] == '/') tempDrive = "/";    // on linux, the only "drive" is /
        string tempPathRelative = tempPath!.Substring(tempDrive.Length);
        _tempAuditor = new DiskAuditor(tempDrive, tempPathRelative, false);     // note that under Linux, some files in the temp path may be inaccessible in such a way as to timeout attempting to open even as few as ten of them.  this is probably a flaw in the .NET Core implementation on Linux.
        string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System)!;
        string systemDrive = Path.GetPathRoot(systemPath) ?? GetApplicationCodePath() ?? "/";   // use the application code path if we can't find the system root, if we can't get that either, try to use the root.  on linux, we should get the application code path
        if (string.IsNullOrEmpty(systemPath) || string.IsNullOrEmpty(systemDrive)) systemDrive = systemPath = "/";
        if (systemPath?[0] == '/') systemDrive = "/";
        string systemPathRelative = systemPath!.Substring(systemDrive.Length);
        _systemAuditor = new DiskAuditor(systemDrive, systemPath, false);
        _ready = true;
    }
    private static string GetApplicationCodePath()
    {
        AppDomain current = AppDomain.CurrentDomain;
        return (current.RelativeSearchPath ?? current.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
    }

    protected override bool Applicable => _ready; // if S3 were optional (for example, if an alternative could be configured), this would check the configuration
    public override async Task Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
    {
        statusBuilder.NatureOfSystem = StatusNatureOfSystem.ChildrenHeterogenous;
        await _tempAuditor.Audit(statusBuilder.AddChild("Temp"));
        await _systemAuditor.Audit(statusBuilder.AddChild("System"));
    }
}

class Application
{
    /// <summary>
    /// Starts the status system.
    /// </summary>
    public static async Task StartStatus()
    {
        await Status.DefaultInstance.Start();
    }
    /// <summary>
    /// Stops the status system.
    /// </summary>
    public static async Task StopStatus()
    {
        await Status.DefaultInstance.Stop();
    }
}
```

## Miscellaneous
Several non-service type utilities and extensions to system classes are also included because they are needed by the implementations.
These include InterlockedExtensions for threadsafe tracking of min/max values, 
ArrayExtensions for comparing arrays by value, 
StringExtensions for doing natural string comparisons,
ConcurrentHashSet for keeping a keyed set of items in a thread-safe collection, 
Date (because that should be implemented by the system and isn't), 
International System of Units (SI) string generation for more readable status reports,
FilteredStackTrace to remove system code tracing from exception stack dumps,
and Pseudorandom for greatly improved and threadsafe random number generation.

# Library Information

## Author and License
AmbientServices is written and maintained by James Ivie.

AmbientServices is licensed under [MIT](https://opensource.org/licenses/MIT).

## Language and Tools
AmbientServices is written in C#, using .NET Standard 2.0, .NET Core 3.1, and .NET 5.0.  Unit tests are written in .NET 5.0.

The code can be built using either Microsoft Visual Studio 2017+, Microsoft Visual Studio Code, or .NET Core command-line utilities.

Binaries are available at https://www.nuget.org/packages/AmbientServices.

## Contributions
Contributions are welcome under the following conditions:
1. enhancements are consistent with the overall scope of the project
2. no new assembly dependencies are introduced
3. code coverage by unit tests cover all new lines and conditions whenever possible
4. documentation (both inline and here) is updated appropriately
5. style for code and documentation contributions remains consistent