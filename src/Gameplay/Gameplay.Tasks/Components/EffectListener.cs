using Friflo.Engine.ECS;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>GE 监听条件。</summary>
public enum EEffectCondition
{
    /// <summary>匹配的 GE 施加到目标（含 Stack 叠加）时完成。</summary>
    Applied,
    /// <summary>匹配的 GE 从目标移除时完成。</summary>
    Removed,
}

/// <summary>GE 监听能力——事件驱动：EffectSystem 施加/移除 GE 时，匹配 <see cref="Query"/> 的 Task 完成（Done）。</summary>
/// <remarks>
/// <para>对应 UE 的 WaitGameplayEffectApplied / WaitGameplayEffectRemoved。</para>
/// <para><see cref="Query"/> 是可变 class 引用——调用者不得在 Task 存活期间修改它（Task 条件应保持创建时快照语义）。</para>
/// </remarks>
public struct EffectListener : IComponent
{
    /// <summary>监听谁身上的 GE（玩家 / 任意 Entity）。</summary>
    public Entity Target;

    /// <summary>GE 匹配过滤（GameplayEffectQuery.Matches(spec)）。</summary>
    public GameplayEffectQuery Query;

    /// <summary>监听条件（Applied / Removed）。</summary>
    public EEffectCondition Condition;
}
