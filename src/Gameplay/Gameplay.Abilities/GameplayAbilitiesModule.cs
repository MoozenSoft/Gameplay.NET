using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Gameplay.Interfaces;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// GAS 子系统模块。将 Attribute、Effect、Ability、Event、Cue、Task、Prediction
/// 全部 System 和 Manager 组织起来，作为 <see cref="IModule"/> 挂到 Gameplay.Core 的
/// World 三阶段调度（PreSimulation / Simulation / PostSimulation）。
/// </summary>
public class GameplayAbilitiesModule : IModule, ITaskCompletionListener
{
    // ── Friflo QuerySystems（挂到 World Simulation 阶段）──
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
    public Gameplay.Tasks.SpawnSystem SpawnSystem { get; }
    public MoveToSystem MoveToSystem { get; }
    public Gameplay.Tasks.TimerSystem TimerSystem { get; }
    public DelaySystem DelaySystem { get; }

    // ── POCO Manager / System（外部调用）──
    public GameplayEventBus EventBus { get; }
    public GameplayEventDispatcher EventDispatcher { get; }
    public AbilityActivationManager ActivationManager { get; }
    public GameplayCueManager? CueManager { get; }
    public PredictionManager PredictionManager { get; }

    /// <summary>
    /// 构造函数——接收 World 并在构造时完成全部挂载。从 world 取 Store / NetMode，
    /// 构造全部 Manager/System，并按原 Phase 顺序挂到三阶段调度。
    /// </summary>
    public GameplayAbilitiesModule(World world)
    {
        var store = world.Store;
        var netMode = world.NetMode;

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
        SpawnSystem = new Gameplay.Tasks.SpawnSystem();
        MoveToSystem = new MoveToSystem();
        TimerSystem = new Gameplay.Tasks.TimerSystem(EventBus);
        DelaySystem = new DelaySystem();

        // ── 挂到 World 三阶段调度（原 Phase 顺序）──
        // PreSimulation: 消费本帧事件（原 Phase 0，在 Simulation 之前确保本帧事件对 System 可见）
        world.AddSystem(new EventDispatcherSystem(EventDispatcher), ESimulationStage.PreSimulation);

        // Simulation: 原 Phase 1-3 的 13 个 System，保持原相对顺序
        world.AddSystem(DelaySystem, ESimulationStage.Simulation);          // Delay 计时（共享——TaskBuilder.Delay 复用）
        world.AddSystem(GameplayEventSystem, ESimulationStage.Simulation);
        world.AddSystem(AttributeListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(TagListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(CommitPhaseListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(EffectListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(AbilityActivateListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(InputListenerSystem, ESimulationStage.Simulation);
        world.AddSystem(SpawnSystem, ESimulationStage.Simulation);
        world.AddSystem(MoveToSystem, ESimulationStage.Simulation);
        world.AddSystem(TimerSystem, ESimulationStage.Simulation);
        world.AddSystem(TaskScheduler, ESimulationStage.Simulation);        // Task 完成检测（所有 Task Done → 通知 ITaskCompletionListener）
        world.AddSystem(EffectSystem, ESimulationStage.Simulation);         // GE Duration/Period Tick + Apply/Remove

        // PostSimulation: 原 Phase 4（Attribute 固定阶段 Flush）+ Phase 5（延迟删除）
        world.AddSystem(new AttributeFlushSystem(AttributeAggregatorManager), ESimulationStage.PostSimulation);
        world.AddSystem(new DeferredDeletionSystem(ActivationManager, TaskScheduler), ESimulationStage.PostSimulation);
    }

    /// <summary>
    /// ITaskCompletionListener —— Owner（ActiveAbility）的所有 Task 完成时结束 Ability。
    /// </summary>
    public void OnAllTasksDone(Entity owner)
        => ActivationManager.CancelAbility(owner);

    /// <summary>注入输入服务（无输入环境如 Dedicated Server 不调用——Input 类 Task 保持 Running）。</summary>
    public void SetInputService(IInputService inputService)
        => InputListenerSystem.SetInputService(inputService);

    private static GameplayCueManager? CreateCueManager(ENetMode netMode)
    {
#if GP_SERVER
        return null; // DS 无表现层
#else
        return new GameplayCueManager();
#endif
    }
}

/// <summary>PreSimulation：消费 GAS 事件帧（原 Phase 0）。</summary>
internal sealed class EventDispatcherSystem : BaseSystem
{
    private readonly GameplayEventDispatcher dispatcher;
    public EventDispatcherSystem(GameplayEventDispatcher dispatcher) => this.dispatcher = dispatcher;
    protected override void OnUpdateGroup() => dispatcher.Tick();
}

/// <summary>PostSimulation：Attribute 固定阶段 Flush（原 Phase 4）。</summary>
internal sealed class AttributeFlushSystem : BaseSystem
{
    private readonly AttributeAggregatorManager manager;
    public AttributeFlushSystem(AttributeAggregatorManager manager) => this.manager = manager;
    protected override void OnUpdateGroup() => manager.Flush();
}

/// <summary>PostSimulation：延迟删除（原 Phase 5，Query 循环内不能 DeleteEntity）。</summary>
internal sealed class DeferredDeletionSystem : BaseSystem
{
    private readonly AbilityActivationManager activationManager;
    private readonly TaskSchedulerSystem taskScheduler;
    public DeferredDeletionSystem(AbilityActivationManager activationManager, TaskSchedulerSystem taskScheduler)
    {
        this.activationManager = activationManager;
        this.taskScheduler = taskScheduler;
    }
    protected override void OnUpdateGroup()
    {
        activationManager.ProcessPendingDeletions();
        taskScheduler.ProcessPendingDeletions();
    }
}
