using UnityEngine;

/// <summary>
/// 输入源抽象。Unity KeyCode 本身覆盖键盘/鼠标(Mouse0-6)/手柄(JoystickButton0-19)，
/// 触摸由 InputBinding.touchGesture 在 InputManager 层单独处理。
///
/// player 是玩家下标（0 起）。带玩家概念的源（如 KeyControllerSource，IO 板按槽位灌位）
/// 按 player 各查各的；键盘/鼠标/手柄/触摸这类共享物理设备没有玩家归属，
/// 实现上忽略 player、对所有玩家返回相同结果。
/// 需要每玩家独立键位的场景走 KeyController.KeySet(int no, ...)，它本身就是按玩家映射的。
/// </summary>
public interface IInputSource
{
    /// <summary>某玩家视角下，某物理键当前是否按住（只处理与本源相关的 KeyCode，其余返回 false）</summary>
    bool GetKey(int player, KeyCode code);
    /// <summary>当前平台/环境该源是否可用（不可用的源整帧跳过）</summary>
    bool IsAvailable();
}
