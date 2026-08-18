namespace Gameplay.Core;

/// <summary>事件标记接口（EventBus 泛型约束）。</summary>
public interface IEvent { }

/// <summary>事件处理器。</summary>
public interface IEventHandler<T> where T : struct, IEvent
{
    void Handle(in T evt);
}
