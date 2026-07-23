using Xunit;
using Friflo.Engine.ECS;
using Gameplay.Abilities;

namespace Gameplay.Tests.Abilities;

/// <summary>
/// GameplayAttributeGenerator 的编译期行为测试。
/// 测试项目中定义 partial struct + [GameplayAttribute] 字段，
/// SG 自动生成 Get{Name} 静态方法，测试直接调用验证。
/// </summary>
public class AttributeSGTests
{
    [Fact]
    public void GeneratedAccessor_GetsCorrectField()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new TestAttributeSet
        {
            Health = new GameplayAttributeData { BaseValue = 100f }
        });

        // 此方法由 GameplayAttributeGenerator 生成
        ref var health = ref TestAttributeSet.GetHealth(entity);
        Assert.Equal(100f, health.BaseValue, 0.001f);
    }

    [Fact]
    public void GeneratedAccessor_ModifiesField()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new TestAttributeSet
        {
            Health = new GameplayAttributeData { BaseValue = 50f }
        });

        ref var health = ref TestAttributeSet.GetHealth(entity);
        health.BaseValue = 200f;

        // 重新读取验证修改生效
        ref var health2 = ref TestAttributeSet.GetHealth(entity);
        Assert.Equal(200f, health2.BaseValue, 0.001f);
    }

    [Fact]
    public void GeneratedAccessor_MultipleFields()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new MultiAttributeSet
        {
            Strength = new GameplayAttributeData { BaseValue = 10f },
            Agility = new GameplayAttributeData { BaseValue = 20f },
            Intelligence = new GameplayAttributeData { BaseValue = 30f }
        });

        ref var str = ref MultiAttributeSet.GetStrength(entity);
        ref var agi = ref MultiAttributeSet.GetAgility(entity);
        ref var intel = ref MultiAttributeSet.GetIntelligence(entity);

        Assert.Equal(10f, str.BaseValue, 0.001f);
        Assert.Equal(20f, agi.BaseValue, 0.001f);
        Assert.Equal(30f, intel.BaseValue, 0.001f);
    }
}

/// <summary>单字段测试 AttributeSet。</summary>
public partial struct TestAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute]
    public GameplayAttributeData Health;
}

/// <summary>多字段测试 AttributeSet。</summary>
public partial struct MultiAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute]
    public GameplayAttributeData Strength;

    [GameplayAttribute]
    public GameplayAttributeData Agility;

    [GameplayAttribute]
    public GameplayAttributeData Intelligence;
}
