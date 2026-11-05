using UnityEngine;

/// <summary>动作→物理键 静态映射表（代码配置，不支持运行时改绑）</summary>
public static class InputMap
{
    /// <summary>
    /// 全量绑定表，InputManager 每帧遍历它把物理键翻译成动作掩码。
    /// 同一个 KeyCode 绑到多个动作是合法的（一次按下同时驱动两个动作），
    /// InputManager 启动时会打日志提醒，便于确认是否预期。
    /// </summary>
    public static readonly InputBinding[] Bindings =
    {
        new InputBinding { action = InputAction.Up,      keys = new[] { KeyCode.UpArrow } },
        new InputBinding { action = InputAction.Down,    keys = new[] { KeyCode.DownArrow } },
        new InputBinding { action = InputAction.Left,    keys = new[] { KeyCode.LeftArrow } },
        new InputBinding { action = InputAction.Right,   keys = new[] { KeyCode.RightArrow } },
        new InputBinding { action = InputAction.Confirm, keys = new[] { KeyCode.KeypadEnter, KeyCode.Mouse0 } },
        new InputBinding { action = InputAction.Cancel,  keys = new[] { KeyCode.Escape } },
        new InputBinding { action = InputAction.Menu,  keys = new[] { KeyCode.P } },
        new InputBinding { action = InputAction.AddScore,   keys = new[] { KeyCode.KeypadPlus } },
        new InputBinding { action = InputAction.DownScore, keys = new[] { KeyCode.KeypadMinus } },
    };
}