namespace Gameplay.Abilities;

/// <summary>预测键，标识一次预测操作。</summary>
public struct PredictionKey
{
    public int Key;

    public bool IsValid => Key > 0;

    public static PredictionKey Invalid => default;
}
