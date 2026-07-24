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

            var owner = GetOwner(ctx);
            if (owner.IsNull) return;

            // WaitGameplayTagAdded
            if (entity.TryGetComponent<WaitGameplayTagAddedComponent>(out var added))
            {
                if (state.State == TaskState.Pending)
                {
                    state.State = TaskState.Running;
                    return;
                }
                if (owner.TryGetComponent<GameplayTagsComponent>(out var tags) && tags.HasTag(added.Tag))
                    state.State = TaskState.Done;
            }

            // WaitGameplayTagRemoved
            if (entity.TryGetComponent<WaitGameplayTagRemovedComponent>(out var removed))
            {
                if (state.State == TaskState.Pending)
                {
                    removed.WasPresent = owner.TryGetComponent<GameplayTagsComponent>(out var t) && t.HasTag(removed.Tag);
                    state.State = TaskState.Running;
                    entity.GetComponent<WaitGameplayTagRemovedComponent>() = removed;
                    return;
                }
                bool hasNow = owner.TryGetComponent<GameplayTagsComponent>(out var tagsNow) && tagsNow.HasTag(removed.Tag);
                if (removed.WasPresent && !hasNow)
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
