using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>
/// 游戏世界，持有 ECS EntityStore 和网络模式信息。
/// </summary>
public class World
{
    private readonly EntityStore _store;

    /// <summary>当前网络模式。</summary>
    public ENetMode NetMode { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(ENetMode netMode)
    {
        NetMode = netMode;
        _store = new EntityStore();
    }

    /// <summary>返回当前网络模式。</summary>
    public ENetMode GetNetMode() => NetMode;

    /// <summary>Friflo ECS 实体存储。</summary>
    public EntityStore Store => _store;
}
