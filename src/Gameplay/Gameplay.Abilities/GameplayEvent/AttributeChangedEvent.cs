using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// Attribute 值变化事件。由 AttributeAggregatorManager.Flush() 在生产阶段发布。
/// </summary>
[GameplayEvent(Tag = "Attribute.Changed")]
public partial struct AttributeChangedEvent
{
    /// <summary>属性所属 Entity。</summary>
    public Entity Target;
    /// <summary>发生变化的属性。</summary>
    public GameplayAttribute Attribute;
    /// <summary>Flush 前的值。</summary>
    public float OldValue;
    /// <summary>Flush 后的值。</summary>
    public float NewValue;
}
