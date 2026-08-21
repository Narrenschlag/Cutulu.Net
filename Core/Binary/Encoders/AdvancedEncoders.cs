namespace Cutulu.Core
{
    using System.Runtime.CompilerServices;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Collections;
    using System.Reflection;
    using System.Linq;
    using System.IO;
    using System;

    /// <summary>
    /// Use static method Register() to see more.
    /// </summary>
    public static class AdvancedEncoders
    {
        class DateTimeEncoder() : BinaryEncoder(typeof(System.DateTime))
        {
            public override void Encode(System.IO.BinaryWriter writer, System.Type type, object value)
            {
                var dateTime = (System.DateTime)value;

                writer.Encode((UNumber16)dateTime.Year);
                writer.Encode((UNumber8)dateTime.Month);
                writer.Encode((UNumber8)dateTime.Day);

                writer.Encode((UNumber8)dateTime.Hour);
                writer.Encode((UNumber8)dateTime.Minute);
                writer.Encode((UNumber8)dateTime.Second);
            }

            public override object Decode(Decoder.Marshal marshal, System.Type type)
            {
                return new System.DateTime(
                    marshal.Decode<UNumber16>(),
                    marshal.Decode<UNumber8>(),
                    marshal.Decode<UNumber8>(),

                    marshal.Decode<UNumber8>(),
                    marshal.Decode<UNumber8>(),
                    marshal.Decode<UNumber8>()
                );
            }
        }

        class KeyValuePairEncoder() : BinaryEncoder(typeof(KeyValuePair<,>))
        {
            private static readonly ConcurrentDictionary<Type, (Type KeyType, Type ValueType, Func<object, object> GetKey, Func<object, object> GetValue)> Cache = new();

            public override void Encode(BinaryWriter writer, Type type, object value)
            {
                var meta = Cache.GetOrAdd(type, CreateMetadata);

                writer.Encode(meta.GetKey(value), meta.KeyType);
                writer.Encode(meta.GetValue(value), meta.ValueType);
            }

            public override object Decode(Decoder.Marshal marshal, Type type)
            {
                var meta = Cache.GetOrAdd(type, CreateMetadata);

                var key = marshal.Decode(meta.KeyType);
                var value = marshal.Decode(meta.ValueType);

                return Activator.CreateInstance(type, key, value);
            }

            private static (Type, Type, Func<object, object>, Func<object, object>) CreateMetadata(Type type)
            {
                var args = type.GetGenericArguments();
                var keyProp = type.GetProperty("Key");
                var valueProp = type.GetProperty("Value");

                return (
                    args[0],
                    args[1],
                    CompileGetter(keyProp, type),
                    CompileGetter(valueProp, type)
                );
            }

            private static Func<object, object> CompileGetter(PropertyInfo prop, Type declaringType)
            {
                var objParam = Expression.Parameter(typeof(object), "obj");
                var cast = Expression.Convert(objParam, declaringType);
                var access = Expression.Property(cast, prop);
                var convert = Expression.Convert(access, typeof(object));
                return Expression.Lambda<Func<object, object>>(convert, objParam).Compile();
            }
        }

        class ICollectionEncoder() : BinaryEncoder(typeof(ICollection<>))
        {
            private static readonly ConcurrentDictionary<Type, Type> ItemTypeCache = [];

            public override void Encode(BinaryWriter writer, Type type, object value)
            {
                var itemType = ItemTypeCache.GetOrAdd(type, t =>
                {
                    return t.GetInterfaces()
                            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                            .GetGenericArguments()[0];
                });

                var count = (UNumber64)(value == null ? 0 : ((ICollection)value).Count);
                writer.Encode(count);

                if (count > 0)
                {
                    foreach (var item in (IEnumerable)value)
                        writer.Encode(item, itemType);
                }
            }

            public override object Decode(Decoder.Marshal marshal, Type type)
            {
                var itemType = ItemTypeCache.GetOrAdd(type, t =>
                {
                    return t.GetInterfaces()
                            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                            .GetGenericArguments()[0];
                });

                var count = marshal.Decode<UNumber64>();
                var listType = typeof(List<>).MakeGenericType(itemType);
                var list = (IList)Activator.CreateInstance(listType);

                for (int i = 0; i < count; i++)
                    list.Add(marshal.Decode(itemType));

                if (type.IsAssignableFrom(listType))
                    return list;

                return Activator.CreateInstance(type, list);
            }
        }

        class TupleEncoder() : BinaryEncoder(typeof(ITuple))
        {
            public override void Encode(BinaryWriter writer, Type type, object value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "Tuple value is null.");

                var (argTypes, getters) = TupleEncoderCache.Cache.GetOrAdd(type, CreateMetadata);

                for (int i = 0; i < argTypes.Length; i++)
                {
                    var item = getters[i]?.Invoke(value); // safe invoke
                    writer.Encode(item, argTypes[i]);
                }
            }

            public override object Decode(Decoder.Marshal marshal, Type type)
            {
                var (argTypes, _) = TupleEncoderCache.Cache.GetOrAdd(type, CreateMetadata);

                var args = new object[argTypes.Length];
                for (int i = 0; i < argTypes.Length; i++)
                    args[i] = marshal.Decode(argTypes[i]);

                return Activator.CreateInstance(type, args);
            }

            private static (Type[] ArgTypes, Func<object, object>[] Getters) CreateMetadata(Type type)
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                                 .Where(f => f.Name.StartsWith("Item"))
                                 .OrderBy(f => f.Name)
                                 .ToArray();

                if (fields.Length == 0)
                    throw new InvalidOperationException($"No ItemN fields found in type {type.FullName}");

                var argTypes = new Type[fields.Length];
                var getters = new Func<object, object>[fields.Length];

                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    argTypes[i] = field.FieldType;

                    var objParam = Expression.Parameter(typeof(object), "obj");
                    var castObj = Expression.Convert(objParam, type);
                    var access = Expression.Field(castObj, field);
                    var convert = Expression.Convert(access, typeof(object));
                    getters[i] = Expression.Lambda<Func<object, object>>(convert, objParam).Compile();
                }

                return (argTypes, getters);
            }

            static class TupleEncoderCache
            {
                public static readonly ConcurrentDictionary<Type, (Type[] ArgTypes, Func<object, object>[] Getters)> Cache = new();
            }
        }
    }
}