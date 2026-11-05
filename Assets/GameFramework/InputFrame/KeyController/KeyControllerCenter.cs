using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按键对应表。所有条目必须是互不相同的单个位标志（1u << n），
/// 因为 KeyController 用 GetKeyIndex() 把值换算成数组下标，重复或非位标志会串位。
/// 上限 32 个，与 APP_KEY_MAX / g_key_buf 的 uint 宽度一致。
/// </summary>
public enum AppKeyCode : uint
{
    Key_Null = 0,

    // 方向与确认（bit 0-2）
    KeyCode_Up      = 1u << 0,
    KeyCode_Down    = 1u << 1,
    KeyCode_OK      = 1u << 2,

    // 取消（bit 3）
    KeyCode_Cancel  = 1u << 3,

    // 手柄 / VR（bit 4-10）
    KeyCode_Menu     = 1u << 4,
    KeyCode_Trigger  = 1u << 5,
    KeyCode_Grip     = 1u << 6,
    KeyCode_Joystick = 1u << 7,
    KeyCode_A        = 1u << 8,
    KeyCode_B        = 1u << 9,
    KeyCode_Home     = 1u << 10,

    // 全局按键（bit 11）
    KeyCode_停急 = 1u << 11,

    // 玩家按键（bit 12-17）
    KeyCode_Player_安全带 = 1u << 12,
    KeyCode_Player_射击前 = 1u << 13,
    KeyCode_Player_射击上 = 1u << 14,
    KeyCode_Player_射击左 = 1u << 15,
    KeyCode_Player_射击右 = 1u << 16,
    KeyCode_Player_投币   = 1u << 17,
}

/// <summary>
/// 按键控制类型
/// </summary>
public enum KeyControllerType
{
    /// <summary>
    /// 全局按键，默认使用
    /// </summary>
    Global,
    ///// <summary>
    ///// 左手柄
    ///// </summary>
    //LeftHand,
    ///// <summary>
    ///// 右手柄按键
    ///// </summary>
    //RightHand,
    ///// <summary>
    ///// 头盔按键
    ///// </summary>
    //HeadHand,
}

public static class KeyControllerCenter
{
    private static Dictionary<KeyControllerType, KeyController> Controllers = new Dictionary<KeyControllerType, KeyController>();
    public static KeyController GlobalKeyController = null;

    /// <summary>
    /// 幂等初始化：确保 GlobalKeyController 存在，且玩家槽位数不少于 playerSum。
    /// 已存在且槽位足够时直接返回；槽位不足时销毁重建（会丢弃当前按键状态与映射）。
    /// 外部应在构造任何 KeyControllerSource 之前调用，避免 PLAYER_MAX 被锁成小值。
    /// </summary>
    public static void EnsureInit(int playerSum)
    {
        if (GlobalKeyController == null)
        {
            Init(playerSum);
            return;
        }
        if (GlobalKeyController.PLAYER_MAX >= playerSum) return;

        RemoveKeyController(KeyControllerType.Global);
        GlobalKeyController = null;
        Init(playerSum);
    }

    public static void Init(int playerSum=8)
    {
        // 已注册则复用，避免 RegisterKeyController 打"已存在"错误日志
        if (HasKeyController(KeyControllerType.Global))
        {
            GlobalKeyController = GetController(KeyControllerType.Global);
            return;
        }
        //foreach (KeyControllerType keyControllerType in Enum.GetValues(typeof(KeyControllerType)))
        //{
        //    RegisterKeyController(keyControllerType);
        //}
        RegisterKeyController(KeyControllerType.Global, playerSum);
        GlobalKeyController = GetController(KeyControllerType.Global);
        //InitKeySet_LeftRightHand(GetController(KeyControllerType.LeftHand));
        //InitKeySet_LeftRightHand(GetController(KeyControllerType.RightHand));
        InitKeySet_Global();

        //EventManager.AddListener(EventDefine.GeneralEvent.ResetAllIOMap, OnEvent_ResetAllIOMap);

        Debug.Log($"KeyControllerCenter Init() playerSum:{playerSum}");
    }
    private static void OnEvent_ResetAllIOMap()
    {
        foreach (KeyValuePair<KeyControllerType, KeyController> controller in Controllers)
        {
            controller.Value.Reset();
        }
    }

    private static void InitKeySet_LeftRightHand(KeyController keyController)
    {
        keyController.KeySet((uint)AppKeyCode.KeyCode_Trigger, KeyCode.H);
        keyController.KeySet((uint)AppKeyCode.KeyCode_Grip, KeyCode.J);
        keyController.KeySet((uint)AppKeyCode.KeyCode_Home, KeyCode.K);
        keyController.KeySet((uint)AppKeyCode.KeyCode_Menu, KeyCode.L);
        keyController.KeySet((uint)AppKeyCode.KeyCode_A, KeyCode.T);
        keyController.KeySet((uint)AppKeyCode.KeyCode_B, KeyCode.Y);
        keyController.KeySet((uint)AppKeyCode.KeyCode_Joystick, KeyCode.U);
    }
    private static void InitKeySet_Global()
    {
        //GetController(KeyControllerType.Global).KeySet((uint)AppKeyCode.KeyCode_Up, KeyCode.I);
        //GetController(KeyControllerType.Global).KeySet((uint)AppKeyCode.KeyCode_OK, KeyCode.O);
        //GetController(KeyControllerType.Global).KeySet((uint)AppKeyCode.KeyCode_Down, KeyCode.P);
    }

    public static KeyController RegisterKeyController(KeyControllerType keyType, int playerSum)
    {
        if (HasKeyController(keyType))
        {
            Debug.LogError($"KeyControllerCenter Register Failed! Exist! {keyType}");
            return null;
        }
        KeyController kc = KeyController.Create(keyType, playerSum);
        Controllers.Add(keyType, kc);
        return kc;
    }
    public static void RemoveKeyController(KeyControllerType keyType)
    {
        if (!HasKeyController(keyType))
        {
            Debug.LogError($"KeyControllerCenter Remove Failed! Dont Exist! {keyType}");
            return;
        }
        Controllers[keyType] = null;
        Controllers.Remove(keyType);
    }
    public static bool HasKeyController(KeyControllerType keyType)
    {
        if (Controllers.ContainsKey(keyType))
        {
            return true;
        }
        return false;
    }
    public static KeyController GetController(KeyControllerType keyType)
    {
        if(HasKeyController(keyType))
        {
            return Controllers[keyType];
        }
        return null;
    }
    public static void Update(float deltaTime)
    {
        foreach (KeyValuePair<KeyControllerType, KeyController> controller in Controllers)
        {
            controller.Value.Update(deltaTime);
        }
    }

    public static void OnDestory()
    {

    }
}