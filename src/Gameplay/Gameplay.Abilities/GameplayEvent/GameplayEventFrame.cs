namespace Gameplay.Abilities;

public class GameplayEventFrame
{
    public StructBuffer<GameplayEventRecord> Records = new();
    public void Reset() => Records.Reset();
}
