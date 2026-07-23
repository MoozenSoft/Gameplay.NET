namespace Gameplay.Abilities;

/// <summary>
/// 静态 Event Handler 接口。通过 RegisterStatic 注册，每帧 Tick 中收到匹配事件时调用。
/// </summary>
public interface IGameplayEventHandler
{
    void Handle(in GameplayEventRecord record);
}
