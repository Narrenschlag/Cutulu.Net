namespace Cutulu.Core;

#if GODOT4_0_OR_GREATER
using FLAGS = Godot.FileAccess.ModeFlags;
using ACCESS = Godot.FileAccess;
using Godot;
#else
using System.IO;
#endif

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System;

public partial class File : IDisposable
{
    public readonly string SystemPath;

#if GODOT4_0_OR_GREATER
    public readonly string GodotPath;
    private ACCESS Access { get; set; }
    public FLAGS Flags { get; private set; }
#endif

    #region Constructor

    public File(string path)
    {
#if GODOT4_0_OR_GREATER
        SystemPath = ProjectSettings.GlobalizePath(path.Trim());
        GodotPath = ProjectSettings.LocalizePath(SystemPath);
#else
        SystemPath = Path.GetFullPath(path.Trim());
#endif
    }

    public void Dispose()
    {
#if GODOT4_0_OR_GREATER
        Access?.Close();
        Access = null;
#endif
        GC.SuppressFinalize(this);
    }

    ~File() => Dispose();

    #endregion

    public string GetExtension() => Path.GetExtension(SystemPath);

    public string GetFileName(bool includeExtension = false) => includeExtension ? Path.GetFileName(SystemPath) : Path.GetFileNameWithoutExtension(SystemPath);

    #region Raw Functions

#if GODOT4_0_OR_GREATER
    public ACCESS Open(FLAGS flags, bool reopen = true)
    {
        if (Access == null || reopen)
        {
            Access?.Close();

            // MkDir
            if (flags.HasFlag(FLAGS.Write)) GetParentDirectory().MakeDir();

            Access = ACCESS.Open(SystemPath, Flags = flags);
            if (Access.IsNull()) Debug.LogError($"FileOpen failed: {ACCESS.GetOpenError()}");
        }
        return Access;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        Access?.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close()
    {
        Access?.Close();
        Access = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadRaw(ulong length)
    {
        return Open(FLAGS.Read, false)?.GetBuffer((long)length) ?? [];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(byte[] buffer)
    {
        if (buffer?.Length > 0)
            Open(FLAGS.Write, false)?.StoreBuffer(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetFileSizeRaw()
    {
        return Open(FLAGS.Read, false)?.GetLength() ?? 0;
    }
#else
    public byte[] ReadRaw(ulong length)
    {
        using var stream = System.IO.File.OpenRead(SystemPath);
        var buffer = new byte[length];

        stream.ReadExactly(buffer, 0, (int)length);
        stream.Close();

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(byte[] buffer)
    {
        System.IO.File.WriteAllBytes(SystemPath, buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetFileSizeRaw()
    {
        return (ulong)new FileInfo(SystemPath).Length;
    }

#endif

    #endregion

    #region Async File Functions

    public async Task<byte[]> ReadAsync(CancellationToken token = default)
    {
        if (Exists() == false) return null;
        return await System.IO.File.ReadAllBytesAsync(SystemPath, token);
    }

    public async Task WriteAsync(byte[] _buffer, CancellationToken token = default)
    {
        if (_buffer.IsEmpty()) return;

        await System.IO.File.WriteAllBytesAsync(SystemPath, _buffer, token);
    }

    public async Task WriteTextAsync(string _string, CancellationToken token = default)
    {
        if (_string.IsEmpty() && Exists() == false) return;

        if (_string.IsEmpty())
        {
            Delete();
            return;
        }

        await System.IO.File.WriteAllTextAsync(SystemPath, _string, token);
    }

    #endregion

    #region Base Functions

    public byte[] Read()
    {
        var _buffer = ReadRaw(GetFileSizeRaw());

#if GODOT4_0_OR_GREATER
        Close();
#endif

        return _buffer;
    }

    public byte[] Read(ulong _length)
    {
        var _buffer = ReadRaw(_length);

#if GODOT4_0_OR_GREATER
        Close();
#endif

        return _buffer;
    }

    public void Write(byte[] _buffer)
    {
        if (_buffer.IsEmpty()) Delete();

        else
        {
#if GODOT4_0_OR_GREATER
            Open(FLAGS.Write, true);
#endif

            WriteRaw(_buffer);

#if GODOT4_0_OR_GREATER
            Flush();
            Close();
#endif
        }
    }

#if GODOT4_0_OR_GREATER
    /// <summary>
    /// Reads an image from the file. Supported formats are png, jpg, jpeg, webp, exr, dds.
    /// </summary>
    public Image ReadImage()
    {
        return Image.LoadFromFile(SystemPath);
    }

    /// <summary>
    /// Writes an image to the file. Supported formats are png, jpg, jpeg, webp, exr, dds.
    /// </summary>
    public void Write(Image image)
    {
        if (image.IsNull()) return;

        Write(SystemPath.GetExtension() switch
        {
            ".png" => image.SavePngToBuffer(),
            ".jpg" => image.SaveJpgToBuffer(),
            ".jpeg" => image.SaveJpgToBuffer(),
            ".webp" => image.SaveWebpToBuffer(),
            ".exr" => image.SaveExrToBuffer(),
            ".dds" => image.SaveDdsToBuffer(),
            _ => image.SavePngToBuffer()
        });
    }
#endif

    #endregion

    #region Encoder functions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead<T>(out T _output)
    {
        return Read().TryDecode(out _output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read<T>()
    {
        return TryRead(out T _output) ? _output : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(object _input)
    {
        Write(_input.Encode());
    }

    #endregion

    #region String functions

    public void WriteString(string _string)
    {
        if (_string.IsEmpty()) return;

        using var _stream = new System.IO.MemoryStream();
        using var _writer = new System.IO.StreamWriter(_stream); // Because of plain text

        _writer.Write(_string);
        _writer.Flush();

        Write(_stream.ToArray());
    }

    public string ReadString()
    {
        using var _stream = new System.IO.MemoryStream(Read());
        using var _reader = new System.IO.StreamReader(_stream); // Because of plain text

        var _string = _reader.ReadToEnd();
        return _string.NotEmpty() ? _string : string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string[] ReadStringLines(bool _include_empty_lines = false)
    {
        return ReadString().Split(['\n', '\r'], _include_empty_lines ? StringSplitOptions.TrimEntries : CONST.StringSplit) ?? [];
    }

    public T ReadJson<T>() => ReadString().json<T>();

    #endregion

    #region GDResource functions

#if GODOT4_0_OR_GREATER
    public T ReadGDResource<T>() where T : Resource
    {
        return GD.Load(GodotPath) is T _t && _t.NotNull() ? _t : default;
    }
#endif

    #endregion

    #region File System

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Exists()
    {
#if GODOT4_0_OR_GREATER
        return ACCESS.FileExists(SystemPath);
#else
        return System.IO.File.Exists(SystemPath);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete()
    {
#if GODOT4_0_OR_GREATER
        DirAccess.RemoveAbsolute(SystemPath);
#else
        System.IO.File.Delete(SystemPath);
#endif
    }

    /// <summary>
    /// Returns parent directory. Won't create if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Directory GetParentDirectory(bool createIfMissing = false)
    {
        return new Directory(SystemPath.TrimToDirectory(), createIfMissing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Directory> GetSiblingDirectories()
    {
        return GetParentDirectory().GetSubDirectories();
    }

    public File[] GetSiblingFiles()
    {
        var _siblings = new Directory(SystemPath).GetSubFiles();

        if (_siblings.Length > 1)
        {
            var _files = new File[_siblings.Length - 1];

            for (int i = 0, k = 0; i < _siblings.Length; i++, k++)
            {
                if (i == k && _siblings[i].SystemPath == SystemPath) i++;
                else _files[k] = _siblings[i];
            }

            return _files;
        }

        return [];
    }

    #endregion
}
