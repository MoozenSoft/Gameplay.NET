using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件序列化器（组件 ↔ 数据）。</summary>
public interface IComponentSerializer<T> where T : struct, IComponent
{
    void Write(in T component, ref ByteWriter writer);
    void Read(ref T component, ref ByteReader reader);
}
