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
    public ref struct Marshal(BinaryReader reader, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None, bool isFirstIteration = true) : IBinaryMarshal
    {
        #region Params & Constructors

        private readonly Func<Marshal, object> GenericNestedObjectDecoder = null;
        private readonly bool HasGenericNestedObjectDecoder = false;
        private bool FirstIterationConsumable = isFirstIteration;

        public IBinaryMarshal.DebugLogEnum DebugLog { get; set; } = debugLog;
        public readonly BinaryReader Reader = reader;

        /// <summary>
        /// The generic object encoder allows for custom encoding of objects.
        /// It is only called if a field or property is found to be typeof(object).
        /// </summary>
        public Marshal(BinaryReader reader, Func<Marshal, object> genericNestedObjectDecoder, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None, bool isFirstIteration = true)
        : this(reader, debugLog, isFirstIteration)
        {
            HasGenericNestedObjectDecoder = genericNestedObjectDecoder != null;
            GenericNestedObjectDecoder = genericNestedObjectDecoder;
        }

        public readonly bool IsValid => Reader != null;

        public readonly long Position
        {
            get => Reader.BaseStream.Position;
            set => Reader.BaseStream.Position = value;
        }

        public readonly long Length => Reader.BaseStream.Length;

        public readonly long RemainingByteLength => Reader.BaseStream.Length - Reader.BaseStream.Position;

        #endregion

        #region Public

        public bool TryDecode<T>(out T value)
        {
            var obj = Decode(typeof(T));

            if (obj is T t && t.NotNull())
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public T Decode<T>(T defaultValue = default)
        {
            var obj = Decode(typeof(T));
            return obj is T t && t.NotNull() ? t : defaultValue;
        }

        public object Decode(Type type)
        {
            BinaryEncoding.LastPropertyType = type;

            var firstIteration = FirstIterationConsumable;
            FirstIterationConsumable = false;

            return type switch
            {
                var t when t == typeof(byte[]) => firstIteration ? Reader.ReadRemainingBytes() : Reader.ReadBytes(Decode<UNumber64>()),
                var t when t == typeof(string) => Reader.ReadString(),
                var t when t == typeof(bool) => Reader.ReadBoolean(),
                var t when t == typeof(char) => Reader.ReadChar(),

                var t when t == typeof(long) => Reader.ReadInt64(),
                var t when t == typeof(ulong) => Reader.ReadUInt64(),
                var t when t == typeof(int) => Reader.ReadInt32(),
                var t when t == typeof(uint) => Reader.ReadUInt32(),
                var t when t == typeof(short) => Reader.ReadInt16(),
                var t when t == typeof(ushort) => Reader.ReadUInt16(),
                var t when t == typeof(byte) => Reader.ReadByte(),
                var t when t == typeof(sbyte) => Reader.ReadSByte(),

                var t when t == typeof(double) => Reader.ReadDouble(),
                var t when t == typeof(float) => Reader.ReadSingle(),

                _ => BinaryEncoding.TryGetEncoder(type, out var decoder) ?
                    decoder.Decode(this, type) :
                    DecodeUnknown(type)
            };
        }

        #endregion

        #region Private

        private object DecodeUnknown(Type _type)
        {
            // Enums
            if (_type.IsEnum)
            {
                return Enum.ToObject(_type, Decode(_type.GetEnumUnderlyingType()));
            }

            // Arrays
            if (_type.IsArray)
            {
                _type = _type.GetElementType();

                // Unable to read beyond end of stream (UNumber is always atleast 1 byte)
                if (Reader.RemainingByteLength() < 1)
                {
                    throw new EndOfStreamException($"Unable to read array. Reached end of stream. {Reader.RemainingByteLength()}");
                }

                var _array = Array.CreateInstance(_type, Decode<UNumber64>());

                for (ushort i = 0; i < _array.Length; i++)
                {
                    _array.SetValue(Decode(_type), i);
                }

                return _array;
            }

            // Classes and structs
            else return AutoDecode(_type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object AutoDecode(Type _type)
        {
            try
            {
                // Use generic object decoder for nested typeof(object) value if available
                if (HasGenericNestedObjectDecoder && _type == typeof(object))
                    return GenericNestedObjectDecoder.Invoke(this);

                var _output = Activator.CreateInstance(_type);

                var _manager = ParameterManager.Open(_type, null, BinaryEncoding.IncludeAttributes, BinaryEncoding.ExcludeAttributes);

                var infos = _manager.GetInfos();
                foreach (ref var info in infos)
                {
                    BinaryEncoding.LastPropertyName = info.GetName();

                    info.SetValue(_output, Decode(info.GetValueType()));
                }

                return _output;
            }

            catch (System.Reflection.TargetInvocationException ex)
            {
                throw new System.Reflection.TargetInvocationException(
                    $"Cannot call Activator.CreateInstance. Check if there are problems with references in constructors.",
                    ex.InnerException
                );
            }

            catch { throw; }
        }

        #endregion
    }
}