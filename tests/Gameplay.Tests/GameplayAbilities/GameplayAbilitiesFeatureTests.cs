// tests/Gameplay.Tests/GameplayAbilities/GameplayAbilitiesFeatureTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay;
using Gameplay.GameplayAbilities;
using Xunit;

public class GameplayAbilitiesFeatureTests
{
    [Fact]
    public void Constructor_RegistersSystems()
    {
        var world = new World(NetMode.Standalone);
        var feature = new GameplayAbilitiesFeature(world.Store, world.NetMode);

        Assert.NotNull(feature.EffectSystem);
        Assert.NotNull(feature.AttributeSystem);
        Assert.NotNull(feature.SystemRoot);
    }

    [Fact]
    public void Update_ExecutesSystems()
    {
        var store = new EntityStore();
        var feature = new GameplayAbilitiesFeature(store, NetMode.Standalone);

        // 创建一个带 DirtyAttributeComponent 的 Entity
        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        feature.AttributeSystem.SetAggregatorValue(entity, attributeId: 0, baseValue: 100f);
        feature.AttributeSystem.AddAggregatorMod(entity, 0, handle: 1, magnitude: 20f, EGameplayModOp.Additive);
        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(0);

        // Update 不抛异常即通过
        feature.Update(0.016f);

        // Dirty bit 被 AttributeSystem 处理并清除
        ref var finalDirty = ref entity.GetComponent<DirtyAttributeComponent>();
        Assert.False(finalDirty.HasBit(0));
    }
}
