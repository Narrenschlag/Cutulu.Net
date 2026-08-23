namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public static partial class Encoder
{
    /// <summary>
    /// This is an internal runtime marshal to give the encoder a bit of memory for decision making.
    /// It provides a little safety and a few utility methods.
    /// </summary>
    public ref struct Marshal(BinaryWriter writer, bool enable_logging = true) : IBinaryMarshal
    {
        public readonly BinaryWriter Writer = writer;
        public bool EnableLogging { get; set; } = enable_logging;

        /// <summary> Is consumed by the decoder. </summary>
        public bool FirstIterationConsumable { get; set; } = true;

        public bool IsValid => Writer != null;

        public long Position
        {
            get => Writer.BaseStream.Position;
            set => Writer.BaseStream.Position = value;
        }

        public long Length
        {
            get => Writer.BaseStream.Length;
        }

        public void Encode(object value, Type type)
        {
            // Write empty array
            if (value.IsNull() && type.IsArray) Writer.Write(new UNumber8());

            // Write object
            else Encoder.Encode(this, value);
        }

        public void Encode<T>(T value) => Encode(value, typeof(T));
    }
}