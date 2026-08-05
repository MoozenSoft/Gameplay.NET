using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>属性聚合管理器（POCO，非 ECS System）——管理 (Entity, GameplayAttribute) → AttributeAggregator 映射、脏队列与固定阶段 Flush。</summary>
/// <remarks>
/// <para>读写委托存储在 AttributeDescriptor 注册表中。</para>
/// </remarks>
public class AttributeAggregatorManager
{
    // ── 核心数据 ──
    private readonly Dictionary<AttributeKey, AttributeAggregator> aggregators = new();
    private readonly Dictionary<GameplayAttribute, AttributeDescriptor> descriptors = new();
    private GameplayEventBus? eventBus;

    // ── 反向索引 ──
    private readonly Dictionary<Entity, List<AttributeKey>> entityToAttributes = new();
    private readonly Dictionary<GameplayEffectHandle, List<AttributeKey>> handleToAttributes = new();

    // ── 脏队列（双缓冲） ──
    private List<AttributeKey> currentDirtyQueue = new();
    private List<AttributeKey> nextDirtyQueue = new();
    private bool isFlushing;

    // ── Attribute 注册 ──

    /// <summary>注册 AttributeDescriptor（读写委托）。</summary>
    /// <remarks>
    /// <para>SG 通过 RegisterAll 批量调用。</para>
    /// <para>同一 Id 重复注册时静默覆盖（支持热重载）。</para>
    /// </remarks>
    internal void RegisterAttribute(GameplayAttribute attr,
        AttributeDescriptor.ReadValue readBase,
        AttributeDescriptor.ReadValue readCurrent,
        AttributeDescriptor.WriteValue writeCurrent)
    {
        descriptors[attr] = new AttributeDescriptor(readBase, readCurrent, writeCurrent);
    }

    /// <summary>从 descriptors 尝试解析委托描述符。未注册时返回 false。</summary>
    private bool TryGetDescriptor(GameplayAttribute attr, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AttributeDescriptor? desc)
        => descriptors.TryGetValue(attr, out desc);

    // ── 公开查询 API ──

    /// <summary>返回上次 Flush 后的已结算 CurrentValue。无 aggregator 时返回 Component BaseValue。</summary>
    public float GetCurrentValue(Entity entity, GameplayAttribute attr)
    {
        var key = new AttributeKey(entity, attr);
        if (aggregators.TryGetValue(key, out var agg))
        {
            if (TryGetDescriptor(attr, out var desc))
            {
                desc.ReadCurrent(entity, out var cv);
                return cv;
            }
            return agg.Evaluate();
        }
        // 无 aggregator → 返回 BaseValue
        if (TryGetDescriptor(attr, out var desc2))
        {
            desc2.ReadBase(entity, out var bv);
            return bv;
        }
        return 0f;
    }

    /// <summary>返回 aggregator 的 BaseValue。不存在时读 Component BaseValue。</summary>
    public float GetBaseValue(Entity entity, GameplayAttribute attr)
    {
        var key = new AttributeKey(entity, attr);
        if (aggregators.TryGetValue(key, out var agg))
            return agg.BaseValue;
        if (TryGetDescriptor(attr, out var desc))
        {
            desc.ReadBase(entity, out var baseValue);
            return baseValue;
        }
        return 0f;
    }

    /// <summary>是否存在 (Entity, Attribute) 的聚合器。</summary>
    public bool HasAggregator(Entity entity, GameplayAttribute attr)
        => aggregators.ContainsKey(new AttributeKey(entity, attr));

    // ── BaseValue 统一入口 ──

    /// <summary>设置 BaseValue——唯一写入入口。值改变时 MarkDirty。</summary>
    public void SetBaseValue(Entity entity, GameplayAttribute attr, float value)
    {
        var key = new AttributeKey(entity, attr);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(key);
        }
        if (agg.SetBaseValue(value))
            MarkDirty(key, agg);
    }

    // ── Aggregator 修改 API ──

    /// <summary>设置聚合器的 Mod 源值。首次创建时从 Component 读取 BaseValue 初始化。</summary>
    public void SetAggregatorValue(Entity entity, GameplayAttribute attr, float sourceValue)
    {
        var key = new AttributeKey(entity, attr);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(key);
        }
        if (agg.SetBaseValue(sourceValue))
            MarkDirty(key, agg);
    }

    /// <summary>为 (Entity, Attribute) 添加 GE Modifier。</summary>
    public void AddAggregatorMod(Entity entity, GameplayAttribute attr, GameplayEffectHandle geHandle,
        float magnitude, EGameplayModOp op)
    {
        var key = new AttributeKey(entity, attr);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(key);
        }
        if (agg.AddMod(geHandle, magnitude, op))
        {
            MarkDirty(key, agg);
            AddHandleToAttributeIndex(geHandle, key);
        }
    }

    /// <summary>移除指定 GE Handle 的所有 Modifier。仅实际删除的 aggregator 才 MarkDirty。</summary>
    public void RemoveAggregatorModsByHandle(GameplayEffectHandle handle)
    {
        if (handleToAttributes.TryGetValue(handle, out var keys))
        {
            foreach (var key in keys)
            {
                if (aggregators.TryGetValue(key, out var agg))
                {
                    if (agg.RemoveModsByHandle(handle))
                        MarkDirty(key, agg);
                }
            }
            handleToAttributes.Remove(handle);
        }
    }

    /// <summary>设置事件总线，用于 Flush 时发布 AttributeChangedEvent。</summary>
    public void SetEventBus(GameplayEventBus bus) => eventBus = bus;

    // ── Flush ──

    /// <summary>固定阶段批量刷新：遍历当前脏队列，Evaluate 并写回 CurrentValue。</summary>
    /// <remarks>
    /// <para>Flush 中新增的 Dirty 进入 nextDirtyQueue，留待下一帧处理。</para>
    /// </remarks>
    public void Flush()
    {
        isFlushing = true;

        for (int i = 0; i < currentDirtyQueue.Count; i++)
        {
            var key = currentDirtyQueue[i];
            if (!aggregators.TryGetValue(key, out var agg))
                continue;

            // 读旧值用于事件比较
            float result = agg.Evaluate();
            agg.Dirty = false;

            if (TryGetDescriptor(key.Attribute, out var desc))
            {
                desc.ReadCurrent(key.Entity, out var oldValue);
                desc.WriteCurrent(key.Entity, result);

                // 值变化时发布事件
                if (eventBus != null && oldValue != result)
                {
                    eventBus.Enqueue(new AttributeChangedEvent
                    {
                        Target    = key.Entity,
                        Attribute = key.Attribute,
                        OldValue  = oldValue,
                        NewValue  = result,
                    }, source: default, target: key.Entity);
                }
            }
        }

        isFlushing = false;
        currentDirtyQueue.Clear();

        // 交换队列：Flush 期间的新增 Dirty 下一帧处理
        Swap(ref currentDirtyQueue, ref nextDirtyQueue);
    }

    // ── Entity 生命周期 ──

    /// <summary>清理指定 Entity 的所有 aggregators 和排队项。谁删 Entity 谁负责调此方法。</summary>
    public void RemoveEntity(Entity entity)
    {
        if (entityToAttributes.TryGetValue(entity, out var keys))
        {
            foreach (var key in keys)
            {
                aggregators.Remove(key);
                currentDirtyQueue.Remove(key);
                nextDirtyQueue.Remove(key);
            }
            entityToAttributes.Remove(entity);
        }
    }

    // ── 内部方法 ──

    private void MarkDirty(AttributeKey key, AttributeAggregator agg)
    {
        if (agg.Dirty) return;
        agg.Dirty = true;

        if (isFlushing)
            nextDirtyQueue.Add(key);
        else
            currentDirtyQueue.Add(key);
    }

    private AttributeAggregator CreateAggregator(AttributeKey key)
    {
        var agg = new AttributeAggregator();
        // 从 Component 读取已有 BaseValue 初始化
        if (TryGetDescriptor(key.Attribute, out var desc))
        {
            desc.ReadBase(key.Entity, out var baseValue);
            agg.SetBaseValue(baseValue);
        }
        aggregators[key] = agg;
        AddEntityToAttributeIndex(key.Entity, key);
        return agg;
    }

    private void AddEntityToAttributeIndex(Entity entity, AttributeKey key)
    {
        if (!entityToAttributes.TryGetValue(entity, out var list))
        {
            list = new List<AttributeKey>();
            entityToAttributes[entity] = list;
        }
        list.Add(key);
    }

    private void AddHandleToAttributeIndex(GameplayEffectHandle handle, AttributeKey key)
    {
        if (!handleToAttributes.TryGetValue(handle, out var list))
        {
            list = new List<AttributeKey>();
            handleToAttributes[handle] = list;
        }
        if (!list.Contains(key))
            list.Add(key);
    }

    private static void Swap<T>(ref List<T> a, ref List<T> b)
    {
        (a, b) = (b, a);
    }
}
