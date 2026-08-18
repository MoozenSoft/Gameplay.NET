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
    private readonly EntityStore _store;
    private readonly SystemRoot _root;
    private readonly SystemGroup _preGroup;
    private readonly SystemGroup _simGroup;
    private readonly SystemGroup _postGroup;
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<Entity> _pendingDeletions = new();

    /// <summary>当前网络模式。</summary>
    public ENetMode NetMode { get; }

    /// <summary>Friflo ECS 实体存储。</summary>
    public EntityStore Store => _store;

    /// <summary>模拟时钟——所有 System 的时间基准。</summary>
    public GameTime Time { get; }

    /// <summary>通用事件总线（双缓冲 + Tick 分发）。</summary>
    public EventBus Events { get; }

    /// <summary>确定性随机源。</summary>
    public DeterministicRng Random { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(ENetMode netMode, ulong seed = 0UL)
    {
        NetMode = netMode;
        _store = new EntityStore();
        Time = new GameTime(ETimeStep.Variable);
        Events = new EventBus();
        Random = new DeterministicRng(seed);
        _root = new SystemRoot(_store);
        _preGroup = new SystemGroup("PreSimulation");
        _simGroup = new SystemGroup("Simulation");
        _postGroup = new SystemGroup("PostSimulation");
        _root.Add(_preGroup);
        _root.Add(_simGroup);
        _root.Add(_postGroup);
    }

    /// <summary>返回当前网络模式。</summary>
    public ENetMode GetNetMode() => NetMode;

    /// <summary>挂载模块（泛型便捷版，模块需有无参构造）。</summary>
    public World AddModule<T>() where T : IModule, new() => AddModule(new T());

    /// <summary>挂载模块——调用其 Build 完成 System/Manager 注册。</summary>
    public World AddModule(IModule module)
    {
        module.Build(this);
        return this;
    }

    /// <summary>将 System 挂到指定模拟阶段。</summary>
    public void AddSystem(BaseSystem system, ESimulationStage stage)
    {
        var group = stage switch
        {
            ESimulationStage.PreSimulation => _preGroup,
            ESimulationStage.Simulation => _simGroup,
            ESimulationStage.PostSimulation => _postGroup,
            _ => _simGroup,
        };
        group.Add(system);
    }

    /// <summary>注册服务（按类型存储，重复注册覆盖）。</summary>
    public void RegisterService<T>(T service) where T : class
        => _services[typeof(T)] = service;

    /// <summary>获取已注册的服务，未注册返回 null。</summary>
    public T? GetService<T>() where T : class
        => _services.TryGetValue(typeof(T), out var box) ? (T)box : null;

    /// <summary>延迟删除——挂到队列，下次 Update 统一执行（Query 循环内不能 DeleteEntity）。</summary>
    public void DeferDelete(Entity entity)
    {
        if (!entity.IsNull) _pendingDeletions.Add(entity);
    }

    /// <summary>推进一帧：时间 → 事件分发 → System 执行 → 延迟删除。</summary>
    public void Update(float deltaTime)
    {
        Time.Advance(deltaTime);                    // 1. 推进时钟
        Events.Tick();                              // 2. 分发事件（System 之前，本帧事件可见）
        _root.Update(new UpdateTick(Time.ScaledDeltaTime, 0)); // 3. 执行 System（Pre → Simulation → Post）
        ProcessPendingDeletions();                  // 4. 全局延迟删除
    }

    private void ProcessPendingDeletions()
    {
        for (int i = 0; i < _pendingDeletions.Count; i++)
        {
            var entity = _pendingDeletions[i];
            if (!entity.IsNull) entity.DeleteEntity();
        }
        _pendingDeletions.Clear();
    }
}
