using System.Collections.Generic;
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

    [Fact]
    public void Equals_ComparesValue()
    {
        Assert.True(new NetworkId(1).Equals(new NetworkId(1)));
        Assert.False(new NetworkId(1).Equals(new NetworkId(2)));
        Assert.Equal(new NetworkId(1), new NetworkId(1));
        Assert.Equal(new NetworkId(1).GetHashCode(), new NetworkId(1).GetHashCode());
    }

    [Fact]
    public void IsUsableAsDictionaryKey_WithoutValueTypeBoxing()
    {
        var dict = new Dictionary<NetworkId, string> { [new NetworkId(7)] = "seven" };
        Assert.Equal("seven", dict[new NetworkId(7)]);

        var set = new HashSet<NetworkId> { new NetworkId(9) };
        Assert.Contains(new NetworkId(9), set);
    }
}
