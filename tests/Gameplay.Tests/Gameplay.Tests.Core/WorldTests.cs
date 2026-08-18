using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class WorldTests
{
    private sealed class TestSystem : QuerySystem<HealthComponent>
    {
        public int RunCount;
        protected override void OnUpdate() => RunCount++;
    }

    private sealed class TestModule : IModule
    {
        public readonly TestSystem System = new();
        public void Build(World world) => world.AddSystem(System, ESimulationStage.Simulation);
    }

    [Fact]
    public void AddModule_InvokesBuild()
    {
        var world = new World(ENetMode.Standalone);
        var module = new TestModule();
        world.AddModule(module);
        Assert.NotNull(module.System);
    }

    [Fact]
    public void AddModuleGeneric_CreatesAndBuilds()
    {
        var world = new World(ENetMode.Standalone);
        world.AddModule<TestModule>();
        // 不抛异常即通过
    }

    [Fact]
    public void RegisterAndGetService_Roundtrips()
    {
        var world = new World(ENetMode.Standalone);
        var svc = new object();
        world.RegisterService(svc);
        Assert.Same(svc, world.GetService<object>());
    }

    [Fact]
    public void DeferDelete_DeletesOnUpdate()
    {
        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        world.DeferDelete(entity);
        world.Update(0.16f);
        Assert.True(world.Store.GetEntityById(entity.Id).IsNull);
    }
}
