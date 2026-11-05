/// <summary>
/// 抽象输入动作。单动作 = 单个位标志，32 个上限。
/// 项目扩展：追加枚举值（1u << n）+ 在 InputMap 加一行绑定。
/// </summary>
public enum InputAction : uint
{
    None       = 0,
    Up         = 1u << 0,
    Down       = 1u << 1,
    Left       = 1u << 2,
    Right      = 1u << 3,
    Confirm    = 1u << 4,
    Cancel     = 1u << 5,
    Menu       = 1u << 6,
    AddScore   = 1u << 8,   // 加分
    DownScore  = 1u << 9,   // 扣分
}

/// <summary>动作事件类型（订阅与分发使用）。Down/Up 之外的四项都由 Up 或按住时长派生</summary>
public enum KeyEvent
{
    Down,        // 本帧刚按下（边沿）
    Up,          // 本帧刚抬起（边沿）
    Click,       // 非长按时抬起，即一次完整短按
    DoubleClick, // 落在双击间隔内的第二次 Click
    LongOnce,    // 按住达到 longPressTime，一次按住只触发一次
    LongMulti,   // 长按之后按 repeatInterval 连发
}