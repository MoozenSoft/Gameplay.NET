// tests/Gameplay.Tests/Gameplay.Tests.Tasks/CommitPhaseListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/CommitPhaseListenerSystem.cs。</summary>
public class CommitPhaseListenerSystemTests
{
    private static Entity CreateActiveAbility(EntityStore store, EAbilityInstanceState state)
    {
        var ability = store.CreateEntity();
        ability.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = state == EAbilityInstanceState.Active,
            Owner = store.CreateEntity(),
            State = state,
        });
        return ability;
    }

    private static (Entity Task, SystemRoot Root) Setup(Entity activeAbility)
    {
        var store = activeAbility.Store;
        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new CommitPhaseListener { Target = activeAbility });

        var root = new SystemRoot(store) { new CommitPhaseListenerSystem() };
        return (task, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void ActiveState_CompletesTask()
    {
        var store = new EntityStore();
        var ability = CreateActiveAbility(store, EAbilityInstanceState.Active);
        var (task, root) = Setup(ability);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // State=Active（Commit 完成）→ Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void CancelledState_DoesNotCompleteTask()
    {
        var store = new EntityStore();
        var ability = CreateActiveAbility(store, EAbilityInstanceState.Cancelled);
        var (task, root) = Setup(ability);

        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void TargetDeleted_CompletesTask()
    {
        var store = new EntityStore();
        var ability = CreateActiveAbility(store, EAbilityInstanceState.Active);
        var (task, root) = Setup(ability);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        ability.DeleteEntity();
        root.Update(new UpdateTick(0.16f, 0)); // 目标已销毁 → 无条件 Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }
}
