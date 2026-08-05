using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>
/// 计时器能力——每 <see cref="Interval"/> 秒产生一次脉冲（向 GameplayEventBus 发 <see cref="PulseEventId"/> 事件），
/// <see cref="RemainingPulses"/> 次后 Task 完成（Done）。对应 UE 的 Repeat（底层能力）。<br/>
/// <see cref="RemainingPulses"/> 为 0 = 无限脉冲（永不完成）。
/// </summary>
public struct TimerComponent : IComponent
{
    /// <summary>脉冲间隔（秒）。</summary>
    public float Interval;

    /// <summary>当前间隔累计。</summary>
    public float Elapsed;

    /// <summary>剩余脉冲数（0 = 无限脉冲，永不完成）。</summary>
    public int RemainingPulses;

    /// <summary>每次脉冲发送的 GameplayEvent ID。</summary>
    public ushort PulseEventId;
}
