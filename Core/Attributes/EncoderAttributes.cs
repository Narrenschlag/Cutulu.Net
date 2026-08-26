namespace Cutulu.Core;

/// <summary>
/// If applied to a property, it will skip encoding and decoding for that property if no custom encoder is defined for it's parent class/struct.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
public class DontEncode : System.Attribute
{
    public DontEncode()
    {

    }
}

/// <summary>
/// If applied to a non-readonly field, it will be automatically encoded and decoded if no custom encoder is defined for it's parent class/struct.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
public class Encodable : System.Attribute
{
    public Encodable()
    {

    }
}

/// <summary>
/// If applied to a custom BinaryEncoder, it will be ignored by the BinaryEncoding class.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
public class DisableEncoder : System.Attribute
{
    public DisableEncoder()
    {

    }
}