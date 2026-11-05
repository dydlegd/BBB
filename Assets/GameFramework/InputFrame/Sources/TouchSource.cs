using UnityEngine;

/// <summary>
/// 触摸输入源。触摸没有对应的 KeyCode，因此 GetKey 恒返回 false、不参与按键轮询；
/// 触摸要参与绑定需走 InputBinding.touchGesture，由 InputManager 调 IsTouching() 处理。
///
/// 触摸屏是一块共享物理设备，没有玩家归属：player 被忽略。
/// </summary>
public class TouchSource : IInputSource
{
    /// <summary>仅触屏设备可用，桌面与编辑器下整源跳过</summary>
    public bool IsAvailable() => Input.touchSupported;

    /// <summary>触摸不走 KeyCode，由 touchGesture 绑定处理</summary>
    public bool GetKey(int player, KeyCode code) => false;

    /// <summary>任意手指按住屏幕（InputManager 翻译 touchGesture 绑定时调用）</summary>
    public static bool IsTouching() => Input.touchCount > 0;
}
