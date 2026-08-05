using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Interfaces;

namespace Gameplay.Tasks;

/// <summary>
/// 输入能力 Driver——每帧轮询 IInputService，满足触发条件时 Task 完成（Done）。<br/>
/// 无输入环境（Server）不注入服务——生命周期照走（Pending→Running），只是条件永不满足。<br/>
/// 同一 Query 内按 <see cref="EInputTrigger"/> 分支——同一能力内的条件分支，不是跨能力 switch。
/// </summary>
public class InputListenerSystem : QuerySystem<InputListener, TaskStateComponent>
{
    private IInputService? inputService;

    /// <summary>注入输入服务（无输入环境传 null，Task 保持 Running 但永不完成）。</summary>
    public void SetInputService(IInputService? service) => inputService = service;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref InputListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            // Pending → Running（生命周期照走——与所有 Driver 一致）
            if (state.State == ETaskState.Pending)
                state.State = ETaskState.Running;
            else if (state.State != ETaskState.Running)
                return;

            if (inputService == null)
                return; // 无输入服务：条件永不满足（Server 无输入是合法场景）

            bool triggered = listener.Trigger switch
            {
                EInputTrigger.Press   => inputService.WasPressedThisFrame(listener.ActionId),
                EInputTrigger.Release => inputService.WasReleasedThisFrame(listener.ActionId),
                EInputTrigger.Hold    => inputService.IsHeld(listener.ActionId),
                _ => false,
            };
            if (triggered)
                TaskCommands.Complete(entity);
        });
    }
}
