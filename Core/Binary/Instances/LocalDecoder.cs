namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public sealed class LocalDecoder : IDisposable
{
    private readonly MemoryStream _memory;
    private readonly BinaryReader _reader;

    public long Length => _memory.Length;
    public long RemainingLength => _memory.Length - _memory.Position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetPosition() => _memory.Position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPosition(long position) => _memory.Position = position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] GetBuffer() => _memory.ToArray();

    public LocalDecoder() { _reader = new(_memory = new()); }

    //public LocalDecoder(byte[] buffer) => _reader = new(_memory = new(buffer)); 

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecode<T>(out T value, bool enable_logging = true)
    {
        long position = _memory.Position;

        if (_reader.TryDecode(out value, enable_logging)) return true;

        // Reset reader if failed to decoder
        _memory.Position = position;

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Decode<T>()
    {
        return _reader.Decode<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object Decode(Type type)
    {
        return _reader.Decode(type);
    }

    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

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

    public void Dispose()
    {
        _reader?.Dispose();
        _memory?.Dispose();
    }
}