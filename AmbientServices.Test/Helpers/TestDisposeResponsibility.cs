using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace AmbientServices.Test.Helpers;

[TestClass]
public class TestDisposeResponsibility
{
    [TestMethod]
    public void OwnershipTransfer()
    {
        {
            using (DisposeResponsibility<Stream> owner = new(new MemoryStream())) { }
            using DisposeResponsibility<Stream> firstOwner = new(new MemoryStream());
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsTrue(firstOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> secondOwner = new(firstOwner);
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> thirdOwner = new();
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> fourthOwner = new(new MemoryStream());
            Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            fourthOwner.TransferResponsibilityFrom(thirdOwner);
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
            using DisposeResponsibility<MemoryStream> fifthOwner = new();
            Assert.ThrowsException<ObjectDisposedException>(() => fifthOwner.Contained);
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            fifthOwner.AssumeResponsibility(new());
            Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
            Assert.IsTrue(fifthOwner.ContainsDisposable);
            Assert.IsNotNull(fifthOwner.NullableContained);
            Assert.ThrowsException<ArgumentNullException>(() => fifthOwner.TransferResponsibilityFrom(null!));
            Assert.IsNotNull(fifthOwner.ToString());
        }
        {
            using (DisposeResponsibility<Derived> owner = new(new Derived())) { }
            using DisposeResponsibility<Derived> firstOwner = new(new Derived());
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsTrue(firstOwner.ContainsDisposable);
            using DisposeResponsibility<Base> secondOwner = new(firstOwner);
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using DisposeResponsibility<Base> thirdOwner = new();
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Base> fourthOwner = new();
            Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
            fourthOwner.TransferResponsibilityFrom(thirdOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
        }
    }
}


abstract class Base : IDisposable
{
    abstract public void Dispose();
}

class Derived : Base
{
    public Derived()
    {
    }
    public override void Dispose()
    {
    }
}
