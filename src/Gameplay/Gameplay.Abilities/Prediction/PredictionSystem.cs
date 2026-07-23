namespace Gameplay.Abilities;

/// <summary>
/// 预测回滚 System。
/// Confirm: 找到所有带该 PredictionKey 的 Entity → 标记 Confirmed。
/// Reject: 销毁预测 Entity + Aggregator 回滚。
/// </summary>
public class PredictionSystem
{
    private IPredictionService? service;

    public void SetService(IPredictionService svc) => service = svc;

    public void Confirm(PredictionKey key)
    {
        // 找到所有带该 PredictionKey 的 Entity → 标记 Confirmed
        service?.Confirm(key);
    }

    public void Reject(PredictionKey key)
    {
        // 销毁预测 Entity + Aggregator 回滚
        service?.Reject(key);
    }
}
