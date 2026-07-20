using Xunit;

// GameplayTagManager 使用静态 mutable 状态，并行测试会导致竞态条件。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
