/// <summary>
/// 输入动作状态机（纯 C#，不依赖 UnityEngine，可单元测试）。
/// 对 32 位动作掩码做帧间分析：Down/Up/单击/双击/长按一次/长按连发。
/// 提炼自 KeyController.KeyAnalysis，去掉多玩家与后台键包袱，去掉 trg 副作用位。
/// </summary>
public class InputStateMachine
{
    const int MAX_ACTIONS = 32;                 // 与 uint 掩码的位数一致
    const float DOUBLE_CLICK_INTERVAL = 0.5f;   // 两次单击构成双击的最大间隔（秒）

    // 逐键分析进度，下标是位号。禁用恢复时由 Resync 全部归零
    readonly float[] longTimer = new float[MAX_ACTIONS];      // 已按住时长；-1 表示长按已触发过
    readonly float[] longMulTimer = new float[MAX_ACTIONS];   // 连发计时器
    readonly float[] dbClickTimer = new float[MAX_ACTIONS];   // 双击间隔计时器
    readonly int[] dbClickCnt = new int[MAX_ACTIONS];         // 已累积的单击次数
    // 配置项，由 SetLongPressTime / SetRepeatInterval 写入，Resync 不清
    readonly float[] longPressTime = new float[MAX_ACTIONS];
    readonly float[] repeatInterval = new float[MAX_ACTIONS];

    uint lastKey;   // 上一帧的按住掩码，边沿分析的比较基线
    uint longMulti;   // 跨帧"正处于长按连发状态"位，松开时清除

    // 本帧解析结果（瞬态位，每次 Tick 重写；InputManager 禁用期间由 ClearFrameEdges 清零）
    public uint FrameDown { get; private set; }
    public uint FrameUp { get; private set; }
    public uint FrameClick { get; private set; }
    public uint FrameDoubleClick { get; private set; }
    public uint FrameLongOnce { get; private set; }
    public uint FrameLongMulti { get; private set; }

    /// <summary>全部动作先套用默认长按参数，个别动作由 InputManager 按 InputBinding 覆盖</summary>
    public InputStateMachine()
    {
        for (int i = 0; i < MAX_ACTIONS; i++)
        {
            longPressTime[i] = 0.8f;
            repeatInterval[i] = 0.075f;
        }
    }

    /// <summary>设置某动作的长按阈值（秒）</summary>
    public void SetLongPressTime(uint singleBitAction, float t)
    {
        longPressTime[BitIndex(singleBitAction)] = t;
    }

    /// <summary>设置某动作长按连发的重复间隔（秒）</summary>
    public void SetRepeatInterval(uint singleBitAction, float t)
    {
        repeatInterval[BitIndex(singleBitAction)] = t;
    }

    /// <summary>该动作当前是否处于按住状态（非边沿，持续为 true）</summary>
    public bool IsDown(uint singleBitAction)
    {
        return (lastKey & singleBitAction) != 0;
    }

    /// <summary>
    /// 推进一帧：由本帧按住掩码与上一帧的差集算出 Down/Up，
    /// 再逐位累计长按与双击进度，结果写进 Frame* 属性。
    /// </summary>
    public void Tick(uint actionMask, float deltaTime)
    {
        FrameDown = (lastKey ^ actionMask) & actionMask;
        FrameUp = (lastKey ^ actionMask) & lastKey;
        lastKey = actionMask;

        FrameClick = 0;
        FrameDoubleClick = 0;
        FrameLongOnce = 0;
        FrameLongMulti = 0;

        for (int i = 0; i < MAX_ACTIONS; i++)
        {
            uint bit = 1u << i;
            bool justEnteredLong = false;

            if ((FrameDown & bit) != 0)
            {
                longTimer[i] = 0;
            }
            if ((actionMask & bit) != 0)
            {
                longTimer[i] += longTimer[i] >= 0 ? deltaTime : 0;
                if (longTimer[i] >= longPressTime[i])
                {
                    longTimer[i] = -1;
                    FrameLongOnce |= bit;
                    longMulti |= bit;          // 跨帧状态位，松开时清除
                    longMulTimer[i] = 0;
                    justEnteredLong = true;
                }
            }
            if ((FrameUp & bit) != 0)
            {
                longMulti &= ~bit;
                if (longTimer[i] > 0)
                {
                    FrameClick |= bit;
                    if (++dbClickCnt[i] % 2 == 0)
                    {
                        FrameDoubleClick |= bit;
                        dbClickTimer[i] = 0;
                        dbClickCnt[i] = 0;
                    }
                }
            }
            if (dbClickCnt[i] > 0)
            {
                dbClickTimer[i] += deltaTime;
                if (dbClickTimer[i] > DOUBLE_CLICK_INTERVAL)
                {
                    dbClickTimer[i] = 0;
                    dbClickCnt[i] = 0;
                }
            }
            if ((longMulti & bit) != 0 && !justEnteredLong)
            {
                longMulTimer[i] += deltaTime;
                if (longMulTimer[i] >= repeatInterval[i])
                {
                    longMulTimer[i] -= repeatInterval[i];
                    FrameLongMulti |= bit;
                }
            }
        }
    }

    /// <summary>清除本帧瞬态位（InputManager 禁用期间调用，防止消费方看到过期边沿）</summary>
    public void ClearFrameEdges()
    {
        FrameDown = 0;
        FrameUp = 0;
        FrameClick = 0;
        FrameDoubleClick = 0;
        FrameLongOnce = 0;
        FrameLongMulti = 0;
    }

    /// <summary>
    /// 与当前按住状态重新对齐：清掉全部瞬态位和逐键分析进度，并把 lastKey 设为 currentMask。
    /// 只清瞬态位是不够的——lastKey 停留在禁用前的值时，禁用窗口内发生的松手/按下
    /// 会在恢复那一帧被算成 Up/Down 边沿。长按与双击计时同样不该把禁用窗口算进去。
    /// </summary>
    public void Resync(uint currentMask)
    {
        ClearFrameEdges();

        lastKey = currentMask;
        longMulti = 0;
        for (int i = 0; i < MAX_ACTIONS; i++)
        {
            longTimer[i] = 0;
            longMulTimer[i] = 0;
            dbClickTimer[i] = 0;
            dbClickCnt[i] = 0;
        }
    }

    /// <summary>单个位标志转数组下标（1u << n 的 n）</summary>
    static int BitIndex(uint singleBit)
    {
        int i = 0;
        while ((singleBit >>= 1) != 0) i++;
        return i;
    }
}
