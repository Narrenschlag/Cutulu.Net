namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

/// <summary>
/// Static class for decoding binary data
/// </summary>
public static partial class Decoder
{
    /// <summary>
    /// Decodes a buffer from given BinaryReader into an object
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Decode(this BinaryReader _reader, Type _type)
    {
        return Decode(new Marshal(_reader), _type);
    }

    /// <summary>
    /// Decodes a buffer from given BinaryReader into an object
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Decode<T>(this BinaryReader reader)
    {
        return (T)Decode(reader, typeof(T));
    }

    /// <summary>
    /// Decodes a buffer into an object
    /// </summary>
    public static object Decode(this byte[] _buffer, Type _type)
    {
        using var memory = new MemoryStream(_buffer);
        using var reader = new BinaryReader(memory);

        return Decode(reader, _type);
    }

    /// <summary>
    /// Decodes a buffer into an object
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Decode<T>(this byte[] _buffer)
    {
        var _obj = Decode(_buffer, typeof(T));
        return _obj is T _t ? _t : default;
    }

    /// <summary>
    /// Safely decodes a buffer into an object
    /// </summary>
    public static bool TryDecode(this BinaryReader _reader, Type _type, out object _value, bool _enable_logging = true)
    {
        try
        {
            return (_value = Decode(_reader, _type)).NotNull();
        }

        catch (Exception ex)
        {
            if (_enable_logging)
            {
                switch (ex)
                {
                    case EndOfStreamException _:
                        Debug.LogError($"Cannot decode as typeof({_type}): Unable to read beyond the end of the stream. Buffer may belong to another data type. [{BinaryEncoding.LastPropertyType.FullName}, {BinaryEncoding.LastPropertyName}?]");
                        Debug.LogWarning($"Error Message: {ex.Message}\n{ex.StackTrace}");
                        break;

                    default:
                        Debug.LogError($"Cannot decode typeof({_type}, {ex.GetType().Name}): {ex.Message}\n{ex.StackTrace}");
                        break;
                }
            }

            _value = default;
            return false;
        }
    }

    /// <summary>
    /// Safely decodes a buffer into an object
    /// </summary>
    public static bool TryDecode<T>(this BinaryReader _reader, out T _value, bool _enable_logging = true)
    {
        if (TryDecode(_reader, typeof(T), out object _obj, _enable_logging) && _obj is T _t)
        {
            _value = _t;
            return true;
        }

        _value = default;
        return false;
    }

#if WEB_APP
    public static async Task<(bool Success, T Value)> TryDecode<T>(this HttpContext http, bool _enable_logging = true)
    {
        if ((http?.Request?.Body ?? null) is Stream stream && stream.CanRead)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            if (ms.Length > 0)
            {
                using var reader = new BinaryReader(ms);

                if (reader.TryDecode(out T value, _enable_logging))
                    return (true, value);
                else return default;
            }

            else if (_enable_logging)
                Debug.LogError($"Http.Request.Body is empty.");
        }

        else if (_enable_logging)
            Debug.LogError($"Http.Request.Body is either null or not readable.");

        return default;
    }
#endif

    /// <summary>
    /// Safely decodes a buffer into an object
    /// </summary>
    public static bool TryDecode(this byte[] _buffer, Type _type, out object _value, bool _enable_logging = true)
    {
        try
        {
            return (_value = _buffer.NotEmpty() ? Decode(_buffer, _type) : null).NotNull();
        }

        catch (Exception ex)
        {
            if (_enable_logging)
            {
                switch (ex)
                {
                    case EndOfStreamException _ex:
                        Debug.LogError($"Cannot decode as typeof({_type}): Unable to read beyond the end of the stream. Buffer may belong to another data type. [{BinaryEncoding.LastPropertyType.FullName}, {BinaryEncoding.LastPropertyName}?, {_buffer.Size()} b]\n<{_ex.Message}>\n{Environment.StackTrace}");
                        Debug.LogWarning($"Error Message: {ex.Message}\n{ex.StackTrace}");
                        break;

                    default:
                        Debug.LogError($"Cannot decode typeof({_type}, {ex.GetType().Name}): {ex.Message}\n{ex.StackTrace}");
                        break;
                }
            }

            _value = default;
            return false;
        }
    }

    /// <summary>
    /// Safely decodes a buffer into an object
    /// </summary>
    public static bool TryDecode<T>(this byte[] _buffer, out T _value, bool _enable_logging = true)
    {
        var _decoded = TryDecode(_buffer, typeof(T), out var _obj, _enable_logging);

        _value = _decoded ? (T)_obj : default;
        return _decoded;
    }

    private static object Decode(Decoder.Marshal marshal, Type type)
    {
        BinaryEncoding.LastPropertyType = type;

        var firstIteration = marshal.FirstIterationConsumable;
        marshal.FirstIterationConsumable = false;
        var reader = marshal.Reader;

        return type switch
        {
            var t when t == typeof(byte[]) => firstIteration ? reader.ReadRemainingBytes() : reader.ReadBytes(Decode<UNumber64>(reader)),
            var t when t == typeof(string) => reader.ReadString(),
            var t when t == typeof(bool) => reader.ReadBoolean(),
            var t when t == typeof(char) => reader.ReadChar(),

            var t when t == typeof(long) => reader.ReadInt64(),
            var t when t == typeof(ulong) => reader.ReadUInt64(),
            var t when t == typeof(int) => reader.ReadInt32(),
            var t when t == typeof(uint) => reader.ReadUInt32(),
            var t when t == typeof(short) => reader.ReadInt16(),
            var t when t == typeof(ushort) => reader.ReadUInt16(),
            var t when t == typeof(byte) => reader.ReadByte(),
            var t when t == typeof(sbyte) => reader.ReadSByte(),

            var t when t == typeof(double) => reader.ReadDouble(),
            var t when t == typeof(float) => reader.ReadSingle(),

            _ => BinaryEncoding.TryGetEncoder(type, out var decoder) ?
                decoder.Decode(marshal, type) :
                DecodeUnknown(marshal, type)
        };

        static object DecodeUnknown(Decoder.Marshal _marshal, Type _type)
        {
            var _reader = _marshal.Reader;

            // Enums
            if (_type.IsEnum)
            {
                return Enum.ToObject(_type, _marshal.Decode(_type.GetEnumUnderlyingType()));
            }

            // Arrays
            if (_type.IsArray)
            {
                _type = _type.GetElementType();

                // Unable to read beyond end of stream (UNumber is always atleast 1 byte)
                if (_reader.RemainingByteLength() < 1)
                {
                    throw new EndOfStreamException($"Unable to read array. Reached end of stream. {_reader.RemainingByteLength()}");
                }

                var _array = Array.CreateInstance(_type, _marshal.Decode<UNumber64>());

                for (ushort i = 0; i < _array.Length; i++)
                {
                    _array.SetValue(_marshal.Decode(_type), i);
                }

                return _array;
            }

            // Classes and structs
            else return AutoDecode(_marshal, _type);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AutoDecode<T>(this Marshal _marshal) => AutoDecode(_marshal, typeof(T)) is T _t ? _t : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object AutoDecode(this Marshal _marshal, Type _type)
    {
        try
        {
            var _output = Activator.CreateInstance(_type);

            var _manager = ParameterManager.Open(_type, null, BinaryEncoding.IncludeAttributes, BinaryEncoding.ExcludeAttributes);

            var infos = _manager.GetInfos();
            foreach (ref var info in infos)
            {
                BinaryEncoding.LastPropertyName = info.GetName();

                info.SetValue(_output, _marshal.Decode(info.GetValueType()));
            }

            return _output;
        }

        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new System.Reflection.TargetInvocationException(
                $"Cannot call Activator.CreateInstance. Check if there are problems with references in constructors.",
                ex.InnerException
            );
        }

        catch { throw; }
    }
}
