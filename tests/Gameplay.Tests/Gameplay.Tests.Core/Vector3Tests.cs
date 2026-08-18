using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class Vector3Tests
{
    [Fact]
    public void Add_ReturnsComponentWiseSum()
    {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);
        var c = a + b;
        Assert.Equal(5f, c.X);
        Assert.Equal(7f, c.Y);
        Assert.Equal(9f, c.Z);
    }

    [Fact]
    public void Scale_MultipliesEachComponent()
    {
        var a = new Vector3(1f, 2f, 3f);
        var c = a * 2f;
        Assert.Equal(2f, c.X);
        Assert.Equal(4f, c.Y);
        Assert.Equal(6f, c.Z);
    }

    [Fact]
    public void Dot_ReturnsScalarProduct()
    {
        var a = new Vector3(1f, 0f, 0f);
        var b = new Vector3(0f, 1f, 0f);
        Assert.Equal(0f, Vector3.Dot(in a, in b));
    }

    [Fact]
    public void Normalized_HasUnitLength()
    {
        var a = new Vector3(3f, 0f, 0f);
        var n = a.Normalized();
        Assert.Equal(1f, n.X, 4);
        Assert.Equal(0f, n.Y, 4);
    }
}
