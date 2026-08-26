namespace Cutulu.Core;

public interface IBinaryMarshal
{
    DebugLogEnum DebugLog { get; set; }

    long Position { get; set; }
    long Length { get; }

    [System.Flags]
    public enum DebugLogEnum : byte
    {
        None = 0,
        Errors = 1 << 0,
        Warnings = 1 << 1,
        Infos = 1 << 2,
    }
}