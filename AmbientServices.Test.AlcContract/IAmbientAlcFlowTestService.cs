namespace AmbientServices.TestSupport.Alc;

/// <summary>
/// Contract type for assembly load context flow tests: must resolve from a single physical assembly so ambient service statics
/// match across the default context and a plugin <see cref="System.Runtime.Loader.AssemblyLoadContext"/> when shared resolution is configured.
/// </summary>
public interface IAmbientAlcFlowTestService
{
    /// <summary>
    /// Arbitrary marker for reference equality in tests.
    /// </summary>
    int Marker { get; }
}
