using Friflo.Engine.ECS;
using Gameplay;
using Gameplay.GameplayTags;
using Xunit;

namespace Gameplay.Tests.GameplayTags;

public class GameplayTagsTests
{
    private World CreateWorld()
    {
        GameplayTagManager.RegisterTags(
            "Damage",
            "Damage.Fire",
            "Damage.Fire.DoT",
            "Damage.Ice",
            "StatusEffect.Stunned",
            "Buff.Regeneration"
        );
        return new World(NetMode.Standalone);
    }

    [Fact]
    public void AddTag_HasTag_ReturnsTrue()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(fireTag);

        Assert.True(tags.HasTag(fireTag));
        Assert.Equal(1, tags.Count);
    }

    [Fact]
    public void RemoveTag_HasTag_ReturnsFalse()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(fireTag);
        tags.RemoveTag(fireTag);

        Assert.False(tags.HasTag(fireTag));
        Assert.Equal(0, tags.Count);
    }

    [Fact]
    public void EmptyKeepsComponent_AfterAllTagsRemoved()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(fireTag);
        tags.RemoveTag(fireTag);

        // 组件仍存在于 Entity 上
        Assert.True(entity.HasComponent<GameplayTagsComponent>());
    }

    [Fact]
    public void Matches_ParentTag_WhenEntityHasChildTag()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var doTTag = GameplayTag.Request("Damage.Fire.DoT");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(doTTag);

        // 层级匹配：DoT 是 Damage 的子孙
        var damageTag = GameplayTag.Request("Damage");
        Assert.True(tags.Matches(damageTag));

        // 层级匹配：DoT 是 Damage.Fire 的子孙
        var fireTag = GameplayTag.Request("Damage.Fire");
        Assert.True(tags.Matches(fireTag));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenNoMatch()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var regenTag = GameplayTag.Request("Buff.Regeneration");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(regenTag);

        // Buff.Regeneration 与 Damage 不相关
        var damageTag = GameplayTag.Request("Damage");
        Assert.False(tags.Matches(damageTag));
    }

    [Fact]
    public void HasTag_ExactMatch_DoesNotMatchParent()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());

        var doTTag = GameplayTag.Request("Damage.Fire.DoT");
        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(doTTag);

        // 精确匹配：Entity 没有 Damage 这个 Tag（只有 DoT）
        var damageTag = GameplayTag.Request("Damage");
        Assert.False(tags.HasTag(damageTag));
    }

    [Fact]
    public void MatchesAny_ReturnsTrue_WhenAnyOverlap()
    {
        var world = CreateWorld();
        var entity1 = world.Store.CreateEntity();
        entity1.AddComponent(new GameplayTagsComponent());
        ref var tags1 = ref entity1.GetComponent<GameplayTagsComponent>();
        tags1.AddTag(GameplayTag.Request("Damage.Fire"));
        tags1.AddTag(GameplayTag.Request("Buff.Regeneration"));

        var entity2 = world.Store.CreateEntity();
        entity2.AddComponent(new GameplayTagsComponent());
        ref var tags2 = ref entity2.GetComponent<GameplayTagsComponent>();
        tags2.AddTag(GameplayTag.Request("Damage.Ice"));

        // tags1 和 tags2 共享 Damage 祖先 → 都有 Damage.* 子标签
        Assert.True(tags1.MatchesAny(tags2));
    }

    [Fact]
    public void Query_EntityWithGameplayTags_IncludedInQueryResult()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTagsComponent());
        entity.AddComponent(new HealthComponent { Value = 100f });

        ref var tags = ref entity.GetComponent<GameplayTagsComponent>();
        tags.AddTag(GameplayTag.Request("StatusEffect.Stunned"));

        var query = world.Store.Query<GameplayTagsComponent, HealthComponent>();
        int count = 0;
        query.ForEachEntity((ref GameplayTagsComponent gameplayTags, ref HealthComponent health, Entity _) =>
        {
            count++;
            Assert.True(gameplayTags.Matches(GameplayTag.Request("StatusEffect.Stunned")));
            Assert.Equal(100f, health.Value);
        });
        Assert.Equal(1, count);
    }
}
