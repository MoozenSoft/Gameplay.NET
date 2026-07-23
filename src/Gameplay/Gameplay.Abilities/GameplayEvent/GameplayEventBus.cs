namespace Gameplay.Abilities;

public class GameplayEventBus
{
    private GameplayEventFrame current = new();
    private GameplayEventFrame pending = new();

    public void Enqueue(in GameplayEventRecord record) => pending.Records.Add(record);
    public GameplayEventFrame Swap() { (current, pending) = (pending, current); return current; }
}
