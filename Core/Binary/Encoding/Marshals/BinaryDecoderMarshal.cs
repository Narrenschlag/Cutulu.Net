namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public static partial class Decoder
{
    /// <summary>
    /// This is an internal runtime marshal to give the decoder a bit of memory for decision making.
    /// It provides a little safety and a few utility methods.
    /// </summary>
    public ref struct Marshal(BinaryReader reader, bool enable_logging = true)
    {
        public readonly BinaryReader Reader = reader;
        public readonly bool EnableLogging = enable_logging;

        /// <summary> Is consumed by the decoder. </summary>
        public bool FirstIterationConsumable = true;

        public bool IsReady => Reader != null;

        public long Position
        {
            get => Reader.BaseStream.Position;
            set => Reader.BaseStream.Position = value;
        }

        public long Length
        {
            get => Reader.BaseStream.Length;
        }

        public long RemainingByteLength
        {
            get => Reader.BaseStream.Length - Reader.BaseStream.Position;
        }

        public object Decode(Type type)
        {
            return Decoder.Decode(this, type);
        }

        public T Decode<T>(T defaultValue = default)
        {
            var obj = Decoder.Decode(this, typeof(T));
            return obj is T t && t.NotNull() ? t : defaultValue;
        }

        public bool TryDecode<T>(out T value)
        {
            var obj = Decoder.Decode(this, typeof(T));

            if (obj is T t && t.NotNull())
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }
    }
}