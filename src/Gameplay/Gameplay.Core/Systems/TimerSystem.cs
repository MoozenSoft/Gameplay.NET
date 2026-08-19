using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>计时递减，到期置 Completed。</summary>
public sealed class TimerSystem : QuerySystem<TimerComponent>
{
    private readonly ForEachEntity<TimerComponent> forEach;   // 缓存委托，避免每帧 this-capturing lambda 分配

    public TimerSystem() => forEach = ForEach;

    private void ForEach(ref TimerComponent timer, Entity _)
    {
        if (timer.Completed)
        {
            // 已完成：Loop 且 Duration>0 时，下一圈从这里开始重置（Completed 已保持一帧供消费方观察）
            if (timer.Loop && timer.Duration > 0f)
            {
                // 多圈 wrap：把超时量折入下一圈，避免 Remaining 仍为负（一次 dt 可能跨多圈）
                while (timer.Remaining <= 0f && timer.Duration > 0f)
                    timer.Remaining += timer.Duration;
                timer.Completed = false;
            }
            return;   // 非 Loop 或 Duration<=0：保持 Completed，不再处理
        }

        timer.Remaining -= Tick.deltaTime;
        if (timer.Remaining <= 0f)
        {
            timer.Completed = true;   // 保持一帧，供消费方观察「一圈完成」；下一圈下一帧重置
        }
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(forEach);
    }
}
