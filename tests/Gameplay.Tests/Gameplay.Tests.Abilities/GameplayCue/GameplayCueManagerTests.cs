// tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayCue/GameplayCueManagerTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class GameplayCueManagerTests
{
    static GameplayCueManagerTests()
    {
        GameplayTagManager.RegisterTags(
            "GameplayCue.Static.Test", "GameplayCue.Burst.Test",
            "GameplayCue.Static.Param", "GameplayCue.Conflict.Test",
            "GameplayCue.Looping.Test", "GameplayCue.Looping.Remove",
            "GameplayCue.Looping.Nonexistent", "GameplayCue.Looping.A",
            "GameplayCue.Looping.B", "GameplayCue.Looping.Multi",
            "GameplayCue.Static.NoTrack", "GameplayCue.Burst.NoTrack");
    }
    [Fact]
    public void RegisterStatic_AddCue_InvokesHandler()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Static.Test");
        var tag = GameplayTag.Request("GameplayCue.Static.Test");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var instigator = store.CreateEntity();
        var target = store.CreateEntity();

        bool invoked = false;
        manager.RegisterStatic(tag, _ => invoked = true);

        var parameters = new GameplayCueParameters { Instigator = instigator, NormalizedMagnitude = 1.0f };
        manager.AddCue(tag, parameters, target);

        Assert.True(invoked);
    }

    [Fact]
    public void RegisterBurst_AddCue_InvokesHandler()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Burst.Test");
        var tag = GameplayTag.Request("GameplayCue.Burst.Test");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var instigator = store.CreateEntity();
        var target = store.CreateEntity();

        bool invoked = false;
        manager.RegisterBurst(tag, _ => invoked = true);

        var parameters = new GameplayCueParameters { Instigator = instigator, NormalizedMagnitude = 0.5f };
        manager.AddCue(tag, parameters, target);

        Assert.True(invoked);
    }

    [Fact]
    public void RegisterStatic_AddCue_PassesParameters()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Static.Param");
        var tag = GameplayTag.Request("GameplayCue.Static.Param");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var instigator = store.CreateEntity();
        var target = store.CreateEntity();

        GameplayCueParameters? captured = null;
        manager.RegisterStatic(tag, p => captured = p);

        var parameters = new GameplayCueParameters { Instigator = instigator, NormalizedMagnitude = 0.75f };
        manager.AddCue(tag, parameters, target);

        Assert.NotNull(captured);
        Assert.Equal(0.75f, captured.Value.NormalizedMagnitude);
        Assert.Equal(instigator.Id, captured.Value.Instigator.Id);
    }

    [Fact]
    public void AddCue_StaticPreferredOverBurst()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Conflict.Test");
        var tag = GameplayTag.Request("GameplayCue.Conflict.Test");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        int callOrder = 0;
        int staticCalled = 0;
        int burstCalled = 0;
        manager.RegisterStatic(tag, _ => { staticCalled = ++callOrder; });
        manager.RegisterBurst(tag, _ => { burstCalled = ++callOrder; });

        manager.AddCue(tag, default, target);

        Assert.Equal(1, staticCalled);
        Assert.Equal(0, burstCalled); // Burst should NOT be called when static exists
    }

    [Fact]
    public void AddCue_UnknownTag_TracksAsLoopingCue()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Looping.Test");
        var tag = GameplayTag.Request("GameplayCue.Looping.Test");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        manager.AddCue(tag, default, target);

        // Verify it's tracked internally
        Assert.True(manager.HasActiveLoopingCue(target, tag));
    }

    [Fact]
    public void RemoveCue_RemovesTracking()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Looping.Remove");
        var tag = GameplayTag.Request("GameplayCue.Looping.Remove");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        manager.AddCue(tag, default, target);
        Assert.True(manager.HasActiveLoopingCue(target, tag));

        manager.RemoveCue(tag, target);
        Assert.False(manager.HasActiveLoopingCue(target, tag));
    }

    [Fact]
    public void RemoveCue_NotTracked_DoesNotThrow()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Looping.Nonexistent");
        var tag = GameplayTag.Request("GameplayCue.Looping.Nonexistent");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        // Should not throw
        manager.RemoveCue(tag, target);
    }

    [Fact]
    public void RemoveAllCues_RemovesAllForTarget()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Looping.A", "GameplayCue.Looping.B");
        var tagA = GameplayTag.Request("GameplayCue.Looping.A");
        var tagB = GameplayTag.Request("GameplayCue.Looping.B");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        manager.AddCue(tagA, default, target);
        manager.AddCue(tagB, default, target);

        manager.RemoveAllCues(target);

        Assert.False(manager.HasActiveLoopingCue(target, tagA));
        Assert.False(manager.HasActiveLoopingCue(target, tagB));
    }

    [Fact]
    public void RemoveAllCues_UnknownEntity_DoesNotThrow()
    {
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        // Should not throw
        manager.RemoveAllCues(target);
    }

    [Fact]
    public void AddCue_Looping_MultipleEntitiesTrackedIndependently()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Looping.Multi");
        var tag = GameplayTag.Request("GameplayCue.Looping.Multi");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var targetA = store.CreateEntity();
        var targetB = store.CreateEntity();

        manager.AddCue(tag, default, targetA);
        manager.AddCue(tag, default, targetB);

        Assert.True(manager.HasActiveLoopingCue(targetA, tag));
        Assert.True(manager.HasActiveLoopingCue(targetB, tag));

        manager.RemoveCue(tag, targetA);

        Assert.False(manager.HasActiveLoopingCue(targetA, tag));
        Assert.True(manager.HasActiveLoopingCue(targetB, tag));
    }

    [Fact]
    public void AddCue_Static_DoesNotTrackAsLooping()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Static.NoTrack");
        var tag = GameplayTag.Request("GameplayCue.Static.NoTrack");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        manager.RegisterStatic(tag, _ => { });

        manager.AddCue(tag, default, target);

        Assert.False(manager.HasActiveLoopingCue(target, tag));
    }

    [Fact]
    public void AddCue_Burst_DoesNotTrackAsLooping()
    {
        GameplayTagManager.RegisterTags("GameplayCue.Burst.NoTrack");
        var tag = GameplayTag.Request("GameplayCue.Burst.NoTrack");
        var manager = new GameplayCueManager();
        var store = new EntityStore();
        var target = store.CreateEntity();

        manager.RegisterBurst(tag, _ => { });

        manager.AddCue(tag, default, target);

        Assert.False(manager.HasActiveLoopingCue(target, tag));
    }
}
