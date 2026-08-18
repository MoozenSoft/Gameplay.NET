using System;
using System.Runtime.InteropServices;

namespace Gameplay.Core;

/// <summary>序列化读取器（ref struct，栈语义）。</summary>
public ref struct ByteReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public ByteReader(ReadOnlySpan<byte> buffer) { _buffer = buffer; _position = 0; }

    public int ReadInt() => ReadStruct<int>();
    public float ReadFloat() => ReadStruct<float>();
    public bool ReadBool() => ReadStruct<byte>() != 0;
    public Vector3 ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());
    public Quaternion ReadQuaternion() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    private T ReadStruct<T>() where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var value = MemoryMarshal.Read<T>(_buffer.Slice(_position, size));
        _position += size;
        return value;
    }
}
