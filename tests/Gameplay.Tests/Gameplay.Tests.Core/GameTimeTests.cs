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
}
