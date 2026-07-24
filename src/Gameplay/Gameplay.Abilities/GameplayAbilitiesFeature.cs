using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Abilities;

/// <summary>
/// GAS 子系统的注册入口。将 Attribute、Effect、Ability、Event、Cue、Task、Prediction
/// 全部 System 和 Manager 组织起来，挂到已有 EntityStore。
/// 不是 World 的包裹，只是注册入口。
/// </summary>
public class GameplayAbilitiesFeature
{
    // ── Friflo QuerySystems（SystemRoot 管理）──
    public AttributeSystem AttributeSystem { get; }
    public EffectSystem EffectSystem { get; }
    public AbilityTaskSystem AbilityTaskSystem { get; }
    public WaitGameplayEventTaskSystem WaitEventTaskSystem { get; }
    public WaitAttributeChangeTaskSystem WaitAttrTaskSystem { get; }
    public WaitGameplayTagTaskSystem WaitTagTaskSystem { get; }
    public WaitAbilityCommitTaskSystem WaitCommitTaskSystem { get; }
    public SystemRoot SystemRoot { get; }

    // ── POCO Manager / System（外部调用）──
    public GameplayEventBus EventBus { get; }
    public EventSystem EventSystem { get; }
    public AbilityActivationSystem ActivationSystem { get; }
    public GameplayCueManager CueManager { get; }
    public PredictionSystem PredictionSystem { get; }

    public GameplayAbilitiesFeature(EntityStore store, NetMode netMode)
    {
        // ── 基础设施 ──
        AttributeSystem = new AttributeSystem();
        EffectSystem = new EffectSystem(AttributeSystem);

        // ── 事件系统 ──
        EventBus = new GameplayEventBus();
        EventSystem = new EventSystem(EventBus);

        // ── Ability 激活 ──
        ActivationSystem = new AbilityActivationSystem(EffectSystem);

        // ── 表现 + 预测 ──
        CueManager = CreateCueManager(netMode);
        PredictionSystem = new PredictionSystem();

        // ── Task 系统 ──
        AbilityTaskSystem = new AbilityTaskSystem(ActivationSystem);
        WaitEventTaskSystem = new WaitGameplayEventTaskSystem(EventSystem, store);
        WaitAttrTaskSystem = new WaitAttributeChangeTaskSystem(AttributeSystem);
        WaitTagTaskSystem = new WaitGameplayTagTaskSystem();
        WaitCommitTaskSystem = new WaitAbilityCommitTaskSystem();

        // ── SystemRoot — 按 Phase 注册 Friflo QuerySystem ──
        SystemRoot = new SystemRoot(store)
        {
            // Phase 1: 内置 Task 推进（Pending→Running + 条件检查）
            WaitEventTaskSystem,
            WaitAttrTaskSystem,
            WaitTagTaskSystem,
            WaitCommitTaskSystem,
            // Phase 2: AbilityTask 完成检测（所有 Task Done → EndAbility）
            AbilityTaskSystem,
            // Phase 3: GE Duration/Period Tick + Apply/Remove
            EffectSystem,
            // Phase 4: Attribute Dirty → Evaluate → CurrentValue
            AttributeSystem,
        };
        // WaitEventTaskSystem 在 Phase 1: 注册 Pending Task 为 EventSystem listener
        // EventSystem.Tick() 在 Update() 开头 Phase 0: 消费本帧事件 → 通知 listener
        // → 下一帧 WaitEventTaskSystem.OnUpdate 检测到 TaskState.Done
    }

    /// <summary>
    /// 每帧更新入口。先消费 Event，再执行 ECS SystemRoot。
    /// </summary>
    public void Update(float deltaTime)
    {
        // Phase 0: Event 交换 + 分发（在 SystemRoot 之前，确保本帧事件对 System 可见）
        EventSystem.Tick();

        // Phase 1-3: ECS System 执行
        SystemRoot.Update(new UpdateTick(deltaTime, 0));
    }

    private static GameplayCueManager CreateCueManager(NetMode netMode)
    {
#if GP_SERVER
        return null; // DS 无表现层
#else
        return new GameplayCueManager();
#endif
    }
}
