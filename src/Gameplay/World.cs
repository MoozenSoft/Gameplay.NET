using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>
/// 游戏世界，持有 ECS EntityStore 和网络模式信息。
/// </summary>
public class World
{
    private readonly EntityStore _store;

    /// <summary>当前网络模式。</summary>
    public NetMode NetMode { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(NetMode netMode)
    {
        NetMode = netMode;
        _store = new EntityStore();
    }

    /// <summary>返回当前网络模式。</summary>
    public NetMode GetNetMode() => NetMode;

    /// <summary>Friflo ECS 实体存储。第一版直接暴露，后续封装。</summary>
    public EntityStore Store => _store;
}
