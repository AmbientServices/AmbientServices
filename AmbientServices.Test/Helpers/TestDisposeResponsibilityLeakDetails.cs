using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AmbientServices.Test;

/// <summary>
/// Tests the creation-site detail collection toggle for <see cref="DisposeResponsibility{T}"/>: that it is off by default,
/// that the explicit leak-reporting function refuses to run while it is off, that turning it on defers the stack rendering to report time,
/// that the finalizer path still reports leaks while it is off, and that an explicit creation-site string works either way.
/// </summary>
/// <remarks>
/// Every test here scopes the setting to its own call context (with either <see cref="DisposeResponsibility.ScopedLeakDetailCollection"/> or a
/// <see cref="ScopedLocalServiceOverride{T}"/> of the settings set), so these tests remain safe to run concurrently with each other and with everything else.
/// </remarks>
[TestClass]
public class TestDisposeResponsibilityLeakDetails
{
    /// <summary>
    /// Gets a settings-set override that supplies no value for the leak-detail setting, so the setting's declared default is what takes effect.
    /// </summary>
    private static ScopedLocalServiceOverride<IAmbientSettingsSet> ScopedSettingsWithoutLeakDetailSetting(string setName) => new(new BasicAmbientSettingsSet(setName));

    [TestMethod]
    public void CollectLeakDetails_IsOffByDefault()
    {
        using ScopedLocalServiceOverride<IAmbientSettingsSet> settings = ScopedSettingsWithoutLeakDetailSetting(nameof(CollectLeakDetails_IsOffByDefault));
        Assert.IsFalse(DisposeResponsibility.CollectLeakDetails, "Creation-site detail collection must be off unless it is turned on.");
        IAmbientSettingInfo? info = SettingsRegistry.DefaultRegistry.TryGetSetting(DisposeResponsibility.CollectLeakDetailsSettingKey);
        Assert.IsNotNull(info, "The leak-detail setting should be registered so it is discoverable.");
        Assert.AreEqual(false, info.DefaultValue);
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
        using DisposeResponsibility<MemoryStream> dr = new(new MemoryStream());
#pragma warning restore CA2000
        Assert.AreEqual("", dr.StackOnCreation, "No creation site should be collected while detail collection is off.");
    }

    [TestMethod]
    public void ScopedLeakDetailCollection_TurnsCollectionOnAndOffAndRestoresIt()
    {
        using ScopedLocalServiceOverride<IAmbientSettingsSet> settings = ScopedSettingsWithoutLeakDetailSetting(nameof(ScopedLeakDetailCollection_TurnsCollectionOnAndOffAndRestoresIt));
        Assert.IsFalse(DisposeResponsibility.CollectLeakDetails);
        using (IDisposable on = DisposeResponsibility.ScopedLeakDetailCollection())
        {
            Assert.IsTrue(DisposeResponsibility.CollectLeakDetails);
            using (IDisposable off = DisposeResponsibility.ScopedLeakDetailCollection(false))
            {
                Assert.IsFalse(DisposeResponsibility.CollectLeakDetails, "An explicit off scope must win over the enclosing on scope.");
            }
            Assert.IsTrue(DisposeResponsibility.CollectLeakDetails, "Disposing the inner scope must restore the enclosing scope's value.");
        }
        Assert.IsFalse(DisposeResponsibility.CollectLeakDetails, "Disposing the scope must restore the previous value.");
    }

    /// <summary>
    /// The scoped override must layer over the context's existing settings rather than replacing them, so unrelated settings keep working inside the scope.
    /// </summary>
    [TestMethod]
    public void ScopedLeakDetailCollection_LeavesOtherSettingsAlone()
    {
        BasicAmbientSettingsSet contextSettings = new(nameof(ScopedLeakDetailCollection_LeavesOtherSettingsAlone));
        string key = nameof(ScopedLeakDetailCollection_LeavesOtherSettingsAlone) + "-setting";
        contextSettings.ChangeSetting(key, "context-value");
        using ScopedLocalServiceOverride<IAmbientSettingsSet> settings = new(contextSettings);
        IAmbientSetting<string> setting = AmbientSettings.GetAmbientSetting<string>(key, "A setting used to verify that scoped leak-detail collection does not hide other settings.", s => s, "default-value");
        Assert.AreEqual("context-value", setting.Value);
        using IDisposable leakDetails = DisposeResponsibility.ScopedLeakDetailCollection();
        Assert.IsTrue(DisposeResponsibility.CollectLeakDetails);
        Assert.AreEqual("context-value", setting.Value, "The scoped leak-detail override must not hide the rest of the context's settings.");
    }

    [TestMethod]
    public void AssertNoUndisposedLeaks_ThrowsWithHelpfulMessage_WhenCollectionIsOff()
    {
        using ScopedLocalServiceOverride<IAmbientSettingsSet> settings = ScopedSettingsWithoutLeakDetailSetting(nameof(AssertNoUndisposedLeaks_ThrowsWithHelpfulMessage_WhenCollectionIsOff));
        Assert.IsFalse(DisposeResponsibility.CollectLeakDetails);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc());
        StringAssert.Contains(ex.Message, DisposeResponsibility.CollectLeakDetailsSettingKey, "The message must name the setting to enable.");
        StringAssert.Contains(ex.Message, nameof(DisposeResponsibility.ScopedLeakDetailCollection), "The message must name the scoped way to enable it.");
    }

    /// <summary>
    /// With collection on, the creation site is captured as a stack object and rendered on demand (not at construction), and the rendering names the frames that created the instance.
    /// </summary>
    [TestMethod]
    public void ScopedLeakDetailCollection_On_DefersRenderingAndCapturesCreatingFrames()
    {
        using IDisposable leakDetails = DisposeResponsibility.ScopedLeakDetailCollection();
        Assert.IsTrue(DisposeResponsibility.CollectLeakDetails);
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
        using DisposeResponsibility<MemoryStream> dr = new(new MemoryStream());
#pragma warning restore CA2000
        // the capture must still be an unrendered StackTrace, and no string should have been built at construction
        Assert.IsInstanceOfType<StackTrace>(GetPrivateFieldValue(dr, "_capturedStackOnCreation"), "The creation stack should be captured as a StackTrace object, not rendered at construction.");
        Assert.IsNull(GetPrivateFieldValue(dr, "_explicitStackOnCreation"), "No creation-site string should exist until one is asked for.");
        string stack = dr.StackOnCreation;
        Assert.IsNotEmpty(stack);
        StringAssert.Contains(stack, nameof(TestDisposeResponsibilityLeakDetails), "The rendered stack should name the frame that created the instance.");
        Assert.AreEqual(stack, dr.StackOnCreation, "Rendering on demand should be repeatable.");
    }

    /// <summary>
    /// The finalizer path (simulated with <see cref="DisposeResponsibility{T}.FinalizeLogic"/>) must still report the leak while collection is off, just without a creation stack.
    /// </summary>
    [TestMethod]
    public void FinalizeLogic_WithCollectionOff_StillReportsLeak_WithoutStack()
    {
        using ScopedLocalServiceOverride<IAmbientSettingsSet> settings = ScopedSettingsWithoutLeakDetailSetting(nameof(FinalizeLogic_WithCollectionOff_StillReportsLeak_WithoutStack));
        MemoryStream ms = new();
        ResponsibilityNotDisposedEventArgs? received = null;
        // only pay attention to the notification for *our* instance so that leaks reported by concurrently-running tests can't affect this one
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e) { if (ReferenceEquals(e.Contained, ms)) received = e; }
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
            DisposeResponsibility<MemoryStream> dr = new(ms);
#pragma warning restore CA2000
            dr.FinalizeLogic();
            Assert.IsNotNull(received, "The leak must still be reported when detail collection is off.");
            Assert.AreSame(ms, received.Contained);
            Assert.AreEqual("", received.StackOnCreation, "No creation stack should be reported when detail collection is off.");
            dr.Dispose();
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }

    /// <summary>
    /// The finalizer path reports the creation stack when collection was on where the instance was constructed.
    /// </summary>
    [TestMethod]
    public void FinalizeLogic_WithCollectionOn_ReportsCapturedStack()
    {
        using IDisposable leakDetails = DisposeResponsibility.ScopedLeakDetailCollection();
        MemoryStream ms = new();
        ResponsibilityNotDisposedEventArgs? received = null;
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e) { if (ReferenceEquals(e.Contained, ms)) received = e; }
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
            DisposeResponsibility<MemoryStream> dr = new(ms);
#pragma warning restore CA2000
            dr.FinalizeLogic();
            Assert.IsNotNull(received);
            StringAssert.Contains(received.StackOnCreation, nameof(TestDisposeResponsibilityLeakDetails), "The report should name the frame that created the leaked instance.");
            dr.Dispose();
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }

    /// <summary>
    /// An explicit creation-site string is kept verbatim (and survives transfers) whether or not detail collection is on, and suppresses stack capture.
    /// </summary>
    [TestMethod]
    public void ExplicitCreationSite_RoundTrips_RegardlessOfCollectionSetting()
    {
        VerifyExplicitCreationSiteRoundTrip(false);
        VerifyExplicitCreationSiteRoundTrip(true);
    }

    private static void VerifyExplicitCreationSiteRoundTrip(bool collectLeakDetails)
    {
        string site = $"explicit-creation-site-{collectLeakDetails}";
        using IDisposable leakDetails = DisposeResponsibility.ScopedLeakDetailCollection(collectLeakDetails);
        Assert.AreEqual(collectLeakDetails, DisposeResponsibility.CollectLeakDetails);
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
        using DisposeResponsibility<MemoryStream> constructed = new(new MemoryStream(), site);
#pragma warning restore CA2000
        Assert.AreEqual(site, constructed.StackOnCreation);
        Assert.IsNull(GetPrivateFieldValue(constructed, "_capturedStackOnCreation"), "An explicit creation site must suppress the stack capture entirely.");
        // the site survives a transfer through the transfer constructor
        using DisposeResponsibility<MemoryStream> transferredTo = new(constructed);
        Assert.AreEqual(site, transferredTo.StackOnCreation);
        Assert.AreEqual("", constructed.StackOnCreation, "The instance that gave up responsibility should no longer report a creation site.");
        // ...and through TransferResponsibilityFrom
        using DisposeResponsibility<MemoryStream> transferredFrom = new();
        transferredFrom.TransferResponsibilityFrom(transferredTo);
        Assert.AreEqual(site, transferredFrom.StackOnCreation);
        // ...and through AssumeResponsibility
        using DisposeResponsibility<MemoryStream> assumed = new();
#pragma warning disable CA2000 // ownership is transferred to the DisposeResponsibility
        assumed.AssumeResponsibility(new MemoryStream(), site + "-assumed");
#pragma warning restore CA2000
        Assert.AreEqual(site + "-assumed", assumed.StackOnCreation);
    }

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Could not find the private field {fieldName} on {instance.GetType().Name}.");
        return field.GetValue(instance);
    }
}
