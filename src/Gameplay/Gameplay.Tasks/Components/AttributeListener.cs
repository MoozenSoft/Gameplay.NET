using Friflo.Engine.ECS;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// 属性监听能力——等待目标 Entity 的指定 GameplayAttribute CurrentValue 发生变化。<br/>
/// 读取的是上一帧 Phase 4 Flush 后的已结算值，具有确定性。
/// </summary>
public struct AttributeListener : IComponent
{
    /// <summary>监听谁身上的属性（玩家 / 任意 Entity）。</summary>
    public Entity Target;

    /// <summary>监听的属性。</summary>
    public GameplayAttribute Attribute;

    /// <summary>注册时的快照值，用于比较变化。</summary>
    public float LastValue;

    /// <summary>等待次数（>0 表示等待多少次变化）。</summary>
    public int Count;
}
