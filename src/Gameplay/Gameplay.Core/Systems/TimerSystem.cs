using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>计时递减，到期置 Completed。</summary>
public sealed class TimerSystem : QuerySystem<TimerComponent>
{
    private readonly ForEachEntity<TimerComponent> _forEach;   // 缓存委托，避免每帧 this-capturing lambda 分配

    public TimerSystem() => _forEach = ForEach;

    private void ForEach(ref TimerComponent timer, Entity _)
    {
        if (timer.Completed) return;
        timer.Remaining -= Tick.deltaTime;
        if (timer.Remaining <= 0f)
        {
            timer.Completed = true;
            if (timer.Loop)
            {
                // while 循环处理 dt > Duration 的多圈 wrap（避免 Remaining 仍为负）
                while (timer.Remaining <= 0f && timer.Duration > 0f)
                    timer.Remaining += timer.Duration;
                timer.Completed = false;
            }
        }
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(_forEach);
    }
}
