namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.Numerics;
using System;

/// <summary>
/// Represents an unsigned number. Dynamic in it's byte size.
/// T stands for the maximum binary value type.
/// </summary>
public readonly struct UNumber<MAX_VALUE_TYPE> :
    IIncrementOperators<UNumber<MAX_VALUE_TYPE>>, IDecrementOperators<UNumber<MAX_VALUE_TYPE>>,
    IAdditionOperators<UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>>,
    ISubtractionOperators<UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>>,
    IMultiplyOperators<UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>>,
    IDivisionOperators<UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>, UNumber<MAX_VALUE_TYPE>>
    where MAX_VALUE_TYPE : unmanaged, IMinMaxValue<MAX_VALUE_TYPE>,
    IBinaryInteger<MAX_VALUE_TYPE>, IUnsignedNumber<MAX_VALUE_TYPE>,
    IIncrementOperators<MAX_VALUE_TYPE>, IDecrementOperators<MAX_VALUE_TYPE>
{
    public static readonly UNumber<MAX_VALUE_TYPE> Zero = new(MAX_VALUE_TYPE.Zero);
    public static readonly UNumber<MAX_VALUE_TYPE> MaxValue = new(MAX_VALUE_TYPE.MaxValue);

    [Encodable] public readonly MAX_VALUE_TYPE Value;

    public UNumber(MAX_VALUE_TYPE value) => Value = value;
    public UNumber() => Value = default;

    public override string ToString() => Value.ToString()!;

    public enum TypeEnum : byte
    {
        Invalid,
        Byte,
        UShort,
        UInt,
        ULong,
    }

    // Type classification still works generically
    public readonly TypeEnum GetTypeEnum() => Unsafe.SizeOf<MAX_VALUE_TYPE>() switch
    {
        1 => TypeEnum.Byte,
        2 => TypeEnum.UShort,
        4 => TypeEnum.UInt,
        8 => TypeEnum.ULong,
        _ => TypeEnum.Invalid,
    };

    public static byte GetLength(TypeEnum _type) => _type switch
    {
        TypeEnum.UShort => 2,
        TypeEnum.UInt => 4,
        TypeEnum.ULong => 8,
        _ => 1,
    };

    // Operators
    public static UNumber<MAX_VALUE_TYPE> operator +(UNumber<MAX_VALUE_TYPE> left, UNumber<MAX_VALUE_TYPE> right) => new(left.Value + right.Value);
    public static UNumber<MAX_VALUE_TYPE> operator -(UNumber<MAX_VALUE_TYPE> left, UNumber<MAX_VALUE_TYPE> right) => new(left.Value - right.Value);
    public static UNumber<MAX_VALUE_TYPE> operator *(UNumber<MAX_VALUE_TYPE> left, UNumber<MAX_VALUE_TYPE> right) => new(left.Value * right.Value);
    public static UNumber<MAX_VALUE_TYPE> operator /(UNumber<MAX_VALUE_TYPE> left, UNumber<MAX_VALUE_TYPE> right) => new(left.Value / right.Value);
    public static UNumber<MAX_VALUE_TYPE> operator ++(UNumber<MAX_VALUE_TYPE> value) => new(value.Value + MAX_VALUE_TYPE.One);
    public static UNumber<MAX_VALUE_TYPE> operator --(UNumber<MAX_VALUE_TYPE> value) => new(value.Value - MAX_VALUE_TYPE.One);

    // Implicit conversions from/to T
    public static implicit operator UNumber<MAX_VALUE_TYPE>(MAX_VALUE_TYPE value) => new(value);
    public static implicit operator MAX_VALUE_TYPE(UNumber<MAX_VALUE_TYPE> value) => value.Value;

    // Implicit operators FROM concrete types — T.CreateSaturating clamps instead of throwing on overflow
    public static implicit operator UNumber<MAX_VALUE_TYPE>(byte value) => new(MAX_VALUE_TYPE.CreateSaturating(value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(ushort value) => new(MAX_VALUE_TYPE.CreateSaturating(value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(uint value) => new(MAX_VALUE_TYPE.CreateSaturating(value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(ulong value) => new(MAX_VALUE_TYPE.CreateSaturating(value));

    // Signed — clamp negatives to zero
    public static implicit operator UNumber<MAX_VALUE_TYPE>(sbyte value) => new(MAX_VALUE_TYPE.CreateSaturating(value < 0 ? 0 : value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(short value) => new(MAX_VALUE_TYPE.CreateSaturating(value < 0 ? 0 : value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(int value) => new(MAX_VALUE_TYPE.CreateSaturating(value < 0 ? 0 : value));
    public static implicit operator UNumber<MAX_VALUE_TYPE>(long value) => new(MAX_VALUE_TYPE.CreateSaturating(value < 0 ? 0 : value));

    /*
    // Implicit operators TO concrete types
    public static implicit operator byte(UNumber<MAX_VALUE_TYPE> value) => byte.CreateSaturating(value.Value);
    public static implicit operator ushort(UNumber<MAX_VALUE_TYPE> value) => ushort.CreateSaturating(value.Value);
    public static implicit operator uint(UNumber<MAX_VALUE_TYPE> value) => uint.CreateSaturating(value.Value);
    public static implicit operator ulong(UNumber<MAX_VALUE_TYPE> value) => ulong.CreateSaturating(value.Value);
    */

    public static implicit operator sbyte(UNumber<MAX_VALUE_TYPE> value) => sbyte.CreateSaturating(value.Value);
    public static implicit operator short(UNumber<MAX_VALUE_TYPE> value) => short.CreateSaturating(value.Value);
    public static implicit operator int(UNumber<MAX_VALUE_TYPE> value) => int.CreateSaturating(value.Value);
    public static implicit operator long(UNumber<MAX_VALUE_TYPE> value) => long.CreateSaturating(value.Value);
}

class UNumberEncoder() : BinaryEncoder(typeof(UNumber<>))
{
    private const byte DIV = 252;

    public override void Encode(System.IO.BinaryWriter writer, System.Type type, object value)
    {
        // Extract the ulong representation regardless of T
        var innerValue = Convert.ToUInt64(value.GetType().GetField("Value")!.GetValue(value));

        // Mirror original logic: small values fit in a single byte
        if (innerValue < DIV)
        {
            writer.Write((byte)innerValue);
            return;
        }

        // Determine the minimum type needed to store the value
        TypeEnum typeEnum =
            innerValue > uint.MaxValue ? TypeEnum.ULong :
            innerValue > ushort.MaxValue ? TypeEnum.UInt :
            innerValue > byte.MaxValue ? TypeEnum.UShort :
            TypeEnum.Byte;

        // Write type prefix byte
        writer.Write((byte)(typeEnum + DIV - 1));

        // Write the value in the minimum required bytes
        switch (typeEnum)
        {
            case TypeEnum.Byte: writer.Write((byte)innerValue); break;
            case TypeEnum.UShort: writer.Write((ushort)innerValue); break;
            case TypeEnum.UInt: writer.Write((uint)innerValue); break;
            case TypeEnum.ULong: writer.Write(innerValue); break;
        }
    }

    public override object Decode(Decoder.Marshal marshal, System.Type type)
    {
        var firstByte = marshal.Reader.ReadByte();

        // Recover the ulong value
        ulong innerValue;
        if (firstByte < DIV)
        {
            innerValue = firstByte;
        }
        else
        {
            var typeEnum = (TypeEnum)(firstByte - DIV + 1);
            innerValue = typeEnum switch
            {
                TypeEnum.Byte => marshal.Reader.ReadByte(),
                TypeEnum.UShort => marshal.Reader.ReadUInt16(),
                TypeEnum.UInt => marshal.Reader.ReadUInt32(),
                TypeEnum.ULong => marshal.Reader.ReadUInt64(),
                _ => 0
            };
        }

        // Reconstruct UNumber<T> — T is the generic arg of the concrete type e.g. UNumber<uint>
        var genericArg = type.GetGenericArguments()[0];
        var converted = Convert.ChangeType(innerValue, genericArg);

        return Activator.CreateInstance(type, converted)!;
    }

    private enum TypeEnum : byte
    {
        Invalid,
        Byte,
        UShort,
        UInt,
        ULong,
    }
}