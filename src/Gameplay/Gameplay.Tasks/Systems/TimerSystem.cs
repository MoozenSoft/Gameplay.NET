using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// 计时器能力 Driver——每 <see cref="TimerComponent.Interval"/> 秒向 GameplayEventBus 发一次脉冲事件，<br/>
/// 剩余脉冲数归零后 Task 完成（Done）。对应 UE 的 Repeat（周期性通知外部）。<br/>
/// Pending 帧不累积（与 DelaySystem 一致——注册帧不推进）；每帧至多 1 次脉冲（大 dt 不风暴，残留累积下一帧不丢）；<br/>
/// RemainingPulses=0 = 无限脉冲（永不完成）。
/// </summary>
public class TimerSystem : QuerySystem<TimerComponent, TaskStateComponent>
{
    private readonly GameplayEventBus eventBus;

    public TimerSystem(GameplayEventBus eventBus)
    {
        this.eventBus = eventBus;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TimerComponent timer, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State == ETaskState.Pending)
            {
                state.State = ETaskState.Running;
                // Interval <= 0：无意义（无限脉冲风暴）→ 防御性完成
                if (timer.Interval <= 0f)
                    TaskCommands.Complete(entity);
                return; // Pending 帧不累积
            }
            if (state.State != ETaskState.Running)
                return;

            timer.Elapsed += Tick.deltaTime;
            // 每帧至多 1 次脉冲——大 dt（暂停恢复/调试卡顿）不会一次入队几十个事件；残留累积到下一帧（不丢）
            if (timer.Elapsed >= timer.Interval)
            {
                timer.Elapsed -= timer.Interval;
                eventBus.Enqueue(new GameplayEventRecord { EventId = timer.PulseEventId });

                if (timer.RemainingPulses > 0)
                {
                    timer.RemainingPulses--;
                    if (timer.RemainingPulses <= 0)
                        TaskCommands.Complete(entity);
                }
            }
        });
    }
}
