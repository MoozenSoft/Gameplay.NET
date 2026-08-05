using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Tasks;

/// <summary>
/// 移动能力 Driver（Action 类）——Duration 插值模型（对齐 UE5 AbilityTask_MoveToLocation）：<br/>
/// Pending 帧快照 <see cref="MoveToComponent.StartLocation"/>（UE5 Activate 时捕获），
/// 每帧 <c>Lerp(Start, Destination, Elapsed/Duration)</c>，时长结束精确落点并完成（Done）。<br/>
/// Duration &lt;= 0：立即落点完成。目标无效/无 Position：防御性完成。
/// </summary>
public class MoveToSystem : QuerySystem<MoveToComponent, TaskStateComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref MoveToComponent move, ref TaskStateComponent state, Entity entity) =>
        {
            // Pending → Running：快照 StartLocation（注册即开始，与 DelaySystem 一致）
            if (state.State == ETaskState.Pending)
            {
                state.State = ETaskState.Running;
                var pendingTarget = move.Target;
                if (pendingTarget.IsNull || !pendingTarget.HasComponent<Position>())
                {
                    TaskCommands.Complete(entity); // 防御：目标无效/无 Position，无法移动
                    return;
                }
                move.StartLocation = pendingTarget.GetComponent<Position>();
            }
            else if (state.State != ETaskState.Running)
                return;

            var target = move.Target;
            if (target.IsNull || !target.HasComponent<Position>())
            {
                TaskCommands.Complete(entity);
                return;
            }

            // Duration <= 0：立即完成，直接落点
            if (move.Duration <= 0f)
            {
                ref var posImmediate = ref target.GetComponent<Position>();
                posImmediate.value = move.Destination.value;
                TaskCommands.Complete(entity);
                return;
            }

            move.Elapsed += Tick.deltaTime;
            float alpha = Math.Min(move.Elapsed / move.Duration, 1f);
            ref var pos = ref target.GetComponent<Position>();
            pos.value = Vector3.Lerp(move.StartLocation.value, move.Destination.value, alpha);

            if (alpha >= 1f)
                TaskCommands.Complete(entity);
        });
    }
}
