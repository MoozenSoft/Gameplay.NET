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
        world.AddSystem(new HealthSystem(world.Events), ESimulationStage.Simulation);

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 0f, Max = 100f, IsAlive = true });

        world.Update(0.16f);   // HealthSystem 标记死亡 → CommandBuffer 删除 → enqueue
        world.Update(0.16f);   // Events.Tick 分发上一帧 enqueue 的死亡事件

        Assert.True(entity.IsNull);   // 实体已删除
        Assert.Equal(1, deaths);      // 死亡事件已分发
    }

    private sealed class DeathCounter : IEventHandler<EntityDeathEvent>
    {
        private readonly System.Action _onDeath;
        public DeathCounter(System.Action onDeath) => _onDeath = onDeath;
        public void Handle(in EntityDeathEvent evt) => _onDeath();
    }
}
