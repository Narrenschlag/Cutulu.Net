namespace Cutulu.Core;

using System.Runtime.CompilerServices;
using System.IO;
using System;

public interface IBinaryMarshal
{
    public bool FirstIterationConsumable { get; set; }
    bool EnableLogging { get; set; }
    bool IsValid { get; }

    long Position { get; set; }
    long Length { get; }
}