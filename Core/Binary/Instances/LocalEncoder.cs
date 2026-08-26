namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public sealed class LocalEncoder : IDisposable
{
    #region Params & Constructors

    public Action<Encoder.Marshal, object> GenericNestedObjectEncoder;
    public IBinaryMarshal.DebugLogEnum DebugLog;

    private readonly MemoryStream _memory;
    private readonly BinaryWriter _writer;

    public long Length => _memory.Length;
    public long Position
    {
        get => _memory.Position;
        set => _memory.Position = value;
    }

    private Encoder.Marshal Marshal => new(_writer, GenericNestedObjectEncoder, DebugLog);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] GetBuffer() => _memory.ToArray();

    public LocalEncoder(IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None)
    {
        _writer = new(_memory = new());
        DebugLog = debugLog;
    }

    public LocalEncoder(Action<Encoder.Marshal, object> genericNestedObjectEncoder, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None) : this(debugLog)
    {
        GenericNestedObjectEncoder = genericNestedObjectEncoder;
    }

    #endregion

    #region Encoding

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Encode<T>(T value)
    {
        Marshal.Encode(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Encode(object value, Type type)
    {
        Marshal.Encode(value, type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEncode<T>(T value)
    {
        return Marshal.TryEncode(value, typeof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEncode(object value, Type type)
    {
        return Marshal.TryEncode(value, type);
    }

    #endregion

    #region Utility & Else

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _memory.SetLength(0);
        _memory.Position = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LocalDecoder ToDecoder(bool reuseStream = false) => reuseStream ? new(_memory) : new(GetBuffer());

    public void Dispose()
    {
        _writer?.Dispose();
        _memory?.Dispose();
    }

    #endregion
}