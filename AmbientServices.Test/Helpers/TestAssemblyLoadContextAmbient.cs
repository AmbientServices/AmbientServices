using AmbientServices.TestSupport.Alc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Documents and guards <see cref="AmbientService{T}"/> behavior under collectible <see cref="AssemblyLoadContext"/> (same patterns as hosts that forward shared dependencies from default).
/// </summary>
[TestClass]
[DoNotParallelize] // Counts loaded AmbientServices assemblies; parallel tests could add extra loads.
public sealed class TestAssemblyLoadContextAmbient
{
    private const string PluginTypeName = "AmbientServices.TestSupport.Alc.AlcFlowPlugin";

    [TestMethod]
    public async Task CollectibleAlc_SharedAmbientServicesAndContract_PluginSeesHostScopedLocalOverride()
    {
        string pluginPath = GetPluginAssemblyPath();
        Assert.IsTrue(File.Exists(pluginPath), "Expected plugin next to test assembly: " + pluginPath);

        using CollectibleAmbientPluginAlc alc = new(pluginPath, shareAmbientServices: true, shareContract: true);
        Assembly pluginAssembly = alc.LoadFromAssemblyPath(pluginPath);
        Type pluginType = pluginAssembly.GetType(PluginTypeName, throwOnError: true)!;
        MethodInfo getAmbientAsm = pluginType.GetMethod("GetAmbientServicesAssembly", BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo readLocal = pluginType.GetMethod("ReadContractLocal", BindingFlags.Public | BindingFlags.Static)!;

        Assembly hostAmbientAssembly = typeof(Ambient).Assembly;
        Assembly pluginViewOfAmbient = (Assembly)getAmbientAsm.Invoke(null, null)!;
        Assert.AreSame(hostAmbientAssembly, pluginViewOfAmbient);
        Assert.AreEqual(1, CountLoadedAmbientServicesAssemblies());

        AmbientAlcFlowTestImpl hostImpl = new() { Marker = 4242 };
        using (Ambient.GetService<IAmbientAlcFlowTestService>().ScopedLocalOverride(hostImpl))
        {
            IAmbientAlcFlowTestService? fromPlugin = (IAmbientAlcFlowTestService?)readLocal.Invoke(null, null);
            Assert.IsNotNull(fromPlugin);
            Assert.AreSame(hostImpl, fromPlugin);

            await Task.Yield();

            fromPlugin = (IAmbientAlcFlowTestService?)readLocal.Invoke(null, null);
            Assert.IsNotNull(fromPlugin);
            Assert.AreSame(hostImpl, fromPlugin);
        }
    }

    [TestMethod]
    public void CollectibleAlc_SharedContractOnly_SecondAmbientServicesLoad_PluginAmbientAssemblyNotDefault()
    {
        string pluginPath = GetPluginAssemblyPath();
        Assert.IsTrue(File.Exists(pluginPath), pluginPath);

        using CollectibleAmbientPluginAlc alc = new(pluginPath, shareAmbientServices: false, shareContract: true);
        Assembly pluginAssembly = alc.LoadFromAssemblyPath(pluginPath);
        Type pluginType = pluginAssembly.GetType(PluginTypeName, throwOnError: true)!;
        MethodInfo getAmbientAsm = pluginType.GetMethod("GetAmbientServicesAssembly", BindingFlags.Public | BindingFlags.Static)!;

        Assembly hostAmbient = typeof(Ambient).Assembly;
        Assembly pluginAmbient = (Assembly)getAmbientAsm.Invoke(null, null)!;
        Assert.AreNotSame(hostAmbient, pluginAmbient, "Returning null from Load can unify with default; negative test loads a second copy from disk next to the plugin.");
        Assert.IsTrue(CountLoadedAmbientServicesAssemblies() >= 2);
    }

    /// <summary>
    /// Contract assembly matches default, but a second AmbientServices load means plugin <see cref="AmbientService{T}"/> statics (and AsyncLocals) are separate —
    /// host <see cref="AmbientService{T}.ScopedLocalOverride"/> does not affect plugin code even on the same thread.
    /// </summary>
    [TestMethod]
    public void CollectibleAlc_SharedContractOnly_HostScopedLocalOverride_NotVisibleFromPlugin()
    {
        string pluginPath = GetPluginAssemblyPath();
        using CollectibleAmbientPluginAlc alc = new(pluginPath, shareAmbientServices: false, shareContract: true);
        Assembly pluginAssembly = alc.LoadFromAssemblyPath(pluginPath);
        Type pluginType = pluginAssembly.GetType(PluginTypeName, throwOnError: true)!;
        MethodInfo readLocal = pluginType.GetMethod("ReadContractLocal", BindingFlags.Public | BindingFlags.Static)!;

        AmbientAlcFlowTestImpl hostImpl = new() { Marker = 991 };
        using (Ambient.GetService<IAmbientAlcFlowTestService>().ScopedLocalOverride(hostImpl))
        {
            Assert.AreSame(hostImpl, Ambient.GetService<IAmbientAlcFlowTestService>().Local);
            IAmbientAlcFlowTestService? fromPlugin = (IAmbientAlcFlowTestService?)readLocal.Invoke(null, null);
            Assert.AreNotSame(hostImpl, fromPlugin, "Plugin uses a separate AmbientServices load; host scoped local must not appear as the same reference (often null in plugin).");
        }
    }

    private static string GetPluginAssemblyPath()
    {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        return Path.Combine(dir, "AmbientServices.Test.AlcPlugin.dll");
    }

    private static int CountLoadedAmbientServicesAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Count(static a =>
            string.Equals(a.GetName().Name, "AmbientServices", StringComparison.Ordinal));
    }

    /// <summary>Forwards selected dependencies from <see cref="AssemblyLoadContext.Default"/> so plugin code shares or isolates ambient statics.</summary>
    private sealed class CollectibleAmbientPluginAlc : AssemblyLoadContext, IDisposable
    {
        private readonly string _pluginAssemblyPath;
        private readonly bool _shareAmbientServices;
        private readonly bool _shareContract;

        public CollectibleAmbientPluginAlc(string pluginAssemblyPath, bool shareAmbientServices, bool shareContract)
            : base(isCollectible: true)
        {
            _pluginAssemblyPath = pluginAssemblyPath;
            _shareAmbientServices = shareAmbientServices;
            _shareContract = shareContract;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? simple = assemblyName.Name;
            if (string.Equals(simple, "AmbientServices", StringComparison.Ordinal))
            {
                if (_shareAmbientServices)
                    return Default.LoadFromAssemblyName(assemblyName);
                string ambientPath = Path.Combine(Path.GetDirectoryName(_pluginAssemblyPath)!, "AmbientServices.dll");
                return LoadFromAssemblyPath(ambientPath);
            }

            if (_shareContract && string.Equals(simple, "AmbientServices.Test.AlcContract", StringComparison.Ordinal))
                return Default.LoadFromAssemblyName(assemblyName);

            return null;
        }

        public void Dispose() => Unload();
    }
}
