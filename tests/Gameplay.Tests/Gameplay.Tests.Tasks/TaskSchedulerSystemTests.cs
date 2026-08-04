// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/AbilityTaskSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

public class TaskSchedulerSystemTests
{
    /// <summary>测试监听器——Owner 全部 Task 完成 → CancelAbility（模拟 GameplayAbilitiesFeature）。</summary>
    private sealed class TestCompletionListener : ITaskCompletionListener
    {
        private readonly AbilityActivationManager mgr;

        public TestCompletionListener(AbilityActivationManager mgr) => this.mgr = mgr;

        public void OnAllTasksDone(Entity owner) => mgr.CancelAbility(owner);
    }

    private static (TaskSchedulerSystem TaskSys, SystemRoot Root, AbilityActivationManager ActivationManager, EntityStore Store) Setup()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);
        var activationManager = new AbilityActivationManager(effectSys);
        var taskSys = new TaskSchedulerSystem();
        taskSys.SetCompletionListener(new TestCompletionListener(activationManager));
        var root = new SystemRoot(store) { taskSys };
        return (taskSys, root, activationManager, store);
    }

    [Fact]
    public void TaskOwnerComponent_Default_Values()
    {
        var comp = new TaskOwnerComponent();
        Assert.Equal(default, comp.Owner);
        Assert.Equal(0, comp.TaskHandle);
    }

    [Fact]
    public void AllTasksDone_CancelsActiveAbility()
    {
        var (taskSys, root, activationManager, store) = Setup();
        

        // ActiveAbility Entity
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // 两个 Task 子 Entity，都标记为 Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = ETaskState.Done });
        task1.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = ETaskState.Done });
        task2.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));
        activationManager.ProcessPendingDeletions();
        taskSys.ProcessPendingDeletions();

        // ActiveAbility 应被 Cancel → DeleteEntity → 实体已不存在
        Assert.True(activeAbility.IsNull);
    }

    [Fact]
    public void SomeTasksPending_DoesNotCancelActiveAbility()
    {
        var (taskSys, root, activationManager, store) = Setup();
        

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // Task1: Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = ETaskState.Done });
        task1.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        // Task2: 仍在 Running
        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = ETaskState.Running });
        task2.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        // ActiveAbility 不应被 Cancel
        var comp = activeAbility.GetComponent<ActiveAbilityComponent>();
        Assert.Equal(EAbilityInstanceState.Active, comp.State);
        Assert.True(comp.IsActive);

        // 已完成的 task1 仍被 Scheduler 销毁（task 自身生命周期与 Owner 决策解耦）
        taskSys.ProcessPendingDeletions();
        Assert.True(task1.IsNull);
    }

    [Fact]
    public void MixedDoneAndCancelled_CancelsActiveAbility()
    {
        var (taskSys, root, activationManager, store) = Setup();
        

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // Task1: Done
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = ETaskState.Done });
        task1.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        // Task2: Cancelled
        var task2 = store.CreateEntity();
        activeAbility.AddChild(task2);
        task2.AddComponent(new TaskStateComponent { State = ETaskState.Cancelled });
        task2.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));
        activationManager.ProcessPendingDeletions();
        taskSys.ProcessPendingDeletions();

        // 全部 Done/Cancelled → 应 Cancel → DeleteEntity
        Assert.True(activeAbility.IsNull);
    }

    [Fact]
    public void PendingState_DoesNotTriggerCancel()
    {
        var (taskSys, root, activationManager, store) = Setup();
        

        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        // 唯一 Task: 仍在 Pending（Query 会匹配，但 state 不是 Done/Cancelled → 跳过）
        var task1 = store.CreateEntity();
        activeAbility.AddChild(task1);
        task1.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task1.AddComponent(new TaskOwnerComponent { Owner = activeAbility });

        root.Update(new UpdateTick(0.16f, 0));

        var comp = activeAbility.GetComponent<ActiveAbilityComponent>();
        Assert.Equal(EAbilityInstanceState.Active, comp.State);
        Assert.True(comp.IsActive);
    }

    [Fact]
    public void NoTaskOwnerEntities_SystemDoesNothing()
    {
        var (taskSys, root, activationManager, store) = Setup();
        

        // 创建 ActiveAbility，但不创建任何 Task entity（无 TaskOwnerComponent）
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
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
