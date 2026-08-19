using System;
using System.Runtime.InteropServices;

namespace Gameplay.Core;

/// <summary>序列化读取器（ref struct，栈语义）。</summary>
public ref struct ByteReader
{
    private readonly ReadOnlySpan<byte> buffer;
    private int position;

    public ByteReader(ReadOnlySpan<byte> buffer) { this.buffer = buffer; position = 0; }

    public int ReadInt() => ReadStruct<int>();
    public float ReadFloat() => ReadStruct<float>();
    public bool ReadBool() => ReadStruct<byte>() != 0;
    public Vector3 ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());
    public Quaternion ReadQuaternion() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    private T ReadStruct<T>() where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var value = MemoryMarshal.Read<T>(buffer.Slice(position, size));
        position += size;
        return value;
    }
}
