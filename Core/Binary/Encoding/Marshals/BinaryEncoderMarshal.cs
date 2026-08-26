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
    public ref struct Marshal(BinaryWriter writer, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None, bool isFirstIteration = true) : IBinaryMarshal
    {
        #region Params & Constructors

        private readonly Action<Marshal, object> GenericNestedObjectEncoder = null;
        private readonly bool HasGenericNestedObjectEncoder = false;
        private bool FirstIterationConsumable = isFirstIteration;

        public IBinaryMarshal.DebugLogEnum DebugLog { get; set; } = debugLog;
        public readonly BinaryWriter Writer = writer;

        /// <summary>
        /// The generic object encoder allows for custom encoding of objects.
        /// It is only called if a field or property is found to be typeof(object).
        /// </summary>
        public Marshal(BinaryWriter writer, Action<Marshal, object> genericNestedObjectEncoder, IBinaryMarshal.DebugLogEnum debugLog = IBinaryMarshal.DebugLogEnum.None, bool isFirstIteration = true)
        : this(writer, debugLog, isFirstIteration)
        {
            HasGenericNestedObjectEncoder = genericNestedObjectEncoder != null;
            GenericNestedObjectEncoder = genericNestedObjectEncoder;
        }

        public readonly bool IsValid => Writer != null;

        public readonly long Position
        {
            get => Writer.BaseStream.Position;
            set => Writer.BaseStream.Position = value;
        }

        public readonly long Length => Writer.BaseStream.Length;

        #endregion

        #region Public

        /// <summary>
        /// Safely encodes an object into a buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEncode(object value, Type _type)
        {
            try
            {
                Encode(value, _type);
                return true;
            }

            catch (Exception ex)
            {
                if (DebugLog.HasFlag(IBinaryMarshal.DebugLogEnum.Errors)) Debug.LogError($"Cannot encode typeof({_type}): {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public void Encode(object value, Type type)
        {
            // Write empty array
            if (value.IsNull() && type.IsArray) Writer.Write(new UNumber8());

            // Write object
            else Encode(value);
        }

        public void Encode<T>(T value) => Encode(value, typeof(T));

        #endregion

        #region Private

        private bool Encode(object obj)
        {
            if (obj.IsNull()) return false;

            var firstIteration = FirstIterationConsumable;
            FirstIterationConsumable = false;
            var writer = Writer;

            switch (obj)
            {
                case byte[] v:
                    if (firstIteration == false) Encode((UNumber64)v.Length);
                    writer.Write(v); break;

                case string v: writer.Write(v); break;
                case bool v: writer.Write(v); break;
                case char v: writer.Write(v); break;

                case long v: writer.Write(v); break;
                case ulong v: writer.Write(v); break;

                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;

                case short v: writer.Write(v); break;
                case ushort v: writer.Write(v); break;

                case byte v: writer.Write(v); break;
                case sbyte v: writer.Write(v); break;

                case double v: writer.Write(v); break;
                case float v: writer.Write(v); break;

                default:
                    var type = obj.GetType();

                    // Encode using custom encoder
                    if (BinaryEncoding.TryGetEncoder(type, out var encoder)) encoder.Encode(this, type, obj);

                    // Encode types without serializer
                    else if (EncodeUnknown(obj) == false) return false;

                    break;
            }

            return true;
        }

        private bool EncodeUnknown(object _obj)
        {
            //if (_obj == null) return false; -> _obj is already null-checked
            var _type = _obj.GetType();

            // Encode enum
            if (_type.IsEnum)
            {
                Encode(Convert.ChangeType(_obj, _type.GetEnumUnderlyingType()));
            }

            // Arrays
            else if (_type.IsArray && _obj is Array array)
            {
                Encode((UNumber64)array.Length);
                _type = _type.GetElementType();
                object _value;

                for (int i = 0; i < array.Length; i++)
                {
                    _value = array.GetValue(i);

                    // Write null array as empty array
                    if (_value == null && _type.IsArray) Writer.Write(new UNumber64());

                    // Write array value
                    else Encode(_value);
                }
            }

            // Classes and structs
            else AutoEncode(_type, _obj);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AutoEncode(Type _type, object _obj)
        {
            if (_obj.IsNull())
            {
                if (_type != null && _type.IsArray)
                    Writer.Write(new UNumber8());

                return;
            }

            var _manager = ParameterManager.Open(_type, null, BinaryEncoding.IncludeAttributes, BinaryEncoding.ExcludeAttributes);
            object _value;

            var infos = _manager.GetInfos();
            foreach (ref var info in infos)
            {
                try
                {
                    // Use generic object encoder for nested typeof(object) value if available
                    if (HasGenericNestedObjectEncoder && info.GetValueType() == typeof(object))
                    {
                        GenericNestedObjectEncoder.Invoke(this, _obj);
                        continue;
                    }

                    _value = info.GetValue(_obj);

                    // Write value
                    if (_value == null) Writer.Write(default(byte)); // Write null string/array as empty string/array
                    else Encode(_value); // Encode value as usual
                }

                catch (Exception ex)
                {
                    throw new Exception($"Could not encode value of type {info.Type} for object of type {_obj.GetType()}: {ex.Message}\n{ex.StackTrace}", ex);
                }
            }
        }

        #endregion
    }
}