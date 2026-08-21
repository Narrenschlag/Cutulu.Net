namespace Cutulu.Core
{
    using System;

    /// <summary>
    /// Represents a number. Dynamic in it's byte size.
    /// </summary>
    public partial struct Number
    {
        public byte[] Buffer { get; set; }

        public readonly TypeEnum GetNumberType() =>
            Buffer.IsEmpty() ? TypeEnum.Invalid :
            Buffer.Length == 8 ? TypeEnum.Long :
            Buffer.Length == 4 ? TypeEnum.Int :
            Buffer.Length == 2 ? TypeEnum.Short :
            TypeEnum.SByte;

        public Number() { Buffer = null; }
        public Number(object value)
        {
            while (true)
            {
                switch (value)
                {
                    case sbyte v:
                        Buffer = v.Encode();
                        return;

                    case short v:
                        if (IsSByte(ref v))
                        {
                            value = (sbyte)v;
                            break;
                        }

                        // Is short
                        else
                        {
                            Buffer = v.Encode();
                            return;
                        }

                    case int v:
                        if (IsShort(ref v))
                        {
                            value = (short)v;
                            break;
                        }

                        // Is int
                        else
                        {
                            Buffer = v.Encode();
                            return;
                        }

                    case long v:
                        if (IsInt(ref v))
                        {
                            value = (int)v;
                            break;
                        }

                        // Is long
                        else
                        {
                            Buffer = v.Encode();
                            return;
                        }

                    default:
                        Buffer = Array.Empty<byte>();
                        return;
                }
            }

            static bool IsSByte(ref short value) => value <= sbyte.MaxValue && value >= sbyte.MinValue;
            static bool IsShort(ref int value) => value <= short.MaxValue && value >= short.MinValue;
            static bool IsInt(ref long value) => value <= int.MaxValue && value >= int.MinValue;
        }

        public readonly sbyte GetSByte()
        {
            return GetNumberType() switch
            {
                TypeEnum.SByte => Buffer.Decode<sbyte>(),
                TypeEnum.Short => (sbyte)Buffer.Decode<short>(),
                TypeEnum.Int => (sbyte)Buffer.Decode<int>(),
                TypeEnum.Long => (sbyte)Buffer.Decode<long>(),
                _ => default,
            };
        }

        public readonly short GetShort()
        {
            return GetNumberType() switch
            {
                TypeEnum.SByte => Buffer.Decode<sbyte>(),
                TypeEnum.Short => Buffer.Decode<short>(),
                TypeEnum.Int => (short)Buffer.Decode<int>(),
                TypeEnum.Long => (short)Buffer.Decode<long>(),
                _ => default,
            };
        }

        public readonly int GetInt()
        {
            return GetNumberType() switch
            {
                TypeEnum.SByte => Buffer.Decode<sbyte>(),
                TypeEnum.Short => Buffer.Decode<short>(),
                TypeEnum.Int => Buffer.Decode<int>(),
                TypeEnum.Long => (int)Buffer.Decode<long>(),
                _ => default,
            };
        }

        public readonly long GetLong()
        {
            return GetNumberType() switch
            {
                TypeEnum.SByte => Buffer.Decode<sbyte>(),
                TypeEnum.Short => Buffer.Decode<short>(),
                TypeEnum.Int => Buffer.Decode<int>(),
                TypeEnum.Long => Buffer.Decode<long>(),
                _ => default,
            };
        }

        public static byte GetNumberId(params Number[] numbers)
        {
            if (numbers.Length > 4) throw new($"Can only identify up to 4 numbers at a time.");

            var bitBuilder = new BitBuilder(default(byte));

            for (var i = 0; i < numbers.Length; i++)
            {
                switch (numbers[i].GetNumberType())
                {
                    case TypeEnum.SByte:
                        bitBuilder.AddBinary("00");
                        break;

                    case TypeEnum.Short:
                        bitBuilder.AddBinary("01");
                        break;

                    case TypeEnum.Int:
                        bitBuilder.AddBinary("10");
                        break;

                    case TypeEnum.Long:
                        bitBuilder.AddBinary("11");
                        break;
                }
            }

            bitBuilder.Fill(8, false);

            return bitBuilder.ByteBuffer[0];
        }

        public static TypeEnum[] ReadNumberId(byte numberId)
        {
            var bitBuilder = new BitBuilder(numberId);
            var result = new TypeEnum[4];

            for (var i = 0; i < 4; i++)
            {
                result[i] = (TypeEnum)bitBuilder.GetByte(i * 2, 2);
                result[i]++;
            }

            return result;
        }

        public static implicit operator Number(sbyte value) => new(value);
        public static implicit operator Number(short value) => new(value);
        public static implicit operator Number(int value) => new(value);
        public static implicit operator Number(long value) => new(value);

        public static implicit operator sbyte(Number value) => value.GetSByte();
        public static implicit operator short(Number value) => value.GetShort();
        public static implicit operator int(Number value) => value.GetInt();
        public static implicit operator long(Number value) => value.GetLong();

        class Encoder() : BinaryEncoder(typeof(Number))
        {
            public override void Encode(System.IO.BinaryWriter writer, System.Type type, object value)
            {
                var number = (Number)value;

                writer.Write((byte)number.Buffer.Length);
                writer.Write(number.Buffer);
            }

            public override object Decode(Decoder.Marshal marshal, System.Type type)
            {
                var _length = marshal.Reader.ReadByte();
                return new Number() { Buffer = marshal.Reader.ReadBytes(_length), };
            }
        }

        public enum TypeEnum : byte
        {
            Invalid,

            SByte,
            Short,
            Int,
            Long,
        }
    }
}