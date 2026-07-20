using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.GameplayTasks;

/// <summary>
/// 每帧推进 DelayTask。<br/>
/// Pending → Running → (Elapsed >= Duration → Done)。
/// 不处理 Done/Cancelled 的销毁，由外部决策。
/// </summary>
public class DelayTaskSystem : QuerySystem<TaskStateComponent, DelayTaskComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity(
            (ref TaskStateComponent state, ref DelayTaskComponent delay, Entity entity) =>
        {
            switch (state.State)
            {
                case TaskState.Pending:
                    state.State = TaskState.Running;
                    // Duration=0 → Elapsed (0) >= Duration (0) → 立即 Done
                    if (delay.Elapsed >= delay.Duration)
                        state.State = TaskState.Done;
                    break;

                case TaskState.Running:
                    delay.Elapsed += Tick.deltaTime;
                    if (delay.Elapsed >= delay.Duration)
                        state.State = TaskState.Done;
                    break;

                // Done / Cancelled → 不处理，等外部决策
            }
        });
    }
}
