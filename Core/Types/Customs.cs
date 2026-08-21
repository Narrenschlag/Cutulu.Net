namespace Cutulu.Core
{
    using System.IO;

    public struct ColorRGB
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

#if GODOT4_0_OR_GREATER
        public ColorRGB(Godot.Color color)
        {
            R = (byte)color.R8;
            G = (byte)color.G8;
            B = (byte)color.B8;
        }
#endif

        public ColorRGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

#if GODOT4_0_OR_GREATER
        public readonly Godot.Color RGB32(float alpha = 1) => new(R / 255f, G / 255f, B / 255f, alpha);
#endif

        class Encoder() : BinaryEncoder(typeof(ColorRGB))
        {
            public override void Encode(BinaryWriter writer, System.Type type, object value)
            {
                var obj = (ColorRGB)value;

                writer.Write(obj.R);
                writer.Write(obj.G);
                writer.Write(obj.B);
            }

            public override object Decode(Decoder.Marshal marshal, System.Type type) => new ColorRGB(marshal.Reader.ReadByte(), marshal.Reader.ReadByte(), marshal.Reader.ReadByte());
        }
    }

#if GODOT4_0_OR_GREATER
    public struct Vector2S
    {
        public short X { get; set; }
        public short Y { get; set; }

        public Vector2S(short x, short y)
        {
            X = x;
            Y = y;
        }

        public readonly Godot.Vector3 ToVector3(float y = 0) => new(X, y, Y);
        public readonly Godot.Vector2 ToVector2() => new(X, Y);

        class Encoder() : BinaryEncoder(typeof(Vector2S))
        {
            public override void Encode(BinaryWriter writer, System.Type type, object value)
            {
                var obj = (Vector2S)value;

                writer.Write(obj.X);
                writer.Write(obj.Y);
            }

            public override object Decode(Decoder.Marshal marshal, System.Type type) => new Vector2S(marshal.Reader.ReadInt16(), marshal.Reader.ReadInt16());
        }
    }
#endif

    public struct Vector38
    {
        public byte[] Values;

        public Vector38(byte x, byte y, byte z) => Values = [x, y, z];

        public class Encoder() : BinaryEncoder(typeof(Vector38))
        {
            public override void Encode(BinaryWriter writer, System.Type type, object value) => writer.Write(((Vector38)value).Values);

            public override object Decode(Decoder.Marshal marshal, System.Type type) => new Vector38(marshal.Reader.ReadByte(), marshal.Reader.ReadByte(), marshal.Reader.ReadByte());
        }
    }
}