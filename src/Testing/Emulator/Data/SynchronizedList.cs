// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

internal sealed class SynchronizedList<T> : IList<T>
{
    private readonly List<T> _items = [];
    private readonly object _sync = new();

    public int Count { get { lock (_sync) return _items.Count; } }
    public bool IsReadOnly => false;
    public T this[int index]
    {
        get { lock (_sync) return _items[index]; }
        set { lock (_sync) _items[index] = value; }
    }

    public void Add(T item) { lock (_sync) _items.Add(item); }
    public void Clear() { lock (_sync) _items.Clear(); }
    public bool Contains(T item) { lock (_sync) return _items.Contains(item); }
    public void CopyTo(T[] array, int arrayIndex) { lock (_sync) _items.CopyTo(array, arrayIndex); }
    public int IndexOf(T item) { lock (_sync) return _items.IndexOf(item); }
    public void Insert(int index, T item) { lock (_sync) _items.Insert(index, item); }
    public bool Remove(T item) { lock (_sync) return _items.Remove(item); }
    public void RemoveAt(int index) { lock (_sync) _items.RemoveAt(index); }
    public IEnumerator<T> GetEnumerator()
    {
        lock (_sync) return ((IEnumerable<T>)_items.ToArray()).GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}