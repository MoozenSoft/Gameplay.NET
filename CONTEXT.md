# Gameplay.NET

游戏玩法类库，基于 ECS + GAS + 状态同步，产出 `Gameplay.dll`。

## Language

**GameplayTag**:
一组层级化的标签标识符，用点分隔（如 `"Damage.Fire"`）。用于分类实体、匹配条件、过滤查询。注册时分配全局唯一 int id。
_Avoid_: Tag, label, flag

**GameplayTags**:
附加到 Entity 上的 `IComponent`，内部用位集存储该实体拥有的所有 GameplayTag。是 GameplayTag 在 ECS 中的运行时载体。
_Avoid_: TagComponent, TagSet

**层级匹配（Hierarchical Matching）**:
查询子标签时自动匹配所有父标签。例如 Entity 有 `Damage.Fire.DoT`，`Matches(Damage)` 返回 true。通过预计算展开集（自身 + 所有子孙 Tag ID）实现。
_Avoid_: Tag matching, parent matching

**展开集（Expanded Set）**:
每个 GameplayTag 在注册完成后预计算的位集，包含该 Tag 自身及其所有子孙 Tag 的 ID。查询时直接做位与，不用递归。
_Avoid_: Descendant set, child set

**RegisterTags**:
唯一写入 GameplayTag 的入口。接受层级 Tag 名字符串数组，自动创建缺失的父节点。通常在游戏启动时调用一次。
_Avoid_: AddTag, CreateTag

**RequestTag**:
只读查询已注册的 GameplayTag。不存在则返回 `GameplayTag.Invalid`。永不创建新 Tag。
_Avoid_: GetTag, FindTag
