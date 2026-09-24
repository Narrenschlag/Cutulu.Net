namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections;
using System;

using Lock = System.Threading.Lock;

/// <summary>
/// Thread safe version of the SwapbackArray. Written on 23rd of September 2026 by Maximilian Schecklmann.
/// </summary>
public class ThreadSafeSwapbackArray<T> : SwapbackArray<T>
{
    private readonly Lock _lock = new();

    public override int Count
    {
        get
        {
            lock (_lock) return base.Count;
        }
    }

    public override int Capacity
    {
        get
        {
            lock (_lock) return base.Capacity;
        }
    }

    public override bool IsSynchronized => true; // Is thread-safe

    public ThreadSafeSwapbackArray(int capacity = 4) : base(capacity) { }

    public ThreadSafeSwapbackArray(params T[] data) : base(data) { }

    public ThreadSafeSwapbackArray(ICollection<T> data) : base(data) { }

    public ThreadSafeSwapbackArray(IList data) : base(data) { }

    public ThreadSafeSwapbackArray(Span<T> span) : base(span) { }

    public override ref T this[int index]
    {
        get
        {
            lock (_lock)
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref _data[index];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Add(T item)
    {
        lock (_lock) base.Add(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void AddRange(ReadOnlySpan<T> items)
    {
        lock (_lock) base.AddRange(items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void AddRange(ICollection<T> items)
    {
        lock (_lock) base.AddRange(items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void RemoveAt(int index)
    {
        lock (_lock) base.RemoveAt(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void RemoveAt(uint index)
    {
        lock (_lock) base.RemoveAt(index);
    }

    public override T[] ToArray()
    {
        lock (_lock) return base.ToArray();
    }

    public override Span<T> AsSpan()
    {
        lock (_lock) return base.AsSpan();
    }

    public override Span<T> AsSpan(int count)
    {
        lock (_lock) return base.AsSpan(count);
    }

    public override Span<T> AsSpan(int start, int count)
    {
        lock (_lock) return base.AsSpan(start, count);
    }

    public override void Clear()
    {
        lock (_lock) base.Clear();
    }

    public override bool Contains(T item)
    {
        lock (_lock) return base.Contains(item);
    }

    public override bool Contains(T item, out int index)
    {
        lock (_lock) return base.Contains(item, out index);
    }

    public override void CopyTo(T[] array, int arrayIndex)
    {
        lock (_lock) base.CopyTo(array, arrayIndex);
    }

    public override bool Remove(T item)
    {
        lock (_lock) return base.Remove(item);
    }

    public override bool Remove(T item, out int index)
    {
        lock (_lock) return base.Remove(item, out index);
    }

    public override IEnumerator<T> GetEnumerator()
    {
        lock (_lock) return base.GetEnumerator();
    }

    public override void CopyTo(Array array, int index)
    {
        lock (_lock) base.CopyTo(array, index);
    }

    public override bool TryAdd(T item)
    {
        lock (_lock) return base.TryAdd(item);
    }

    public override bool TryAdd(T item, out int index)
    {
        lock (_lock) return base.TryAdd(item, out index);
    }

    public override SwapbackArray<T> ClearDuplicates()
    {
        lock (_lock) return base.ClearDuplicates();
    }

    public override void TryAddRange(ReadOnlySpan<T> items)
    {
        lock (_lock) base.TryAddRange(items);
    }

    public override void TryAddRange(params T[] items)
    {
        lock (_lock) base.TryAddRange(items);
    }

    public override int IndexOf(T item)
    {
        lock (_lock) return base.IndexOf(item);
    }

    public override void Enumerate(Action<T> action)
    {
        lock (_lock) base.Enumerate(action);
    }
}