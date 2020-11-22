using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

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
#endregion






#region AmbientLoggerSample
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
#endregion






#region AmbientProgressSample
/// <summary>
/// A class that downloads and unzips a zip package.
/// </summary>
class DownloadAndUnzip
{
    private static readonly IAmbientProgressService AmbientProgress = Ambient.GetService<IAmbientProgressService>().Global;

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
#endregion






#region AmbientSettingsSample
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
#endregion






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
#endregion






#region DisableAmbientServiceSample
/// <summary>
/// A sample setup class that disables the cache implementation when it is initialized.
/// </summary>
class Setup
{
    private static readonly AmbientService<IAmbientCache> _Cache = Ambient.GetService<IAmbientCache>();
    static Setup()
    {
        _Cache.Global = null;
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

    public string GetRawValue(string key)
    {
        return System.Configuration.ConfigurationManager.AppSettings[key];
    }
    public object GetTypedValue(string key)
    {
        string rawValue = System.Configuration.ConfigurationManager.AppSettings[key];
        IAmbientSettingInfo ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue) : rawValue;
    }
}
#endregion






#region OverrideAmbientServiceLocalSample
/// <summary>
/// An implementation of <see cref="IAmbientSettingsSet"/> that overrides specific settings.
/// </summary>
class LocalAmbientSettingsOverride : IAmbientSettingsSet, IDisposable
{
    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

    private readonly IAmbientSettingsSet _oldSettings;
    private readonly Dictionary<string, string> _overrides;

    /// <summary>
    /// For the life of this instance, overrides the settings in the specified dictionary with their corresponding values.
    /// </summary>
    /// <param name="overrides">A Dictionary containing the key/value pairs to override.</param>
    public LocalAmbientSettingsOverride(Dictionary<string, string> overrides)
    {
        _oldSettings = _Settings.Local;
        _Settings.Override = this;
        _overrides = new Dictionary<string, string>();
    }

    public string SetName => nameof(LocalAmbientSettingsOverride);

    /// <summary>
    /// Disposes of this instance, returning the ambient settings to their former value.
    /// </summary>
    public void Dispose()
    {
        _Settings.Override = _oldSettings;
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
        IAmbientSettingInfo ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        return (ps != null) ? ps.Convert(this, rawValue) : rawValue;
    }
}
#endregion





#region AmbientStatisticsSample

/// <summary>
/// A class that represents a type of request.
/// </summary>
public class RequestType
{
    private static readonly AmbientService<IAmbientStatistics> _AmbientStatistics = Ambient.GetService<IAmbientStatistics>();

    private readonly IAmbientStatistic _pendingRequests;
    private readonly IAmbientStatistic _totalRequests;
    private readonly IAmbientStatistic _totalProcessingTime;
    private readonly IAmbientStatistic _retries;
    private readonly IAmbientStatistic _failures;
    private readonly IAmbientStatistic _timeouts;

    /// <summary>
    /// Constructs a RequestType with the specified type name.
    /// </summary>
    /// <param name="typeName">The name of the request type.</param>
    public RequestType(string typeName)
    {
        IAmbientStatistics ambientStatistics = _AmbientStatistics.Local;
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
    public IAmbientStatistic PendingRequests { get { return _pendingRequests; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of requests.
    /// </summary>
    public IAmbientStatistic TotalRequests { get { return _totalRequests; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total processing time.
    /// </summary>
    public IAmbientStatistic TotalProcessingTime { get { return _totalProcessingTime; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of retries.
    /// </summary>
    public IAmbientStatistic Retries { get { return _retries; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of failures.
    /// </summary>
    public IAmbientStatistic Failures { get { return _failures; } }
    /// <summary>
    /// Gets the <see cref="IAmbientStatistic"/> that tracks the total number of timeouts.
    /// </summary>
    public IAmbientStatistic Timeouts { get { return _timeouts; } }
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
    private static readonly AmbientService<IAmbientStatistics> _AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    /// <summary>
    /// Writes all statistics with their current values to the specified <see cref="XmlWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> to write the data to.</param>
    public static void ToXml(XmlWriter writer)
    {
        writer.WriteStartElement("statistics");
        foreach (IAmbientStatisticReader statistic in _AmbientStatistics.Local?.Statistics.Values ?? Array.Empty<IAmbientStatisticReader>())
        {
            writer.WriteStartElement("statistic");
            writer.WriteAttributeString("id", statistic.Id);
            writer.WriteAttributeString("value", statistic.SampleValue.ToString());
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
        using (AmbientBottleneckAccessor access = _bottleneck.EnterBottleneck())
        {
            using (FileStream f = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                f.Position = fileOffset;
                bytesRead = f.Read(buffer, bufferOffset, bytes);
            }
            access.SetUsage(1, (bytesRead + IopsPageSize - 1) / IopsPageSize); // note that this approximation of IOPS won't be correct if the file is fragmented, and the lookup and opening of the file will likely use some IOPS as well--more accurate estmates can be obtained after long-term usage and comparison to AWS metrics
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
    private Dictionary<string, double> _mostRecentWindowTopBottlenecks;  // interlocked
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
    public Dictionary<string, double> RecentBottleneckSummary
    {
        get
        {
            return _mostRecentWindowTopBottlenecks;
        }
    }
}
#endregion


#region AmbientServiceProfilerSample
/// <summary>
/// A class that access a SQL database and reports profiling information to the system profiling system.
/// </summary>
class SqlAccessor
{
    private static readonly AmbientService<IAmbientServiceProfiler> _ServiceProfiler = Ambient.GetService<IAmbientServiceProfiler>();

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

    private async Task<T> ExecuteAsync<T>(SqlCommand command, Func<CancellationToken, Task<T>> f, string table = null, CancellationToken cancel = default(CancellationToken))
    {
        string systemId = _systemIdPrefix + (string.IsNullOrEmpty(table) ? "" : $"/Table:{table}");
        T ret;
        try
        {
            _ServiceProfiler.Local?.SwitchSystem(systemId);
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
            _ServiceProfiler.Local?.SwitchSystem(null, systemId);
        }
        return ret;
    }

    public Task<int> ExecuteNonQueryAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string table = null)
    {
        return ExecuteAsync<int>(command, command.ExecuteNonQueryAsync, table, cancel);
    }
    public Task<SqlDataReader> ExecuteReaderAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string table = null)
    {
        return ExecuteAsync<SqlDataReader>(command, command.ExecuteReaderAsync, table, cancel);
    }
    public Task<object> ExecuteScalarAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string table = null)
    {
        return ExecuteAsync<object>(command, command.ExecuteScalarAsync, table, cancel);
    }
    public Task<XmlReader> ExecuteXmlReaderAsync(SqlCommand command, CancellationToken cancel = default(CancellationToken), string table = null)
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
    private Dictionary<string, long> _mostRecentWindowServiceProfile;  // interlocked
    private AmbientServiceProfilerFactory _factory;
    private IDisposable _timeWindow;
    /// <summary>
    /// Constructs a Bottleneck reporter that holds onto the top ten utilized bottlenecks for the entire process for the previous one-minute window.
    /// </summary>
    public ProfileReporter()
    {
        _factory = new AmbientServiceProfilerFactory();
        _timeWindow = _factory.CreateTimeWindowProfiler(nameof(ProfileReporter), TimeSpan.FromMilliseconds(100), OnMostRecentWindowClosed);
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
    public Dictionary<string, long> RecentProfile
    {
        get
        {
            return _mostRecentWindowServiceProfile;
        }
    }
}
#endregion
