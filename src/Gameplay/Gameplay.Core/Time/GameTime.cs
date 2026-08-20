using System;

namespace Gameplay.Core;

/// <summary>模拟时钟——所有 System 的时间基准。</summary>
public sealed class GameTime
{
    /// <summary>本步（未缩放）步长。Variable 模式 = 渲染帧 dt；Fixed 模式 = <see cref="FixedDeltaTime"/>。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>时间缩放后的步长（受 <see cref="TimeScale"/> 与 <see cref="IsPaused"/> 影响）。</summary>
    public float ScaledDeltaTime { get; private set; }

    /// <summary>时间缩放（1 = 正常速度）。</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>暂停时 ScaledDeltaTime 恒为 0。</summary>
    public bool IsPaused { get; set; }

    /// <summary>已执行的步数（Variable = 帧数；Fixed = 子步数）。</summary>
    public long Tick { get; private set; }

    /// <summary>累计模拟时间（TimeScale 后，跨步累加）。</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>步长模式。</summary>
    public ETimeStep Mode { get; }

    private float fixedDeltaTime = 1f / 60f;

    /// <summary>固定步长（仅 Fixed 模式使用，默认 60 Hz）。非正值或 NaN 抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
    public float FixedDeltaTime
    {
        get => fixedDeltaTime;
        set => fixedDeltaTime = value > 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "FixedDeltaTime 必须为正数");
    }

    /// <summary>单帧最多子步数（防「螺旋死亡」：一帧耗时过长时丢弃溢出，模拟变慢而非雪崩）。</summary>
    public int MaxSubSteps { get; set; } = 8;

    /// <summary>未消费的真实时间（仅 Fixed 模式累积）。</summary>
    public float Accumulator { get; private set; }

    public GameTime(ETimeStep mode) => Mode = mode;

    /// <summary>推进一个模拟步（步长 deltaTime）。Variable 模式直接调用；Fixed 模式经 <see cref="TryConsumeFixedStep"/> 调用。</summary>
    public void Advance(float deltaTime)
    {
        DeltaTime = deltaTime;
        ScaledDeltaTime = IsPaused ? 0f : deltaTime * TimeScale;
        ElapsedTime += ScaledDeltaTime;
        Tick++;
    }

    /// <summary>喂入一帧真实时间。Fixed 模式：累加并 clamp 到 <see cref="FixedDeltaTime"/> * <see cref="MaxSubSteps"/>；Variable 模式直接推进一步。</summary>
    public void Feed(float deltaTime)
    {
        if (Mode == ETimeStep.Fixed)
        {
            float max = FixedDeltaTime * MaxSubSteps;
            Accumulator += deltaTime;
            if (Accumulator > max) Accumulator = max;
        }
        else
        {
            Advance(deltaTime);
        }
    }

    /// <summary>Fixed 模式：累计时间足以推进一个固定子步时消费并返回 true（scaledDeltaTime = 该子步缩放后步长）；不足返回 false。</summary>
    public bool TryConsumeFixedStep(out float scaledDeltaTime)
    {
        if (Accumulator < FixedDeltaTime)
        {
            scaledDeltaTime = 0f;
            return false;
        }
        Accumulator -= FixedDeltaTime;
        Advance(FixedDeltaTime);
        scaledDeltaTime = ScaledDeltaTime;
        return true;
    }
}
