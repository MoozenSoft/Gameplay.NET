using System;
using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>
/// Task Builder（Facade/Factory）——创建 Task Entity 的唯一入口。<br/>
/// 本身无逻辑：创建 TaskState + TaskOwner + 能力组件（Archetype 组合），并挂到 Owner 下。<br/>
/// 使用 Friflo <see cref="EntityStoreExtensions.CreateEntity{T1,T2,T3}"/> 重载，
/// 创建即最终 Archetype——避免逐个 AddComponent 的多次 structural change（Archetype 迁移）。<br/>
/// 底层不存在 GameplayTask / AbilityTask 之分——只有不同的 Owner 与 Component 组合。<br/>
/// Ability 用 TaskBuilder.WaitXxx（owner = ActiveAbility），Gameplay 用 TaskBuilder.Delay / TaskBuilder.MoveTo（owner = 任意 Entity）。
/// </summary>
public static class TaskBuilder
{
    /// <summary>创建延时 Task——等待指定时间后完成（Done）。</summary>
    public static Entity Delay(EntityStore store, float duration, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new DelayComponent { Duration = duration, Elapsed = 0f });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建事件监听 Task——收到匹配的 GameplayEvent 时完成（Done）。</summary>
    public static Entity WaitEvent(EntityStore store, ushort eventId, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new GameplayEventListener { EventId = eventId });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建 Tag 监听 Task——目标获得指定 Tag 时完成（Done）。</summary>
    public static Entity WaitTagAdded(EntityStore store, Entity target, GameplayTag tag, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, tag, null, TagCondition.Added, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag 监听 Task——目标移除指定 Tag 时完成（Done）。</summary>
    public static Entity WaitTagRemoved(EntityStore store, Entity target, GameplayTag tag, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, tag, null, TagCondition.Removed, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag Query 监听 Task——目标获得指定 Tag 集合（全部）时完成（Done）。</summary>
    public static Entity WaitTagQueryAdded(EntityStore store, Entity target, GameplayTagContainer required, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, default, required, TagCondition.Added, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag Query 监听 Task——目标失去指定 Tag 集合中的任一 Tag（条件被破坏）时完成（Done）。</summary>
    public static Entity WaitTagQueryRemoved(EntityStore store, Entity target, GameplayTagContainer required, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, default, required, TagCondition.Removed, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性监听 Task——目标属性变化指定次数后完成（Done）。</summary>
    public static Entity WaitAttributeChange(EntityStore store, Entity target, GameplayAttribute attribute, int count, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Changed, 0f, count, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性阈值 Task——目标属性 CurrentValue 超过阈值时完成（Done）。</summary>
    public static Entity WaitAttributeAbove(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Above, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性阈值 Task——目标属性 CurrentValue 低于阈值时完成（Done）。</summary>
    public static Entity WaitAttributeBelow(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Below, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性比值阈值 Task——目标属性 CurrentValue/BaseValue 超过阈值时完成（Done）。</summary>
    public static Entity WaitAttributeRatioAbove(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.RatioAbove, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性比值阈值 Task——目标属性 CurrentValue/BaseValue 低于阈值时完成（Done）。</summary>
    public static Entity WaitAttributeRatioBelow(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.RatioBelow, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Commit 阶段监听 Task——目标 ActiveAbility Commit 完成（State=Active）时完成（Done）。</summary>
    public static Entity WaitCommitPhase(EntityStore store, Entity activeAbility, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new CommitPhaseListener { Target = activeAbility });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建属性监听 Task 基础 Archetype：TaskState + TaskOwner + AttributeListener（指定条件）。</summary>
    private static Entity CreateAttributeTask(EntityStore store, Entity target, GameplayAttribute attribute,
        EAttributeCondition condition, float threshold, int count, Entity owner, int taskHandle)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new AttributeListener
            {
                Target = target,
                Attribute = attribute,
                Condition = condition,
                Threshold = threshold,
                Count = count,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>
    /// 创建 Tag 监听 Task 基础 Archetype：TaskState + TaskOwner + TagListenerComponent。<br/>
    /// 单 Tag 模式（required 为 null）用 tag；Query 模式用 required（防御性拷贝——可变 class 引用，
    /// 防调用者后续修改改写所有 Task 的条件）。
    /// </summary>
    private static Entity CreateTagListenerTask(EntityStore store, Entity target, GameplayTag tag,
        GameplayTagContainer? required, TagCondition condition, Entity owner, int taskHandle)
    {
        if (required == null)
        {
            // fail-fast：无效 Tag（未注册名 → Request 返回 Invalid）会让条件永不满足，静默挂起
            if (!tag.IsValid)
                throw new ArgumentException("Tag 无效——未注册的 Tag 名会让条件永不满足", nameof(tag));
        }
        else if (required.Count == 0)
        {
            // 空集合的 HasAll 恒真/恒假，语义无意义
            throw new ArgumentException("RequiredTags 不能为空", nameof(required));
        }

        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new TagListenerComponent
            {
                Target = target,
                Tag = tag,
                RequiredTags = required == null ? null : CopyContainer(required),
                Condition = condition,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>拷贝 GameplayTagContainer（冷路径，可接受分配）。</summary>
    private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
    {
        var copy = new GameplayTagContainer();
        foreach (var tag in source)
            copy.AddTag(tag);
        return copy;
    }
}
