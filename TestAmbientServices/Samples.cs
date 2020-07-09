using AmbientServices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A simple class representing a user for the sample.
/// </summary>
class User
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; }
    /// <summary>
    /// Gets or sets user's password hash.
    /// </summary>
    public byte[] PasswordHash { get; set; }

    /// <summary>
    /// Checks the specified password against the hash.
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    public bool CheckPassword(string password)
    {
        // implement password hash and comparison here

        return false;
    }
    /// <summary>
    /// Finds the user with the specified email address.
    /// </summary>
    /// <param name="email">The email of the user to find.</param>
    /// <returns></returns>
    public static User Find(string email)
    {
        // implement database lookup here

        return null;
    }
    /// <summary>
    /// Creates the user in the database.
    /// </summary>
    public void Create()
    {

        // implement database insert here

    }
    /// <summary>
    /// Updates the user in the database.
    /// </summary>
    public void Update()
    {

        // implement database update here

    }
    /// <summary>
    /// Deletes the user record from the database.
    /// </summary>
    /// <param name="email">The email of the user to delete.</param>
    public static void Delete(string email)
    {

        // implement database remove here

    }
}



/*
 * 
 * 
 * 
 * Samples included in README.md begin here
 * 
 * 
 * 
 * */



#region AmbientCacheSample
/// <summary>
/// A user manager class that shows how the caching ambient service might be used.
/// </summary>
class UserManager
{
    private static IAmbientCache AmbientCache = ServiceBroker<IAmbientCache>.GlobalImplementation;

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
#endregion


#region AmbientLoggerSample
/// <summary>
/// A static class with extensions methods used to log various assembly events.
/// </summary>
public static class AssemblyLoggingExtensions
{
    private static readonly ILogger<Assembly> _Logger = ServiceBroker<IAmbientLogger>.GlobalImplementation.GetLogger<Assembly>();

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
#endregion






#region AmbientProgressSample
/// <summary>
/// A class that downloads and unzips
/// </summary>
class DownloadAndUnzip
{
    private static IAmbientProgress AmbientProgress = ServiceBroker<IAmbientProgress>.GlobalImplementation;

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
#endregion






#region AmbientSettingsSample
/// <summary>
/// A class that manages a pool of buffers.
/// </summary>
class BufferPool
{
    private static readonly IAmbientSettings AmbientSettings = AmbientServices.ServiceBroker<IAmbientSettings>.GlobalImplementation;
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
#endregion





#region CustomAmbientServiceSample
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
#endregion




#region DisableAmbientServiceSample
/// <summary>
/// A sample setup class that disables the cache implementation when it is initialized.
/// </summary>
class Setup
{
    static Setup()
    {
        ServiceBroker<IAmbientCache>.GlobalImplementation = null;
    }
}
#endregion





#region OverrideAmbientServiceGlobalSample
/// <summary>
/// An application setup class that registers an implementation of <see cref="IAmbientSettings"/> that uses <see cref="Configuration.AppSettings"/> for the settings as the ambient service.
/// </summary>
class SetupApplication
{
    static SetupApplication()
    {
        ServiceBroker<IAmbientSettings>.GlobalImplementation = new AppConfigAmbientSettings();
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
        public AppConfigSetting(string name, Func<string, T> convert, T defaultValue = default(T))
        {
            string valueString = GetValue(name);
            _value = (valueString == null) ? defaultValue : convert(valueString);
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
#endregion






#region OverrideAmbientServiceLocalSample
/// <summary>
/// An implementation of <see cref="IAmbientSettings"/> that overrides specific settings.
/// </summary>
class LocalAmbientSettingsOverride : IAmbientSettings, IDisposable
{
    private readonly IAmbientSettings _oldSettings;
    private readonly Dictionary<string, string> _overrides;

    /// <summary>
    /// For the life of this instance, overrides the settings in the specified dictionary with their corresponding values.
    /// </summary>
    /// <param name="overrides">A Dictionary containing the key/value pairs to override.</param>
    public LocalAmbientSettingsOverride(Dictionary<string, string> overrides)
    {
        _oldSettings = ServiceBroker<IAmbientSettings>.LocalImplementation;
        ServiceBroker<IAmbientSettings>.LocalImplementation = this;
        _overrides = new Dictionary<string, string>();
    }
    /// <summary>
    /// Disposes of this instance, returning the ambient settings to their former value.
    /// </summary>
    public void Dispose()
    {
        ServiceBroker<IAmbientSettings>.LocalImplementation = _oldSettings;
    }

    public ISetting<T> GetSetting<T>(string key, Func<string, T> convert, T defaultValue = default(T))
    {
        return new OverrideSetting<T>(this, key, convert, defaultValue);
    }
    private string GetOverride(string name)
    {
        string value;
        if (_overrides.TryGetValue(name, out value))
        {
            return value;
        }
        return null;
    }
    class OverrideSetting<T> : ISetting<T>
    {
        private T _value;
        public OverrideSetting(LocalAmbientSettingsOverride overrideSettings, string name, Func<string, T> convert, T defaultValue = default(T))
        {
            string valueString = overrideSettings.GetOverride(name);
            _value = (valueString == null) ? overrideSettings._oldSettings.GetSetting<T>(name, convert, defaultValue).Value : convert(valueString);
        }

        public T Value => _value;

        // NOTE: to implement support for settings that change on the fly, see the reference implementation in the BasicAmbientSettings class in AmbientServices on GitHub, as it can be quite complicated
#pragma warning disable CS0067
        public event EventHandler<SettingValueChangedEventArgs<T>> ValueChanged;
#pragma warning restore CS0067
    }
}
#endregion
