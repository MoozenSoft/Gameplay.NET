using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

public struct GameplayEventRecord
{
    public ushort EventId;
    public Entity Source;
    public Entity Target;
    public float Magnitude;
    public int PayloadIndex;
}
