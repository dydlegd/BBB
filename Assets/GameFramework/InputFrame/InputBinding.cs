using UnityEngine;

/// <summary>一个动作到物理按键的绑定（可跨输入源）</summary>
public class InputBinding
{
    /// <summary>目标动作，必须是 InputAction 中的单个位标志</summary>
    public InputAction action;
    /// <summary>触发该动作的物理键，任意一个按住即视为该动作被按住</summary>
    public KeyCode[] keys = new KeyCode[0];
    /// <summary>是否绑定"任意触摸按住"</summary>
    public bool touchGesture;
    /// <summary>按住多久算长按（秒），达到即触发 LongOnce 并进入连发</summary>
    public float longPressTime = 0.8f;
    /// <summary>长按连发的重复间隔（秒）</summary>
    public float repeatInterval = 0.075f;
}
