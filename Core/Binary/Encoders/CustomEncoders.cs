namespace Cutulu.Core;

using System.IO;
using System;

/// <summary>
/// Use static method Register() to see more.
/// </summary>
public static class CustomEncoders
{
    class hyteFormatter() : BinaryEncoder(typeof(Int4))
    {
        public override void Encode(BinaryWriter writer, Type type, object value) => writer.Write(((Int4)value).Byte);
        public override object Decode(Decoder.Marshal marshal, Type type) => new Int4() { Byte = marshal.Reader.ReadByte() };
    }

    class qyteFormatter() : BinaryEncoder(typeof(Int2))
    {
        public override void Encode(BinaryWriter writer, Type type, object value) => writer.Write(((Int2)value).Byte);
        public override object Decode(Decoder.Marshal marshal, Type type) => new Int2() { Byte = marshal.Reader.ReadByte() };
    }

    class middleFormatter() : BinaryEncoder(typeof(Int24))
    {
        public override void Encode(BinaryWriter writer, Type type, object value) => writer.Write(((Int24)value).Bytes);
        public override object Decode(Decoder.Marshal marshal, Type type) => new Int24() { Bytes = marshal.Reader.ReadBytes(3) };
    }

    class umiddleFormatter() : BinaryEncoder(typeof(UInt24))
    {
        public override void Encode(BinaryWriter writer, Type type, object value) => writer.Write(((UInt24)value).Bytes);
        public override object Decode(Decoder.Marshal marshal, Type type) => new UInt24() { Bytes = marshal.Reader.ReadBytes(3) };
    }
}