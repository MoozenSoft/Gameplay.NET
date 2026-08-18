using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件模板条目（实例化时写回组件值的动作）。</summary>
internal readonly struct PrefabComponent
{
    public readonly Action<Entity> Apply;   // 实例化时写回组件值

    public PrefabComponent(Action<Entity> apply) => Apply = apply;
}

/// <summary>Prefab 构建器。</summary>
public sealed class PrefabBuilder
{
    internal readonly List<PrefabComponent> Components = new();

    public PrefabBuilder With<T>() where T : struct, IComponent
    {
        Components.Add(new PrefabComponent(e => e.AddComponent<T>()));
        return this;
    }

    public PrefabBuilder With<T>(in T value) where T : struct, IComponent
    {
        var component = value;   // 拷贝：in 只读参数不能直接按 ref 传入委托
        Components.Add(new PrefabComponent(e => e.AddComponent(component)));
        return this;
    }
}

/// <summary>Archetype 蓝图（纯数据模板）。</summary>
public sealed class Prefab
{
    private readonly PrefabComponent[] _components;

    private Prefab(PrefabComponent[] components) => _components = components;

    public static Prefab Define(Action<PrefabBuilder> config)
    {
        var builder = new PrefabBuilder();
        config(builder);
        return new Prefab(builder.Components.ToArray());
    }

    public Entity Instantiate(EntityStore store)
    {
        var entity = store.CreateEntity();
        foreach (var c in _components)
            c.Apply(entity);
        return entity;
    }
}

/// <summary>Prefab 全局注册中心（模板跨 World 共享，自增 id 索引）。</summary>
public static class PrefabRegistry
{
    private static readonly Dictionary<int, Prefab> ById = new();
    private static int _nextId = 1;

    public static int Register(Prefab prefab)
    {
        var id = _nextId++;
        ById[id] = prefab;
        return id;
    }

    public static Prefab? GetById(int id)
        => ById.TryGetValue(id, out var p) ? p : null;
}
