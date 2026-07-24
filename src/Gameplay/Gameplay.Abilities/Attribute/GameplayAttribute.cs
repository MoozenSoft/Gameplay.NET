using System;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAttribute 类型安全句柄——编译期由 SG 生成。
/// 持有 AttributeId + 写回委托，替代裸 int。
/// </summary>
public readonly struct GameplayAttribute
{
    /// <summary>全局唯一 AttributeId（SG 分配）。</summary>
    public readonly int Id;

    private readonly Action<Entity, float> writeCurrentValue;

    internal GameplayAttribute(int id, Action<Entity, float> writeCurrentValue)
    {
        Id = id;
        this.writeCurrentValue = writeCurrentValue;
    }

    /// <summary>将评估后的 CurrentValue 写回组件字段。由 AttributeSystem 调用。</summary>
    public void WriteCurrentValue(Entity entity, float value)
        => writeCurrentValue(entity, value);
}
