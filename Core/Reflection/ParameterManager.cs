namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System;

/// <summary>
/// A class that manages a set of parameters for a given type. 3rd gen. Last updated by Maximilian Schecklmann, 2026-08-26.
/// </summary>
public partial class ParameterManager
{
    private static readonly bool DynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;

    private static readonly ConcurrentDictionary<CacheKey, ParameterManager> Cache = [];
    public static void ClearCache() => Cache.Clear();

    public readonly ushort PropertyCount;
    public readonly ushort FieldCount;
    public readonly Type BaseType;
    public readonly Type Type;

    private readonly ParameterInfo[] Parameters;
    private readonly string[] NameToIdx;

    public static bool EnableLogging { get; set; } = false;

    private ParameterManager(Type type, Type baseType, Attribute[] include, Attribute[] exclude)
    {
        Stopwatch stopwatch = EnableLogging ? Stopwatch.StartNew() : null;

        BaseType = baseType;
        Type = type;

        PropertyInfo[] properties = type.GetSetProperties();
        FieldInfo[] fields = type.GetFields();

        var includeForProperties = FilterByTarget(include, AttributeTargets.Property);
        var excludeForProperties = FilterByTarget(exclude, AttributeTargets.Property);
        var includeForFields = FilterByTarget(include, AttributeTargets.Field);
        var excludeForFields = FilterByTarget(exclude, AttributeTargets.Field);

        SwapbackArray<(string Name, ParameterInfo Param)> parameters = [];

        // Include GetSet Properties and Fields
        if (baseType != typeof(object) && type.IsSubclassOf(BaseType))
        {
            properties = properties[..(properties.Length - Open(BaseType).PropertyCount)];
            fields = fields[..(fields.Length - Open(BaseType).FieldCount)];
        }

        PropertyCount = 0;
        FieldCount = 0;

        // Setup NameToIdx
        var propertySpan = properties.AsSpan();
        foreach (ref var property in propertySpan)
        {
            if (PassesFilter(property.GetCustomAttributes(), includeForProperties.AsSpan(), excludeForProperties.AsSpan()))
            {
                parameters.Add((PrepareString(property.Name), new ParameterInfo(property)));

                PropertyCount++;
            }
        }

        var fieldSpan = fields.AsSpan();
        foreach (ref var field in fieldSpan)
        {
            if (PassesFilter(field.GetCustomAttributes(), includeForFields.AsSpan(), excludeForFields.AsSpan()))
            {
                parameters.Add((PrepareString(field.Name), new ParameterInfo(field)));

                FieldCount++;
            }
        }

        // Seal array by sorting
        NameToIdx = new string[parameters.Count];
        int i = 0;

        var nameSpan = parameters.AsSpan();
        foreach (ref var param in nameSpan)
            NameToIdx[i++] = param.Name;

        NameToIdx.Sort();

        // Apply parameters in sorted order
        Parameters = new ParameterInfo[parameters.Count];

        var paramSpan = parameters.AsSpan();
        foreach (ref var param in paramSpan)
        {
            i = NameToIdx.BinarySearch(param.Name);
            Parameters[i] = param.Param;
        }

        if (EnableLogging)
        {
            stopwatch.Stop();
            Debug.LogR($"Found [b]{Parameters.Length} parameters[/b] in [b][color=aqua]{Type.Name}[/color]<[color=gold]{stopwatch.ElapsedMilliseconds} ms[/color]>[/b] [{FieldCount} fields, {PropertyCount} properties] [color=seagreen][{string.Join(", ", include.Select(a => a.GetType().Name))}][/color] [color=indianred][{string.Join(", ", exclude.Select(a => a.GetType().Name))}][/color]");
        }
    }

    private static bool PassesFilter(
        IEnumerable<Attribute> attributes,
        ReadOnlySpan<Attribute> include,
        ReadOnlySpan<Attribute> exclude)
    {
        // No filters = include everything
        if (include.IsEmpty && exclude.IsEmpty) return true;

        bool included = include.IsEmpty; // if no include filter, default to included
        bool excluded = false;

        foreach (var attr in attributes)
        {
            Type attrType = attr.GetType();

            if (!included)
                foreach (ref readonly var inc in include)
                    if (attrType == inc.GetType()) { included = true; break; }

            if (!excluded)
                foreach (ref readonly var exc in exclude)
                    if (attrType == exc.GetType()) { excluded = true; break; }

            if (included && excluded) break; // can't change anymore, early out
        }

        return included && !excluded;
    }

    private static Attribute[] FilterByTarget(Attribute[] attributes, AttributeTargets target)
    {
        if (attributes == null || attributes.Length == 0) return [];

        var result = new List<Attribute>();
        foreach (var attr in attributes)
        {
            var usage = attr.GetType().GetCustomAttribute<AttributeUsageAttribute>();
            // If no AttributeUsage is defined, assume it applies to everything
            if (usage == null || (usage.ValidOn & target) != 0)
                result.Add(attr);
        }

        return [.. result];
    }

    public static ParameterManager Open<T, B>() where B : T => Open(typeof(T), typeof(B));

    public static ParameterManager Open<T>(Type baseType = null) => Open(typeof(T), baseType);

    /// <summary>
    /// Optimizes the whole process of getting a PropertyManager for a given type by caching it for repeated calls
    /// </summary>
    public static ParameterManager Open(Type type, Type baseType = null)
    => OpenInternal(type, baseType, null, null);

    public static ParameterManager Open(Type type, Type baseType, params Attribute[] include)
        => OpenInternal(type, baseType, include, null);

    public static ParameterManager Open(Type type, Type baseType, Attribute[] include, params Attribute[] exclude)
        => OpenInternal(type, baseType, include, exclude);

    private static ParameterManager OpenInternal(Type type, Type baseType, Attribute[] include, Attribute[] exclude)
    {
        var key = new CacheKey(type, baseType ?? typeof(object), ComputeFilterHash(include, exclude));
        return Cache.GetOrAdd(key, _ => new ParameterManager(type, baseType ?? typeof(object), include, exclude));
    }

    private static int ComputeFilterHash(Attribute[] include, Attribute[] exclude)
    {
        var hash = new HashCode();

        if (include != null)
        {
            foreach (var a in include)
            {
                hash.Add(a.GetType());
            }

            hash.Add(-1); // separator
        }

        if (exclude != null)
        {
            foreach (var a in exclude)
            {
                hash.Add(a.GetType());
            }
        }

        return hash.ToHashCode();
    }

    public Span<string> GetNames() => NameToIdx.AsSpan();
    public Span<ParameterInfo> GetInfos() => Parameters.AsSpan();

    public bool TryGetParamIndex(string _name, out int _idx)
    {
        _idx = GetIndex(_name);
        return _idx >= 0;
    }

    public int GetIndex(string _name)
    {
        return NameToIdx.BinarySearch(PrepareString(_name));
    }

    public string GetName(int _idx) => Parameters[_idx].GetName();

    public ParameterInfo GetInfo(int _idx) => Parameters[_idx];

    public ParameterInfo GetInfo(string _name) => GetInfo(GetIndex(_name));

    public Type GetType(int _idx) => GetInfo(_idx).GetValueType();

    public Type GetType(string _name)
    {
        int i = NameToIdx.BinarySearch(PrepareString(_name));
        return i >= 0 ? GetType(i) : null;
    }

    public object GetValue(object _ref, int _idx) => GetInfo(_idx).GetValue(_ref);

    public object GetValue(object _ref, string _name)
    {
        int i = NameToIdx.BinarySearch(PrepareString(_name));
        return i >= 0 ? GetInfo(i).GetValue(_ref) : default;
    }

    public void SetValue(object _ref, int _idx, object _value)
    {
        GetInfo(_idx).SetValue(_ref, _value);
    }

    public void SetValue(object _ref, string _name, object _value)
    {
        int i = NameToIdx.BinarySearch(PrepareString(_name));
        if (i >= 0) SetValue(_ref, i, _value);
    }

    public static string PrepareString(string _str) => _str.Trim().ToLower();

    /// <summary>
    /// Returns every property index with a non equal value (a.Value != b.Value, performs a.Value.IsEqualTo(b.Value)). Returns empty if every value is equal, a or b is null or there's a type mismatch.
    /// </summary>
    public int[] GetNonEqual(object _a, object _b, params int[] blacklist)
    {
        if (_a.IsNull() || _b.IsNull() || _a.GetType() != _b.GetType() || _a == _b) return [];

        var list = new List<int>();

        for (var i = 0; i < Parameters.Length; i++)
        {
            if (GetValue(_a, i).IsEqualTo(GetValue(_b, i)) == false)
                list.Add(i);
        }

        // Handle blacklist
        if (blacklist.NotEmpty())
        {
            foreach (var idx in blacklist)
                list.Remove(idx);
        }

        return [.. list];
    }

    public static Func<object, object> BuildGetter(MemberInfo member, Type declaringType)
    {
        if (DynamicCodeSupported)
        {
            try
            {
                return BuildGetterExpression(member, declaringType);
            }
            catch (Exception)
            {
                // Fall through to reflection fallback below.
                // (Some AOT configs report IsDynamicCodeSupported=true but still fail on first use.)
            }
        }

        return BuildGetterReflection(member);
    }

    private static Func<object, object> BuildGetterExpression(MemberInfo member, Type declaringType)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var instanceCast = Expression.Convert(instanceParam, declaringType);

        Expression access = member is PropertyInfo p
            ? Expression.Property(instanceCast, p)
            : Expression.Field(instanceCast, (FieldInfo)member);

        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxed, instanceParam).Compile();
    }

    private static Func<object, object> BuildGetterReflection(MemberInfo member)
    {
        // Plain reflection — slower, but works everywhere (full AOT, IL2CPP, trimmed builds).
        return member is PropertyInfo p
            ? (obj => p.GetValue(obj))
            : (obj => ((FieldInfo)member).GetValue(obj));
    }

    public static Action<object, object> BuildSetter(MemberInfo member, Type declaringType, Type memberType)
    {
        if (DynamicCodeSupported)
        {
            try
            {
                return BuildSetterExpression(member, declaringType, memberType);
            }
            catch (Exception)
            {
                // Fall through to reflection fallback below.
            }
        }

        return BuildSetterReflection(member);
    }

    private static Action<object, object> BuildSetterExpression(MemberInfo member, Type declaringType, Type memberType)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        // This is important because we may work with structs or other value type.
        // Value types need Unbox to get a writable pointer into the box.
        // Expression.Convert on a value type does unbox.any, which copies
        // the value out, assignments through it are silently lost.
        Expression instanceCast = declaringType.IsValueType
            ? Expression.Unbox(instanceParam, declaringType)
            : Expression.Convert(instanceParam, declaringType);

        var valueCast = Expression.Convert(valueParam, memberType);

        Expression access = member is PropertyInfo p
            ? Expression.Property(instanceCast, p)
            : Expression.Field(instanceCast, (FieldInfo)member);

        var assign = Expression.Assign(access, valueCast);

        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    private static Action<object, object> BuildSetterReflection(MemberInfo member)
    {
        return member is PropertyInfo p
            ? (obj, val) => p.SetValue(obj, val)
            : (obj, val) => ((FieldInfo)member).SetValue(obj, val);
    }

    private readonly struct CacheKey(Type type, Type baseType, int filterHash) : IEquatable<CacheKey>
    {
        public readonly Type Type = type;
        public readonly Type BaseType = baseType;
        public readonly int FilterHash = filterHash;

        public bool Equals(CacheKey other) =>
            Type == other.Type &&
            BaseType == other.BaseType &&
            FilterHash == other.FilterHash;

        public override int GetHashCode() => HashCode.Combine(Type, BaseType, FilterHash);

        public override bool Equals(object obj) => obj is CacheKey key && Equals(key);
    }
}

public readonly struct ParameterInfo
{
    private readonly string _name;
    private readonly Type _type;

    private readonly Func<object, object> _getter;
    private readonly Action<object, object> _setter;

    private readonly System.Reflection.ParameterInfo[] _index;

    public string Name => _name;
    public Type Type => _type;

    public ParameterInfo(PropertyInfo property)
    {
        _name = property.Name;
        _type = property.PropertyType;
        _index = property.GetIndexParameters();

        _getter = property.CanRead
            ? ParameterManager.BuildGetter(property, property.DeclaringType)
            : null;

        _setter = property.CanWrite
            ? ParameterManager.BuildSetter(property, property.DeclaringType, property.PropertyType)
            : null;
    }

    public ParameterInfo(FieldInfo field)
    {
        _name = field.Name;
        _type = field.FieldType;
        _index = null;

        _getter = ParameterManager.BuildGetter(field, field.DeclaringType);
        _setter = field.IsInitOnly ? null : ParameterManager.BuildSetter(field, field.DeclaringType, field.FieldType);
    }

    public readonly string GetName() => _name;
    public readonly Type GetValueType() => _type;

    [Obsolete("Use GetValueType() instead")]
    public readonly new Type GetType() => base.GetType();

    public readonly object GetValue(object _ref) =>
    _getter is not null ? _getter(_ref) : throw new InvalidOperationException($"'{_name}' has no getter.");

    public readonly void SetValue(object _ref, object _value)
    {
        if (_setter is null) throw new InvalidOperationException($"'{_name}' has no setter.");
        _setter(_ref, _value);
    }

    public readonly System.Reflection.ParameterInfo[] GetIndexParameters() => _index;
    public readonly bool HasIndexParameters() => _index.NotEmpty();
}