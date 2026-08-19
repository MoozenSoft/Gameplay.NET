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
        public TestModule(World world) => world.AddSystem(System, ESimulationStage.Simulation);
    }

    [Fact]
    public void AddModule_RegistersModule()
    {
        var world = new World(ENetMode.Standalone);
        var module = new TestModule(world);
        world.AddModule(module);
        // 模块构造时已通过构造函数挂载 System，注册不抛异常即通过
        Assert.NotNull(module.System);
    }

    [Fact]
    public void AddModule_ModuleSystemRunsOnUpdate()
    {
        var world = new World(ENetMode.Standalone);
        var module = new TestModule(world);
        world.AddModule(module);

        world.Update(0.16f);

        // 构造函数已把 System 挂到 Simulation 阶段，Update 应驱动其执行
        Assert.Equal(1, module.System.RunCount);
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
