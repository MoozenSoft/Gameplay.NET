namespace Gameplay.Core;

/// <summary>游戏世界模块——向 World 挂载 System/Manager。</summary>
public interface IModule
{
    void Build(World world);
}
