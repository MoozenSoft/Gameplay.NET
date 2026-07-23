namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class StructBufferTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var buf = new StructBuffer<int>();
        buf.Add(10);
        buf.Add(20);
        Assert.Equal(2, buf.Count);
    }

    [Fact]
    public void GetRef_ReturnsCorrectValue()
    {
        var buf = new StructBuffer<float>();
        int idx = buf.Add(3.14f);
        Assert.Equal(3.14f, buf.GetRef(idx), 0.001f);
    }

    [Fact]
    public void Reset_ClearsCount()
    {
        var buf = new StructBuffer<int>();
        buf.Add(1);
        buf.Add(2);
        buf.Reset();
        Assert.Equal(0, buf.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_Grows()
    {
        var buf = new StructBuffer<int>();
        for (int i = 0; i < 200; i++)
            buf.Add(i);
        Assert.Equal(200, buf.Count);
        Assert.Equal(150, buf.GetRef(150));
    }
}
