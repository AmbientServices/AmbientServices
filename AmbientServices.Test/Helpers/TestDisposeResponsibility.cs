using AmbientServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AmbientServices.Test.Helpers;

[TestClass]
public class TestDisposeResponsibility
{
    [TestMethod]
    public void OwnershipTransfer()
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
        secondOwner.TransferTo(thirdOwner);
        Assert.IsFalse(firstOwner.ContainsDisposable);
        Assert.IsFalse(secondOwner.ContainsDisposable);
        Assert.IsTrue(thirdOwner.ContainsDisposable);
        using DisposeResponsibility<Stream> fourthOwner = new();
        Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        fourthOwner.TransferFrom(thirdOwner);
        Assert.IsFalse(firstOwner.ContainsDisposable);
        Assert.IsFalse(secondOwner.ContainsDisposable);
        Assert.IsFalse(thirdOwner.ContainsDisposable);
        Assert.IsTrue(fourthOwner.ContainsDisposable);
        using DisposeResponsibility<Stream> fifthOwner = new(new MemoryStream());
        Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        fourthOwner.TransferTo(fifthOwner);
        Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        Assert.IsFalse(firstOwner.ContainsDisposable);
        Assert.IsFalse(secondOwner.ContainsDisposable);
        Assert.IsFalse(thirdOwner.ContainsDisposable);
        Assert.IsFalse(fourthOwner.ContainsDisposable);
        Assert.IsTrue(fifthOwner.ContainsDisposable);
        using DisposeResponsibility<Stream> sixthOwner = new(new MemoryStream());
        Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        sixthOwner.TransferFrom(fifthOwner);
        Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        Assert.IsFalse(firstOwner.ContainsDisposable);
        Assert.IsFalse(secondOwner.ContainsDisposable);
        Assert.IsFalse(thirdOwner.ContainsDisposable);
        Assert.IsFalse(fourthOwner.ContainsDisposable);
        Assert.IsFalse(fifthOwner.ContainsDisposable);
        Assert.IsTrue(sixthOwner.ContainsDisposable);
        using DisposeResponsibility<MemoryStream> seventhOwner = new();
        Assert.ThrowsException<ObjectDisposedException>(() => seventhOwner.Contained);
        Assert.AreEqual(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        seventhOwner.Transfer(new());
        Assert.AreEqual(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
        Assert.IsFalse(firstOwner.ContainsDisposable);
        Assert.IsFalse(secondOwner.ContainsDisposable);
        Assert.IsFalse(thirdOwner.ContainsDisposable);
        Assert.IsFalse(fourthOwner.ContainsDisposable);
        Assert.IsFalse(fifthOwner.ContainsDisposable);
        Assert.IsTrue(sixthOwner.ContainsDisposable);
        Assert.IsTrue(seventhOwner.ContainsDisposable);
        Assert.ThrowsException<ArgumentNullException>(() => seventhOwner.TransferTo(null!));
        Assert.ThrowsException<ArgumentNullException>(() => seventhOwner.TransferFrom(null!));
        Assert.IsNotNull(seventhOwner.ToString());
    }
}
