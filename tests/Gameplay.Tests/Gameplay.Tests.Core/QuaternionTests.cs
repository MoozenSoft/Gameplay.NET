using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class QuaternionTests
{
    [Fact]
    public void Identity_HasW1ZeroXYZ()
    {
        var q = Quaternion.Identity;
        Assert.Equal(0f, q.X);
        Assert.Equal(0f, q.Y);
        Assert.Equal(0f, q.Z);
        Assert.Equal(1f, q.W);
    }

    [Fact]
    public void Equals_SameComponents_True()
    {
        var a = new Quaternion(1f, 2f, 3f, 4f);
        var b = new Quaternion(1f, 2f, 3f, 4f);
        Assert.True(a.Equals(b));
    }
}
