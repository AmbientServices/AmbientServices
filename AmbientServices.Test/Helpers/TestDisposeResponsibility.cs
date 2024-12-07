using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestDisposeResponsibility
{
    [TestMethod]
    public async Task OwnershipTransfer()
    {
        {
            using (DisposeResponsibility<Stream> owner = new(new MemoryStream())) { }
            using DisposeResponsibility<Stream> firstOwner = new(new MemoryStream());
            //Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsTrue(firstOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> secondOwner = new(firstOwner);
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> thirdOwner = new();
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> fourthOwner = new(new MemoryStream());
#if DEBUG
            Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            fourthOwner.TransferResponsibilityFrom(thirdOwner);
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            await using DisposeResponsibility<MemoryStream> fifthOwner = new();
#else
            using DisposeResponsibility<MemoryStream> fifthOwner = new();
            await Task.Yield();
#endif
            Assert.ThrowsException<ObjectDisposedException>(() => fifthOwner.Contained);
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            fifthOwner.AssumeResponsibility(new());
#if DEBUG
            Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
            Assert.IsTrue(fifthOwner.ContainsDisposable);
            Assert.IsNotNull(fifthOwner.NullableContained);
            Assert.ThrowsException<ArgumentNullException>(() => fifthOwner.TransferResponsibilityFrom(null!));
            Assert.IsNotNull(fifthOwner.ToString());
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            await using DisposeResponsibility<MemoryStream> emptyOwner1 = new();
            _ = emptyOwner1.ToString();
#pragma warning disable CA2000
            await using DisposeResponsibility<Derived> emptyOwner2 = new(new Derived());
            _ = emptyOwner2.ToString();
#pragma warning restore CA2000
#endif
        }
#pragma warning disable CA2000
        Derived toBeDisposed = new();
        {
            using (DisposeResponsibility<Derived> owner = new(new Derived())) { }
            using DisposeResponsibility<Derived> firstOwner = new(toBeDisposed);
#pragma warning restore CA2000
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsTrue(firstOwner.ContainsDisposable);
            using DisposeResponsibility<Base> secondOwner = new(firstOwner);
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using DisposeResponsibility<Base> thirdOwner = new();
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Base> fourthOwner = new();
#if DEBUG
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            fourthOwner.TransferResponsibilityFrom(thirdOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);

            Assert.IsFalse(toBeDisposed.Disposed);
        }
        Assert.IsTrue(toBeDisposed.Disposed);
        {
            using DisposeResponsibility<Derived> disposeResponsibility = Subfunction();
        }
    }
    private static DisposeResponsibility<Derived> Subfunction()
    {
#pragma warning disable CA2000
        using DisposeResponsibility<Derived> owner = new(new Derived());
#pragma warning restore CA2000
        return owner.TransferResponsibilityToCaller();
    }
}


abstract class Base : IDisposable
{
    public bool Disposed { get; protected set; }
    abstract public void Dispose();
}

class Derived : Base
{
    public Derived()
    {
    }
    public override void Dispose()
    {
        Disposed = true;
    }
}
