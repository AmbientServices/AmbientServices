using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
        // the verification requires leak-detail collection, which is off by default
        using IDisposable leakDetails = DisposeResponsibility.ScopedLeakDetailCollection();
        DisposeResponsibilityMstestVerification.AfterAllTestsInAssembly();
    }
}
