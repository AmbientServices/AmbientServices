using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace AmbientServices.Test;

/// <remarks>Serialized: mutates static <see cref="DisposeResponsibility.ResponsibilityNotDisposed"/> via reflection.</remarks>
[TestClass]
[DoNotParallelize]
public class TestDisposeResponsibilityFinalizeLogic
{
    [TestMethod]
    public void FinalizeLogic_WithSubscribers_RaisesResponsibilityNotDisposed_WithContainedAndStack()
    {
        ResponsibilityNotDisposedEventArgs? received = null;
        object? senderSeen = null;
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e)
        {
            senderSeen = sender;
            received = e;
        }
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
            MemoryStream ms = new();
            DisposeResponsibility<MemoryStream> dr = new(ms, "unit-test-stack");
            dr.FinalizeLogic();
            Assert.IsNotNull(received, "Event should fire.");
            Assert.AreSame(dr, senderSeen);
            Assert.AreSame(ms, received!.Contained);
            Assert.AreEqual("unit-test-stack", received.StackOnCreation);
            dr.Dispose();
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }

    [TestMethod]
    public void FinalizeLogic_WithSubscribers_NullContained_StillRaisesWithStack()
    {
        ResponsibilityNotDisposedEventArgs? received = null;
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e) => received = e;
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
            DisposeResponsibility<MemoryStream> dr = new(null, "null-contained-stack");
            dr.FinalizeLogic();
            Assert.IsNotNull(received);
            Assert.IsNull(received!.Contained);
            Assert.AreEqual("null-contained-stack", received.StackOnCreation);
            dr.Dispose();
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }

    [TestMethod]
    public void FinalizeLogic_WithoutSubscribers_NullContained_DoesNotThrow()
    {
        FieldInfo field = GetResponsibilityNotDisposedBackingField();
        object? previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            DisposeResponsibility<MemoryStream> dr = new(null, "stack-for-null");
            dr.FinalizeLogic();
            dr.Dispose();
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    [TestMethod]
    public void FinalizeLogic_WithoutSubscribers_NonNullContained_DoesNotThrow()
    {
        FieldInfo field = GetResponsibilityNotDisposedBackingField();
        object? previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            MemoryStream ms = new();
            DisposeResponsibility<MemoryStream> dr = new(ms);
            dr.FinalizeLogic();
            Assert.IsTrue(ms.CanRead, "FinalizeLogic must not dispose the contained instance.");
            dr.Dispose();
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    private static FieldInfo GetResponsibilityNotDisposedBackingField()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type t = typeof(DisposeResponsibility);
        FieldInfo? field = t.GetField("ResponsibilityNotDisposed", flags);
        if (field != null)
            return field;
        foreach (FieldInfo fi in t.GetFields(flags))
        {
            if (fi.Name.Contains("ResponsibilityNotDisposed", StringComparison.Ordinal))
                return fi;
        }
        throw new InvalidOperationException("Could not find static backing field for DisposeResponsibility.ResponsibilityNotDisposed.");
    }

    [TestMethod]
    public void AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc_ThrowsWhenLeakedAndNoHandler()
    {
        FieldInfo field = GetResponsibilityNotDisposedBackingField();
        object? previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            LeakUndisposedDisposeResponsibility();
            try
            {
                DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, "Undisposed DisposeResponsibility");
            }
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    [TestMethod]
    public void AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc_NoThrow_WhenDisposed()
    {
        FieldInfo field = GetResponsibilityNotDisposedBackingField();
        object? previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            using (DisposeResponsibility<MemoryStream> dr = new(new MemoryStream())) { }
            DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc();
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    /// <summary>
    /// After <see cref="IShirkResponsibility.ShirkResponsibility"/> is called (via the transfer constructor),
    /// <see cref="DisposeResponsibility{T}.FinalizeLogic"/> must not report a leak — the object intentionally
    /// gave up its responsibility and there is nothing left to dispose.
    /// </summary>
    [TestMethod]
    public void FinalizeLogic_AfterShirk_DoesNotRaiseLeak()
    {
        bool eventFired = false;
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e) => eventFired = true;
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
            MemoryStream ms = new();
#pragma warning disable CA2000 // dr1 is intentionally not in a using — we verify no leak fires
            DisposeResponsibility<MemoryStream> dr1 = new(ms);
#pragma warning restore CA2000
            using DisposeResponsibility<MemoryStream> dr2 = new(dr1); // transfer — shirks dr1
            dr1.FinalizeLogic(); // must be silent: dr1 was shirked, not leaked
            Assert.IsFalse(eventFired, "FinalizeLogic must not report a leak for a shirked DisposeResponsibility.");
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }

    /// <summary>
    /// A DR that is shirked via the transfer constructor but never explicitly disposed must not
    /// surface as an undisposed leak after a full GC, because <see cref="IShirkResponsibility.ShirkResponsibility"/>
    /// now suppresses the finalizer.
    /// </summary>
    [TestMethod]
    public void AssertNoUndisposedLeaksAfterFullGc_ShirkedWithoutExplicitDispose_DoesNotThrow()
    {
        FieldInfo field = GetResponsibilityNotDisposedBackingField();
        object? previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            ShirkWithoutExplicitDispose();
            DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc();
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    private static void ShirkWithoutExplicitDispose()
    {
        MemoryStream ms = new();
#pragma warning disable CA2000 // dr1 intentionally not disposed — the test verifies no leak fires after shirk
        DisposeResponsibility<MemoryStream> dr1 = new(ms);
#pragma warning restore CA2000
        using DisposeResponsibility<MemoryStream> dr2 = new(dr1); // transfer — shirks dr1; dr2 is properly disposed
        // dr1 is NOT explicitly disposed, but ShirkResponsibility now calls GC.SuppressFinalize
    }

    private static void LeakUndisposedDisposeResponsibility()
    {
#pragma warning disable CA2000 // Intentional leak: finalizer must run without Dispose for this test.
        _ = new DisposeResponsibility<MemoryStream>(new MemoryStream());
#pragma warning restore CA2000
    }
}
