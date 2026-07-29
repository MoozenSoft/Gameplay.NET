namespace Gameplay.Abilities;

public sealed partial class GameplayEventFrame
{
    public StructBuffer<GameplayEventRecord> Records = new();

    partial void ResetPayloads();

    public void Reset()
    {
        Records.Reset();
        ResetPayloads();
    }
}
