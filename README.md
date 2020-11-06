# Overview
AmbientServices is an assembly that provides abstractions for basic services which are both universal and optional, allowing assemblies that use it to be used in a variety of systems that provide vastly different implementations (or no implementation) of those basic services.

These basic services include caching, clock, logging, progress/cancellation, and settings.  Interfaces for those services are provided here.
By accessing these services through the interfaces provided here, library authors can utilize new basic services as they become available without changing their external interface, and library consumers can use those libraries without having to provide dependencies for systems that they may or may not use.
If consumers want the added benefits provided by a more complicated implementation of one or more of those services, they can provide a bridge to their own implementations of these basic services and register them with the AmbientServices service.
With one simple registration, the services will automatically be utilized by every library that uses AmbientServices.

The well known dependency injection pattern is one possible solution to this problem, but dependency injection requires the code consumer to pass the required dependencies to each object constructor (or worse, each function), which can be cumbersome.  When the functionality is optional anyway, this can be more work than it's worth, especially when you're just trying to get things up and running quickly.
Dependency injection becomes even more cumbersome when the assembly being used adds or removes service dependencies, requiring the consumer to update every constructor invocation with the new dependencies.
Dependency injection still makes sense for services that are required, but when services are optional anyway, AmbientServices is a better option.

By convention, AmbientServices should not be used for information that alters the outputs of functions that use it in any way that the caller might care about.  Side-effects should either not alter the relationship between inputs and outputs at all, or should not alter them unexpectedly.  

For example, logging should never alter function outputs at all.  Caching may affect the output, but only by giving results that are slightly stale, and only in cases where there are already hidden inputs (like a database) anyway.  Some functions may measure the passage of time during processing and might record that information or change their outputs based on the duration of time passed, but callers should not be surprised when the passage of time is slower or faster than their expected "normal".  Settings (often stored in a configuration file) can alter the output of a function, but never in a way that the caller is concerned about.  In fact, the very concept of settings is in reality a type of parameter intended to affect functions without requiring the caller to be concerned with their specific values.  Progress tracking and cancellation may be useful for the caller, but never affects the output of the function other than aborting its processing altogether.


# Getting Started
In Visual Studio, use Manage Nuget Packages and search nuget.org for AmbientServices to add a package reference for this library.

For .NET Core environments, use:
`dotnet add package https://www.nuget.org/packages/AmbientServices/`


# Built-in Basic Ambient Services

## AmbientCache

The ambient cache interface abstracts a simple cache of the type that is universally applicable.  Some items are cached for a specific amount of time, others are cached indefinitely.  Items cached temporarily may have their expiration time extended or shortened each time they are retrieved or updated.  Both types of items may expire from the cache at any time according to cache limits and/or memory capacity.  Items may be removed from the cache manually at any time.  

In order to prevent unexpected alteration of outputs, care must be taken to ensure that cached items are based entierly on the inputs.  For functions that are not "pure" (database queries for example), the results should always be based entirely on the inputs and either the current state of the database or some previous state (when it uses cached results).  For example, if the cache key does not contain all the inputs identifying the item being cached, completely different results could be obtained depending on the order in which calls to the cache were made.  This is true of all caches and naturally every cache user and implementor understands that this type of usage is erroneous and must be avoided.

### Helpers

The `AmbientCache<TOWNER>` generic class provides a wrapper of the ambient cache that attaches the owner type name as a prefix for each cache key to prevent cross-class cache key conflicts, and ignores calls when there is no cache provider or it has been suppressed.

### Settings

BasicAmbientCache-EjectFrequency: the number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.  Default is 100.
BasicAmbientCache-ItemCount: the number of items (timed and untimed combined) to cache.  Default is 1000.


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
    public static async Task<User> FindUser(string email)
    {
        string userKey = nameof(User) + "-" + email;
        User user = await Cache.Retrieve<User>(userKey, TimeSpan.FromMinutes(15));
        if (user != null)
        {
            user = User.Find(email);
            await Cache.Store<User>(false, userKey, user, TimeSpan.FromMinutes(15));
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
### Default Provider
The default provider has a small local-only cache using a very simple implementation.

## AmbientLogger

The ambient logger interface abstracts a simple logging system of the type that is universally applicable.  The provider implementation simply receives strings to log and flushes them when called.

Logging should never affect control flow or results.  The only side-effect should be transparent to the caller.  Every user and implementor should understand this implied part of the logging interface contract.

### Helpers

The `AmbientLogger<TOWNER>` generic class provides a wrapper of the ambient cache that attaches the owner type, a severity level, and a category to each message and filters them according to settings from the ambient or specified settings.  Overloads that take a message-generating lambda are also provided.  These overloads should be used when generating the log message from the provided input data is expensive and the caller wants to avoid that expense when the message is going to be filtered anyway.

### Settings

`AmbientLogger-Format`: A format string that controls what entries in the log look like where {0} is the entry time, {1} is the level, {2} is the log owner type, {3} is the category, and {4} is the message.  Default is {0:yyMMdd HHmmss.fff} [{1}:{2}]{3}{4}.
`AmbientLogFilter-LogLevel`: the AmbientLogLevel above which logs will be filtered (entries at this level and below will be logged).  Default is Information.
`AmbientLogFilter-TypeAllow`: A regular expression indicating which type(s) are allowed to be logged.  Default is null, meaning all types are allowed.
`AmbientLogFilter-TypeBlock`: A regular expression indicating which type(s) should be blocked from being logged.  Default is null, meaning no types should be blocked.
`AmbientLogFilter-CategoryAllow`: A regular expression indicating which categorie(s) are allowed to be logged.  Default is null, meaning all categories are allowed.
`AmbientLogFilter-CategoryBlock`: A regular expression indicating which categorie(s) should be blocked from being logged.  Default is null, meaning no categories should be blocked.

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
### Default Provider
The default provider asynchronously buffers the log messages and flushes them in batches out to the System Diagnostics Trace (which would slow code dramatically if each log message was written synchronously).

## AmbientProgress

The ambient progress interface abstracts a simple context-following progress tracker of the type that is universally applicable.  Progress tracking tracks the proportion of an operation that has completed processing and the item currently being processed and provides easy aggregation of subprocess progress.  The ambient context is checked for cancellation each time the progress is updated or parts are started or completed.

Progress tracking should never affect control flow or results, except in the event of a cancellation, in which case there are no functional results.  Naturally consumers and providers should avoid any usage or implementation to the contrary.

### Helpers

The only helper class here is `AmbientCancellationTokenSource`, which is a superset of the framework's `CancellationTokenSource` that can raise cancellation using an ambient clock.

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
    private static readonly IAmbientProgressProvider AmbientProgress = Service.GetAccessor<IAmbientProgressProvider>().GlobalProvider;

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
        IAmbientProgress progress = AmbientProgress?.Progress;
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
        IAmbientProgress progress = AmbientProgress?.Progress;
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
        IAmbientProgress progress = AmbientProgress?.Progress;
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
### Default Provider
The default provider tracks progress and provides access to the data, but does not output the progress information anywhere.

## AmbientClock

The ambient clock interface abstracts a system clock.  Artificial clock control can be important in testing, especially to efficiently and quickly exercise timeout conditions and to avoid timeouts when tests run under heavy CPU load (as you would ususally want them to run in order to get through them as quickly as possible).  Ideally, the underlying platform would provide some kind of thread or execution-context-specific clock for use by timeout logic, but unfortunatly most platforms do not provide this functionality.  This service provides that missing functionality, at least for the purposes of testing.

Clocks, of course, are generally counter to the goals of purely functional programming, and even in imperative programming, it makes sense that functions that aren't obviously time-dependent should not have their outputs unexpectedly affected by the clock.  One such acceptable usage is logging with timestamps.  Another acceptable usage is timeouts.  For all programs, clocks could indirectly appear to be frozen if the CPU is unexpectedly fast or the system clock has an unexpectedly low resolution.  Correspondingly, clocks could appear to skip ahead if the system CPU is overloaded and the thread doesn't get scheduled or if the system goes to sleep or hibernates and then later resumes.  The artificial clock AmbientClock provides simply allows an upstream service consumer to simulate those conditions for both unit and integration testing purposes.  These are important edge cases to test for systems that need a high degree of reliability and graceful degredation.  

Clocks should never go backwards.  Provider implementors must ensure this holds true.

### Helpers

The `AmbientClock` static class provides an abstraction that automatically uses the system clock if there is no registered provider.  It also provides a `Pause` function that allows the caller to temporarily pause time as seen by the ambient clock.  The `SkipAhead` function allows the caller to move the paused clock forward (ignored if the clock is not paused).  `AmbientClock` can also issue an `AmbientCancellationToken` that is cancelled by the ambient clock provider.
The `AmbientStopwatch` class provides a time measuring class similar to the framework's `Stopwatch` class, but pauses when the ambient clock is paused.
The `AmbientTimer` class provides a callback similar to the framework's `Timer` class, but follows the ambient clock.

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
        AmbientCancellationTokenSource cts = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => AsyncFunctionThatShouldCancelAfterOneSecond(cts.Token));

        // switch the current call context to the artifically-paused ambient clock and try again
        using (AmbientClock.Pause())
        {
            AmbientCancellationTokenSource cts2 = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromSeconds(1));
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
            AmbientCancellationTokenSource cts = AmbientClock.CreateCancellationTokenSource(TimeSpan.FromSeconds(1));
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
### Default Provider
There is no default provider.  This causes the helper classes to use the system clock.

## AmbientSettings

The ambient settings interface abstracts a simple string-based settings accessor.  Each setting has a value identified by a unique string.  The value of the setting is always a string, but each setting may be converted to a desired type by specifying a delegate that converts the string into the desired strongly-typed value.
Often the value for a setting may change on the fly, so the value exposed by the helper class might change after initialization.  Users can also subscribe to an event that notifies them when the value for a setting changes, in case they need to do something more complicated than just parsing the new value.  Value change event notifications may arrive asynchronously on any thread at any time, so users must not depend on the notification occurring before they get an updated value.  
A call-context-specific override can be used for some settings, but of course no change notifications can occur when the value changes due to setting a call-context-local provider or changes of the value within a call-context-local provider (where would the notification go?).

Providers may or may not provide post-initialization settings value updates but if they do, they should also raise the notifications.

Among other things, the ambient settings system is designed to provide sensible access to settings and notification of changes during system startup and shutdown.  For example, at the beginning of startup, the settings just use default values.  At some point, the global provider can be replaced with a provider that reads from a local configuration, and then later on with a provider that reads settings from a centralized settings store.  Users of settings don't need to bother with knowing where the settings come from, only that they might change during system startup.  This is especially useful for things like logging.  Errors that occur before the location of shared logs is determined (that location might be stored in a central database) can be stored in the event log or local file system as desired.  Once the centralized settings are hooked up, logging can automatically switch to a remote provider indicated in the centralized settings store.  No central (and often complicated) "startup" code is required for this kind of transition.  Code can (and should) automatically use the default or local settings until the central settings become available.

Settings by their very nature must be considered inputs for the purposes of functional programming.  They are by definition not passed on the stack (otherwise, they're just insanely-overpopulated collections of parameters someone decided to call "settings").

### Helpers

The `IAmbientSetting<T>` generic helper interface provides access to a type-converted setting and an event to notify subscribers when the setting value changes.
The `AmbientSettings` static class is used to construct an `IAmbientSetting<T>` for the caller.  Settings provided by `AmbientSettings` can be "provider" settings whose value comes from an explicit provider specified during construction, or "ambient" settings whose value comes from the default ambient provider (even if there is a local override in the call-context when the value is retrieved).

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
                byte[] buffer;
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
### Default Provider
The default provider just uses the default value as the initial value.  An alternate interface, `IMutableAmbientSettings`, can be used to change the settings values in this implementation.  Other service implementations may or may not support changing settings values and may or may not support this interface to do so.  The simplicity of this abstraction is due to the wide variety of settings systems available.  Since the interface is only one function, implementing a bridge to Configuration.AppSettings or some other more appropriate settings repository is very simple.


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
[DefaultAmbientServiceProvider]
class BasicAmbientCallStack : IAmbientCallStack
{
    static private AsyncLocal<ImmutableStack<string>> _Stack = new AsyncLocal<ImmutableStack<string>>();

    static private ImmutableStack<string> GetStack()
    {
        ImmutableStack<string> stack = _Stack.Value;
        if (_Stack.Value == null)
        {
            stack = ImmutableStack<string>.Empty;
            _Stack.Value = stack;
        }
        return stack;
    }

    public IDisposable Scope(string entry)
    {
        ImmutableStack<string> stack = GetStack();
        stack = stack.Push(entry);
        return new CallStackEntry(stack);
    }

    public IEnumerable<string> Entries { get { return GetStack(); } }

    class CallStackEntry : IDisposable
    {
        private ImmutableStack<string> _stack;

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
                        _stack.Pop();
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
    private static readonly ServiceAccessor<IAmbientCacheProvider> _CacheProvider = Service.GetAccessor<IAmbientCacheProvider>();
    static Setup()
    {
        _CacheProvider.GlobalProvider = null;
    }
}
```

## Overriding An Ambient Service Globally
[//]: # (OverrideAmbientServiceGlobalSample)
```csharp
/// <summary>
/// An application setup class that registers an implementation of <see cref="IAmbientSettingsProvider"/> that uses <see cref="Configuration.AppSettings"/> for the settings as the ambient service.
/// </summary>
class SetupApplication
{
    static SetupApplication()
    {
        ServiceAccessor<IAmbientSettingsProvider> SettingsProvider = Service.GetAccessor<IAmbientSettingsProvider>();
        SettingsProvider.GlobalProvider = new AppConfigAmbientSettings();
    }
}
/// <summary>
/// An implementation of <see cref="IAmbientSettingsProvider"/> that uses <see cref="Configuration.AppSettings"/> as the backend settings store.
/// </summary>
class AppConfigAmbientSettings : IAmbientSettingsProvider
{
    public string ProviderName => "AppConfig";

    public string GetRawValue(string key)
    {
        return System.Configuration.ConfigurationManager.AppSettings[key];
    }
    public object GetTypedValue(string key)
    {
        string rawValue = System.Configuration.ConfigurationManager.AppSettings[key];
        IProviderSetting ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue) : rawValue;
    }
}
```

## Overriding An Ambient Service Locally From A Call Context
[//]: # (OverrideAmbientServiceLocalSample)
```csharp
/// <summary>
/// An implementation of <see cref="IAmbientSettingsProvider"/> that overrides specific settings.
/// </summary>
class LocalAmbientSettingsOverride : IAmbientSettingsProvider, IDisposable
{
    private static readonly ServiceAccessor<IAmbientSettingsProvider> _SettingsProvider = Service.GetAccessor<IAmbientSettingsProvider>();

    private readonly IAmbientSettingsProvider _oldSettings;
    private readonly Dictionary<string, string> _overrides;

    /// <summary>
    /// For the life of this instance, overrides the settings in the specified dictionary with their corresponding values.
    /// </summary>
    /// <param name="overrides">A Dictionary containing the key/value pairs to override.</param>
    public LocalAmbientSettingsOverride(Dictionary<string, string> overrides)
    {
        _oldSettings = _SettingsProvider.Provider;
        _SettingsProvider.ProviderOverride = this;
        _overrides = new Dictionary<string, string>();
    }

    public string ProviderName => nameof(LocalAmbientSettingsOverride);

    /// <summary>
    /// Disposes of this instance, returning the ambient settings to their former value.
    /// </summary>
    public void Dispose()
    {
        _SettingsProvider.ProviderOverride = _oldSettings;
    }

    public string GetRawValue(string key)
    {
        string value;
        if (_overrides.TryGetValue(key, out value))
        {
            return value;
        }
        return _oldSettings.GetRawValue(key);
    }
    public object GetTypedValue(string key)
    {
        string rawValue = GetRawValue(key);
        IProviderSetting ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue) : rawValue;
    }
}
```


# Library Information

## Author and License
AmbientServices is written and maintained by James Ivie.

AmbientServices is licensed under [MIT](https://opensource.org/licenses/MIT).

## Language and Tools
AmbientServices is written in C#, using .NET Standard 2.0.  Unit tests are written in .NET Core 2.1.

The code can be built using either Microsoft Visual Studio 2017+, Microsoft Visual Studio Code, or .NET Core command-line utilities.

Binaries are available at https://www.nuget.org/packages/AmbientServices.