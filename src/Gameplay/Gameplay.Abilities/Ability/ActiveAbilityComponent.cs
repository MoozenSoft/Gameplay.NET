using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// ActiveAbility 的运行时 Component。激活时创建子 Entity 挂此 Component，结束时销毁。
/// </summary>
public struct ActiveAbilityComponent : IComponent
{
    public float StartTime;                       // 激活时间戳
    public int Handle;                            // 全局唯一 ID
    public int DefinitionId;                      // Ability 静态定义 Registry 查表 key
    public bool IsActive;                         // 是否激活中
    public Entity Owner;                          // 归属的 Owner Entity
    public AbilityInstanceState State;            // 当前状态
}
