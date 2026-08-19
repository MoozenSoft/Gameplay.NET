using System;
using System.Collections.Generic;

namespace Gameplay.Core;

/// <summary>Core 通用事件总线（双缓冲 + Tick 分发）。事件低频，接受装箱。</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, object> queues = new();
    private IEventQueue[] snapshot = Array.Empty<IEventQueue>();

    public void Enqueue<T>(in T evt) where T : struct, IEvent
        => GetQueue<T>().Pending.Add(evt);

    public void Subscribe<T>(IEventHandler<T> handler) where T : struct, IEvent
        => GetQueue<T>().Handlers.Add(handler);

    public void Unsubscribe<T>(IEventHandler<T> handler) where T : struct, IEvent
        => GetQueue<T>().Handlers.Remove(handler);

    /// <summary>每帧分发：先快照队列再逐个派发，分发中 Enqueue 新类型不破坏迭代。</summary>
    public void Tick()
    {
        int count = queues.Count;
        if (count == 0) return;
        if (snapshot.Length < count)
            snapshot = new IEventQueue[count];
        queues.Values.CopyTo(snapshot, 0);
        for (int i = 0; i < count; i++)
            snapshot[i].Dispatch();
    }

    private EventQueue<T> GetQueue<T>() where T : struct, IEvent
    {
        if (queues.TryGetValue(typeof(T), out var box))
            return (EventQueue<T>)box;
        var queue = new EventQueue<T>();
        queues[typeof(T)] = queue;
        return queue;
    }

    private interface IEventQueue
    {
        void Dispatch();
    }

    private sealed class EventQueue<T> : IEventQueue where T : struct, IEvent
    {
        public readonly List<T> Pending = new();
        public readonly List<T> Processing = new();
        public readonly List<IEventHandler<T>> Handlers = new();

        private readonly List<IEventHandler<T>> snapshot = new();

        public void Dispatch()
        {
            if (Pending.Count == 0) return;
            // swap：处理本帧之前入队的事件，分发中再 Enqueue 落入下一帧
            Processing.AddRange(Pending);
            Pending.Clear();
            snapshot.Clear();
            snapshot.AddRange(Handlers);   // 快照，分发中 Subscribe/Unsubscribe 不影响本次迭代
            try
            {
                for (int i = 0; i < Processing.Count; i++)
                {
                    T evt = Processing[i];
                    for (int h = 0; h < snapshot.Count; h++)
                        snapshot[h].Handle(in evt);
                }
            }
            finally
            {
                // handler 抛异常也清空 Processing，避免残留 stale 事件下次误重发
                Processing.Clear();
            }
        }
    }
}
