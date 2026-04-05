using System.Reflection;
using AmbientServices;

namespace AmbientServices.TestSupport.Alc;

/// <summary>
/// Entry points invoked via reflection from unit tests after loading this assembly into a collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
/// </summary>
public static class AlcFlowPlugin
{
    /// <summary>
    /// Returns the assembly that defines <see cref="Ambient"/> from this assembly's point of view.
    /// </summary>
    public static Assembly GetAmbientServicesAssembly() => typeof(Ambient).Assembly;

    /// <summary>
    /// Reads <see cref="AmbientService{T}.Local"/> for the contract interface using this load context's <see cref="AmbientService{T}.Instance"/>.
    /// </summary>
    public static IAmbientAlcFlowTestService? ReadContractLocal() =>
        Ambient.GetService<IAmbientAlcFlowTestService>().Local;
}
