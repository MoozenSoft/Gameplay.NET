using System;
using Friflo.Engine.ECS;
using Gameplay;
using Gameplay.Tags;
using Xunit;

namespace Gameplay.Tests.Tags;

public class GameplayTagEdgeCaseTests
{
    [Fact]
    public void TagName_Trimmed_BeforeRegistration()
    {
        GameplayTagManager.RegisterTags("  Damage  ");
        var tag = GameplayTag.Request("Damage");
        Assert.True(tag.IsValid);
    }

    [Fact]
    public void TagName_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(""));
    }

    [Fact]
    public void TagName_StartsWithDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(".Damage"));
    }

    [Fact]
    public void TagName_EndsWithDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage."));
    }

    [Fact]
    public void TagName_DoubleDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage..Fire"));
    }

    [Fact]
    public void TagName_CaseSensitive()
    {
        GameplayTagManager.RegisterTags("Damage");
        var upper = GameplayTag.Request("Damage");
        var lower = GameplayTag.Request("damage");

        Assert.True(upper.IsValid);
        Assert.True(lower.IsValid); // 两个不同 tag，各自自动注册
        Assert.NotEqual(upper.ToString(), lower.ToString()); // 大小写敏感
    }

    [Fact]
    public void ToString_ReturnsFullName()
    {
        GameplayTagManager.RegisterTags("Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        Assert.Equal("Damage.Fire", fire.ToString());
    }

    [Fact]
    public void OperateOnMultipleEntities_EachHasOwnTagSet()
    {
        GameplayTagManager.RegisterTags("Damage", "Buff");
        var world = new World(NetMode.Standalone);

        var entityA = world.Store.CreateEntity();
        var entityB = world.Store.CreateEntity();

        entityA.AddComponent(new GameplayTagsComponent());
        entityB.AddComponent(new GameplayTagsComponent());

        ref var tagsA = ref entityA.GetComponent<GameplayTagsComponent>();
        ref var tagsB = ref entityB.GetComponent<GameplayTagsComponent>();

        tagsA.AddTag(GameplayTag.Request("Damage"));
        tagsB.AddTag(GameplayTag.Request("Buff"));

        Assert.True(tagsA.HasTag(GameplayTag.Request("Damage")));
        Assert.False(tagsA.HasTag(GameplayTag.Request("Buff")));
        Assert.False(tagsB.HasTag(GameplayTag.Request("Damage")));
        Assert.True(tagsB.HasTag(GameplayTag.Request("Buff")));
    }

    [Fact]
    public void RegisterTags_Incremental_Works()
    {
        // 先注册一批
        GameplayTagManager.RegisterTags("Damage");
        var dmg1 = GameplayTag.Request("Damage");
        Assert.True(dmg1.IsValid);

        // 再注册新的一批（增量）
        GameplayTagManager.RegisterTags("Damage.Fire", "Damage.Ice");
        var fire = GameplayTag.Request("Damage.Fire");
        var ice  = GameplayTag.Request("Damage.Ice");

        Assert.True(fire.IsValid);
        Assert.True(ice.IsValid);
        Assert.True(fire.Matches(dmg1));
        Assert.True(ice.Matches(dmg1));
    }
}
