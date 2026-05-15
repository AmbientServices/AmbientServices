using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientServices.Test;

/// <summary>
/// Covers the MSTest integration described in README (region <c>DisposeResponsibilityMstestSample</c> in <c>Samples.cs</c>).
/// </summary>
[TestClass]
public class TestDisposeResponsibilityMstestReadme
{
    [TestMethod]
    public void DisposeResponsibilityMstestVerification_AfterAllTestsInAssembly_NoFailure_WhenNothingLeaked()
    {
        DisposeResponsibilityMstestVerification.AfterAllTestsInAssembly();
    }
}
