namespace Cutulu.Core;

using System.IO;
using System;

/// <summary>
/// A write-once binary buffer that supports encoding multiple objects sequentially
/// and tracking the byte range of each encoded segment for later retrieval without allocating seperate buffer arrays.
/// <para>
/// Start indices are stored compactly: index 0 is always 0 and is omitted from
/// <see cref="OrderedStartIndexArray"/>, so the array holds only the starts of
/// objects at positions 1, 2, … (i.e. the cumulative byte offset just after each
/// preceding object).
/// </para>
/// </summary>
public class BufferPool : IDisposable
{
    /// <summary>
    /// Stores the byte offset at which each object after the first begins.
    /// Index 0 (always 0) is implicit and not stored here.
    /// </summary>
    private readonly SwapbackArray<int> OrderedStartIndexArray = [];

    private readonly MemoryStream Stream;
    private readonly BinaryWriter Writer;

    /// <summary>Initialises an empty pool.</summary>
    public BufferPool()
    {
        Stream = new MemoryStream();
        Writer = new BinaryWriter(Stream);
    }

    /// <summary>
    /// Initialises a pool by reading <paramref name="length"/> raw bytes from
    /// <paramref name="reader"/>. The bytes are treated as a single, unindexed blob
    /// (no start-index entry is created).
    /// </summary>
    public BufferPool(BinaryReader reader, int length) : this()
    {
        Writer.Write(reader.ReadBytes(length));
    }

    /// <summary>
    /// Initialises a pool with a single encoded object. Equivalent to constructing
    /// an empty pool and calling <see cref="Encode(object, bool)"/> once.
    /// </summary>
    public BufferPool(object obj, bool iAmAwareThisIsNotCapacity) : this()
    {
        if (iAmAwareThisIsNotCapacity) Writer.Encode(obj);
    }

    /// <summary>Returns a copy of the underlying buffer as a byte array.</summary>
    public byte[] GetBuffer() => Stream?.ToArray() ?? [];

    /// <summary>Returns the total number of bytes currently written to the pool.</summary>
    public int GetLength() => (int)(Stream?.Length ?? 0);

    /// <summary>Returns true if the pool is not empty.</summary>
    public bool NotEmpty() => GetLength() > 0;

    /// <summary>Returns true if the pool is empty.</summary>
    public bool IsEmpty() => GetLength() < 1;

    /// <summary>Clears the pool and resets the start-index array.</summary>
    public void Clear()
    {
        if (Stream.NotNull())
        {
            Stream?.SetLength(0);
            Stream.Position = 0;
        }

        OrderedStartIndexArray.Clear();
    }

    /// <summary>
    /// Encodes <paramref name="obj"/> and appends its bytes to the pool.
    /// A start-index entry is recorded so the segment can later be retrieved via
    /// <see cref="GetStart"/>, <see cref="GetLength(int)"/>, or <see cref="GetRanges"/>.
    /// <para>
    /// The first object never produces an index entry (its start is always 0).
    /// Subsequent objects record their start only when bytes were actually written,
    /// unless <paramref name="forceAddStartIndex"/> is <c>true</c>, in which case
    /// the entry is added even if <paramref name="obj"/> encoded to zero bytes.
    /// </para>
    /// <para>
    /// <b>Note:</b> <paramref name="forceAddStartIndex"/> is silently ignored for the
    /// very first call (when the stream is empty) because index 0 is implicit.
    /// </para>
    /// </summary>
    /// <param name="obj">The object to encode.</param>
    /// <param name="forceAddStartIndex">
    /// When <c>true</c>, records a start-index entry even if encoding produced no bytes.
    /// Defaults to <c>true</c>.
    /// </param>
    public void Encode(object obj, bool forceAddStartIndex = true)
    {
        int length0 = GetLength();

        Writer.Encode(obj);

        if (length0 > 0 && (forceAddStartIndex || GetLength() > length0))
            OrderedStartIndexArray.Add(length0);
    }

    public byte[] GetBuffer(int entryIdx)
    {
        var length = GetLength(entryIdx);
        var start = GetStart(entryIdx);

        long position = Stream.Position;

        byte[] buffer = new byte[length];
        Stream.Position = start;
        Stream.Read(buffer, 0, length);

        Stream.Position = position;
        return buffer;
    }

    /// <summary>
    /// Returns the number of encoded segments. Always at least 1, even for an
    /// empty pool. Check <see cref="GetLength()"/> to distinguish a truly empty pool.
    /// </summary>
    public int GetIndexCount() => NotEmpty() ? OrderedStartIndexArray.Count + 1 : 0;

    /// <summary>
    /// Returns the byte offset at which segment <paramref name="index"/> begins.
    /// Segment 0 always starts at 0.
    /// </summary>
    public int GetStart(int index) => index < 1 ? 0 : OrderedStartIndexArray[index - 1];

    /// <summary>
    /// Returns the byte length of segment <paramref name="index"/>.
    /// The last segment extends to the end of the buffer.
    /// </summary>
    public int GetLength(int index)
    {
        int end = index >= OrderedStartIndexArray.Count ? GetLength() : OrderedStartIndexArray[index];
        return end - GetStart(index);
    }

    /// <summary>
    /// Returns the (Start, Length) byte range for every encoded segment, in order.
    /// </summary>
    public (int Start, int Length)[] GetRanges()
    {
        var array = new (int Start, int Length)[GetIndexCount()];

        for (int i = 0; i < array.Length; i++)
        {
            array[i] = (GetStart(i), GetLength(i));
        }

        return array;
    }

    /// <summary>Disposes the writer and the underlying stream.</summary>
    public void Dispose()
    {
        Writer?.Dispose();
        Stream?.Dispose();
    }

    /// <summary>
    /// Handles binary serialisation and deserialisation of <see cref="BufferPool"/>.
    /// Wire format: [int totalLength][bytes][int startIndexCount][int… startIndices]
    /// </summary>
    class Encoder() : BinaryEncoder(typeof(BufferPool))
    {
        public override void Encode(BinaryWriter writer, Type type, object value)
        {
            if (value is BufferPool pool)
            {
                writer.Write(pool.GetLength());
                writer.Write(pool.GetBuffer());

                writer.Write(pool.OrderedStartIndexArray.Count);
                var span = pool.OrderedStartIndexArray.AsSpan();
                for (int i = 0; i < span.Length; i++)
                {
                    writer.Write(span[i]);
                }
            }
        }

        public override object Decode(Decoder.Marshal marshal, Type type)
        {
            var pool = new BufferPool(marshal.Reader, marshal.Reader.ReadInt32());

            int count = marshal.Reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                pool.OrderedStartIndexArray.Add(marshal.Reader.ReadInt32());
            }

            return pool;
        }
    }
}