using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>等待 Owner 获得指定 Tag 的 Task Component。</summary>
public struct WaitGameplayTagAddedComponent : IComponent
{
    public GameplayTag Tag;
}

/// <summary>等待 Owner 移除指定 Tag 的 Task Component。</summary>
public struct WaitGameplayTagRemovedComponent : IComponent
{
    public GameplayTag Tag;
    /// <summary>注册时 Tag 是否存在。</summary>
    public bool WasPresent;
}

/// <summary>
/// WaitGameplayTag Task System —— 每帧检查 Owner Entity 的 Tag 状态变化。
/// </summary>
public class WaitGameplayTagTaskSystem : QuerySystem<AbilityTaskContextComponent, TaskStateComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref AbilityTaskContextComponent ctx, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != TaskState.Pending && state.State != TaskState.Running) return;

            // Pending→Running 在 guard 之前，防止 owner 无效时任务卡在 Pending
            if (state.State == TaskState.Pending)
            {
                // WaitGameplayTagRemoved：如果 tag 本来就不在，立即 Done
                if (entity.TryGetComponent<WaitGameplayTagRemovedComponent>(out var removed))
                {
                    var pendingOwner = GetOwner(ctx);
                    if (!pendingOwner.IsNull && pendingOwner.TryGetComponent<GameplayTagsComponent>(out var t) && t.HasTag(removed.Tag))
                    {
                        removed.WasPresent = true;
                        entity.GetComponent<WaitGameplayTagRemovedComponent>() = removed;
                        state.State = TaskState.Running;
                    }
                    else
                    {
                        state.State = TaskState.Done; // Tag 不存在 → 已完成
                    }
                    return;
                }
                state.State = TaskState.Running;
                return;
            }

            var owner = GetOwner(ctx);
            if (owner.IsNull) return;

            // WaitGameplayTagAdded: 检查 tag 是否已出现
            if (entity.TryGetComponent<WaitGameplayTagAddedComponent>(out var added))
            {
                if (owner.TryGetComponent<GameplayTagsComponent>(out var tags) && tags.HasTag(added.Tag))
                    state.State = TaskState.Done;
            }

            // WaitGameplayTagRemoved: 检查 tag 是否已被移除
            if (entity.TryGetComponent<WaitGameplayTagRemovedComponent>(out var removedR))
            {
                bool hasNow = owner.TryGetComponent<GameplayTagsComponent>(out var tagsNow) && tagsNow.HasTag(removedR.Tag);
                if (removedR.WasPresent && !hasNow)
                    state.State = TaskState.Done;
            }
        });
    }

    private static Entity GetOwner(AbilityTaskContextComponent ctx)
    {
        if (ctx.ActiveAbility.IsNull) return default;
        if (!ctx.ActiveAbility.TryGetComponent<ActiveAbilityComponent>(out var activeComp)) return default;
        return activeComp.Owner;
    }
}
