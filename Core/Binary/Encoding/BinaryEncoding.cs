namespace Cutulu.Core;

using System.Collections.Generic;
using System.IO;
using System;

/// <summary>
/// Static class for encoding and decoding binary data
/// </summary>
public static class BinaryEncoding
{
    public static readonly Attribute[] IncludeAttributes = [new Encodable()];
    public static readonly Attribute[] ExcludeAttributes = [new DontEncode()];

    #region Register Encoders

    public static readonly Dictionary<nint, BinaryEncoder> Encoders = [];
    [ThreadStatic] public static Type LastPropertyType;
    [ThreadStatic] public static Type LastContainerType;
    [ThreadStatic] public static string LastPropertyName;
    private static int EncoderCount;

    public static int GetEncoderCount() => EncoderCount;

    static BinaryEncoding()
    {
        EncoderCount = 0;

        RegisterAllEncoders();
    }

    private static void RegisterAllEncoders()
    {
        var finder = new Reflection.TypeFinder(typeof(BinaryEncoder), Reflection.TypeFinder.DefaultFlags);
        // assembly = null → TypeFinder scans all assemblies itself

        if (finder.Types.TryGetValue(typeof(BinaryEncoder), out var found) == false)
            return;

        Debug.Log($"[{nameof(BinaryEncoding)}] Found {found.Count} encoder(s) across all assemblies.");

        foreach (var classType in found)
            RegisterEncoder(classType);
    }

    private static void RegisterEncoder(Type encoderType)
    {
        if (encoderType.IsNull()) return;

        try
        {
            // Skip inactive encoders and non instancable encoders
            if (
                encoderType.IsDefined(typeof(DisableEncoder), inherit: false) ||
                Activator.CreateInstance(encoderType) is not BinaryEncoder newEncoder
            ) return;

            // Assign source type handle
            nint handle = newEncoder.SourceHandle;

            // Assign to the Encoders dictionary
            if (Encoders.TryGetValue(handle, out var oldEncoder) == false)
            {
                Encoders[handle] = newEncoder;
                EncoderCount++;
            }

            else if (oldEncoder.GetPriority() <= newEncoder.GetPriority())
            {
                Encoders[handle] = newEncoder;
            }
        }

        catch (Exception ex)
        {
            Debug.LogError($"Failed to register encoders for typeof({encoderType.FullName}): {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static bool TryGetEncoder(Type type, out BinaryEncoder encoder)
    {
        if (type.IsNull() || type.IsArray) // Skip nulls and arrays, which are a special case
        {
            encoder = null;
            return false;
        }

        // Water down type to generic definition if generic
        if (type.IsGenericType) type = type.GetGenericTypeDefinition();

        nint baseHandle = type.TypeHandle.Value;
        nint handle = baseHandle;

        // Return encoder if found
        if (Encoders.TryGetValue(handle, out encoder)) return true;

        // Iterate potential encoders by checking inheritance
        Type[] interfaces = type.GetInterfaces();
        if (interfaces.NotEmpty())
        {
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].IsGenericType) interfaces[i] = interfaces[i].GetGenericTypeDefinition();
                handle = interfaces[i].TypeHandle.Value;

                if (Encoders.TryGetValue(handle, out encoder))
                {
                    // Assign encoder to type handle
                    Encoders[baseHandle] = encoder;
                    return true;
                }
            }
        }

        // Recursively check base type
        while ((type = type.BaseType).NotNull())
        {
            if (type.IsGenericType) type = type.GetGenericTypeDefinition();
            handle = type.TypeHandle.Value;

            if (Encoders.TryGetValue(handle, out encoder))
            {
                // Assign encoder to type handle
                Encoders[baseHandle] = encoder;
                return true;
            }
        }

        // No encoder could be found
        return false;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Returns all remaining bytes in stream of BinaryReader
    /// </summary>
    public static byte[] ReadRemainingBytes(this BinaryReader reader)
    {
        return reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
    }

    /// <summary>
    /// Returns length of remaining bytes in stream of BinaryReader
    /// </summary>
    public static long RemainingByteLength(this BinaryReader reader)
    {
        return reader.BaseStream.Length - reader.BaseStream.Position;
    }

    #endregion
}