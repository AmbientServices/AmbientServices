﻿using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
#if NET8_0_OR_GREATER
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;


#if NET5_0_OR_GREATER
using System.Net.Http;
#else
using System.Net;
#endif

/*
* 
* 
* Note that the samples are in a separate assembly because the test assembly has special access to internal classes and functions.
* We need to be sure that none of the sample code uses that special access.
* 
* 
* 
* */
/// <summary>
/// A simple class representing a user for the sample.
/// </summary>
class User
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; private set; } = "";
    /// <summary>
    /// Gets or sets user's password hash.
    /// </summary>
    public byte[] PasswordHash { get; private set; } = new byte[0];

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
    public static User? Find(string email)
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
    private static readonly AmbientLocalCache<UserManager> Cache = new();

    /// <summary>
    /// Finds the user with the specified emali address.
    /// </summary>
    /// <param name="email">The emali address for the user.</param>
    /// <returns>The <see cref="User"/>, if one was found, or null if the user was not found.</returns>
    public static async ValueTask<User?> FindUser(string email)
    {
        string userKey = nameof(User) + "-" + email;
        User? user = await Cache.Retrieve<User>(userKey, TimeSpan.FromMinutes(15));
        if (user != null)
        {
            user = User.Find(email);
            if (user != null) await Cache.Store<User>(userKey, user, false, TimeSpan.FromMinutes(15)); else await Cache.Remove<User>(userKey);
        }
        return user;
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async ValueTask CreateUser(User user)
    {
        string userKey = nameof(User) + "-" + user.Email;
        user.Create();
        await Cache.Store<User>(userKey, user, false, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Updates the specified user. (Presumably with a new password)
    /// </summary>
    /// <param name="user">The updated <see cref="User"/>.</param>
    public static async ValueTask UpdateUser(User user)
    {
        string userKey = nameof(User) + "-" + user.Email;
        user.Update();
        await Cache.Store<User>(userKey, user, false, TimeSpan.FromMinutes(15));
    }
    /// <summary>
    /// Deletes the specified user.
    /// </summary>
    /// <param name="email">The email of the user to be deleted.</param>
    public static async ValueTask DeleteUser(string email)
    {
        string userKey = nameof(User) + "-" + email;
        User.Delete(email);
        await Cache.Remove<User>(userKey);
    }
}
#endregion






#region AmbientLoggerSample
/// <summary>
/// A static class with extensions methods used to log various assembly events.
/// </summary>
public static class AssemblyLoggingExtensions
{
    private static readonly AmbientLogger<Assembly> Logger = new();

    /// <summary>
    /// Log that the assembly was loaded.
    /// </summary>
    /// <param name="assembly">The assembly that was loaded.</param>
    public static void LogLoaded(this Assembly assembly)
    {
        Logger.Filter("Lifetime", AmbientLogLevel.Trace)?.Log(new { Action = "AssemblyLoaded", Assembly = assembly.FullName });
    }
    /// <summary>
    /// Logs that there was an assembly load exception.
    /// </summary>
    /// <param name="assembly">The <see cref="AssemblyName"/> for the assembly that failed to load.</param>
    /// <param name="ex">The <see cref="Exception"/> that occured during the failed load.</param>
    /// <param name="operation">The operation that needed the assembly.</param>
    public static void LogLoadException(this AssemblyName assemblyName, Exception ex, string operation)
    {
        Logger.Error(ex, "Error loading assembly " + assemblyName.FullName + " while attempting to perform operation " + operation);
    }
    /// <summary>
    /// Logs that an assembly was scanned.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> that was scanned.</param>
    public static void LogScanned(this Assembly assembly)
    {
        Logger.Filter("Scan", AmbientLogLevel.Trace)?.Log(new { Action = "AssemblyScanned", Assembly = assembly.FullName });
    }
}
/// <summary>
/// A static class that does processing that logs to a rotating file instead of the default System.Diagnostics trace log.
/// </summary>
public static class MyProgram
{
    private static readonly AmbientLogger<Assembly> Logger = new();
    /// <summary>
    /// Does the main processing.
    /// </summary>
    public static void Process()
    {
        using AmbientFileLogger bl = new();
        using (new ScopedLocalServiceOverride<IAmbientLogger>(bl))
        {
            try
            {
                Logger.Filter("Process", AmbientLogLevel.Debug)?.Log(new { Action = "Sample Program Starting processing..." });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, level: AmbientLogLevel.Critical);
            }
        }
    }
}
#endregion






#region AmbientProgressSample
/// <summary>
/// A class that downloads and unzips a zip package.
/// </summary>
class DownloadAndUnzip
{
    private readonly string _targetFolder;
    private readonly string _downloadUrl;
    private readonly MemoryStream _package;

    public DownloadAndUnzip(string targetFolder, string downloadUrl)
    {
        _targetFolder = targetFolder;
        _downloadUrl = downloadUrl;
        _package = new MemoryStream();
    }

    public async ValueTask MainOperation(CancellationToken cancel = default)
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
#if NET5_0_OR_GREATER
    public async ValueTask Download()
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        CancellationToken cancel = progress?.CancellationToken ?? default;
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(_downloadUrl);
        long totalBytesRead = 0;
        int bytesRead;
        byte[] buffer = new byte[8192];
        long contentLength = response.Content.Headers.ContentLength ?? 1000000;
        using Stream downloadReader = await response.Content.ReadAsStreamAsync();
        while ((bytesRead = await downloadReader.ReadAsync(buffer, 0, buffer.Length, cancel)) != 0)
        {
            await _package.WriteAsync(buffer, 0, bytesRead, cancel);
            totalBytesRead += bytesRead;
            progress?.Update(totalBytesRead * 1.0f / contentLength);
        }
    }
#else
    public async ValueTask Download()
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        CancellationToken cancel = progress?.CancellationToken ?? default;
        HttpWebRequest request = WebRequest.CreateHttp(_downlaodUrl);
        using WebResponse response = request.GetResponse();
        long totalBytesRead = 0;
        int bytesRead;
        long totalBytes = response.ContentLength;
        byte[] buffer = new byte[8192];
        using Stream downloadReader = response.GetResponseStream();
        while ((bytesRead = await downloadReader.ReadAsync(buffer, 0, buffer.Length, cancel)) != 0)
        {
            await _package.WriteAsync(buffer, 0, bytesRead, cancel);
            totalBytesRead += bytesRead;
            progress?.Update(totalBytesRead * 1.0f / totalBytes);
        }
    }
#endif
    public ValueTask Unzip()
    {
        IAmbientProgress? progress = AmbientProgressService.Progress;
        CancellationToken cancel = progress?.CancellationToken ?? default;

        using ZipArchive archive = new(_package);
        int entries = archive.Entries.Count;
        for (int entry = 0; entry < entries; ++entry)
        {
            ZipArchiveEntry archiveEntry = archive.Entries[entry];
            // update the progress
            progress?.Update(entry * 1.0f / entries, archiveEntry.FullName);
            archiveEntry.ExtractToFile(Path.Combine(_targetFolder, archiveEntry.FullName));
        }
        return default;
    }
}
#endregion






#region AmbientSettingsSample
/// <summary>
/// A class that manages a pool of buffers.
/// </summary>
class BufferPool
{
    private static readonly IAmbientSetting<int> MaxTotalBufferBytes = AmbientSettings.GetAmbientSetting<int>(nameof(BufferPool) + "-MaxTotalBytes", "The maximum total number of bytes to use for all the allocated buffers.  The default value is 1MB.", s => Int32.Parse(s), "1000000");
    private static readonly IAmbientSetting<int> DefaultBufferBytes = AmbientSettings.GetAmbientSetting<int>(nameof(BufferPool) + "-BufferBytes", "The number of bytes to allocate for each buffer.  The default value is 8000 bytes.", s => Int32.Parse(s), "8000");

    private readonly SizedBufferRecycler _recycler;  // interlocked

    class SizedBufferRecycler
    {
        private readonly ConcurrentBag<byte[]> _bag;

        public SizedBufferRecycler(int bufferBytes)
        {
            BufferBytes = bufferBytes;
            _bag = new ConcurrentBag<byte[]>();
        }
        public int BufferBytes { get; }
        public byte[] GetBuffer(int bytes)
        {
            if (bytes < BufferBytes)
            {
                byte[]? buffer;
                if (_bag.TryTake(out buffer))
                {
                    return buffer;
                }
            }
            return new byte[Math.Max(bytes, BufferBytes)];
        }
        public void Recycle(byte[] buffer)
        {
            if (buffer.Length == BufferBytes && _bag.Count * BufferBytes < MaxTotalBufferBytes.Value)
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
#endregion






#region CustomAmbientServiceSample
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
    private static readonly AsyncLocal<ImmutableStack<string>> Stack = new();

    private static ImmutableStack<string> GetStack()
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

    public IEnumerable<string> Entries => GetStack();

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
#endregion






#region DisableAmbientServiceSample
/// <summary>
/// A sample setup class that disables the cache implementation when it is initialized.
/// </summary>
class Setup
{
    private static readonly AmbientService<IAmbientLocalCache> Cache = Ambient.GetService<IAmbientLocalCache>();
    static Setup()
    {
        Cache.Global = null;
    }
}
#endregion






#region OverrideAmbientServiceGlobalSample
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
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => false;
    /// <summary>
    /// Changes the specified setting, if possible.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed (it may have had already the same value).</returns>
    public bool ChangeSetting(string key, string? value) => throw new InvalidOperationException($"{nameof(AppConfigAmbientSettings)} is not mutable.");
}
#endregion






#region OverrideAmbientServiceLocalSample
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
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => false;
    /// <summary>
    /// Changes the specified setting, if possible.
    /// For many ambient settings services, the value will only be reflected in memory until the process shuts down, but other services may persist the change.
    /// </summary>
    /// <param name="key">A string that uniquely identifies the setting.</param>
    /// <param name="value">The new string value for the setting, or null if the setting should be removed.</param>
    /// <returns>Whether or not the setting actually changed (it may have had already the same value).</returns>
    public bool ChangeSetting(string key, string? value) => throw new InvalidOperationException($"{nameof(LocalAmbientSettingsOverride)} is not mutable.");
}
#endregion






#region AmbientStatisticsSample

/// <summary>
/// A class that represents a type of request.
/// </summary>
public class RequestType
{
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();

    /// <summary>
    /// Constructs a RequestType with the specified type name.
    /// </summary>
    /// <param name="typeName">The name of the request type.</param>
    public RequestType(string typeName)
    {
        IAmbientStatistics? ambientStatistics = AmbientStatistics.Local;
        PendingRequests = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Raw, typeName + "-RequestsPending", "Pending Requests", "The number of requests currently executing", false, 0, null, null, "", 1.0);
        TotalRequests = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Cumulative, typeName + "-TotalRequests", "Total Requests", "The total number of requests that have finished executing", false, 0, null, null, "", 1.0);
        TotalProcessingTime = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Cumulative, typeName + "-TotalProcessingTime", "Total Processing Time", "The total time spent processing requests (only includes completed requests)", false, 0, null, null, "seconds", Stopwatch.Frequency);
        Retries = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Cumulative, typeName + "-Retries", "Retries", "The total number of retries", false, 0, null, null, "", 1.0);
        Failures = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Cumulative, typeName + "-Failures", "Failures", "The total number of failures", false, 0, null, null, "", 1.0);
        Timeouts = ambientStatistics?.GetOrAddStatistic(AmbientStatisicType.Cumulative, typeName + "-Timeouts", "Timeouts", "The total number of timeouts", false, 0, null, null, "", 1.0);
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
    public IAmbientStatistic? PendingRequests { get; }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of requests.
    /// </summary>
    public IAmbientStatistic? TotalRequests { get; }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total processing time.
    /// </summary>
    public IAmbientStatistic? TotalProcessingTime { get; }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of retries.
    /// </summary>
    public IAmbientStatistic? Retries { get; }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of failures.
    /// </summary>
    public IAmbientStatistic? Failures { get; }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of timeouts.
    /// </summary>
    public IAmbientStatistic? Timeouts { get; }
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
        requestType.PendingRequests?.IncrementRaw();
    }

    /// <summary>
    /// Reports a failure during the processing of the request.
    /// </summary>
    public void ReportFailure()
    {
        _requestType.Failures?.IncrementRaw();
    }
    /// <summary>
    /// Reports a timeout during the processing of the request.
    /// </summary>
    public void ReportTimeout()
    {
        _requestType.Timeouts?.IncrementRaw();
    }
    /// <summary>
    /// Reports a retry during the processing of the request.
    /// </summary>
    public void ReportRetry()
    {
        _requestType.Retries?.IncrementRaw();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _requestType.PendingRequests?.AddRaw(-1);
                _requestType.TotalRequests?.IncrementRaw();
                _requestType.TotalProcessingTime?.AddRaw(_stopwatch.ElapsedTicks);
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
            writer.WriteAttributeString("value", statistic.CurrentRawValue.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }
}
#endregion






#region AmbientBottleneckDetectorSample
/// <summary>
/// A class that holds a thread-safe queue which reports on the associated bottleneck.
/// </summary>
class GlobalQueue
{
    private static readonly Mutex Mutex = new(false);
    private static readonly Queue<object> Queue = new();
    private static readonly AmbientBottleneck GlobalQueueBottleneck = new("GlobalQueue-Access", AmbientBottleneckUtilizationAlgorithm.Linear, true, "A bottleneck which only allows one accessor at a time.");

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
    private readonly AmbientBottleneck _bottleneck = new("Ebs-Iops", AmbientBottleneckUtilizationAlgorithm.Linear, false, "A bottleneck which has a limit, but which is not based on access time.", 1000, TimeSpan.FromSeconds(1));

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
            using (FileStream f = new(filePath, FileMode.Open, FileAccess.Read))
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
    private readonly AmbientBottleneckSurveyorCoordinator _surveyor = new();
    private Dictionary<string, double>? _mostRecentWindowTopBottlenecks;  // interlocked
    private readonly IDisposable _timeWindow;

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
        Dictionary<string, double> mostRecentWindowTopBottlenecks = new();
        foreach (AmbientBottleneckAccessor record in analysis.GetMostUtilizedBottlenecks(10))
        {
            mostRecentWindowTopBottlenecks.Add(record.Bottleneck.Id, record.Utilization);
        }
        Interlocked.Exchange(ref _mostRecentWindowTopBottlenecks, mostRecentWindowTopBottlenecks);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a dictionary containing the top 10 bottlenecks with their overall utilization for the most recent time window.
    /// </summary>
    public Dictionary<string, double>? RecentBottleneckSummary => _mostRecentWindowTopBottlenecks;
}
#endregion






#region AmbientServiceProfilerSample
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

    private async Task<T> ExecuteAsync<T>(SqlCommand command, Func<CancellationToken, Task<T>> f, string? table = null, CancellationToken cancel = default)
    {
        string systemId = _systemIdPrefix + (string.IsNullOrEmpty(table) ? "" : $"/Table:{table}");
        T ret;
        try
        {
            ServiceProfiler.Local?.SwitchSystem(systemId);
            ret = await f(cancel);
            systemId += $"/Result:Success";
        }
        catch (Exception e)
        {
            if (e.Message.ToUpperInvariant().Contains("TIMEOUT")) systemId += $"/Result:Timeout";
            else systemId += $"/Result:Error";
            throw;
        }
        finally
        {
            ServiceProfiler.Local?.SwitchSystem("", systemId);
        }
        return ret;
    }

    public async ValueTask<int> ExecuteNonQueryAsync(SqlCommand command, CancellationToken cancel = default, string? table = null)
    {
        return await ExecuteAsync<int>(command, command.ExecuteNonQueryAsync, table, cancel);
    }
    public async ValueTask<SqlDataReader> ExecuteReaderAsync(SqlCommand command, CancellationToken cancel = default, string? table = null)
    {
        return await ExecuteAsync<SqlDataReader>(command, command.ExecuteReaderAsync, table, cancel);
    }
    public async ValueTask<object> ExecuteScalarAsync(SqlCommand command, CancellationToken cancel = default, string? table = null)
    {
        return await ExecuteAsync<object>(command, command.ExecuteScalarAsync, table, cancel);
    }
    public async ValueTask<XmlReader> ExecuteXmlReaderAsync(SqlCommand command, CancellationToken cancel = default, string? table = null)
    {
        return await ExecuteAsync<XmlReader>(command, command.ExecuteXmlReaderAsync, table, cancel);
    }
}
/// <summary>
/// A class that collects bottleneck statistics and reports on them.
/// </summary>
class ProfileReporter
{
    private Dictionary<string, long>? _mostRecentWindowServiceProfile;  // interlocked
    private readonly AmbientServiceProfilerCoordinator _coordinator;
    private readonly IDisposable? _timeWindow;
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
        Dictionary<string, long> serviceProfile = new();
        foreach (AmbientServiceProfilerAccumulator record in profile.ProfilerStatistics)
        {
            serviceProfile.Add(record.Group, record.TotalStopwatchTicksUsed);
        }
        Interlocked.Exchange(ref _mostRecentWindowServiceProfile, serviceProfile);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a dictionary containing the service profile for the most recent time window.
    /// </summary>
    public Dictionary<string, long>? RecentProfile => _mostRecentWindowServiceProfile;
}
#endregion






#region DisposeResponsibilitySample
/// <summary>
/// A static class that contains utilities for managing files.
/// </summary>
public static class FileManager
{
    /// <summary>
    /// Opens two related files at the same time.
    /// </summary>
    /// <param name="filePath1">The full path to the first file to be opened.</param>
    /// <param name="filePath2">The full path to the second file to be opened.</param>
    public static DisposeResponsibility<(Stream Stream1, Stream Stream2)> OpenRelatedFiles(string filePath1, string filePath2)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: this style warning is painfully wrong in this case
        FileStream stream1 = new(filePath1, FileMode.Open, FileAccess.Read);
        FileStream stream2;
        try
        {
            stream2 = new(filePath2, FileMode.Open, FileAccess.Read);
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch 
        {
            stream1.Dispose();
            throw;
        }
        return new((stream1, stream2));
    }
}

public class DisposeResponsibilityUsage
{
    public static void Sample()
    {
        using DisposeResponsibility<(Stream Stream1, Stream Stream2)> files = FileManager.OpenRelatedFiles("file1.txt", "file2.txt");
#pragma warning disable CA2000 // Dispose objects before losing scope: this style warning is painfully wrong in this case
        StreamReader reader1 = new(files.Contained.Stream1);
        string s1 = reader1.ReadToEnd();
        StreamReader reader2 = new(files.Contained.Stream2);
#pragma warning restore CA2000 // Dispose objects before losing scope
        string s2 = reader1.ReadToEnd();
    }
}
#endregion








#region StatusSample
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
    public async ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
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
                StatusResultsBuilder readBuilder = new("Read");
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
                            using (FileStream fs = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                byte[] b = new byte[1];
#pragma warning disable CA2022
                                await fs.ReadAsync(b.AsMemory(0, 1), cancel);
#pragma warning restore CA2022
                                await fs.FlushAsync(cancel);
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
                StatusResultsBuilder writeBuilder = new("Write");
                StatusResultsBuilder readBuilder = new("Read");
                statusBuilder.AddChild(writeBuilder);
                statusBuilder.AddChild(readBuilder);
                // attempt to write a temporary file
                string targetPath = Path.Combine(_driveInfo.RootDirectory.FullName, Guid.NewGuid().ToString("N"));
                try
                {
                    AmbientStopwatch s = AmbientStopwatch.StartNew();
                    try
                    {
                        using FileStream fs = new(targetPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096);
                        byte[] b = new byte[1];
#pragma warning disable CA2022
                        await fs.WriteAsync(b.AsMemory(0, 1), cancel);
#pragma warning restore CA2022
                        await fs.FlushAsync(cancel);
                        writeBuilder.AddProperty("ResponseMs", s.ElapsedMilliseconds);
                        writeBuilder.AddOkay("Ok", "Success", "The write operation succeeded.");
                    }
                    catch (Exception e)
                    {
                        writeBuilder.AddException(e);
                    }
                    s.Reset();
                    try
                    {
                        using (FileStream fs = new(targetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            byte[] b = new byte[1];
#pragma warning disable CA2022
                            await fs.ReadAsync(b.AsMemory(0, 1), cancel);
#pragma warning restore CA2022
                            await fs.FlushAsync(cancel);
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
        string tempPath = Path.GetTempPath()!;
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
        return (current.RelativeSearchPath ?? current.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
    }

    protected override bool Applicable => _ready; // if S3 were optional (for example, if an alternative could be configured), this would check the configuration
    public override async ValueTask Audit(StatusResultsBuilder statusBuilder, CancellationToken cancel = default)
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
    public static async ValueTask StartStatus()
    {
        await Status.DefaultInstance.Start();
    }
    /// <summary>
    /// Stops the status system.
    /// </summary>
    public static async ValueTask StopStatus()
    {
        await Status.DefaultInstance.Stop();
    }
}
#endregion







namespace Tests // 2021-12-29: under net6.0 currently, tests cannot be discovered if they're not in a namespace
{
#region AmbientClockSample
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
        using AmbientCancellationTokenSource cts = new(TimeSpan.FromSeconds(1));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => AsyncFunctionThatShouldCancelAfterOneSecond(cts.Token).AsTask());

        // switch the current call context to the artifically-paused ambient clock and try again
        using (AmbientClock.Pause())
        {
            using AmbientCancellationTokenSource cts2 = new(TimeSpan.FromSeconds(1));
            // this should *not* throw because the clock has been paused
            await AsyncFunctionThatShouldCancelAfterOneSecond(cts2.Token);

            // this skips the artifical paused clock ahead, raising the cancellation
            AmbientClock.SkipAhead(TimeSpan.FromSeconds(1));
            // make sure the cancellation got raised
            Assert.ThrowsException<OperationCanceledException>(() => cts2.Token.ThrowIfCancellationRequested());
        }
    }
    private async ValueTask AsyncFunctionThatShouldCancelAfterOneSecond(CancellationToken cancel)
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
            using AmbientCancellationTokenSource cts = new(TimeSpan.FromSeconds(1));
            await AsyncFunctionThatCouldTimeoutUnderHeavyLoad(cts.Token);
        }
    }
    private async ValueTask AsyncFunctionThatCouldTimeoutUnderHeavyLoad(CancellationToken cancel)
    {
        AmbientStopwatch stopwatch = new(true);
        for (int count = 0; count < 9; ++count)
        {
            await Task.Delay(100);
            cancel.ThrowIfCancellationRequested();
        }
        // if we finished before getting cancelled, we must have been scheduled within about 10 milliseconds on average, or we must be running using a paused ambient clock
    }
}
#endregion
}





