using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class NetworkIdTests
{
    [Fact]
    public void Default_IsInvalid()
    {
        Assert.False(default(NetworkId).IsValid);
    }

    [Fact]
    public void PositiveValue_IsValid()
    {
        Assert.True(new NetworkId(1).IsValid);
        Assert.Equal(42, new NetworkId(42).Value);
    }
}
