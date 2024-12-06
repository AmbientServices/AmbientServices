using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices;

/// <summary>
/// A non-blocking version of <see cref="HashSet{T}"/>.
/// </summary>
#pragma warning disable CA1710  // we're following the precedent set by the framework itself rather than the code analyzer rules here, and given the name of this class, it would be very confusing not to
public class ConcurrentHashSet<T> : /* ISerializable, IDeserializationCallback, */ ISet<T>, ICollection<T>, IEnumerable<T>, System.Collections.IEnumerable
#pragma warning restore CA1710
    where T : notnull // ConcurrentDictionary<,> requires this
{
    private readonly ConcurrentDictionary<T, byte> _dict;
    /// <summary>
    /// Constructs an empty ConcurrentHashSet.
    /// </summary>
    public ConcurrentHashSet()
        : this(EqualityComparer<T>.Default)
    {
    }
    /// <summary>
    /// Constructs a ConcurrentHashSet with the specified items in it.
    /// </summary>
    /// <param name="collection">An enumeration of items to initialize the set with.</param>
    public ConcurrentHashSet(IEnumerable<T> collection)
        : this(collection, EqualityComparer<T>.Default)
    {
    }
    /// <summary>
    /// Constructs a ConcurrentHashSet with the specified item comparer.
    /// </summary>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> to use to compare items in the set.</param>
    public ConcurrentHashSet(IEqualityComparer<T> comparer)
    {
        _dict = new ConcurrentDictionary<T, byte>(comparer);
        Comparer = comparer;
    }
    /// <summary>
    /// Constructs a ConcurrentHashSet with the specified item comparer and the specified items in it.
    /// </summary>
    /// <param name="collection">An enumeration of items to initialize the set with.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> to use to compare items in the set.</param>
    public ConcurrentHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
    {
        _dict = new ConcurrentDictionary<T, byte>(collection.Select(a => new KeyValuePair<T, byte>(a, 0)), comparer);
        Comparer = comparer;
    }
    //protected ConcurrentHashSet(SerializationInfo info, StreamingScope scope)
    //{
    //}
    /// <summary>
    /// Gets the item comparer used for this set.
    /// </summary>
    public IEqualityComparer<T> Comparer { get; }
    /// <summary>
    /// Gets the number of items currently in this set.
    /// </summary>
    public int Count => _dict.Count;
    /// <summary>
    /// Gets whether or not the set is empty.
    /// </summary>
    public bool IsEmpty => _dict.IsEmpty;
    /// <summary>
    /// Adds the specified item to the set if it is not already there.
    /// </summary>
    /// <param name="item">The item to add to the set.</param>
    /// <returns>Whether or not the item was added (as opposed to it already being there).</returns>
    public bool Add(T item) { return _dict.TryAdd(item, 0); }
    /// <summary>
    /// Adds the specified item to the set if it is not already there.
    /// </summary>
    /// <param name="item">The item to add to the set.</param>
    /// <returns>Whether or not the item was added (as opposed to it already being there).</returns>
    public bool TryAdd(T item) { return _dict.TryAdd(item, 0); }
    /// <summary>
    /// Adds the specified item to the set if it is not already there.
    /// </summary>
    /// <param name="item">The item to add to the set.</param>
    void ICollection<T>.Add(T item) { _dict.TryAdd(item, 0); }
    /// <summary>
    /// Clears all items from the set.
    /// </summary>
    public void Clear() { _dict.Clear(); }
    /// <summary>
    /// Checks to see whether or not the specified item is in the set.
    /// </summary>
    /// <param name="item">The item to look for.</param>
    /// <returns><b>true</b> if the item is in the set, otherwise <b>false</b>.</returns>
    public bool Contains(T item) { return _dict.ContainsKey(item); }
    /// <summary>
    /// Copies all the items in the set into the specified array.
    /// </summary>
    /// <param name="array">The array to copy items into.</param>
    public void CopyTo(T[] array) { _dict.Keys.CopyTo(array, 0); }
    /// <summary>
    /// Copies all the items in the set into the specified array starting at the specified location.
    /// </summary>
    /// <param name="array">The array to copy items into.</param>
    /// <param name="arrayIndex">The offset within the array where the first item is to be placed.</param>
    public void CopyTo(T[] array, int arrayIndex) { _dict.Keys.CopyTo(array, arrayIndex); }
//        public void CopyTo(T[] array, int arrayIndex, int count) { _dict.Keys.CopyTo(array, arrayIndex, count); }
    private static readonly HashSetComparer _DefaultComparer = new();

#pragma warning disable CA1000  // not sure how else this could possibly be accomplished?
    /// <summary>
    /// Creates a <see cref="IEqualityComparer{T}"/> for comparing sets (using the default item comparer).
    /// </summary>
    /// <returns>An <see cref="IEqualityComparer{T}"/></returns>
    public static IEqualityComparer<ConcurrentHashSet<T>> CreateSetComparer()
    {
        return _DefaultComparer;
    }
#pragma warning restore CA1000

    private class HashSetComparer : IEqualityComparer<ConcurrentHashSet<T>>
    {
        public bool Equals(ConcurrentHashSet<T>? x, ConcurrentHashSet<T>? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            return x.IsSubsetOf(y) && y.IsSubsetOf(x);
        }
        public int GetHashCode(ConcurrentHashSet<T> obj)
        {
            int hashcode = 0;
            foreach (T item in obj)
            {
                hashcode ^= item.GetHashCode();
            }
            return hashcode;
        }
    }
    /// <summary>
    /// Removes all elements in the specified collection from the current set.
    /// </summary>
    /// <param name="other">An enumeration of items to remove from this set.</param>
    public void ExceptWith(IEnumerable<T>? other)
    {
        if (other == null) return;
        byte junk;
        foreach (T item in other)
        {
            _dict.TryRemove(item, out junk);
        }
    }
    //[SecurityCritical]
    //public virtual void GetObjectData(SerializationInfo info, StreamingScope scope);
    /// <summary>
    /// Modifies the current set to contain only elements that are present in that object and in the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to keep.</param>
    public void IntersectWith(IEnumerable<T>? other)
    {
        if (other == null) { Clear(); return; }
        HashSet<T> keep = new(other);
        foreach (T item in this)
        {
            if (!keep.Contains(item))
            {
                Remove(item);
            }
        }
    }
    /// <summary>
    /// Determines whether this set is a proper subset of the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns><b>true</b> if this set is a proper subset of the specified collection.</returns>
    public bool IsProperSubsetOf(IEnumerable<T>? other)
    {
        if (other == null) return false;
        HashSet<T> valid = new(other);
        if (_dict.Count >= valid.Count) return false;
        foreach (T item in this)
        {
            if (!valid.Contains(item)) return false;
        }
        return true;
    }
    /// <summary>
    /// Determines whether this set is a proper superset of the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns><b>true</b> if this set is a proper superset of the specified collection.</returns>
    public bool IsProperSupersetOf(IEnumerable<T>? other)
    {
        if (other == null) return !_dict.IsEmpty;
        int items = 0;
        foreach (T item in other)
        {
            ++items;
            if (!_dict.ContainsKey(item)) return false;
        }
        if (_dict.Count <= items) return false;
        return true;
    }
    /// <summary>
    /// Determines whether this set is a subset of the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns><b>true</b> if this set is a subset of the specified collection.</returns>
    public bool IsSubsetOf(IEnumerable<T>? other)
    {
        if (other == null) return _dict.IsEmpty;
        HashSet<T> valid = new(other);
        if (_dict.Count > valid.Count) return false;
        foreach (T item in this)
        {
            if (!valid.Contains(item)) return false;
        }
        return true;
    }
    /// <summary>
    /// Determines whether this set is a superset of the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns><b>true</b> if this set is a superset of the specified collection.</returns>
    public bool IsSupersetOf(IEnumerable<T>? other)
    {
        if (other == null) return true;
        int items = 0;
        foreach (T item in other)
        {
            ++items;
            if (!_dict.ContainsKey(item)) return false;
        }
        return true;
    }
    //        public virtual void OnDeserialization(object sender);
    /// <summary>
    /// Checks to see whether or not there are any items common between this set and the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns><b>true</b> if there is at least one item that exists in both this set and the specified collection.</returns>
    public bool Overlaps(IEnumerable<T>? other)
    {
        if (other == null) return false;
        foreach (T item in other)
        {
            if (_dict.ContainsKey(item)) return true;
        }
        return false;
    }
    /// <summary>
    /// Removes an item from the set.
    /// </summary>
    /// <param name="item">The item to remove from the set.</param>
    /// <returns>Whether or not the item was removed (it may not have been there to begin with).</returns>
    public bool Remove(T item) { byte junk; return _dict.TryRemove(item, out junk); }
    /// <summary>
    /// Removes all items in the set that match the specified predicate.
    /// </summary>
    /// <param name="match">A <see cref="Predicate{T}"/> to use to evaluate each item in the set.</param>
    /// <returns>The number of items removed from the set.</returns>
    public int RemoveWhere(Predicate<T>? match)
    {
        if (match == null) return 0;
        int removed = 0;
        foreach (T item in this)
        {
            if (match(item))
            {
                Remove(item);
                ++removed;
            }
        }
        return removed;
    }
    /// <summary>
    /// Checks to see whether or not this set contains exactly the same items as the specified collection.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    /// <returns>Whether or not this set contains exactly the same items as the specified collection.</returns>
    public bool SetEquals(IEnumerable<T>? other)
    {
        if (other == null) return _dict.IsEmpty;
        int items = 0;
        foreach (T item in other)
        {
            ++items;
            if (!_dict.ContainsKey(item)) return false;
        }
        return _dict.Count == items;
    }
    /// <summary>
    /// Modifies the current set to contain only elements that are present either in that object or in the specified collection, but not both.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    public void SymmetricExceptWith(IEnumerable<T>? other)
    {
        if (other == null) return;
        foreach (T item in other)
        {
            if (_dict.ContainsKey(item))
            {
                Remove(item);
            }
            else
            {
                Add(item);
            }
        }
    }
    //        public void TrimExcess() {  }
    /// <summary>
    /// Modifies the current set to contain all elements that are present in itself, the specified collection, or both.
    /// </summary>
    /// <param name="other">An enumeration of items to compare to.</param>
    public void UnionWith(IEnumerable<T>? other)
    {
        if (other == null) return;
        foreach (T item in other)
        {
            Add(item);
        }
    }
    /// <summary>
    /// Gets whether or not this set is readonly.
    /// </summary>
    public bool IsReadOnly => false;

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _dict.Keys.GetEnumerator();
    }
    /// <summary>
    /// Gets an <see cref="IEnumerator{T}"/> that can be used to enumerate items in this set.
    /// </summary>
    /// <returns>An <see cref="IEnumerator{T}"/> that can be used to enumerate items in this set.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return _dict.Keys.GetEnumerator();
    }

    /// <summary>
    /// Gets a string representation of this instance.
    /// </summary>
    /// <returns>A string representation of this instance.</returns>
    public override string ToString()
    {
        return "{" + string.Join(",", this.Take(20)) + "}";
    }
}
