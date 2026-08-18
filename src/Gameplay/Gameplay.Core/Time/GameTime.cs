namespace Gameplay.Core;

/// <summary>模拟时钟——所有 System 的时间基准。</summary>
public sealed class GameTime
{
    /// <summary>本帧（未缩放）步长。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>时间缩放后的步长（受 <see cref="TimeScale"/> 与 <see cref="IsPaused"/> 影响）。</summary>
    public float ScaledDeltaTime { get; private set; }

    /// <summary>时间缩放（1 = 正常速度）。</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>暂停时 ScaledDeltaTime 恒为 0。</summary>
    public bool IsPaused { get; set; }

    /// <summary>递增帧号。</summary>
    public long Tick { get; private set; }

    /// <summary>步长模式。</summary>
    public ETimeStep Mode { get; }

    public GameTime(ETimeStep mode) => Mode = mode;

    /// <summary>推进一帧。</summary>
    public void Advance(float deltaTime)
    {
        DeltaTime = deltaTime;
        ScaledDeltaTime = IsPaused ? 0f : deltaTime * TimeScale;
        Tick++;
    }
}
