// tests/Gameplay.Tests/Gameplay.Tests.Tasks/TaskCommandsTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Runtime/TaskCommands.cs——Driver System 的状态命令门面。</summary>
public class TaskCommandsTests
{
    private static Entity CreateTask(EntityStore store, ETaskState initialState)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new TaskStateComponent { State = initialState });
        return entity;
    }

    [Fact]
    public void Complete_SetsTaskStateToDone()
    {
        var store = new EntityStore();
        var entity = CreateTask(store, ETaskState.Running);

        TaskCommands.Complete(entity);

        Assert.Equal(ETaskState.Done, entity.GetComponent<TaskStateComponent>().State);
    }

    [Fact]
    public void Cancel_SetsTaskStateToCancelled()
    {
        var store = new EntityStore();
        var entity = CreateTask(store, ETaskState.Running);

        TaskCommands.Cancel(entity);

        Assert.Equal(ETaskState.Cancelled, entity.GetComponent<TaskStateComponent>().State);
    }

    [Fact]
    public void Complete_OnNullEntity_IsSafe()
    {
        TaskCommands.Complete(default); // 不应抛异常
    }

    [Fact]
    public void Cancel_OnNullEntity_IsSafe()
    {
        TaskCommands.Cancel(default); // 不应抛异常
    }

    [Fact]
    public void Complete_OnEntityWithoutTaskState_IsSafe()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity(); // 无 TaskStateComponent

        TaskCommands.Complete(entity); // 不应抛异常
    }
}
