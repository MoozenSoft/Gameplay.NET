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
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new TagListenerComponent
            {
                Target = target,
                Tag = tag,
                Condition = TagCondition.Added,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建 Tag 监听 Task——目标移除指定 Tag 时完成（Done）。</summary>
    public static Entity WaitTagRemoved(EntityStore store, Entity target, GameplayTag tag, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new TagListenerComponent
            {
                Target = target,
                Tag = tag,
                Condition = TagCondition.Removed,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建属性监听 Task——目标属性变化指定次数后完成（Done）。</summary>
    public static Entity WaitAttributeChange(EntityStore store, Entity target, GameplayAttribute attribute, int count, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new AttributeListener
            {
                Target = target,
                Attribute = attribute,
                Count = count,
            });
        owner.AddChild(entity);
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
}
