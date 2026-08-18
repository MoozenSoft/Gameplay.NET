using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Gameplay.Interfaces;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// GAS 子系统的注册入口。将 Attribute、Effect、Ability、Event、Cue、Task、Prediction
/// 全部 System 和 Manager 组织起来，挂到已有 EntityStore。
/// 不是 World 的包裹，只是注册入口。
/// </summary>
public class GameplayAbilitiesFeature : ITaskCompletionListener
{
    // ── Friflo QuerySystems（SystemRoot 管理）──
    public AttributeAggregatorManager AttributeAggregatorManager { get; }
    public EffectSystem EffectSystem { get; }
    public TaskSchedulerSystem TaskScheduler { get; }
    public GameplayEventSystem GameplayEventSystem { get; }
    public AttributeListenerSystem AttributeListenerSystem { get; }
    public TagListenerSystem TagListenerSystem { get; }
    public CommitPhaseListenerSystem CommitPhaseListenerSystem { get; }
    public EffectListenerSystem EffectListenerSystem { get; }
    public AbilityActivateListenerSystem AbilityActivateListenerSystem { get; }
    public InputListenerSystem InputListenerSystem { get; }
    public SpawnSystem SpawnSystem { get; }
    public MoveToSystem MoveToSystem { get; }
    public TimerSystem TimerSystem { get; }
    public DelaySystem DelaySystem { get; }
    public SystemRoot SystemRoot { get; }

    // ── POCO Manager / System（外部调用）──
    public GameplayEventBus EventBus { get; }
    public GameplayEventDispatcher EventDispatcher { get; }
    public AbilityActivationManager ActivationManager { get; }
    public GameplayCueManager? CueManager { get; }
    public PredictionManager PredictionManager { get; }

    public GameplayAbilitiesFeature(EntityStore store, ENetMode netMode)
    {
        // ── 基础设施 ──
        AttributeAggregatorManager = new AttributeAggregatorManager();
        EffectSystem = new EffectSystem(AttributeAggregatorManager);

        // ── 事件系统 ──
        EventBus = new GameplayEventBus();
        EventDispatcher = new GameplayEventDispatcher(EventBus);
        AttributeAggregatorManager.SetEventBus(EventBus);

        // ── Ability 激活 ──
        ActivationManager = new AbilityActivationManager(EffectSystem);

        // ── 表现 + 预测 ──
        CueManager = CreateCueManager(netMode);
        PredictionManager = new PredictionManager();

        // ── Task 系统（Runtime + Driver）──
        TaskScheduler = new TaskSchedulerSystem();
        TaskScheduler.SetCompletionListener(this); // 全部 Task 完成 → CancelAbility
        GameplayEventSystem = new GameplayEventSystem(EventDispatcher, store);
        AttributeListenerSystem = new AttributeListenerSystem(AttributeAggregatorManager);
        TagListenerSystem = new TagListenerSystem();
        CommitPhaseListenerSystem = new CommitPhaseListenerSystem();
        EffectListenerSystem = new EffectListenerSystem(EffectSystem, store);
        AbilityActivateListenerSystem = new AbilityActivateListenerSystem(ActivationManager, store);
        InputListenerSystem = new InputListenerSystem();
        SpawnSystem = new SpawnSystem();
        MoveToSystem = new MoveToSystem();
        TimerSystem = new TimerSystem(EventBus);
        DelaySystem = new DelaySystem();

        // ── SystemRoot — 按 Phase 注册 Friflo QuerySystem ──
        SystemRoot = new SystemRoot(store)
        {
            // Phase 1: 内置 Task 推进（Pending→Running + 条件检查）
            DelaySystem,          // Delay 计时（共享——TaskBuilder.Delay 复用）
            GameplayEventSystem,
            AttributeListenerSystem,
            TagListenerSystem,
            CommitPhaseListenerSystem,
            EffectListenerSystem,
            AbilityActivateListenerSystem,
            InputListenerSystem,
            SpawnSystem,
            MoveToSystem,
            TimerSystem,
            // Phase 2: Task 完成检测（所有 Task Done → 通知 ITaskCompletionListener）
            TaskScheduler,
            // Phase 3: GE Duration/Period Tick + Apply/Remove
            EffectSystem,
        };
        // GameplayEventSystem 在 Phase 1: 注册 Pending Task 为 GameplayEventDispatcher listener
        // GameplayEventDispatcher.Tick() 在 Update() 开头 Phase 0: 消费本帧事件 → 通知 listener
        // → 下一帧 GameplayEventSystem.OnUpdate 检测到 ETaskState.Done
    }

    /// <summary>
    /// ITaskCompletionListener —— Owner（ActiveAbility）的所有 Task 完成时结束 Ability。
    /// </summary>
    public void OnAllTasksDone(Entity owner)
        => ActivationManager.CancelAbility(owner);

    /// <summary>注入输入服务（无输入环境如 Dedicated Server 不调用——Input 类 Task 保持 Running）。</summary>
    public void SetInputService(IInputService inputService)
        => InputListenerSystem.SetInputService(inputService);

    /// <summary>
    /// 每帧更新入口。先消费 Event，再执行 ECS SystemRoot。
    /// </summary>
    public void Update(float deltaTime)
    {
        // Phase 0: Event 交换 + 分发（在 SystemRoot 之前，确保本帧事件对 System 可见）
        EventDispatcher.Tick();

        // Phase 1-3: ECS System 执行（Task + Effect）
        SystemRoot.Update(new UpdateTick(deltaTime, 0));

        // Phase 4: Attribute 固定阶段 Flush（统一 Evaluate + 写回 CurrentValue）
        AttributeAggregatorManager.Flush();

        // Phase 5: 延迟删除（Query 循环内不能 DeleteEntity）
        ActivationManager.ProcessPendingDeletions();
        TaskScheduler.ProcessPendingDeletions();
    }

    private static GameplayCueManager? CreateCueManager(ENetMode netMode)
    {
#if GP_SERVER
        return null; // DS 无表现层
#else
        return new GameplayCueManager();
#endif
    }
}
