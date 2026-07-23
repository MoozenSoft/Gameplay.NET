// src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationSystem.cs
using Friflo.Engine.ECS;
using Gameplay.Tags;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// Ability 激活流程 System（POCO，不继承 QuerySystem）。
/// 接收 AbilityActivationRequest → Requirements → Commit → Execute。
/// </summary>
public class AbilityActivationSystem
{
    private readonly EffectSystem effectSystem;
    private int nextHandle = 1;

    public AbilityActivationSystem(EffectSystem effectSystem)
    {
        this.effectSystem = effectSystem;
    }

    /// <summary>尝试激活 Ability。返回 true = 成功。</summary>
    public bool TryActivateAbility(in AbilityActivationRequest request)
    {
        var owner = request.Owner;

        // 查找 AbilitySpec
        if (!owner.TryGetComponent<AbilityCollectionComponent>(out var collection))
            return false;
        if (request.SpecHandle < 0 || request.SpecHandle >= collection.Specs.Length)
            return false;

        var spec = collection.Specs[request.SpecHandle];
        var ability = spec.Ability;
        if (ability == null) return false;

        // ── 1. Requirements（纯检查）──
        foreach (var req in ability.Requirements)
        {
            if (!req.Evaluate(owner, spec, request))
                return false;
        }
        // 内置: Tag 检查
        var tagReq = new TagRequirement();
        if (!tagReq.Evaluate(owner, spec, request))
            return false;

        // ── 2. Commit（副作用）──
        foreach (var commit in ability.CommitActions)
        {
            commit.Execute(owner, spec, request);
        }

        // ── 3. Create ActiveAbility Entity ──
        int handle = nextHandle++;
        var activeEntity = owner.Store.CreateEntity();
        activeEntity.AddChild(owner);
        activeEntity.AddComponent(new ActiveAbilityComponent
        {
            StartTime = 0f, // TBD: world time
            Handle = handle,
            IsActive = true,
            Owner = owner,
            State = EAbilityInstanceState.Active,
        });

        // ── 4. Execute ──
        var executor = ability.Executor;
        if (executor != null)
        {
            try
            {
                executor.Execute(activeEntity, request);
            }
            catch
            {
                // 回滚 Commit
                RollbackCommit(ref activeEntity, owner, spec);
                activeEntity.DeleteEntity();
                return false;
            }
        }

        // 添加 ActivationOwnedTags
        if (ability.ActivationOwnedTags.Count > 0)
        {
            if (owner.TryGetComponent<GameplayTagsComponent>(out var tags))
            {
                // 使用 TagSource ref counting（后续 Plan 完善）
                foreach (var tag in ability.ActivationOwnedTags)
                    tags.AddTag(tag);
            }
        }

        return true;
    }

    /// <summary>取消 Ability。</summary>
    public void CancelAbility(Entity activeEntity)
    {
        if (!activeEntity.TryGetComponent<ActiveAbilityComponent>(out var comp))
            return;

        comp.State = EAbilityInstanceState.Cancelled;
        comp.IsActive = false;

        var owner = comp.Owner;

        // 移除 ActivationOwnedTags
        if (!owner.IsNull)
        {
            // 通过 AbilitySpec 查找 Definition → 移除对应 Tag
            // Plan 2 简化：不做反查
        }

        // 将 WaitCancel Task 标记为 Done
        foreach (var child in activeEntity.ChildEntities)
        {
            if (child.HasComponent<WaitCancelComponent>() && child.HasComponent<TaskStateComponent>())
            {
                ref var taskState = ref child.GetComponent<TaskStateComponent>();
                taskState.State = TaskState.Done;
            }
        }

        activeEntity.DeleteEntity();
    }

    private void RollbackCommit(ref Entity activeEntity, Entity owner, AbilitySpec spec)
    {
        // 撤销 Cooldown: 查找 Handle → RemoveEffect
        // 撤销 Cost: 退还 Attribute
        // Plan 2 简化为占位
    }
}
