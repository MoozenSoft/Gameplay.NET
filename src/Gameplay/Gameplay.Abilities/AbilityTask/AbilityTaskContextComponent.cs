// src/Gameplay/Gameplay.Abilities/AbilityTask/AbilityTaskContextComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 标记 Task Entity 归属于哪个 ActiveAbility，AbilityTaskSystem 据此判断全部 Task 是否完成。
/// 挂在每个 Task Entity（ActiveAbility 的子 Entity）上。
/// </summary>
public struct AbilityTaskContextComponent : IComponent
{
    /// <summary>归属的 ActiveAbility Entity。</summary>
    public Entity ActiveAbility;

    /// <summary>Task 句柄（预留，供后续 Task 管理使用）。</summary>
    public int TaskHandle;
}
