using UnityEngine;

/// <summary>
/// 手柄输入源。只认 KeyCode.JoystickButton0~19；
/// 摇杆与扳机属于轴而非按键，不在 KeyCode 覆盖范围内，需要时得另写一个轴输入源。
///
/// 手柄是一块共享物理设备，没有玩家归属：忽略 player，对所有玩家返回相同结果。
/// 需要按手柄区分玩家时应改用 Unity 的 Input.GetJoystickNames() 下标另建输入源。
/// </summary>
public class GamepadSource : IInputSource
{
    int lastCheckFrame = -1;
    bool available;

    /// <summary>
    /// 有任意手柄连接才可用。Input.GetJoystickNames() 每调一次就新分配一个字符串数组，
    /// 因此限制为每帧最多查一次并缓存结果，避免在轮询路径上产生 GC。
    /// </summary>
    public bool IsAvailable()
    {
        if (Time.frameCount != lastCheckFrame)
        {
            lastCheckFrame = Time.frameCount;
            available = Input.GetJoystickNames().Length > 0;
        }
        return available;
    }

    /// <summary>非手柄键码直接返回 false，只认领 JoystickButton0~19。player 被忽略（共享设备）</summary>
    public bool GetKey(int player, KeyCode code)
    {
        if (!IsAvailable()) return false;
        if (!KeyboardSource.IsJoystickCode(code)) return false;
        return Input.GetKey(code);
    }
}
