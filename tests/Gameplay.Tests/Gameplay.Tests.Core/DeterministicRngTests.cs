using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRng(42UL);
        var b = new DeterministicRng(42UL);
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var a = new DeterministicRng(1UL);
        var b = new DeterministicRng(2UL);
        Assert.NotEqual(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void NextFloat_IsInUnitInterval()
    {
        var rng = new DeterministicRng(7UL);
        for (int i = 0; i < 1000; i++)
        {
            var f = rng.NextFloat();
            Assert.InRange(f, 0f, 1f);
        }
    }

    [Fact]
    public void Fork_ProducesIndependentStream()
    {
        var rng = new DeterministicRng(42UL);
        var fork = rng.Fork(1);
        Assert.NotEqual(rng.NextUInt(), fork.NextUInt());
    }
}
