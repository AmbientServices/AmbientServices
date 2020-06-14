# Overview
AmbientServices is a service that provides abstractions for basic services which are both universal and optional, allowing libraries to be used in a variety of systems that provide vastly different implementations (or no implementation) of those basic services.

These basic services include caching, logging, progress/cancellation, and settings, and interfaces for those services are provided here.
By accessing these services through the interfaces provided by AmbientServices, code integrators can use libraries without having to provide dependencies for systems that they may or may not have implemented yet.
If integrators want the added benefits provided by a more complicated implementation of one or more of those services, they can provide a bridge to their own implementations of these basic services and register them with the AmbientServices service.
With one simple registration, the services will automatically be utilized by every library that uses AmbientServices.

The well known dependency injection pattern is one possible solution to this problem, but dependency injection requires the code consumer to pass the required dependencies to each object constructor, which can be cumbersome, and when the functionality is optional anyway, this can be more work than it's worth, especially when you're just trying to get things up and running.
Dependency injection becomes even more cumbersome when the library being used adds or removes dependencies, requiring the code user to update every constructor invocation with the new dependencies.
Dependency injection still makes sense for services that are required, but when services are optional, AmbientServices is a better option.

# Built-in Basic Ambient Services

## AmbientCache

The ambient cache interface abstracts a simple cache of the type that is universally applicable.  Some items are cached for a specific amount of time, others are cached indefinitely.  Items cached temporarily may have their expiration time extended or shortened each time they are retrieved or updated.  Both types of items may expire from the cache at any time.  Items may be removed from the cache manually at any time.

### Sample
```csharp
/// <summary>
/// A user manager class that shows how the caching ambient service might be used.
/// </summary>
class UserManager
{
    private static IAmbientCache AmbientCache = Registry<IAmbientCache>.Implementation;

    /// <summary>
    /// Finds the user with the specified emali address.
    /// </summary>
    /// <param name="email">The emali address for the user.</param>
    /// <returns>The <see cref="User"/>, if one was found, or null if the user was not found.</returns>
    public static async Task<User> FindUser(string email)
    {
        string userKey = "User-" + email;
        User user = await AmbientCache?.TryGet<User>(userKey, TimeSpan.FromMinutes(15));
        if (user != null)
        {
            user = User.Find(email);
            await AmbientCache?.Set<User>(false, userKey, user, TimeSpan.FromMinutes(15));
        }
        return user;
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async Task CreateUser(User user)
    {
        string userKey = "User-" + user.Email;
        user.Create();
        await AmbientCache?.Set<User>(false, userKey, user, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async Task UpdateUser(User user)
    {
        string userKey = "User-" + user.Email;
        user.Update();
        await AmbientCache?.Set<User>(false, userKey, user, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Deletes the specified user.
    /// </summary>
    /// <param name="email">The email of the user to be deleted.</param>
    public static async Task DeleteUser(string email)
    {
        string userKey = "User-" + email;
        User.Delete(email);
        await AmbientCache?.Remove<User>(false, userKey);
    }
}
```
### Default Implementation
The default implementation provides a small local-only cache using a very simple implementation.

## AmbientLogger

The ambient logger interface abstracts a simple logging system of the type that is universally applicable.  Log messages are classified by level, an associated class type, and a specified category.  In some implementations, these may be used to filter what is actually logged.

### Sample
```csharp
/// <summary>
/// A static class with extensions methods used to log various assembly events.
/// </summary>
public static class AssemblyLoggingExtensions
{
    private static readonly ILogger<Assembly> _Logger = Registry<IAmbientLogger>.Implementation.GetLogger<Assembly>();

    /// <summary>
    /// Log that the assembly was loaded.
    /// </summary>
    /// <param name="assembly">The assembly that was loaded.</param>
    public static void LogLoaded(this Assembly assembly)
    {
        _Logger?.Log("AssemblyLoaded: " + assembly.FullName, "Lifetime", LogLevel.Trace);
    }
    /// <summary>
    /// Logs that there was an assembly load exception.
    /// </summary>
    /// <param name="assembly">The <see cref="AssemblyName"/> for the assembly that failed to load.</param>
    /// <param name="ex">The <see cref="Exception"/> that occured during the failed load.</param>
    /// <param name="operation">The operation that needed the assembly.</param>
    public static void LogLoadException(this AssemblyName assemblyName, Exception ex, string operation)
    {
        _Logger?.Log("Error loading assembly " + assemblyName.FullName + " while attempting to perform operation " + operation, ex, "Lifetime");
    }
    /// <summary>
    /// Logs that an assembly was scanned.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> that was scanned.</param>
    public static void LogScanned(this Assembly assembly)
    {
        _Logger?.Log("Assembly " + assembly.FullName + " scanned", "Scan", LogLevel.Trace);
    }
}
```
### Default Implementation
The default implementation asynchronously buffers the log messages and flushes them in batches out to the System Diagnostics Trace (which would slow code dramatically if each log message was written synchronously).

## AmbientProgress

The ambient progress interface abstracts a simple context-following progress tracker of the type that is universally applicable.  Progress tracking tracks the proportion of an operation that has completed processing and the item currently being processed and provides easy aggregation of subprocess progress.

### Sample
```csharp
/// <summary>
/// A class that downloads and unzips
/// </summary>
class DownloadAndUnzip
{
    private static IAmbientProgress AmbientProgress = Registry<IAmbientProgress>.Implementation;

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
        IProgress progress = AmbientProgress?.Progress;
        using (IProgress subprogress = progress?.TrackPart(0.01f, 0.75f, "Download "))
        {
            await Download();
        }
        using (IProgress subprogress = progress?.TrackPart(0.75f, 0.99f, "Unzip "))
        {
            await Unzip();
        }
    }
    public async Task Download()
    {
        IProgress progress = AmbientProgress?.Progress;
        CancellationToken cancel = progress.GetCancellationTokenOrDefault();
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
        IProgress progress = AmbientProgress?.Progress;
        CancellationToken cancel = progress.GetCancellationTokenOrDefault();

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

## AmbientSettings

The ambient settings interface abstracts a simple string-based settings accessor.  Each setting is identified by a string path identifying the value within the settings.  The underlying value of the setting is always a string, but each setting may be converted to a desired type by specifying a delegate that converts the string into the desired strongly-typed value.
Settings values may change on the fly, so the value returned by the Value property can change after initialization.  Users can also subscribe to an event that notifies them when the value for a setting changes, in case they need to trigger something more complicated than just parsing the new value.
Implementations may or may not provide post-initialization settings value updates but if they do, they should also trigger the notifications.
That event may arrive asynchronously on any thread at any time.

### Sample
```csharp
/// <summary>
/// A class that manages a pool of buffers.
/// </summary>
class BufferPool
{
    private static readonly IAmbientSettings AmbientSettings = AmbientServices.Registry<IAmbientSettings>.Implementation;
    private static readonly ISetting<int> MaxTotalBufferBytes = AmbientSettings.GetSetting<int>(nameof(BufferPool) + "-MaxTotalBytes", s => Int32.Parse(s), 1000 * 1000);
    private static readonly ISetting<int> DefaultBufferBytes = AmbientSettings.GetSetting<int>(nameof(BufferPool) + "-DefaultBufferBytes", s => Int32.Parse(s), 8000);

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
        }
    }

    /// <summary>
    /// Constructs a buffer pool using the ambient settings.
    /// </summary>
    public BufferPool()
    {
        DefaultBufferBytes.ValueChanged += _DefaultBufferBytes_SettingValueChanged;
        int bufferBytes = DefaultBufferBytes.Value;
        _recycler = new SizedBufferRecycler(bufferBytes);
    }

    private void _DefaultBufferBytes_SettingValueChanged(object sender, SettingValueChangedEventArgs<int> e)
    {
        // yes, there may be a race condition here depending on the implementation of the settings value changed event, but that would only happen if the setting changed twice very quickly, and even so, it would only result in buffers not getting recycled correctly
        // this could be handled by rechecking the value every time we get a new buffer, but for now it's just not worth it
        SizedBufferRecycler newRecycler = new SizedBufferRecycler(e.NewValue);
        System.Threading.Interlocked.Exchange(ref _recycler, newRecycler);
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
The default implementation simply uses the default value as the initial value.  An alternate interface, IMutableAmbientSettings, can be used to change the settings values in this implementation.  Other service implementations may or may not support changing settings values and may or may not support this interface to do so.  The simplicity of this implementation is due to the wide variety of settings systems available.  Since the interface is only one function, implementing a bridge to Configuration.AppSettings or some other more appropriate settings repository is very simple.

# Customizing Ambient Services

## Implementing A New Ambient Service
```csharp
/// <summary>
/// An interface that abstracts a simple ambient call stack tracking service.
/// </summary>
interface IAmbientCallStack
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
/// </summary>
[DefaultAmbientServiceAttribute]
class BasicAmbientCallStack : IAmbientCallStack
{
    static private ThreadLocal<Stack<string>> _stack = new ThreadLocal<Stack<string>>();

    static private Stack<string> GetStack()
    {
        Stack<string> stack = _stack.Value;
        if (_stack.Value == null)
        {
            stack = new Stack<string>();
            _stack.Value = stack;
        }
        return stack;
    }

    public IDisposable Scope(string entry)
    {
        Stack<string> stack = GetStack();
        stack.Push(entry);
        return new CallStackEntry(stack);
    }

    public IEnumerable<string> Entries { get { return GetStack(); } }

    class CallStackEntry : IDisposable
    {
        private Stack<string> _stack;

        public CallStackEntry(Stack<string> stack)
        {
            _stack = stack;
        }

        #region IDisposable Support
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
        #endregion
    }
}
```    

## Disabling An Ambient Service
```csharp
/// <summary>
/// A sample setup class that disables the cache implementation when it is initialized.
/// </summary>
class Setup
{
    static Setup()
    {
        Registry<IAmbientCache>.Implementation = null;
    }
}
```    

## Overriding An Ambient Service
```csharp
/// <summary>
/// An application setup class that registers an implementation of <see cref="IAmbientSettings"/> that uses <see cref="Configuration.AppSettings"/> for the settings as the ambient service.
/// </summary>
class SetupApplication
{
    static SetupApplication()
    {
        Registry<IAmbientSettings>.Implementation = new AppConfigAmbientSettings();
    }
}
/// <summary>
/// An implementation of <see cref="IAmbientSettings"/> that uses <see cref="Configuration.AppSettings"/> as the backend settings store.
/// </summary>
class AppConfigAmbientSettings : IAmbientSettings
{
    public ISetting<T> GetSetting<T>(string key, Func<string, T> convert, T defaultValue = default(T))
    {
        return new AppConfigSetting<T>(key, convert, defaultValue);
    }
    class AppConfigSetting<T> : ISetting<T>
    {
        private T _value;
        private string _name;
        private T _defaultValue;
        private Func<string, T> _convert;
        public AppConfigSetting(string name, Func<string, T> convert, T defaultValue = default(T))
        {
            _name = name;
            _defaultValue = defaultValue;
            _convert = convert;
            string valueString = GetValue(name);
            if (valueString == null) _value = defaultValue;
            _value = convert(valueString);
        }
        private static string GetValue(string name)
        {
            return ConfigurationManager.AppSettings[name];
        }

        public T Value => _value;

        // NOTE: to implement support for settings that change on the fly, see the reference implementation in the BasicAmbientSettings class in AmbientServices on GitHub, as it can be quite complicated
#pragma warning disable CS0067
        public event EventHandler<SettingValueChangedEventArgs<T>> ValueChanged;
#pragma warning restore CS0067
    }
}
```    
# Library Information

## Authors and license
This library is licensed under [MIT](https://opensource.org/licenses/MIT).

The library is written and maintained by James Ivie.

## Language and Tools
The library is written in C#, using .NET Standard 2.0.  The tests are written in .NET Core 2.2.

The code is built using either Microsoft Visual Studio 2017+ or Microsoft Visual Studio Code.

Binaries are available on https://www.nuget.org/packages/AmbientServices.
