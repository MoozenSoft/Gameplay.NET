// tests/Gameplay.Tests/Gameplay.Tests.Tasks/InputListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Interfaces;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/InputListenerSystem.cs——每帧轮询 IInputService。</summary>
public class InputListenerSystemTests
{
    private const int JumpAction = 1;
    private const int AttackAction = 2;

    /// <summary>测试用假输入服务——手动设置本帧状态。</summary>
    private sealed class FakeInputService : IInputService
    {
        public bool PressedThisFrame;
        public bool ReleasedThisFrame;
        public bool Held;

        public bool WasPressedThisFrame(int actionId) => actionId == JumpAction && PressedThisFrame;
        public bool WasReleasedThisFrame(int actionId) => actionId == JumpAction && ReleasedThisFrame;
        public bool IsHeld(int actionId) => actionId == JumpAction && Held;
    }

    private static (Entity Task, FakeInputService Input, SystemRoot Root) Setup(EInputTrigger trigger)
    {
        var store = new EntityStore();
        var input = new FakeInputService();
        var system = new InputListenerSystem();
        system.SetInputService(input);

        var task = TaskBuilder.WaitInputPress(store, JumpAction, owner: store.CreateEntity());
        task.GetComponent<InputListener>().Trigger = trigger;

        var root = new SystemRoot(store) { system };
        return (task, input, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void Press_CompletesWhenPressedThisFrame()
    {
        var (task, input, root) = Setup(EInputTrigger.Press);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        input.PressedThisFrame = true;
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Press_StaysRunningWhenNotPressed()
    {
        var (task, input, root) = Setup(EInputTrigger.Press);

        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void Release_CompletesWhenReleasedThisFrame()
    {
        var (task, input, root) = Setup(EInputTrigger.Release);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        input.ReleasedThisFrame = true;
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Hold_CompletesWhenHeld()
    {
        var (task, input, root) = Setup(EInputTrigger.Hold);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        input.Held = true;
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void WithoutInputService_TaskStaysRunning()
    {
        var store = new EntityStore();
        var system = new InputListenerSystem(); // 未注入服务（Server 场景）

        var task = TaskBuilder.WaitInputPress(store, JumpAction, owner: store.CreateEntity());
        var root = new SystemRoot(store) { system };

        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));

        // 无输入服务：生命周期照走（Pending → Running），但条件永不满足
        Assert.Equal(ETaskState.Running, GetState(task));
    }
}
