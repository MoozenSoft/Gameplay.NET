using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>
/// 游戏世界——持有 ECS EntityStore、时间/事件/随机成员，并提供模块挂载、系统调度、
/// 服务注册与延迟删除能力。
/// </summary>
public class World
{
    private readonly EntityStore store;
    private readonly SystemRoot root;
    private readonly SystemGroup preGroup;
    private readonly SystemGroup simGroup;
    private readonly SystemGroup postGroup;
    private readonly Dictionary<Type, object> services = new();
    private readonly HashSet<Entity> pendingDeletions = new();   // HashSet：同一实体被多 System 重复 DeferDelete 时去重
    private readonly List<IModule> modules;

    /// <summary>当前网络模式。</summary>
    public ENetMode NetMode { get; }

    /// <summary>Friflo ECS 实体存储。</summary>
    public EntityStore Store => store;

    /// <summary>模拟时钟——所有 System 的时间基准。</summary>
    public GameTime Time { get; }

    /// <summary>通用事件总线（双缓冲 + Tick 分发）。</summary>
    public EventBus Events { get; }

    /// <summary>确定性随机源。</summary>
    public DeterministicRng Random { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(ENetMode netMode, ulong seed = 0UL, ETimeStep timeStep = ETimeStep.Variable)
    {
        NetMode = netMode;
        store = new EntityStore();
        Time = new GameTime(timeStep);
        Events = new EventBus();
        Random = new DeterministicRng(seed);
        root = new SystemRoot(store);
        preGroup = new SystemGroup("PreSimulation");
        simGroup = new SystemGroup("Simulation");
        postGroup = new SystemGroup("PostSimulation");
        root.Add(preGroup);
        root.Add(simGroup);
        root.Add(postGroup);
        modules = new List<IModule>();
    }

    /// <summary>返回当前网络模式。</summary>
    public ENetMode GetNetMode() => NetMode;

    /// <summary>注册模块（模块构造时已通过构造函数接收 World 完成挂载，此处仅追踪）。</summary>
    public World AddModule(IModule module)
    {
        modules.Add(module);
        return this;
    }

    /// <summary>将 System 挂到指定模拟阶段。</summary>
    public void AddSystem(BaseSystem system, ESimulationStage stage)
    {
        var group = stage switch
        {
            ESimulationStage.PreSimulation => preGroup,
            ESimulationStage.Simulation => simGroup,
            ESimulationStage.PostSimulation => postGroup,
            _ => simGroup,
        };
        group.Add(system);
    }

    /// <summary>注册服务（按类型存储，重复注册覆盖）。</summary>
    public void RegisterService<T>(T service) where T : class
        => services[typeof(T)] = service;

    /// <summary>获取已注册的服务，未注册返回 null。</summary>
    public T? GetService<T>() where T : class
        => services.TryGetValue(typeof(T), out var box) ? (T)box : null;

    /// <summary>延迟删除——挂到队列，帧末统一执行（Query 循环内不能 DeleteEntity；HashSet 自动去重）。</summary>
    public void DeferDelete(Entity entity)
    {
        if (!entity.IsNull) pendingDeletions.Add(entity);
    }

    /// <summary>推进一帧：时间 → 事件分发 → System 执行 → 本帧事件分发 → 延迟删除。
    /// 双 Tick：第一发处理上一帧事件；System 入队本帧事件后第二次 Tick 立即分发（此时实体仍存活，可安全读组件），帧末才删除。
    /// Fixed 模式：先 Feed 累积，再循环消费 0..MaxSubSteps 个固定子步（每子步执行一次 System），事件仍每帧分发一次。</summary>
    public void Update(float deltaTime)
    {
        if (Time.Mode == ETimeStep.Fixed)
        {
            Time.Feed(deltaTime);                   // 1. 累积（内部 clamp 到 FixedDeltaTime*MaxSubSteps）
            Events.Tick();                          // 2. 分发上一帧事件
            while (Time.TryConsumeFixedStep(out var stepDt))
                root.Update(new UpdateTick(stepDt, Time.ElapsedTime));  // 3. 每子步执行 System
            Events.Tick();                          // 4. 分发本帧 enqueue 的事件（死亡事件此时实体未删）
        }
        else
        {
            Time.Advance(deltaTime);                // 1. 推进时钟
            Events.Tick();                          // 2. 分发上一帧事件
            root.Update(new UpdateTick(Time.ScaledDeltaTime, Time.ElapsedTime));  // 3. System 执行
            Events.Tick();                          // 4. 分发本帧 enqueue 的事件（死亡事件此时实体未删）
        }
        ProcessPendingDeletions();                  // 5. 帧末真正删除
    }

    private void ProcessPendingDeletions()
    {
        foreach (var entity in pendingDeletions)
        {
            if (!entity.IsNull) entity.DeleteEntity();
        }
        pendingDeletions.Clear();
    }
}
