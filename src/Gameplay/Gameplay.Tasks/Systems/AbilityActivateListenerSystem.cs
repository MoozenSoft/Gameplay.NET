using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>
/// Ability 激活能力 Driver——事件驱动：监听 AbilityActivationManager 的激活事件，<br/>
/// 匹配（AssetTags 相交）的 Task 完成（Done）。<br/>
/// 与 EffectListenerSystem 同构：OnUpdate 内注册事件 + Pending→Running；
/// 事件回调（Update 之外）用 store.Query&lt;&gt;() 遍历（QuerySystem.Query 此时为 null）。
/// </summary>
public class AbilityActivateListenerSystem : QuerySystem<AbilityActivateListener, TaskStateComponent>
{
    private readonly AbilityActivationManager activationManager;
    private readonly EntityStore store;
    private bool hooked;

    public AbilityActivateListenerSystem(AbilityActivationManager activationManager, EntityStore store)
    {
        this.activationManager = activationManager;
        this.store = store;
    }

    protected override void OnUpdate()
    {
        // 注册激活事件（仅一次）
        if (!hooked)
        {
            activationManager.AbilityActivated += OnAbilityActivated;
            hooked = true;
        }

        // Pending → Running（事件驱动的任务仍走标准生命周期；事件只作用于 Running 之后）
        Query.ForEachEntity((ref AbilityActivateListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State == ETaskState.Pending)
                state.State = ETaskState.Running;
        });
    }

    private void OnAbilityActivated(GameplayAbility ability, Entity owner)
    {
        // 事件回调在 Update 之外——Query 属性为 null，用 store.Query 遍历
        var query = store.Query<AbilityActivateListener, TaskStateComponent>();
        query.ForEachEntity((ref AbilityActivateListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Running) return;
            if (!listener.Character.IsNull && listener.Character.Id != owner.Id) return; // 按激活者过滤
            if (!Matches(listener, ability)) return;
            TaskCommands.Complete(entity);
        });
    }

    /// <summary>判定：监听容器为 null/空 = 匹配任何；否则要求与 Ability.AssetTags 相交。</summary>
    private static bool Matches(in AbilityActivateListener listener, GameplayAbility ability)
    {
        var tags = listener.AbilityTags;
        if (tags == null || tags.Count == 0)
            return true;
        return ability.AssetTags.HasAny(tags);
    }
}
