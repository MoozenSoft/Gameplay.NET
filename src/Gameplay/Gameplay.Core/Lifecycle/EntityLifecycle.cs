using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体生命周期事件类型。</summary>
public enum EEntityLifecycleType
{
    EntityCreated,
    EntityDeleted,
    ComponentAdded,
    ComponentRemoved,
}

/// <summary>实体生命周期事件。</summary>
public struct EntityLifecycleEvent
{
    public EEntityLifecycleType Type;
    public Entity Entity;
    public ComponentType ComponentType;   // 增删组件时有效
}

/// <summary>实体生命周期事件处理器。</summary>
public delegate void EntityLifecycleHandler(in EntityLifecycleEvent evt);

/// <summary>Friflo 实体事件的统一订阅面（即时转发，薄封装）。</summary>
public static class EntityLifecycle
{
    private sealed class HandlerList
    {
        public readonly List<EntityLifecycleHandler> Handlers = new();
        public readonly List<EntityLifecycleHandler> Snapshot = new();   // 分发快照：reentrant Subscribe/Unsubscribe 不影响本次迭代
        public bool Hooked;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityStore, HandlerList> handlerMap = new();

    public static void Subscribe(World world, EntityLifecycleHandler handler)
    {
        var store = world.Store;
        var list = handlerMap.GetOrCreateValue(store);
        list.Handlers.Add(handler);
        if (!list.Hooked)
        {
            list.Hooked = true;
            store.OnEntityCreate += OnEntityCreate;
            store.OnEntityDelete += OnEntityDelete;
            store.OnComponentAdded += OnComponentAdded;
            store.OnComponentRemoved += OnComponentRemoved;
        }
    }

    public static void Unsubscribe(World world, EntityLifecycleHandler handler)
    {
        if (!handlerMap.TryGetValue(world.Store, out var list)) return;
        list.Handlers.Remove(handler);
    }

    private static void Dispatch(EntityStore store, in EntityLifecycleEvent evt)
    {
        if (!handlerMap.TryGetValue(store, out var list)) return;
        list.Snapshot.Clear();
        list.Snapshot.AddRange(list.Handlers);   // 快照，分发中 Subscribe/Unsubscribe 不影响本次迭代
        foreach (var h in list.Snapshot) h(in evt);
    }

    private static void OnEntityCreate(EntityCreate args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.EntityCreated, Entity = args.Entity });

    private static void OnEntityDelete(EntityDelete args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.EntityDeleted, Entity = args.Entity });

    private static void OnComponentAdded(ComponentChanged args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.ComponentAdded, Entity = args.Store.GetEntityById(args.EntityId), ComponentType = args.ComponentType });

    private static void OnComponentRemoved(ComponentChanged args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.ComponentRemoved, Entity = args.Store.GetEntityById(args.EntityId), ComponentType = args.ComponentType });
}
