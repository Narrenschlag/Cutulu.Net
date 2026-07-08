namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections;
using System;

public static class Collectionf
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NotEmpty<T>(this IEnumerable<T> collection)
    {
        if (collection != null)
        {
            switch (collection)
            {
                case ICollection<T> c: return c.Count > 0;
                case IReadOnlyCollection<T> r: return r.Count > 0;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty<T>(this IEnumerable<T> collection) => !NotEmpty(collection);

    public static bool NotEmpty<T>(this T[] collection) => collection != null && collection.Length > 0;
    public static bool IsEmpty<T>(this T[] collection) => !NotEmpty(collection);

    public static int Size<T>(this ICollection<T> collection) => collection != null ? collection.Count : 0;

    public static T[] ToArray<T>(this ICollection<T> collection)
    {
        if (collection.NotEmpty())
        {
            var array = new T[collection.Count];

            collection.CopyTo(array, 0);
            return array;
        }

        return [];
    }

    public static T[] ToArray<T>(this ICollection collection)
    {
        if (collection != null && collection.Count > 0)
        {
            var array = new T[collection.Count];

            collection.CopyTo(array, 0);
            return array;
        }

        return [];
    }

    public static T RandomElement<T>(this ICollection<T> source)
    {
        if (source.IsNull()) throw new ArgumentNullException(nameof(source));

        if (source.Count == 0) throw new InvalidOperationException("Collection contains no elements.");

        var rnd = System.Random.Shared;
        int index = rnd.Next(source.Count);

        foreach (var item in source)
        {
            if (index-- == 0)
                return item;
        }

        throw new InvalidOperationException("Collection changed during enumeration.");
    }

    public static T RandomElement<T>(this IEnumerable<T> source)
    {
        if (source.IsNull()) throw new ArgumentNullException(nameof(source));

        using var enumerator = source.GetEnumerator();

        if (!enumerator.MoveNext()) throw new InvalidOperationException("Sequence contains no elements.");

        T result = enumerator.Current;
        int count = 1;

        var rnd = System.Random.Shared;

        while (enumerator.MoveNext())
        {
            count++;

            // Replace current selection with probability 1/count
            if (rnd.Next(count) == 0) result = enumerator.Current;
        }

        return result;
    }
}