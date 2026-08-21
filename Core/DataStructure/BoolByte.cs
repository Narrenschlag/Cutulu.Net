namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public struct BoolByte
{
    public static readonly BoolByte True = new()
    {
        _bits = byte.MaxValue
    };

    public static readonly BoolByte False = new()
    {
        _bits = 0
    };

    public const byte BoolCountCapacity = 8;
    private byte _bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoolByte(byte bits) => _bits = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoolByte(bool i1, bool i2 = false, bool i3 = false, bool i4 = false,
                    bool i5 = false, bool i6 = false, bool i7 = false, bool i8 = false)
    {
        _bits = (byte)(
            (i1 ? 0x01 : 0) | (i2 ? 0x02 : 0) |
            (i3 ? 0x04 : 0) | (i4 ? 0x08 : 0) |
            (i5 ? 0x10 : 0) | (i6 ? 0x20 : 0) |
            (i7 ? 0x40 : 0) | (i8 ? 0x80 : 0));
    }

    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & (1 << index)) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | (1 << index) : _bits & ~(1 << index));
    }

    public bool Item1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x01) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x01 : _bits & ~0x01);
    }
    public bool Item2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x02) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x02 : _bits & ~0x02);
    }
    public bool Item3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x04) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x04 : _bits & ~0x04);
    }
    public bool Item4
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x08) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x08 : _bits & ~0x08);
    }
    public bool Item5
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x10) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x10 : _bits & ~0x10);
    }
    public bool Item6
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x20) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x20 : _bits & ~0x20);
    }
    public bool Item7
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x40) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x40 : _bits & ~0x40);
    }
    public bool Item8
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (_bits & 0x80) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _bits = (byte)(value ? _bits | 0x80 : _bits & ~0x80);
    }

    // Branchless indexed access — no switch, no array
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index) => (_bits & (1 << index)) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, bool value)
    {
        int mask = 1 << index;
        _bits = (byte)(value ? _bits | mask : _bits & ~mask);
    }

    // Flip a bit without branching
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Toggle(int index) => _bits ^= (byte)(1 << index);

    // Check multiple bits at once
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AnySet() => _bits != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllSet() => _bits == 0xFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount() => System.Numerics.BitOperations.PopCount(_bits);

    public readonly byte RawByte => _bits;

    // Implicit conversions so you can treat it as a byte directly
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte(BoolByte b) => b._bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BoolByte(byte b) => new(b);

    public override string ToString() => $"[{_bits:B8}]"; // binary string e.g. [00000101]

    class Encoder() : BinaryEncoder(typeof(BoolByte))
    {
        public override void Encode(BinaryWriter writer, Type type, object value)
        {
            writer.Write(((BoolByte)value).RawByte);
        }

        public override object Decode(Decoder.Marshal marshal, Type type)
        {
            return new BoolByte(marshal.Reader.ReadByte());
        }
    }
}