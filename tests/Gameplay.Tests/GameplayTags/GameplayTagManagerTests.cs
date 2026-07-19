using System;
using Xunit;

namespace Gameplay.Tests;

public class GameplayTagManagerTests
{
    [Fact]
    public void RegisterTags_CreatesHierarchy()
    {
        // Build 应由首次 RequestTag 自动触发
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");

        var fireTag = GameplayTag.Request("Damage.Fire");
        Assert.True(fireTag.IsValid);

        var damageTag = GameplayTag.Request("Damage");
        Assert.True(damageTag.IsValid);

        // Damage.Fire 是 Damage 的子孙
        Assert.True(fireTag.Matches(damageTag));
    }

    [Fact]
    public void RegisterTags_AutoCreatesParentNodes()
    {
        // 只注册叶子节点，父节点自动创建
        GameplayTagManager.RegisterTags("A.B.C");

        var aTag = GameplayTag.Request("A");
        var bTag = GameplayTag.Request("A.B");
        var cTag = GameplayTag.Request("A.B.C");

        Assert.True(aTag.IsValid);
        Assert.True(bTag.IsValid);
        Assert.True(cTag.IsValid);
        Assert.True(cTag.Matches(aTag));
    }

    [Fact]
    public void RegisterTags_DuplicateIsIdempotent()
    {
        GameplayTagManager.RegisterTags("Damage");
        var tag1 = GameplayTag.Request("Damage");
        GameplayTagManager.RegisterTags("Damage");
        var tag2 = GameplayTag.Request("Damage");
        Assert.Equal(tag1, tag2);
    }

    [Fact]
    public void RequestTag_ReturnsInvalid_WhenNotRegistered()
    {
        // RequestTag 不创建——必须返回 Invalid
        var tag = GameplayTag.Request("Not.Exists");
        Assert.False(tag.IsValid);
    }

    [Fact]
    public void Matches_ParentMatchesChild()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        var damage = GameplayTag.Request("Damage");
        Assert.True(fire.Matches(damage));
    }

    [Fact]
    public void Matches_ChildDoesNotMatchParent()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        var damage = GameplayTag.Request("Damage");
        Assert.False(damage.Matches(fire));
    }

    [Fact]
    public void MatchesExact_SameIdReturnsTrue()
    {
        GameplayTagManager.RegisterTags("Damage");
        var a = GameplayTag.Request("Damage");
        var b = GameplayTag.Request("Damage");
        Assert.True(a.MatchesExact(b));
    }

    [Fact]
    public void Matches_SelfMatchesSelf()
    {
        GameplayTagManager.RegisterTags("Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        Assert.True(fire.Matches(fire));
    }

    [Fact]
    public void RequestTag_ReturnsSameIdForSameName()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var a = GameplayTag.Request("Damage.Fire");
        var b = GameplayTag.Request("Damage.Fire");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void InvalidTag_HasIdZero_AndIsNotValid()
    {
        var invalid = default(GameplayTag);
        Assert.False(invalid.IsValid);
        Assert.Equal("Invalid", invalid.ToString());
    }

    // ---- 验证层负向测试 ----

    [Fact]
    public void RegisterTags_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(""));
    }

    [Fact]
    public void RegisterTags_LeadingDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(".Damage"));
    }

    [Fact]
    public void RegisterTags_TrailingDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage."));
    }

    [Fact]
    public void RegisterTags_DoubleDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage..Fire"));
    }
}
