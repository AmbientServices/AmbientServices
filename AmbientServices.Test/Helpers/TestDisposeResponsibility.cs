using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using (DisposeResponsibility<Stream> emptyShort = new())
            {
            }
            using DisposeResponsibility<Stream> thirdOwner = new();
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Stream> fourthOwner = new(new MemoryStream());
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            fourthOwner.TransferResponsibilityFrom(thirdOwner);
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
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
            Assert.Throws<ObjectDisposedException>(() => fifthOwner.Contained);
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            fifthOwner.AssumeResponsibility(new());
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(2, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsFalse(thirdOwner.ContainsDisposable);
            Assert.IsTrue(fourthOwner.ContainsDisposable);
            Assert.IsTrue(fifthOwner.ContainsDisposable);
            Assert.IsNotNull(fifthOwner.NullableContained);
            Assert.Throws<ArgumentNullException>(() => fifthOwner.TransferResponsibilityFrom(null!));
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
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsTrue(firstOwner.ContainsDisposable);
            using DisposeResponsibility<Base> secondOwner = new(firstOwner);
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsTrue(secondOwner.ContainsDisposable);
            using DisposeResponsibility<Base> thirdOwner = new();
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
#endif
            thirdOwner.TransferResponsibilityFrom(secondOwner);
            Assert.IsFalse(firstOwner.ContainsDisposable);
            Assert.IsFalse(secondOwner.ContainsDisposable);
            Assert.IsTrue(thirdOwner.ContainsDisposable);
            using DisposeResponsibility<Base> fourthOwner = new();
#if DEBUG
            Assert.IsGreaterThanOrEqualTo(1, DisposeResponsibility.AllPendingDisposals.Select(e => e.Count).Sum());
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
    [TestMethod]
    public void DisposeResponsibilityNotification()
    {
        ResponsibilityNotDisposedEventArgs? args = null;
        void Handler(object? sender, ResponsibilityNotDisposedEventArgs e) => args = e;
        DisposeResponsibility.ResponsibilityNotDisposed += Handler;
        try
        {
            AllocateAndDontDispose();
            int gcCountBefore = GC.CollectionCount(2);
            for (int attempt = 0; attempt < 10 && args == null; ++attempt)
            {
                AllocateAndDontDispose();
                // allocate and then free a whole bunch of stuff to hopefully trigger the finalizer
                for (int a = 0; a < 1000; ++a)
                {
                    using IDisposable d = new MemoryStream(2048);
                }
                GC.Collect();// 2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                if (args != null) break;
                System.Threading.Thread.Sleep(Pseudorandom.Next.NextInt32 % ((attempt + 1) * 100));
            }
            int gcCountAfter = GC.CollectionCount(2);
            // did we actually get a garbage collection?
            if (gcCountAfter != gcCountBefore)
            {
                Assert.IsNotNull(args);
            }
            // else just skip the testing
        }
        finally
        {
            DisposeResponsibility.ResponsibilityNotDisposed -= Handler;
        }
    }
    private static void AllocateAndDontDispose()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        DisposeResponsibility<Derived> d = new(new Derived());
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// <see cref="DisposeResponsibility{T}.Dispose"/> calls private <c>DisposeContained</c>; types that are only <see cref="IDisposable"/>
    /// ensure the sync dispose branch is exercised (not only <see cref="MemoryStream"/>, which is also <see cref="IAsyncDisposable"/>).
    /// </summary>
    [TestMethod]
    public void Dispose_Contained_IdisposableOnly_DisposesViaDisposeContained()
    {
#pragma warning disable CA2000 // ownership transferred to DisposeResponsibility
        SyncDisposableOnly inner = new();
#pragma warning restore CA2000
        Assert.IsFalse(inner.Disposed);
#pragma warning disable CA2000
        using (DisposeResponsibility<SyncDisposableOnly> dr = new(inner))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(inner.Disposed);
    }

    /// <summary>
    /// <c>DisposeContained</c> sync path for <see cref="IAsyncDisposable"/> without <see cref="IDisposable"/> (second branch + Wait).
    /// </summary>
    [TestMethod]
    public void Dispose_Contained_IAsyncDisposableOnly_DisposesViaDisposeContained()
    {
#pragma warning disable CA2000
        AsyncDisposableOnly inner = new();
#pragma warning restore CA2000
        Assert.IsFalse(inner.DisposedAsync);
#pragma warning disable CA2000
        using (DisposeResponsibility<AsyncDisposableOnly> dr = new(inner))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(inner.DisposedAsync);
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// <c>DisposeContainedAsync</c> first branch: contained is only <see cref="IAsyncDisposable"/> (await <see cref="IAsyncDisposable.DisposeAsync"/>).
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_Contained_IAsyncDisposableOnly_DisposesViaDisposeContainedAsync()
    {
#pragma warning disable CA2000
        AsyncDisposableOnly inner = new();
#pragma warning restore CA2000
        Assert.IsFalse(inner.DisposedAsync);
#pragma warning disable CA2000
        await using (DisposeResponsibility<AsyncDisposableOnly> dr = new(inner))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(inner.DisposedAsync);
    }

    /// <summary>
    /// <see cref="DisposeResponsibility{T}.DisposeAsync"/> when nothing is contained: only <c>GC.SuppressFinalize</c>.
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_EmptyContained_SuppressesFinalizeOnly()
    {
        await using (DisposeResponsibility<MemoryStream> dr = new())
        {
            Assert.IsFalse(dr.ContainsDisposable);
        }
    }

    /// <summary>
    /// <c>DisposeContainedAsync</c> tuple loop: async element then sync-only element.
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_Tuple_MixedAsyncDisposableAndDisposable_DisposesBoth()
    {
#pragma warning disable CA2000
        AsyncDisposableOnly a = new();
        SyncDisposableOnly b = new();
#pragma warning restore CA2000
#pragma warning disable CA2000
        await using (DisposeResponsibility<(AsyncDisposableOnly, SyncDisposableOnly)> dr = new((a, b)))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(a.DisposedAsync);
        Assert.IsTrue(b.Disposed);
    }

    /// <summary>
    /// <c>DisposeContainedAsync</c> when contained is <see cref="IDisposable"/> only (not <see cref="IAsyncDisposable"/>).
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_Contained_IdisposableOnly_DisposesViaDisposeContainedAsync()
    {
#pragma warning disable CA2000
        SyncDisposableOnly inner = new();
#pragma warning restore CA2000
        Assert.IsFalse(inner.Disposed);
#pragma warning disable CA2000
        await using (DisposeResponsibility<SyncDisposableOnly> dr = new(inner))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(inner.Disposed);
    }

    /// <summary>
    /// <c>DisposeContainedAsync</c> tuple branch: elements can be async-disposable.
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_Contained_ValueTupleWithAsync_DisposesViaDisposeContainedAsync()
    {
#pragma warning disable CA2000
        AsyncDisposableOnly a = new();
        AsyncDisposableOnly b = new();
#pragma warning restore CA2000
        (AsyncDisposableOnly, AsyncDisposableOnly) pair = (a, b);
#pragma warning disable CA2000
        await using (DisposeResponsibility<(AsyncDisposableOnly, AsyncDisposableOnly)> dr = new(pair))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(a.DisposedAsync);
        Assert.IsTrue(b.DisposedAsync);
    }

    /// <summary>
    /// <c>DisposeContainedAsync</c> tuple loop: sync-only elements (both branches in loop are <see cref="IDisposable"/>).
    /// </summary>
    [TestMethod]
    public async Task DisposeAsync_Tuple_TwoSyncDisposables_DisposesViaDisposeContainedAsync()
    {
#pragma warning disable CA2000
        SyncDisposableOnly a = new();
        SyncDisposableOnly b = new();
#pragma warning restore CA2000
#pragma warning disable CA2000
        await using (DisposeResponsibility<(SyncDisposableOnly, SyncDisposableOnly)> dr = new((a, b)))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(a.Disposed);
        Assert.IsTrue(b.Disposed);
    }
#endif

    /// <summary>
    /// <c>DisposeContained</c> tuple branch for sync <see cref="IDisposable"/> elements.
    /// </summary>
    [TestMethod]
    public void Dispose_Contained_ValueTuple_DisposesViaDisposeContained()
    {
#pragma warning disable CA2000
        SyncDisposableOnly a = new();
        SyncDisposableOnly b = new();
#pragma warning restore CA2000
#pragma warning disable CA2000
        using (DisposeResponsibility<(SyncDisposableOnly, SyncDisposableOnly)> dr = new((a, b)))
#pragma warning restore CA2000
        {
            Assert.IsTrue(dr.ContainsDisposable);
        }
        Assert.IsTrue(a.Disposed);
        Assert.IsTrue(b.Disposed);
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

/// <summary>Implements only <see cref="IDisposable"/> so <see cref="DisposeResponsibility{T}.DisposeAsync"/> does not take the IAsyncDisposable fast path.</summary>
internal sealed class SyncDisposableOnly : IDisposable
{
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}

/// <summary>Implements only <see cref="IAsyncDisposable"/> so sync <see cref="DisposeResponsibility{T}.Dispose"/> hits the IAsyncDisposable branch of <c>DisposeContained</c>.</summary>
internal sealed class AsyncDisposableOnly : IAsyncDisposable
{
    public bool DisposedAsync { get; private set; }
    public ValueTask DisposeAsync()
    {
        DisposedAsync = true;
        return default;
    }
}
