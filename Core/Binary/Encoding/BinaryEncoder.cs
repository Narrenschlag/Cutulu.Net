namespace Cutulu.Core;

using System.IO;
using System;

/// <summary>
/// Defines a generic base class for binary encoding, automatically registered by Cutulu.BinaryEncoding.
/// <para>
/// To make this encoder work, you need to implement a parameterless constructor that calls this constructor by it's encodable type.
/// </para>
/// <para>
/// To mute this encoder, add the DisableEncoder attribute to the class.
/// </para>
/// </summary>
public abstract class BinaryEncoder
{
    public readonly nint SourceHandle;
    public readonly Type SourceType;

    public BinaryEncoder(Type sourceType)
    {
        SourceHandle = (SourceType = sourceType).TypeHandle.Value;
    }

    public virtual int GetPriority() => 0;

    public abstract void Encode(BinaryWriter writer, Type type, object value);

    public abstract object Decode(BinaryReader reader, Type type);

    public static SwapbackArray<EncoderMeta> EncodeMeta(BinaryWriter writer, params MetaEntry[] entries)
    {
        SwapbackArray<EncoderMeta> meta = new(entries.Size());

        if (entries.NotEmpty())
        {
            LocalEncoder encoder = new();
            long start;

            foreach (var (name, value) in entries)
            {
                start = encoder.Position;
                encoder.Write(value);
                meta.Add(new(name, encoder.Position - start));
            }

            writer.Encode(meta);
            writer.Write(encoder.GetBuffer());
        }

        return meta;
    }

    public static void DecodeMeta(BinaryReader reader, Action<string, byte[]> switchStatement)
    {
        var meta = reader.Decode<SwapbackArray<EncoderMeta>>();

        byte[] buffer;

        foreach (var m in meta)
        {
            buffer = reader.ReadBytes(m.Length);
            if (buffer.IsEmpty()) continue;

            switchStatement?.Invoke(m.ParamName, buffer);
        }
    }

    public record struct MetaEntry(string ParamName, object Value);
}