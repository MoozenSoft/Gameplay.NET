using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayCue 表现管理器。
/// Static/Burst Cue 走 POCO 消息通道（立即执行 handler），Looping Cue 走 Entity 生命周期跟踪。
/// </summary>
public class GameplayCueManager
{
    private readonly Dictionary<GameplayTag, Action<GameplayCueParameters>> staticHandlers = new();
    private readonly Dictionary<GameplayTag, Action<GameplayCueParameters>> burstHandlers = new();

    // Internal for test access (InternalsVisibleTo)
    internal readonly Dictionary<Entity, List<GameplayTag>> activeLoopingCues = new();

    /// <summary>注册 Static GameplayCue handler。Static Cue 在 AddCue 时立即执行。</summary>
    public void RegisterStatic(GameplayTag tag, Action<GameplayCueParameters> handler)
    {
        staticHandlers[tag] = handler;
    }

    /// <summary>注册 Burst GameplayCue handler。Burst Cue 在 AddCue 时立即执行。</summary>
    public void RegisterBurst(GameplayTag tag, Action<GameplayCueParameters> handler)
    {
        burstHandlers[tag] = handler;
    }

    /// <summary>
    /// 添加 GameplayCue。
    /// Static/Burst 优先：存在已注册 handler 则立即执行，不跟踪生命周期。
    /// 否则视为 Looping Cue，跟踪到目标 Entity 上，供后续 RemoveCue / RemoveAllCues 使用。
    /// </summary>
    public void AddCue(GameplayTag tag, GameplayCueParameters parameters, Entity target)
    {
        if (staticHandlers.TryGetValue(tag, out var staticHandler))
        {
            staticHandler.Invoke(parameters);
            return;
        }

        if (burstHandlers.TryGetValue(tag, out var burstHandler))
        {
            burstHandler.Invoke(parameters);
            return;
        }

        // Looping Cue: track on target Entity
        if (!activeLoopingCues.TryGetValue(target, out var tags))
        {
            tags = new List<GameplayTag>();
            activeLoopingCues[target] = tags;
        }

        if (!tags.Contains(tag))
        {
            tags.Add(tag);
        }
    }

    /// <summary>移除目标 Entity 上的指定 Looping Cue。</summary>
    public void RemoveCue(GameplayTag tag, Entity target)
    {
        if (!activeLoopingCues.TryGetValue(target, out var tags))
            return;

        tags.Remove(tag);
        if (tags.Count == 0)
        {
            activeLoopingCues.Remove(target);
        }
    }

    /// <summary>移除目标 Entity 上的所有 Looping Cue。</summary>
    public void RemoveAllCues(Entity target)
    {
        activeLoopingCues.Remove(target);
    }

    // Internal helper for testing
    internal bool HasActiveLoopingCue(Entity target, GameplayTag tag)
    {
        return activeLoopingCues.TryGetValue(target, out var tags) && tags.Contains(tag);
    }
}
