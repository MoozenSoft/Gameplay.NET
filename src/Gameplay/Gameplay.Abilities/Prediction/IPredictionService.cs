namespace Gameplay.Abilities;

/// <summary>预测服务接口，由网络层实现。</summary>
public interface IPredictionService
{
    PredictionKey Begin();
    void Confirm(PredictionKey key);
    void Reject(PredictionKey key);
}
