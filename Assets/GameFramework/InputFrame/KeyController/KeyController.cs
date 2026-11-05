using UnityEngine;
using System.Collections;
using System;


public class KeyController
{
    public int PLAYER_MAX { get; private set; }
    private const int APP_KEY_MAX = 32;
    private const int BS_KEY_MAX = 32;
    private const float DOUBLE_CLICK_INTERVAL = 0.5f;
    public uint g_key_buf;
    public uint[] g_player_keybuf;// = new uint[PLAYER_MAX];

    private KeyClass[] g_key;// = new KeyClass[PLAYER_MAX];
    private KeyClass g_bkey = new KeyClass();
    public KeyControllerType m_KeyControllerType { get; private set; }

    public static KeyController Create(KeyControllerType keyControllerType, int playerSum=8)
    {
        KeyController keyController = new KeyController();
        keyController.Init(keyControllerType, playerSum);
        return keyController;
    }

    protected class KeyClass
    {
        public float[] long_timer = new float[APP_KEY_MAX];
        public float[] long_mul_timer = new float[APP_KEY_MAX];
        public float[] db_click_timer = new float[APP_KEY_MAX];
        public int[] db_click_cnt = new int[APP_KEY_MAX];
        public float[] long_time_time = new float[APP_KEY_MAX];
        public float[] long_continue_interval = new float[APP_KEY_MAX];
        public KeyCode[] remap_code = new KeyCode[APP_KEY_MAX];

        public uint get_key;
        public uint last_key;
        public uint down;
        public uint up;
        public uint trg;
        public uint click;
        public uint db_click;
        public uint long_once;
        public uint long_multi;

        public KeyClass()
        {
            for (int i = 0; i < APP_KEY_MAX; i++)
            {
                long_time_time[i] = 0.8f;
                long_continue_interval[i] = 0.075f;
            }
        }
    }

    private void Init(KeyControllerType keyControllerType, int playerSum)
    {
        PLAYER_MAX = playerSum;
        g_player_keybuf = new uint[PLAYER_MAX];
        g_key = new KeyClass[PLAYER_MAX];
        m_KeyControllerType = keyControllerType;

        for (int no = 0; no < PLAYER_MAX; no++)
        {
            g_key[no] = new KeyClass();
            //KeySet(no, (uint)AppKeyCode.KeyCode_Bet, KeyCode.Return);
            //KeySet(no, (uint)AppKeyCode.KeyCode_UpScore, KeyCode.PageUp);
            //KeySet(no, (uint)AppKeyCode.KeyCode_DownScore, KeyCode.PageDown);
            //KeySet(no, (uint)AppKeyCode.KeyCode_CoinIn, KeyCode.F1);
            //KeySet(no, (uint)AppKeyCode.KeyCode_CoinOut, KeyCode.F2);
            //KeySet(no, (uint)AppKeyCode.KeyCode_TicketOutSign, KeyCode.F3);
            //KeySet(no, (uint)AppKeyCode.KeyCode_CoinOutSign, KeyCode.F4);
            //KeySet(no, (uint)AppKeyCode.KeyCode_Up, KeyCode.UpArrow);
            //KeySet(no, (uint)AppKeyCode.KeyCode_Down, KeyCode.DownArrow);
            //KeySet(no, (uint)AppKeyCode.KeyCode_Left, KeyCode.LeftArrow);
            //KeySet(no, (uint)AppKeyCode.KeyCode_Right, KeyCode.RightArrow);
        }
        //KeySet((uint)AppKeyCode.KeyCode_BsSetting, KeyCode.V);
        //KeySet((uint)AppKeyCode.KeyCode_BsOK, KeyCode.RightControl);
        //KeySet((uint)AppKeyCode.KeyCode_BsUp, KeyCode.UpArrow);
        //KeySet((uint)AppKeyCode.KeyCode_BsDown, KeyCode.DownArrow);
        //KeySet((uint)AppKeyCode.KeyCode_BsLeft, KeyCode.LeftArrow);
        //KeySet((uint)AppKeyCode.KeyCode_BsRight, KeyCode.RightArrow);

        //KeySet((uint)AppKeyCode.KeyCode_Trigger, KeyCode.Q);
        //KeySet((uint)AppKeyCode.KeyCode_Grip, KeyCode.W);
        //KeySet((uint)AppKeyCode.KeyCode_Home, KeyCode.E);
        //KeySet((uint)AppKeyCode.KeyCode_Menu, KeyCode.R);
        //KeySet((uint)AppKeyCode.KeyCode_A, KeyCode.T);
        //KeySet((uint)AppKeyCode.KeyCode_B, KeyCode.Y);
        //KeySet((uint)AppKeyCode.KeyCode_Joystick, KeyCode.U);

        //KeySet((uint)AppKeyCode.KeyCode_Up, KeyCode.I);
        //KeySet((uint)AppKeyCode.KeyCode_OK, KeyCode.O);
        //KeySet((uint)AppKeyCode.KeyCode_Down, KeyCode.P);


    }
    public void Reset()
    {
        for (int no = 0; no < PLAYER_MAX; no++)
        {
            g_player_keybuf[no] = 0;
        }
        g_key_buf = 0;
    }
    public void Update(float deltaTime)
    {
        ParseKeyBuf(deltaTime);
    }

    #region 按键处理
    private void ParseKeyBuf(float deltaTime)
    {
        for (int no = 0; no < PLAYER_MAX; no++)
        {
            KeyAnalysisInit(ref g_key[no]);
        }
        KeyAnalysisInit(ref g_bkey);

        for (int no = 0; no < PLAYER_MAX; no++)
        {
            //idx = no * (int)te_CommuMsg.COMMU_MSG_MAX + (int)te_CommuMsg.key_up;      //找到每个玩家key开始地址.
            //key_tmp = 0;
            //for (int i = 0; i < APP_KEY_MAX; i++)
            //{   //把每个玩家的key从数组转成一个int.
            //    key_tmp |= buf[idx + i] != 0 ? 1u << i : 0;
            //}
            //KeyAnalysis(ref g_key[no], APP_KEY_MAX, (AppKeyCode)key_tmp, GetBindingKeyMap(no));

            KeyAnalysis(ref g_key[no], APP_KEY_MAX, g_player_keybuf[no], GetBindingKeyMap(no), deltaTime);
        }

        //idx = 0;    //找到最后一位玩家最后一位，接着的第一位是进后台按键.
        KeyAnalysis(ref g_bkey, BS_KEY_MAX, g_key_buf, GetBindingKeyMap(), deltaTime);
    }

    void KeyAnalysisInit(ref KeyClass key)
    {
        key.down = 0;
        key.up = 0;
        key.click = 0;
        key.db_click = 0;
        key.long_once = 0;
    }

    void KeyAnalysis(ref KeyClass key, int max_key, uint app_key, uint sys_key, float deltaTime)
    {
        uint key_tmp;

        key.get_key = app_key | sys_key;
        key.down = (key.last_key ^ key.get_key) & key.get_key;
        key.up = (key.last_key ^ key.get_key) & key.last_key;
        key.last_key = key.get_key;
        key.trg = key.get_key;

        for (int i = 0; i < max_key; i++)
        {
            key_tmp = 1u << i;
            if (((int)key.down & key_tmp) != 0)
            {
                key.long_timer[i] = 0;
            }
            if (((int)key.get_key & key_tmp) != 0)
            {
                key.long_timer[i] += key.long_timer[i] >= 0 ? deltaTime : 0;
                if (key.long_timer[i] >= key.long_time_time[i])
                {
                    key.long_timer[i] = -1;
                    key.long_once |= (uint)key_tmp;
                    key.long_multi |= (uint)key_tmp;
                }
            }
            if (((int)key.up & key_tmp) != 0)
            {
                key.long_multi &= ~(uint)key_tmp;

                key.click |= key.long_timer[i] > 0 ? (uint)key_tmp : 0;

                if (((int)key.click & key_tmp) != 0)
                {
                    if (++key.db_click_cnt[i] % 2 == 0)
                    {
                        key.db_click |= (uint)key_tmp;
                        key.db_click_timer[i] = 0;
                        key.db_click_cnt[i] = 0;
                    }
                }
            }

            if (key.db_click_cnt[i] > 0)
            {
                key.db_click_timer[i] += deltaTime;
                if (key.db_click_timer[i] > DOUBLE_CLICK_INTERVAL)
                {
                    key.db_click_timer[i] = 0;
                    key.db_click_cnt[i] = 0;
                }
            }
        }
    }

    private static int GetKeyIndex(uint key)
    {
        int i, tmp;

        tmp = (int)key;
        for (i = 0; i < APP_KEY_MAX; i++)
        {
            tmp >>= 1;
            if (tmp == 0)
            {
                break;
            }
        }
        return i;
    }

    private uint GetBindingKeyMap(int no)
    {
        int i, val = 0;

        for (i = 0; i < APP_KEY_MAX; i++)
        {
            if (Input.GetKey(g_key[no].remap_code[i]))
            {
                val |= 1 << i;
            }
        }

        return (uint)val;
    }

    private uint GetBindingKeyMap()
    {
        int i, val = 0;

        for (i = 0; i < APP_KEY_MAX; i++)
        {
            if (Input.GetKey(g_bkey.remap_code[i]))
            {
                val |= 1 << i;
            }
        }

        return (uint)val;
    }
    #endregion

    #region 玩家按键
    public bool GetKey(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        return (g_key[no].get_key & key) != 0 ? true : false;
    }

    public bool GetKeyDown(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].down & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyUp(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].up & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyTrg(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].trg & key) != 0 ? true : false;
        g_key[no].trg &= ~key;
        return ret;
    }

    public bool GetKeyClick(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].click & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyClickDouble(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].db_click & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyLongOnce(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        bool ret = (g_key[no].long_once & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyLongMulti(int no, uint key)
    {
        if (no < 0 || no >= PLAYER_MAX) return false;
        if ((g_key[no].long_multi & key) == 0)
        {
            return false;
        }

        int idx = GetKeyIndex(key);

        g_key[no].long_mul_timer[idx] += Time.unscaledDeltaTime;
        if (g_key[no].long_mul_timer[idx] < g_key[no].long_continue_interval[idx])
        {
            return false;
        }
        g_key[no].long_mul_timer[idx] -= g_key[no].long_continue_interval[idx];

        return true;
    }

    public void SetKeyLongTimeTime(int no, uint key, float t)
    {
        if (no < 0 || no >= PLAYER_MAX) return;
        g_key[no].long_time_time[GetKeyIndex(key)] = t;
    }

    public void SetKeyLongContinueInterval(int no, uint key, float t)
    {
        if (no < 0 || no >= PLAYER_MAX) return;
        g_key[no].long_continue_interval[GetKeyIndex(key)] = t;
    }

    public void KeySet(int no, uint app_key, KeyCode key, float long_time, float interval)
    {
        if (no < 0 || no >= PLAYER_MAX) return;
        int idx = GetKeyIndex(app_key);

        g_key[no].remap_code[idx] = key;
        g_key[no].long_time_time[idx] = long_time;
        g_key[no].long_continue_interval[idx] = interval;
    }

    public void KeySet(int no, uint app_key, KeyCode key)
    {
        if (no < 0 || no >= PLAYER_MAX) return;
        g_key[no].remap_code[GetKeyIndex(app_key)] = key;
    }

    public void KeySet(int no, uint app_key, float long_time, float interval)
    {
        if (no < 0 || no >= PLAYER_MAX) return;
        int idx = GetKeyIndex(app_key);

        g_key[no].long_time_time[idx] = long_time;
        g_key[no].long_continue_interval[idx] = interval;
    }
    #endregion

    #region 后台按键
    public bool GetKey(uint key)
    {
        return (g_bkey.get_key & key) != 0 ? true : false;
    }

    public bool GetKeyDown(uint key)
    {
        return (g_bkey.down & key) != 0 ? true : false;
    }

    public bool GetKeyUp(uint key)
    {
        return (g_bkey.up & key) != 0 ? true : false;
    }

    public bool GetKeyClick(uint key)
    {
        bool ret = (g_bkey.click & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyClickDouble(uint key)
    {
        bool ret = (g_bkey.db_click & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyLongOnce(uint key)
    {
        bool ret = (g_bkey.long_once & key) != 0 ? true : false;
        return ret;
    }

    public bool GetKeyLongMulti(uint key)
    {
        if ((g_bkey.long_multi & key) == 0)
        {
            return false;
        }

        int idx = GetKeyIndex(key);

        g_bkey.long_mul_timer[idx] += Time.unscaledDeltaTime;
        if (g_bkey.long_mul_timer[idx] < g_bkey.long_continue_interval[idx])
        {
            return false;
        }
        g_bkey.long_mul_timer[idx] -= g_bkey.long_continue_interval[idx];

        return true;
    }

    public void KeySet(uint app_key, KeyCode key)
    {
        int idx = GetKeyIndex(app_key);

        g_bkey.remap_code[idx] = key;
    }

    public void KeySet(uint app_key, KeyCode key, float long_time, float interval)
    {
        int idx = GetKeyIndex(app_key);

        g_bkey.remap_code[idx] = key;
        g_bkey.long_time_time[idx] = long_time;
        g_bkey.long_continue_interval[idx] = interval;
    }
    #endregion
}

