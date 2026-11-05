using UnityEngine;

/// <summary>
/// 键盘输入源。Unity 把键盘/鼠标/手柄编在同一个 KeyCode 枚举里，
/// 所以每个源都先按码段过滤、只认领属于自己的键，其余一律返回 false，避免重复计数。
///
/// 键盘是一块共享物理设备，没有玩家归属：忽略 player，对所有玩家返回相同结果。
/// </summary>
public class KeyboardSource : IInputSource
{
    /// <summary>编辑器与真机恒可用</summary>
    public bool IsAvailable() => true;

    /// <summary>非鼠标/非手柄的键码才交给 Input.GetKey。player 被忽略（共享设备）</summary>
    public bool GetKey(int player, KeyCode code)
    {
        if (IsMouseCode(code) || IsJoystickCode(code)) return false;
        return Input.GetKey(code);
    }

    /// <summary>KeyCode.Mouse0~Mouse6 码段判定（MouseSource 复用）</summary>
    internal static bool IsMouseCode(KeyCode code) =>
        code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6;

    /// <summary>KeyCode.JoystickButton0~19 码段判定（GamepadSource 复用）</summary>
    internal static bool IsJoystickCode(KeyCode code) =>
        (code >= KeyCode.JoystickButton0 && code <= KeyCode.JoystickButton19);
}
