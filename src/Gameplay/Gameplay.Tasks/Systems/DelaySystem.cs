using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Tasks;

/// <summary>延时能力 Driver——每帧推进 DelayComponent。</summary>
/// <remarks>
/// <para>Pending → Running → (Elapsed ≥ Duration → Done)。</para>
/// <para>不处理 Done/Cancelled 的销毁，由 TaskSchedulerSystem 统一负责。</para>
/// </remarks>
public class DelaySystem : QuerySystem<TaskStateComponent, DelayComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity(
            (ref TaskStateComponent state, ref DelayComponent delay, Entity entity) =>
        {
            switch (state.State)
            {
                case ETaskState.Pending:
                    state.State = ETaskState.Running;
                    // Duration=0 → Elapsed (0) >= Duration (0) → 立即 Done
                    if (delay.Elapsed >= delay.Duration)
                        TaskCommands.Complete(entity);
                    break;

                case ETaskState.Running:
                    delay.Elapsed += Tick.deltaTime;
                    if (delay.Elapsed >= delay.Duration)
                        TaskCommands.Complete(entity);
                    break;

                // Done / Cancelled → 不处理，等 TaskSchedulerSystem 统一销毁
            }
        });
    }
}
