namespace Gameplay.Interfaces;

/// <summary>
/// 输入服务抽象——Input Service（非 ECS）采集设备输入并实现此接口，ECS System 只读查询。<br/>
/// 对应架构"Input 边界"：Input Service 是 ECS 与非 ECS 的桥梁，System 不碰设备。<br/>
/// 无输入环境（如 Dedicated Server）不注入服务——Input 类 Task 保持 Running。
/// </summary>
public interface IInputService
{
    /// <summary>本帧内是否按下了指定动作（上升沿）。</summary>
    bool WasPressedThisFrame(int actionId);

    /// <summary>本帧内是否释放了指定动作（下降沿）。</summary>
    bool WasReleasedThisFrame(int actionId);

    /// <summary>当前是否按住。</summary>
    bool IsHeld(int actionId);
}
