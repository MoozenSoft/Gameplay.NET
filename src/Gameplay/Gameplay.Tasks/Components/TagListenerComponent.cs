using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>Tag 监听条件。</summary>
public enum TagCondition
{
    /// <summary>等待 Tag 出现（Added）。</summary>
    Added,
    /// <summary>等待 Tag 移除（Removed）。</summary>
    Removed,
}

/// <summary>
/// Tag 监听能力——等待目标 Entity 获得/移除指定 GameplayTag。<br/>
/// 合并旧 WaitGameplayTagAddedComponent + WaitGameplayTagRemovedComponent，
/// 由 TagListenerSystem 在同一个 Query 内按 <see cref="Condition"/> 分支处理。
/// </summary>
public struct TagListenerComponent : IComponent
{
    /// <summary>监听谁身上的 Tag（玩家 / 任意 Entity）。</summary>
    public Entity Target;

    /// <summary>监听的 Tag。</summary>
    public GameplayTag Tag;

    /// <summary>监听条件（Added / Removed）。</summary>
    public TagCondition Condition;

    /// <summary>Removed 模式：注册时 Tag 是否存在（快照）。</summary>
    public bool WasPresent;
}
