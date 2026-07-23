// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/AbilityTaskSystemTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

public class AbilityTaskSystemTests
{
    [Fact]
    public void AbilityTaskContextComponent_Default_Values()
    {
        var comp = new AbilityTaskContextComponent();
        Assert.Equal(default, comp.ActiveAbility);
        Assert.Equal(0, comp.TaskHandle);
    }

    [Fact]
    public void AllTasksDone_CancelsActiveAbility()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var activationSys = new AbilityActivationSystem(effectSys);
        var taskSys = new AbilityTaskSystem(activationSys);
        var root = new SystemRoot(store) { taskSys };

        // ActiveAbility Entity
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // 两个 Task 子 Entity，都标记为 Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = TaskState.Done });
        task1.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = TaskState.Done });
        task2.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        // ActiveAbility 应被 Cancel → DeleteEntity → 实体已不存在
        Assert.True(activeAbility.IsNull);
    }

    [Fact]
    public void SomeTasksPending_DoesNotCancelActiveAbility()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var activationSys = new AbilityActivationSystem(effectSys);
        var taskSys = new AbilityTaskSystem(activationSys);
        var root = new SystemRoot(store) { taskSys };

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // Task1: Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = TaskState.Done });
        task1.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        // Task2: 仍在 Running
        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = TaskState.Running });
        task2.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        // ActiveAbility 不应被 Cancel
        var comp = activeAbility.GetComponent<ActiveAbilityComponent>();
        Assert.Equal(EAbilityInstanceState.Active, comp.State);
        Assert.True(comp.IsActive);
    }

    [Fact]
    public void MixedDoneAndCancelled_CancelsActiveAbility()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var activationSys = new AbilityActivationSystem(effectSys);
        var taskSys = new AbilityTaskSystem(activationSys);
        var root = new SystemRoot(store) { taskSys };

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // Task1: Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = TaskState.Done });
        task1.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        // Task2: Cancelled
        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = TaskState.Cancelled });
        task2.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        // 全部 Done/Cancelled → 应 Cancel → DeleteEntity
        Assert.True(activeAbility.IsNull);
    }

    [Fact]
    public void PendingState_DoesNotTriggerCancel()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var activationSys = new AbilityActivationSystem(effectSys);
        var taskSys = new AbilityTaskSystem(activationSys);
        var root = new SystemRoot(store) { taskSys };

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // 唯一 Task: 仍在 Pending（Query 会匹配，但 state 不是 Done/Cancelled → 跳过）
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = TaskState.Pending });
        task1.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        var comp = activeAbility.GetComponent<ActiveAbilityComponent>();
        Assert.Equal(EAbilityInstanceState.Active, comp.State);
        Assert.True(comp.IsActive);
    }

    [Fact]
    public void NoTaskContextEntities_SystemDoesNothing()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var activationSys = new AbilityActivationSystem(effectSys);
        var taskSys = new AbilityTaskSystem(activationSys);
        var root = new SystemRoot(store) { taskSys };

        // 创建 ActiveAbility，但不创建任何 Task entity（无 AbilityTaskContextComponent）
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        root.Update(new UpdateTick(0.16f, 0));

        // 没有 Task 实体被 Query 匹配，ActiveAbility 保持不变
        var comp = activeAbility.GetComponent<ActiveAbilityComponent>();
        Assert.Equal(EAbilityInstanceState.Active, comp.State);
        Assert.True(comp.IsActive);
    }
}
