using System;

namespace Gameplay.Abilities;

/// <summary>
/// 通用无 GC struct 缓冲。
/// 内部使用 T[] 存储，只重置计数不清内存，适用于热路径复用。
/// </summary>
public sealed class StructBuffer<T> where T : struct
{
    private T[] buffer = Array.Empty<T>();
    private int count;

    public int Count => count;

    public int Add(in T value)
    {
        if (count >= buffer.Length)
        {
            int newSize = buffer.Length == 0 ? 16 : buffer.Length * 2;
            Array.Resize(ref buffer, newSize);
        }
        buffer[count] = value;
        return count++;
    }

    public ref T GetRef(int index) => ref buffer[index];

    public void Reset() { count = 0; } // 只重置计数，不清内存
}
