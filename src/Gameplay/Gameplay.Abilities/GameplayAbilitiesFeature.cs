using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Abilities;

/// <summary>
/// GAS 子系统的注册入口。接收已有 EntityStore，挂上 AttributeSystem 和 EffectSystem。
/// 不是 World 的包裹，只是 System 的注册入口。
/// </summary>
public class GameplayAbilitiesFeature
{
    public AttributeSystem AttributeSystem { get; }
    public EffectSystem EffectSystem { get; }
    public SystemRoot SystemRoot { get; }

    public GameplayAbilitiesFeature(EntityStore store, NetMode netMode)
    {
        AttributeSystem = new AttributeSystem();
        EffectSystem = new EffectSystem(AttributeSystem);
        SystemRoot = new SystemRoot(store)
        {
            EffectSystem,       // Phase 1: GE Apply/Remove + Tick
            AttributeSystem,    // Phase 2: Dirty → Evaluate
        };
    }

    public void Update(float deltaTime) => SystemRoot.Update(new UpdateTick(deltaTime, 0));
}
