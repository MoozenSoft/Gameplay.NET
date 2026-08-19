using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class GameTimeTests
{
    [Fact]
    public void Advance_UpdatesDeltaTimeAndTick()
    {
        var time = new GameTime(ETimeStep.Variable);
        time.Advance(0.16f);
        Assert.Equal(0.16f, time.DeltaTime);
        Assert.Equal(1, time.Tick);
    }

    [Fact]
    public void TimeScale_ScalesScaledDeltaTime()
    {
        var time = new GameTime(ETimeStep.Variable) { TimeScale = 0.5f };
        time.Advance(0.16f);
        Assert.Equal(0.16f, time.DeltaTime);
        Assert.Equal(0.08f, time.ScaledDeltaTime, 4);
    }

    [Fact]
    public void IsPaused_ZeroScaledDeltaTime()
    {
        var time = new GameTime(ETimeStep.Variable) { IsPaused = true };
        time.Advance(0.16f);
        Assert.Equal(0f, time.ScaledDeltaTime);
    }

    [Fact]
    public void Fixed_Feed_ConsumesSubSteps()
    {
        var time = new GameTime(ETimeStep.Fixed);
        float fixedDt = time.FixedDeltaTime;
        time.Feed(fixedDt * 2f);

        Assert.True(time.TryConsumeFixedStep(out var s1));
        Assert.True(time.TryConsumeFixedStep(out var s2));
        Assert.False(time.TryConsumeFixedStep(out _));

        Assert.Equal(fixedDt, s1, 4);
        Assert.Equal(fixedDt, s2, 4);
        Assert.Equal(2, time.Tick);
    }

    [Fact]
    public void Fixed_Feed_BelowStep_DoesNotConsume()
    {
        var time = new GameTime(ETimeStep.Fixed);
        time.Feed(time.FixedDeltaTime * 0.5f);

        Assert.False(time.TryConsumeFixedStep(out _));
        Assert.Equal(0, time.Tick);
    }

    [Fact]
    public void Fixed_AccumulatorClampsToMaxSubSteps()
    {
        var time = new GameTime(ETimeStep.Fixed);
        time.Feed(1000f);   // 远超 MaxSubSteps，应被 clamp 到 FixedDeltaTime * MaxSubSteps

        int steps = 0;
        while (time.TryConsumeFixedStep(out _)) steps++;

        Assert.Equal(time.MaxSubSteps, steps);
    }

    [Fact]
    public void Fixed_Paused_ConsumesButZeroScaledDeltaTime()
    {
        var time = new GameTime(ETimeStep.Fixed) { IsPaused = true };
        time.Feed(time.FixedDeltaTime);

        Assert.True(time.TryConsumeFixedStep(out var scaled));
        Assert.Equal(0f, scaled);
        Assert.Equal(1, time.Tick);
    }
}
