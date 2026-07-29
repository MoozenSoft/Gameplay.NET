using Xunit;
using Gameplay.Abilities;

namespace Gameplay.Tests.Abilities;

/// <summary>
/// GameplayEventGenerator 的编译期行为测试。
/// Gameplay.dll 中定义 [GameplayEvent(Tag = "AttributeChanged.Changed")]，
/// SG 生成 EGameplayEventKind enum + GameplayEventRegistry + Frame/Bus partials。
/// </summary>
public class GameplayEventSGTests
{
    [Fact]
    public void EGameplayEventKind_AttributeChanged_HasCorrectValue()
    {
        Assert.Equal((ushort)1, (ushort)EGameplayEventKind.AttributeChanged);
    }

    [Fact]
    public void EGameplayEventKind_Unknown_IsZero()
    {
        Assert.Equal((ushort)0, (ushort)EGameplayEventKind.Unknown);
    }

    [Fact]
    public void GameplayEventRegistry_Tags_MapsAttributeChangedTag()
    {
        Assert.True(GameplayEventRegistry.Tags.TryGetValue(1, out var tag));
        Assert.Equal("Attribute.Changed", tag);
    }
}
