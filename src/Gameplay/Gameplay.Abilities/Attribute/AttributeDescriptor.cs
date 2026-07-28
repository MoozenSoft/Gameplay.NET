using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// Manager 内部存储的 GameplayAttribute 读写委托描述符。
/// 由 SG 通过 RegisterAttribute 注册，不暴露给外部。
/// </summary>
internal sealed class AttributeDescriptor
{
    internal delegate void ReadValue(Entity entity, out float value);
    internal delegate void WriteValue(Entity entity, float value);

    internal readonly ReadValue ReadBase;
    internal readonly ReadValue ReadCurrent;
    internal readonly WriteValue WriteCurrent;

    internal AttributeDescriptor(
        ReadValue readBase,
        ReadValue readCurrent,
        WriteValue writeCurrent)
    {
        ReadBase = readBase;
        ReadCurrent = readCurrent;
        WriteCurrent = writeCurrent;
    }
}
