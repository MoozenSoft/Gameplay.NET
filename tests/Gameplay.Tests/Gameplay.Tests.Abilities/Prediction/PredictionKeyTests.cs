// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Prediction/PredictionKeyTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class PredictionKeyTests
{
    [Fact]
    public void Default_IsInvalid()
    {
        var key = default(PredictionKey);
        Assert.False(key.IsValid);
    }

    [Fact]
    public void Invalid_Static_ReturnsInvalid()
    {
        var key = PredictionKey.Invalid;
        Assert.False(key.IsValid);
        Assert.Equal(0, key.Key);
    }

    [Fact]
    public void KeyGreaterThanZero_IsValid()
    {
        var key = new PredictionKey { Key = 1 };
        Assert.True(key.IsValid);
    }

    [Fact]
    public void KeyZero_IsInvalid()
    {
        var key = new PredictionKey { Key = 0 };
        Assert.False(key.IsValid);
    }

    [Fact]
    public void KeyNegative_IsInvalid()
    {
        var key = new PredictionKey { Key = -1 };
        Assert.False(key.IsValid);
    }

    [Fact]
    public void IPredictionService_Begin_ReturnsPredictionKey()
    {
        var service = new TestPredictionService();
        var key = service.Begin();
        Assert.True(key.IsValid);
    }

    [Fact]
    public void IPredictionService_Confirm_ReceivesKey()
    {
        var service = new TestPredictionService();
        var key = service.Begin();
        service.Confirm(key);
        Assert.True(service.LastConfirmed.Equals(key));
    }

    [Fact]
    public void IPredictionService_Reject_ReceivesKey()
    {
        var service = new TestPredictionService();
        var key = service.Begin();
        service.Reject(key);
        Assert.True(service.LastRejected.Equals(key));
    }

    [Fact]
    public void PredictionSystem_Confirm_DelegatesToService()
    {
        var service = new TestPredictionService();
        var system = new PredictionSystem();
        system.SetService(service);

        var key = service.Begin();
        system.Confirm(key);
        Assert.True(service.LastConfirmed.Equals(key));
    }

    [Fact]
    public void PredictionSystem_Reject_DelegatesToService()
    {
        var service = new TestPredictionService();
        var system = new PredictionSystem();
        system.SetService(service);

        var key = service.Begin();
        system.Reject(key);
        Assert.True(service.LastRejected.Equals(key));
    }

    [Fact]
    public void PredictionSystem_NoServiceSet_ConfirmDoesNotThrow()
    {
        var system = new PredictionSystem();
        var ex = Record.Exception(() => system.Confirm(new PredictionKey { Key = 1 }));
        Assert.Null(ex);
    }

    [Fact]
    public void PredictionSystem_NoServiceSet_RejectDoesNotThrow()
    {
        var system = new PredictionSystem();
        var ex = Record.Exception(() => system.Reject(new PredictionKey { Key = 1 }));
        Assert.Null(ex);
    }

    /// <summary>Test double for IPredictionService.</summary>
    private class TestPredictionService : IPredictionService
    {
        private int nextKey = 1;

        public PredictionKey LastConfirmed { get; private set; }
        public PredictionKey LastRejected { get; private set; }

        public PredictionKey Begin()
        {
            return new PredictionKey { Key = nextKey++ };
        }

        public void Confirm(PredictionKey key)
        {
            LastConfirmed = key;
        }

        public void Reject(PredictionKey key)
        {
            LastRejected = key;
        }
    }
}
