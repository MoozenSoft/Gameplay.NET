using System;
using System.Runtime.InteropServices;

namespace Gameplay.Core;

/// <summary>序列化写入器（ref struct，栈语义）。</summary>
public ref struct ByteWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public ByteWriter(Span<byte> buffer) { _buffer = buffer; _position = 0; }

    /// <summary>已写入的字节数。</summary>
    public int BytesWritten => _position;

    public void Write(int value) => WriteStruct(value);
    public void Write(float value) => WriteStruct(value);
    public void Write(bool value) => WriteStruct(value ? (byte)1 : (byte)0);
    public void Write(in Vector3 v) { Write(v.X); Write(v.Y); Write(v.Z); }
    public void Write(in Quaternion q) { Write(q.X); Write(q.Y); Write(q.Z); Write(q.W); }

    private void WriteStruct<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
#pragma warning disable CS9191 // netstandard2.1 的 MemoryMarshal.Write 重载为 ref T，net10.0 为 ref readonly T，统一用 ref
        MemoryMarshal.Write(_buffer.Slice(_position, size), ref value);
#pragma warning restore CS9191
        _position += size;
    }
}
