using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 旧 KeyController 框架的输入源适配器，是 InputManager 里唯一带玩家概念的源。
/// 与 KeyControllerCenter.GlobalKeyController 共用同一个实例，
/// 因此外部写 g_player_keybuf[player] 的位能直接驱动对应玩家的动作。
///
/// 三条触发路径，对 InputManager 而言是 OR 关系：
///   1. 每玩家键盘路径 —— Bind(player, ...) 调 KeySet 配 remap_code，各玩家独立映射
///   2. 默认键盘路径   —— 两参 Bind(...) 等价于 Bind(0, ...)
///   3. 外部路径       —— BindExternal(...) 只登记反查表，由外部写 g_player_keybuf 置位
///
/// 注意：KeyController 的默认键位映射（KeySet 调用）在旧代码中全部被注释，
/// 因此构造时必须显式配置映射，否则读出的掩码恒为 0。
/// </summary>
public class KeyControllerSource : IInputSource
{
    /// <summary>每个玩家各自的 KeyCode → AppKeyCode 反查表</summary>
    readonly Dictionary<int, Dictionary<KeyCode, uint>> codeToAppKeyByPlayer =
        new Dictionary<int, Dictionary<KeyCode, uint>>();
    int lastDriveFrame = -1;

    /// <summary>
    /// 底层 KeyController，外部灌按键位的入口（g_player_keybuf[player]）。
    /// 刻意每次现取而不缓存字段：EnsureInit 扩容时会重建实例，缓存会让扩容失效。
    /// </summary>
    public KeyController Controller => KeyControllerCenter.GlobalKeyController;

    /// <param name="playerCount">需要的玩家槽位数，透传给 KeyControllerCenter.EnsureInit</param>
    public KeyControllerSource(int playerCount = 1)
    {
        KeyControllerCenter.EnsureInit(playerCount);

        // 显式配置一套默认映射（可按项目需要扩充）
        Bind(KeyCode.Return, AppKeyCode.KeyCode_OK);
        Bind(KeyCode.Escape, AppKeyCode.KeyCode_Cancel);
    }

    /// <summary>为 0 号玩家配置映射，等价于 Bind(0, code, appKey)</summary>
    public void Bind(KeyCode code, AppKeyCode appKey) => Bind(0, code, appKey);

    /// <summary>完整映射：登记该玩家的反查表 + 配置 KeyController 的键盘映射（走键盘路径）</summary>
    public void Bind(int player, KeyCode code, AppKeyCode appKey)
    {
        var controller = Controller;
        if (controller == null) return;
        if (player < 0 || player >= controller.PLAYER_MAX)
        {
            Debug.LogWarning($"[KeyControllerSource] Bind 失败：玩家 {player} 超出槽位上限 {controller.PLAYER_MAX}");
            return;
        }
        controller.KeySet(player, (uint)appKey, code);
        Map(player)[code] = (uint)appKey;
    }

    /// <summary>为 0 号玩家登记外部映射</summary>
    public void BindExternal(KeyCode code, AppKeyCode appKey) => BindExternal(0, code, appKey);

    /// <summary>
    /// 仅登记反查表，不配置键盘映射。用于按键不来自 Unity 键盘的场景
    /// （串口 / IO 板 / 网络），由外部写 g_player_keybuf[player] 置位触发。
    /// </summary>
    public void BindExternal(int player, KeyCode code, AppKeyCode appKey)
    {
        Map(player)[code] = (uint)appKey;
    }

    Dictionary<KeyCode, uint> Map(int player)
    {
        if (!codeToAppKeyByPlayer.TryGetValue(player, out var map))
        {
            map = new Dictionary<KeyCode, uint>();
            codeToAppKeyByPlayer[player] = map;
        }
        return map;
    }

    public bool IsAvailable() => true;

    public bool GetKey(int player, KeyCode code)
    {
        if (!codeToAppKeyByPlayer.TryGetValue(player, out var map)) return false;
        if (!map.TryGetValue(code, out uint appKey)) return false;

        var controller = Controller;
        if (controller == null) return false;

        // 每帧最多驱动一次 KeyController.Update（一帧内会被多个绑定重复查询）
        if (Time.frameCount != lastDriveFrame)
        {
            lastDriveFrame = Time.frameCount;
            controller.Update(Time.deltaTime);
        }
        return controller.GetKey(player, appKey);
    }
}
