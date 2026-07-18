using Xunit;

namespace Gameplay.Tests;

public class HealthComponentTests
{
    [Fact]
    public void CreateEntity_AddHealthComponent_CanReadAndModify()
    {
        // 创建 Standalone World
        var world = new World(NetMode.Standalone);
        Assert.Equal(NetMode.Standalone, world.GetNetMode());

        // 创建 Entity 并添加 HealthComponent
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Value = 100f });

        // 读取并验证初始值
        ref var health = ref entity.GetComponent<HealthComponent>();
        Assert.Equal(100f, health.Value);

        // 修改值（模拟伤害）
        health.Value -= 30f;
        Assert.Equal(70f, health.Value);
    }
}
