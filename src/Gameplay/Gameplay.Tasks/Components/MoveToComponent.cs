using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>
/// 移动能力（Action 类）——Duration 插值模型（对齐 UE5 AbilityTask_MoveToLocation）：<br/>
/// 在 <see cref="Duration"/> 秒内将 <see cref="Target"/> 从 <see cref="StartLocation"/> 线性插值到 <see cref="Destination"/>，完成后 Task 结束（Done）。<br/>
/// <see cref="StartLocation"/> 由 MoveToSystem 在 Pending 帧快照（类似 UE5 Activate 时捕获）。
/// </summary>
public struct MoveToComponent : IComponent
{
    /// <summary>要移动的实体（需挂 Position）。</summary>
    public Entity Target;

    /// <summary>开始位置（Pending 帧快照，仅供插值起点）。</summary>
    public Position StartLocation;

    /// <summary>目标位置。</summary>
    public Position Destination;

    /// <summary>移动时长（秒）。&lt;= 0 表示立即完成。</summary>
    public float Duration;

    /// <summary>已移动时间。</summary>
    public float Elapsed;
}
