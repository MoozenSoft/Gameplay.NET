namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

/// <summary>
/// AttributeChangedEvent 端到端测试：
/// Manager.Flush → Bus.Enqueue → Dispatcher.Tick → Handler 读取记录
/// </summary>
public class AttributeChangedEventIntegrationTests
{
    [Fact]
    public void Flush_AttributeChanged_EnqueuesEvent()
    {
        // Arrange
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var mgr = new AttributeAggregatorManager();
        mgr.SetEventBus(bus);

        var entity = store.CreateEntity();
        entity.AddComponent(new E2ETestAttrSet { Health = new() { BaseValue = 100f } });

        // 注册 attribute
        mgr.RegisterAttribute(new GameplayAttribute(1),
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.BaseValue; },
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<E2ETestAttrSet>().Health.CurrentValue = v; });

        var attr = new GameplayAttribute(1);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);

        // Act: Flush → 应发布 AttributeChangedEvent
        mgr.Flush();

        // Assert: Event 在 Bus 的 pending 帧中
        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);

        ref var record = ref frame.Records.GetRef(0);
        Assert.Equal((ushort)EGameplayEventKind.AttributeChanged, record.EventId);
        Assert.Equal(entity, record.Target);
        Assert.True(record.PayloadIndex >= 0);

        // 读取 payload
        ref var payload = ref frame.GetAttributeChangedEvent(record.PayloadIndex);
        Assert.Equal(attr, payload.Attribute);
        Assert.Equal(0f, payload.OldValue);
        Assert.Equal(120f, payload.NewValue);
    }

    [Fact]
    public void Flush_NoChange_NoEventPublished()
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var mgr = new AttributeAggregatorManager();
        mgr.SetEventBus(bus);

        var entity = store.CreateEntity();
        entity.AddComponent(new E2ETestAttrSet { Health = new() { BaseValue = 100f, CurrentValue = 100f } });

        mgr.RegisterAttribute(new GameplayAttribute(1),
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.BaseValue; },
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<E2ETestAttrSet>().Health.CurrentValue = v; });

        // 仅设置 BaseValue 不改变 CurrentValue（Flush 后 Old=Current=100, New=100）
        var attr = new GameplayAttribute(1);
        mgr.SetBaseValue(entity, attr, 100f);

        // Act
        mgr.Flush();

        // Assert: 值未变 → 不发布事件
        var frame = bus.Swap();
        Assert.Equal(0, frame.Records.Count);
    }

    [Fact]
    public void Dispatcher_Tick_HandlerReadsPayload()
    {
        var bus = new GameplayEventBus();
        var dispatcher = new GameplayEventDispatcher(bus);

        var handler = new RecordCapturingHandler();
        dispatcher.RegisterStatic((ushort)EGameplayEventKind.AttributeChanged, handler);

        // Enqueue via Bus（模拟 Manager.Flush）
        bus.Enqueue(new AttributeChangedEvent
        {
            Target    = default,
            Attribute = new GameplayAttribute(5),
            OldValue  = 10f,
            NewValue  = 30f,
        }, source: default, target: default);

        // Act
        dispatcher.Tick();

        // Assert: Handler 收到事件
        Assert.True(handler.WasCalled);
        Assert.NotNull(handler.CapturedRecord);
        Assert.Equal((ushort)EGameplayEventKind.AttributeChanged, handler.CapturedRecord!.Value.EventId);

        // Frame 被重置
        var frame = bus.Swap();
        Assert.Equal(0, frame.Records.Count);
    }

    [Fact]
    public void FullPipeline_ManagerFlush_DispatcherDispatch_HandlerReceives()
    {
        // Arrange
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var dispatcher = new GameplayEventDispatcher(bus);
        var mgr = new AttributeAggregatorManager();
        mgr.SetEventBus(bus);

        var handler = new RecordCapturingHandler();
        dispatcher.RegisterStatic((ushort)EGameplayEventKind.AttributeChanged, handler);

        var entity = store.CreateEntity();
        entity.AddComponent(new E2ETestAttrSet { Health = new() { BaseValue = 100f } });

        mgr.RegisterAttribute(new GameplayAttribute(1),
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.BaseValue; },
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<E2ETestAttrSet>().Health.CurrentValue = v; });

        mgr.AddAggregatorMod(entity, new GameplayAttribute(1), new GameplayEffectHandle(1), 50f, EGameplayModOp.Additive);

        // Act: Flush → Event 进入 pending
        mgr.Flush();

        // 此时 event 在 pending 帧，Handler 还没收到
        Assert.False(handler.WasCalled);

        // Dispatcher.Tick() → 分发事件
        dispatcher.Tick();

        // Assert: Handler 收到了事件
        Assert.True(handler.WasCalled);
        Assert.Equal((ushort)EGameplayEventKind.AttributeChanged, handler.CapturedRecord!.Value.EventId);
        Assert.True(handler.CapturedRecord!.Value.PayloadIndex >= 0);
    }

    [Fact]
    public void Flush_WithoutEventBus_DoesNotThrow()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        // 不调用 SetEventBus

        var entity = store.CreateEntity();
        entity.AddComponent(new E2ETestAttrSet { Health = new() { BaseValue = 100f } });

        mgr.RegisterAttribute(new GameplayAttribute(1),
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.BaseValue; },
            (Entity e, out float v) => { v = e.GetComponent<E2ETestAttrSet>().Health.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<E2ETestAttrSet>().Health.CurrentValue = v; });

        mgr.AddAggregatorMod(entity, new GameplayAttribute(1), new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);

        // 没有 EventBus → 不抛异常，仅不发布事件
        mgr.Flush();
    }

    // ── 测试辅助 ──

    private class RecordCapturingHandler : IGameplayEventHandler
    {
        public bool WasCalled;
        public GameplayEventRecord? CapturedRecord;

        public void Handle(in GameplayEventRecord record)
        {
            WasCalled = true;
            CapturedRecord = record;
        }
    }

    private struct E2ETestAttrSet : IAttributeSetComponent
    {
        public GameplayAttributeData Health;
    }
}
