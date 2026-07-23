// src/Gameplay/Gameplay.Abilities/AbilityTask/WaitDelayTask.cs
using Friflo.Engine.ECS;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// WaitDelayTask —— 等待指定时间的 Task。<br/>
/// 通过给 Task Entity 挂载 <see cref="DelayTaskComponent"/> + <see cref="TaskStateComponent"/> 复用现有的 <see cref="DelayTaskSystem"/>。<br/>
/// 不需要自定义定 System，DelayTaskSystem 自动处理 Pending → Running → Done 的时序。
/// </summary>
public static class WaitDelayTask
{
    /// <summary>创建等待延时 Task Entity。</summary>
    /// <param name="store">EntityStore</param>
    /// <param name="duration">等待时长（秒）</param>
    /// <param name="activeAbility">归属的 ActiveAbility Entity</param>
    /// <param name="taskHandle">Task 句柄（可选，默认 0）</param>
    /// <returns>创建的 Task Entity</returns>
    public static Entity Create(EntityStore store, float duration, Entity activeAbility, int taskHandle = 0)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new TaskStateComponent { State = TaskState.Pending });
        entity.AddComponent(new TaskOwnerComponent { Owner = default });
        entity.AddComponent(new DelayTaskComponent { Duration = duration, Elapsed = 0f });
        entity.AddComponent(new AbilityTaskContextComponent
        {
            ActiveAbility = activeAbility,
            TaskHandle = taskHandle,
        });
        return entity;
    }
}
