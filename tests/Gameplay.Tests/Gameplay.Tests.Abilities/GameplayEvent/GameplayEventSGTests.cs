using Xunit;
using Gameplay.Abilities;

namespace Gameplay.Tests.Abilities;

/// <summary>
/// GameplayEventGenerator 的编译期行为测试。
/// 测试项目中定义 partial struct + [GameplayEvent(Tag = "...")]，
/// SG 自动生成 EGameplayEventKind enum + GameplayEventRegistry，
/// 测试直接引用验证。
/// </summary>
public class GameplayEventSGTests
{
    [Fact]
    public void EGameplayEventKind_Damage_HasCorrectValue()
    {
        // [GameplayEvent(Tag = "Event.Damage")] 应生成 Damage = 1
        Assert.Equal((ushort)1, (ushort)EGameplayEventKind.Damage);
    }

    [Fact]
    public void EGameplayEventKind_Unknown_IsZero()
    {
        Assert.Equal((ushort)0, (ushort)EGameplayEventKind.Unknown);
    }

    [Fact]
    public void GameplayEventRegistry_Tags_MapsDamageTag()
    {
        Assert.True(GameplayEventRegistry.Tags.TryGetValue(1, out var tag));
        Assert.Equal("Event.Damage", tag);
    }

    [Fact]
    public void GameplayEventRegistry_Tags_HasExactCount()
    {
        // 只有一个测试 struct，所以 Tags 应有 1 个条目
        Assert.Single(GameplayEventRegistry.Tags);
    }
}

/// <summary>单事件测试 struct。SG 扫描此 struct 的 [GameplayEvent] attribute。</summary>
[GameplayEvent(Tag = "Event.Damage")]
public partial struct DamageEvent
{
    public float Amount;
}
