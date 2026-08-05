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

/// <summary>Tag 监听能力——等待目标 Entity 获得/移除指定 GameplayTag（或 Tag 集合）。</summary>
/// <remarks>
/// <para><see cref="RequiredTags"/> 非空时为 Query 模式：全部 Tag 出现/消失才满足条件
/// （Added = HasAll；Removed = HasAll 从 true 变 false）。</para>
/// <para>Added / Removed 两种条件由 TagListenerSystem 在同一个 Query 内按 <see cref="Condition"/> 分支处理。</para>
/// </remarks>
public struct TagListenerComponent : IComponent
{
    /// <summary>监听谁身上的 Tag（玩家 / 任意 Entity）。</summary>
    public Entity Target;

    /// <summary>监听的 Tag（单 Tag 模式，<see cref="RequiredTags"/> 为空时生效）。</summary>
    public GameplayTag Tag;

    /// <summary>Query 模式：要求全部出现的 Tag 集合（非空时优先于单 Tag）。</summary>
    public GameplayTagContainer? RequiredTags;

    /// <summary>监听条件（Added / Removed）。</summary>
    public TagCondition Condition;

    /// <summary>Removed 模式：注册时条件是否已满足（快照）。</summary>
    public bool WasPresent;
}
