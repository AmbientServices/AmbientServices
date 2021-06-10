using AmbientServices;
using AmbientServices.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    [TestClass]
    public class TestConcurrentHashSet
    {
        [TestMethod]
        public void ConcurrentHashSet()
        {
            ConcurrentHashSet<int> playSet = new ConcurrentHashSet<int>();
            ConcurrentHashSet<int> smallSet = new ConcurrentHashSet<int>(new int[] { 0, 1 });
            ConcurrentHashSet<int> bigSet = new ConcurrentHashSet<int>(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, EqualityComparer<int>.Default);
            ConcurrentHashSet<int> nullSet = null;
            Assert.AreEqual(EqualityComparer<int>.Default, bigSet.Comparer);
            Assert.IsFalse(string.IsNullOrEmpty(smallSet.ToString()));
            Assert.IsTrue(smallSet.IsProperSubsetOf(bigSet));
            Assert.IsTrue(smallSet.IsSubsetOf(bigSet));
            Assert.IsTrue(bigSet.IsProperSupersetOf(smallSet));
            Assert.IsTrue(bigSet.IsSupersetOf(smallSet));
            playSet.TryAdd(0);
            ((ICollection<int>)playSet).Add(1);
            Assert.IsTrue(playSet.Contains(0));
            Assert.IsTrue(smallSet.SetEquals(playSet));
            Assert.IsFalse(smallSet.SetEquals(bigSet));
            Assert.IsFalse(smallSet.SetEquals(null));
            Assert.IsTrue(smallSet.IsSubsetOf(playSet));
            Assert.IsFalse(bigSet.IsSubsetOf(playSet));
            Assert.IsFalse(bigSet.IsSubsetOf(null));
            Assert.IsFalse(playSet.IsSupersetOf(bigSet));
            Assert.IsTrue(playSet.IsSupersetOf(null));
            Assert.IsFalse(bigSet.IsSubsetOf(playSet));
            Assert.IsFalse(bigSet.IsSubsetOf(null));
            Assert.IsFalse(smallSet.IsProperSubsetOf(playSet));
            Assert.IsFalse(playSet.IsProperSupersetOf(smallSet));
            Assert.IsTrue(playSet.IsProperSupersetOf(null));
            Assert.IsFalse(smallSet.IsProperSubsetOf(null));
            Assert.IsFalse(smallSet.IsProperSubsetOf(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 })));
            Assert.IsFalse(smallSet.IsSupersetOf(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 })));
            Assert.IsFalse(smallSet.IsSubsetOf(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 })));
            Assert.IsFalse(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 }).IsProperSupersetOf(smallSet));
            Assert.IsFalse(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 }).IsSupersetOf(smallSet));
            Assert.IsFalse(new ConcurrentHashSet<int>(new int[] { 3, 4, 5 }).Overlaps(smallSet));
            IEqualityComparer<ConcurrentHashSet<int>> setComparer = ConcurrentHashSet<int>.CreateSetComparer();
            Assert.IsTrue(setComparer.Equals(smallSet, playSet));
            Assert.IsFalse(setComparer.Equals(bigSet, playSet));
            Assert.AreEqual(setComparer.GetHashCode(smallSet), setComparer.GetHashCode(playSet));
            Assert.IsTrue(setComparer.Equals(smallSet, smallSet));
            Assert.IsFalse(setComparer.Equals(smallSet, nullSet));
            Assert.IsFalse(setComparer.Equals(nullSet, smallSet));
            Assert.IsFalse(smallSet.IsReadOnly);

            Assert.IsTrue(smallSet.Overlaps(bigSet));
            Assert.IsFalse(smallSet.Overlaps(null));

            int[] test = new int[2];
            smallSet.CopyTo(test);
            smallSet.CopyTo(test, 0);

            smallSet.Add(2);

            playSet.Remove(1);
            bigSet.ExceptWith(playSet);
            bigSet.ExceptWith(null);
            Assert.IsFalse(bigSet.Contains(0));
            Assert.IsTrue(bigSet.Contains(1));
            smallSet.IntersectWith(playSet);
            Assert.IsTrue(smallSet.Contains(0));
            Assert.IsFalse(smallSet.Contains(1));

            ConcurrentHashSet<int> niSet = new ConcurrentHashSet<int>();
            niSet.Add(0);
            Assert.IsFalse(niSet.IsEmpty);
            niSet.IntersectWith(null);
            Assert.IsTrue(niSet.IsEmpty);
            niSet.Add(0);
            Assert.AreEqual(1, niSet.Count);
            niSet.RemoveWhere(null);
            Assert.AreEqual(1, niSet.Count);

            smallSet.IntersectWith(playSet);

            playSet.Add(0);
            playSet.Add(2);
            playSet.RemoveWhere(n => n != 0);
            Assert.IsTrue(playSet.Contains(0));
            Assert.IsFalse(playSet.Contains(1));

            playSet.Add(0);
            playSet.Add(2);
            smallSet.Add(1);
            playSet.SymmetricExceptWith(smallSet);
            playSet.SymmetricExceptWith(null);
            Assert.IsFalse(playSet.Contains(0));
            Assert.IsTrue(playSet.Contains(1));
            Assert.IsTrue(playSet.Contains(2));

            playSet.UnionWith(smallSet);
            playSet.UnionWith(null);
            Assert.IsTrue(playSet.Contains(0));
            Assert.IsTrue(playSet.Contains(1));
            Assert.IsTrue(playSet.Contains(2));
            Assert.IsFalse(playSet.Contains(3));

            playSet.Clear();
            Assert.IsFalse(smallSet.IsSubsetOf(playSet));

            foreach (object o in ((System.Collections.IEnumerable)smallSet))
            {
                Assert.IsTrue(o is int);
                Assert.IsTrue((int)o < 10);
            }
        }
    }
}
