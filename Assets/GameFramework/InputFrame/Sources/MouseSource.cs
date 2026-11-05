using UnityEngine;

/// <summary>
/// 鼠标输入源。Unity 把鼠标按键编进 KeyCode.Mouse0~Mouse6，
/// 这里减去 Mouse0 换算成 Input.GetMouseButton 需要的 0~6 下标。
///
/// 鼠标是一块共享物理设备，没有玩家归属：忽略 player，对所有玩家返回相同结果。
/// </summary>
public class MouseSource : IInputSource
{
    /// <summary>编辑器与真机恒可用</summary>
    public bool IsAvailable() => true;

    /// <summary>非鼠标键码直接返回 false，只认领 Mouse0~Mouse6。player 被忽略（共享设备）</summary>
    public bool GetKey(int player, KeyCode code)
    {
        if (!KeyboardSource.IsMouseCode(code)) return false;
        return Input.GetMouseButton((int)code - (int)KeyCode.Mouse0);
    }
}
