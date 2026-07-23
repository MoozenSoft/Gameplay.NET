// src/Gameplay/GameplayTags/GameplayTagContainer.cs
using System.Collections;
using System.Collections.Generic;

namespace Gameplay.GameplayTags;

/// <summary>GameplayTag 容器，用于 Tag 条件匹配和授权。</summary>
public class GameplayTagContainer : IEnumerable<GameplayTag>
{
    internal GameplayTagSet tagSet;

    public int Count => tagSet.Count;

    public void AddTag(GameplayTag tag) => tagSet.Set(tag.id);

    public void Add(GameplayTag tag) => AddTag(tag);

    public bool HasAny(GameplayTagContainer other) => tagSet.HasAny(other.tagSet);

    public bool HasTag(GameplayTag tag) => tagSet.Has(tag.id);

    public void RemoveTag(GameplayTag tag) => tagSet.Clear(tag.id);

    public bool HasAll(GameplayTagContainer required) => tagSet.HasAll(required.tagSet);

    // ── 枚举（支持 collection initializer + foreach）──

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<GameplayTag> IEnumerable<GameplayTag>.GetEnumerator()
        => throw new System.NotSupportedException("使用 struct Enumerator 避免装箱");

    IEnumerator IEnumerable.GetEnumerator()
        => throw new System.NotSupportedException("使用 struct Enumerator 避免装箱");

    public struct Enumerator
    {
        private readonly GameplayTagContainer _container;
        private int _wordIndex;
        private int _bitIndex;

        internal Enumerator(GameplayTagContainer container)
        {
            _container = container;
            _wordIndex = 0;
            _bitIndex = -1;
        }

        public GameplayTag Current
        {
            get
            {
                int id = _wordIndex * 64 + _bitIndex;
                return new GameplayTag(id);
            }
        }

        public bool MoveNext()
        {
            var bits = _container.tagSet.Bits;
            _bitIndex++;
            while (_wordIndex < bits.Length)
            {
                long currentWord = bits[_wordIndex];
                while (_bitIndex < 64)
                {
                    if ((currentWord & (1L << _bitIndex)) != 0)
                        return true;
                    _bitIndex++;
                }
                _wordIndex++;
                _bitIndex = 0;
            }
            return false;
        }
    }
}
