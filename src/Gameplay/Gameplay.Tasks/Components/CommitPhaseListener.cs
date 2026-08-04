using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>
/// Commit 阶段监听能力——当目标 ActiveAbility 的 State 变为 Active（Commit 已完成）时 Task 完成（Done）。<br/>
/// 注：引用 Gameplay.Abilities 域的 ActiveAbilityComponent，形成命名空间循环（技术债，见计划）。<br/>
/// 单一程序集内合法；拆程序集时迁回 Abilities 域。
/// </summary>
public struct CommitPhaseListener : IComponent
{
    /// <summary>监听的 ActiveAbility Entity。</summary>
    public Entity Target;
}
