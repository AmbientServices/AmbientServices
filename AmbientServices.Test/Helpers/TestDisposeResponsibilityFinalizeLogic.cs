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

    private static void LeakUndisposedDisposeResponsibility()
    {
#pragma warning disable CA2000 // Intentional leak: finalizer must run without Dispose for this test.
        _ = new DisposeResponsibility<MemoryStream>(new MemoryStream());
#pragma warning restore CA2000
    }
}
