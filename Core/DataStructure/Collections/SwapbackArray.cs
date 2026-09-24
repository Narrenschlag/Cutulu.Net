namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

/// <summary>
/// Written by Maximilian Schecklmann on 3th of Nov 2025, inspired by Nic Barker's implementation.
/// </summary>
public class SwapbackArray<T> : ICollection<T>, IEnumerable<T>, ICollection, IEnumerable, IReadOnlyCollection<T>
{
    protected T[] _data;

    protected int _count;

    public virtual int Count => _count;
    public virtual int Length => Count;
    public virtual int Capacity => _data.Length;

    public virtual bool IsReadOnly => false; // Collections are not read-only by default
    public virtual bool IsSynchronized => false; // Not thread-safe
    public virtual object SyncRoot => this; // Use this instance as sync root

    public SwapbackArray(int capacity = 4)
    {
        _data = capacity == 0 ? [] : new T[capacity];
    }

    public SwapbackArray(params T[] data)
    {
        _data = data ?? [];
        _count = _data.Length;
    }

    public SwapbackArray(ICollection<T> data)
    {
        lock (data)
        {
            _data = new T[data.Count];
            _count = data.Count;
            data.CopyTo(_data, 0);
        }
    }

    public SwapbackArray(IList data)
    {
        _data = new T[data.Count];
        _count = data.Count;
        data.CopyTo(_data, 0);
    }

    public SwapbackArray(Span<T> span)
    {
        _data = new T[span.Length];
        _count = span.Length;
        span.CopyTo(_data);
    }

    public virtual ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref _data[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(T item)
    {
        if (item.IsNull()) return;

        if (_count == _data.Length)
        {
            var newCap = Math.Max(4, _data.Length * 2);
            Array.Resize(ref _data, newCap);
        }

        _data[_count++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void AddRange(params T[] items)
    {
        AddRange(items.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void AddRange(ReadOnlySpan<T> items)
    {
        int newCount = _count + items.Length;

        // Ensure capacity
        if (newCount > _data.Length)
        {
            int newCap = Math.Max(_data.Length * 2, newCount);
            Array.Resize(ref _data, newCap);
        }

        // Bulk copy using spans (fastest)
        items.CopyTo(new Span<T>(_data, _count, items.Length));
        _count = newCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void AddRange(ICollection<T> items)
    {
        int newCount = _count + items.Count;

        // Ensure capacity
        if (newCount > _data.Length)
        {
            int newCap = Math.Max(_data.Length * 2, newCount);
            Array.Resize(ref _data, newCap);
        }

        // Bulk copy using spans (fastest)
        items.CopyTo(_data, _count);
        _count = newCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

        _count--;
        if (index < _count)
            _data[index] = _data[_count];
        _data[_count] = default!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void RemoveAt(uint index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (uint)_count);

        _count--;
        if (index < _count)
            _data[index] = _data[_count];
        _data[_count] = default!;
    }

    public virtual T[] ToArray()
    {
        T[] array = new T[_count];
        Array.Copy(_data, 0, array, 0, _count);
        return array;
    }

    public virtual Span<T> AsSpan() => _data.AsSpan(0, _count);

    public virtual Span<T> AsSpan(int count) => _data.AsSpan(0, int.Min(_count, count));

    public virtual Span<T> AsSpan(int start, int count)
    {
        start = Math.Max(0, Math.Min(start, _count));
        count = Math.Min(count, _count - start);
        return _data.AsSpan(start, count);
    }

    public virtual void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_data, 0, _count); // Only clear if T contains references
        }
        _count = 0;
    }

    public virtual bool Contains(T item)
    {
        return Array.IndexOf(_data, item, 0, _count) >= 0; // Only search within Count
    }

    public virtual bool Contains(T item, out int index)
    {
        return (index = Array.IndexOf(_data, item, 0, _count)) >= 0; // Only search within Count
    }

    public virtual void CopyTo(T[] array, int arrayIndex)
    {
        Array.Copy(_data, 0, array, arrayIndex, _count);
    }

    public virtual bool Remove(T item)
    {
        if (item is null) return false; // Use non-godot null check to gurantee removal

        for (int i = 0; i < _count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(_data[i], item))
            {
                RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public virtual bool Remove(T item, out int index)
    {
        index = 0;

        if (item.IsNull()) return false;

        for (int i = 0; i < _count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(_data[i], item))
            {
                RemoveAt(index = i);
                return true;
            }
        }

        return false;
    }

    public virtual IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _data[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public virtual void CopyTo(Array array, int index)
    {
        Array.Copy(_data, 0, array, index, _count);
    }

    /// <summary>
    /// Adds item to the array if it is not null and not already in it.
    /// </summary>
    public virtual bool TryAdd(T item) => TryAdd(item, out _);

    /// <summary>
    /// Adds item to the array if it is not null and not already in it.
    /// </summary>
    public virtual bool TryAdd(T item, out int index)
    {
        if (item.IsNull())
        {
            index = -1;
            return false;
        }

        else if (Contains(item, out index))
        {
            return false;
        }

        else
        {
            index = Count;
            Add(item);
            return true;
        }
    }

    /// <summary>
    /// Removes all duplicate items and null instances from the array.
    /// </summary>
    public virtual SwapbackArray<T> ClearDuplicates()
    {
        HashSet<T> seen = new(int.Max(4, _count / 2));

        for (int i = _count - 1; i >= 0; i--)
        {
            if (_data[i].IsNull() || seen.Add(_data[i]) == false)
                RemoveAt(i);
        }

        return this;
    }

    /// <summary>
    /// Adds items to the array if they are not null and not already in it.
    /// </summary>
    public virtual void TryAddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return;

        HashSet<T> seen = [];
        int i;

        for (i = 0; i < _count; i++)
        {
            if (_data[i].NotNull())
                seen.Add(_data[i]);
        }

        for (i = 0; i < items.Length; i++)
        {
            if (items[i].NotNull() && seen.Add(items[i]))
                Add(items[i]);
        }
    }

    public virtual void TryAddRange(params T[] items) => TryAddRange(items.AsSpan());

    public virtual int IndexOf(T item)
    {
        if (item.NotNull())
            for (int i = 0; i < _count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_data[i], item))
                    return i;
            }

        return -1;
    }

    public virtual void Enumerate(Action<T> action)
    {
        for (int i = 0; i < _count; i++)
            try { action(_data[i]); }
            catch (Exception e) { Debug.LogError($"Failed to invoke action: {e.Message}\n{e.StackTrace}"); }
    }
}

class SwapbackArrayEncoder() : BinaryEncoder(typeof(SwapbackArray<>))
{
    private static readonly ConcurrentDictionary<Type, Type> ItemTypeCache = [];

    public override void Encode(Core.Encoder.Marshal writer, Type type, object value)
    {
        var itemType = ItemTypeCache.GetOrAdd(type, t =>
        {
            return t.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                    .GetGenericArguments()[0];
        });

        var count = (UNumber64)(value == null ? 0 : ((ICollection)value).Count);
        writer.Encode(count);

        if (count > 0)
        {
            foreach (var item in (IEnumerable)value)
                writer.Encode(item, itemType);
        }
    }

    public override object Decode(Decoder.Marshal marshal, Type type)
    {
        var itemType = ItemTypeCache.GetOrAdd(type, t =>
        {
            return t.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                    .GetGenericArguments()[0];
        });

        var count = marshal.Decode<UNumber64>();
        var arrayType = itemType.MakeArrayType(); // T[]
        var array = (Array)Activator.CreateInstance(arrayType, (int)count);

        for (int i = 0; i < count; i++)
            array.SetValue(marshal.Decode(itemType), i);

        return Activator.CreateInstance(type, array); // matches SwapbackArray(params T[] data)
    }
}