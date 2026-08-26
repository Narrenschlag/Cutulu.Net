namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

/// <summary>
/// Static class for encoding and decoding binary data
/// </summary>
public static partial class Encoder
{
    /// <summary>
    /// Writes encoded buffer of an object to given BinaryWriter
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(this BinaryWriter _writer, object _obj, Type _type)
    {
        new Marshal(_writer).Encode(_obj);
    }

    /// <summary>
    /// Writes encoded buffer of an object to given BinaryWriter
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode<T>(this BinaryWriter _writer, T _obj)
    {
        if (_obj.NotNull()) Encode(_writer, _obj, typeof(T));

        // Write empty array
        else if (typeof(T).IsArray) _writer.Write(new UNumber8());
    }

    /// <summary>
    /// Encodes an object into a buffer
    /// </summary>
    public static byte[] Encode(this object obj, Type _type)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        Encode(writer, obj, _type);

        return memory.ToArray();
    }

    /// <summary>
    /// Encodes an object into a buffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encode<T>(this T obj)
    {
        return obj.NotNull() ? Encode(obj, obj.GetType()) : [];
    }

    /// <summary>
    /// Safely encodes an object into a buffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncode(this BinaryWriter _writer, object _obj, Type _type, bool _enable_logging = true)
    {
        return new Marshal(_writer, _enable_logging ? IBinaryMarshal.DebugLogEnum.Errors : IBinaryMarshal.DebugLogEnum.None).TryEncode(_obj, _type);
    }

    /// <summary>
    /// Safely encodes an object into a buffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncode<T>(this BinaryWriter _writer, T _obj, bool _enable_logging = true)
    {
        return TryEncode(_writer, _obj, typeof(T), _enable_logging);
    }

    /// <summary>
    /// Safely encodes an object into a buffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncode(object _obj, Type _type, out byte[] _buffer, bool _enable_logging = true)
    {
        try
        {
            return (_buffer = Encode(_obj, _type)) != null;
        }

        catch (Exception ex)
        {
            if (_enable_logging) Debug.LogError($"Cannot encode typeof({_type}): {ex.Message}\n{ex.StackTrace}");
            _buffer = null;
            return false;
        }
    }

    /// <summary>
    /// Safely encodes an object into a buffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncode<T>(T _obj, out byte[] _buffer, bool _enable_logging = true)
    {
        return TryEncode(_obj, typeof(T), out _buffer, _enable_logging);
    }
}