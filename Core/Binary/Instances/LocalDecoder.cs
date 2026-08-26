namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public sealed class LocalDecoder : IDisposable
{
    #region Params & Constructors

    public Func<Decoder.Marshal, object> GenericNestedObjectDecoder;
    public IBinaryMarshal.DebugLogEnum DebugLog;

    private readonly MemoryStream _memory;
    private readonly BinaryReader _reader;

    public long RemainingLength => _memory.Length - _memory.Position;
    public long Length => _memory.Length;
    public long Position
    {
        get => _memory.Position;
        set => _memory.Position = value;
    }

    private Decoder.Marshal Marshal => new(_reader, GenericNestedObjectDecoder, DebugLog);

    public LocalDecoder(IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None)
    {
        _reader = new(_memory = new());
        DebugLog = debugLog;
    }

    public LocalDecoder(Func<Decoder.Marshal, object> genericNestedObjectDecoder, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None) : this(debugLog)
    {
        GenericNestedObjectDecoder = genericNestedObjectDecoder;
    }

    public LocalDecoder(byte[] buffer)
    {
        _memory = new MemoryStream();
        _memory.Write(buffer, 0, buffer.Length);
        _memory.Position = 0;
        _reader = new BinaryReader(_memory);
    }

    public LocalDecoder(MemoryStream stream)
    {
        _memory = stream;
        _memory.Position = 0;
        _reader = new BinaryReader(_memory);
    }

    #endregion

    #region Decoding

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Decode<T>()
    {
        return Marshal.Decode<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object Decode(Type type)
    {
        return Marshal.Decode(type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecode<T>(out T value)
    {
        long position = _memory.Position;

        if (Marshal.TryDecode(out value)) return true;

        // Reset reader if failed to decode
        _memory.Position = position;

        value = default;
        return false;
    }

    #endregion

    #region Utility & Else

    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] GetBuffer() => _memory.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(byte[] buffer) => Append(buffer.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(Span<byte> buffer)
    {
        long position = _memory.Position;

        _memory.Write(buffer);

        _memory.Position = position; // Keep old position
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _memory.SetLength(0);
        _memory.Position = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(byte[] buffer) => Reset(buffer.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition() => _memory.Position = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(Span<byte> buffer)
    {
        Clear();

        Append(buffer);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _memory?.Dispose();
    }

    #endregion

    #region Static

#if WEB_APP
    public static async Task<LocalDecoder> Create(HttpContext http, bool _enable_logging = true)
    {
        if ((http?.Request?.Body ?? null) is Stream stream && stream.CanRead)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            if (memoryStream.Length > 0)
            {
                using var reader = new BinaryReader(memoryStream);
                return new LocalDecoder(memoryStream);
            }

            else if (_enable_logging)
                Debug.LogError($"Http.Request.Body is empty.");
        }

        else if (_enable_logging)
            Debug.LogError($"Http.Request.Body is either null or not readable.");

        return null;
    }
#endif

    #endregion
}