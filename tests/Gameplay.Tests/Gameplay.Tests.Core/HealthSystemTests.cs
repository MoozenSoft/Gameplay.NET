using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class HealthSystemTests
{
    [Fact]
    public void Update_ZeroHealth_MarksDead_AndEnqueuesDeath()
    {
        var world = new World(ENetMode.Standalone);
        var deaths = 0;
        world.Events.Subscribe<EntityDeathEvent>(new DeathCounter(() => deaths++));
        world.AddSystem(new HealthSystem(world.Events, world.DeferDelete), ESimulationStage.Simulation);

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 0f, Max = 100f, IsAlive = true });

        world.Update(0.16f);   // 死亡事件本帧分发（World 双 Tick），实体帧末删除

        Assert.True(entity.IsNull);   // 实体已删除
        Assert.Equal(1, deaths);      // 死亡事件已分发（本帧）
    }

    [Fact]
    public void EntityWithHealthAndLifetimeBothExpired_DoesNotCrash()
    {
        // 同一实体同时满足 HealthSystem 与 LifetimeSystem 的删除条件 → DeferDelete 去重，不崩溃（#1）
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new HealthSystem(world.Events, world.DeferDelete), ESimulationStage.Simulation);
        world.AddSystem(new LifetimeSystem(world.DeferDelete), ESimulationStage.Simulation);

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 0f, Max = 100f, IsAlive = true });
        entity.AddComponent(new LifetimeComponent { Remaining = -1f });

        world.Update(0.16f);   // 两个 System 都 DeferDelete 同一实体 → HashSet 去重，只删一次

        Assert.True(world.Store.GetEntityById(entity.Id).IsNull);
    }

    private sealed class DeathCounter : IEventHandler<EntityDeathEvent>
    {
        private readonly System.Action _onDeath;
        public DeathCounter(System.Action onDeath) => _onDeath = onDeath;
        public void Handle(in EntityDeathEvent evt) => _onDeath();
    }
}
