// tests/Gameplay.Tests/GameplayAbilities/Attribute/GameplayAttributeDataTests.cs
using Xunit;
using Gameplay.GameplayAbilities;

namespace Gameplay.Tests.GameplayAbilities;

public class GameplayAttributeDataTests
{
    [Fact]
    public void Default_BaseAndCurrent_AreZero()
    {
        var data = new GameplayAttributeData();
        Assert.Equal(0f, data.BaseValue);
        Assert.Equal(0f, data.CurrentValue);
    }

    [Fact]
    public void SetBaseValue_CurrentValueUnchanged()
    {
        var data = new GameplayAttributeData { BaseValue = 100f, CurrentValue = 80f };
        data.BaseValue = 120f;
        Assert.Equal(120f, data.BaseValue);
        Assert.Equal(80f, data.CurrentValue); // Current 不联动，等待 Evaluator
    }
}
