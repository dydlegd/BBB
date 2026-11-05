using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用输入管理器（单例 MonoBehaviour，DontDestroyOnLoad）。
/// 每帧：轮询输入源 → 按 InputMap 翻译为动作掩码 → 状态机帧间分析 → 分发事件。
///
/// 多玩家：每个玩家一份 InputStateMachine，按住状态与长按/双击进度完全隔离。
/// 共享物理设备（键盘/鼠标/手柄/触摸）对所有玩家返回相同值；
/// 带玩家概念的源（KeyControllerSource，IO 板按槽位灌位）按玩家各查各的。
///
/// 【使用示例】
///
/// 1. 场景准备
///    场景中任意 GameObject 上挂一个 InputManager 组件即可（自动单例化并跨场景保留）。
///    多人玩法在 Inspector 里把 PlayerCount 调到对应人数（默认 1，行为与单玩家一致）。
///
/// 2. 事件订阅（推荐：回调只在对应边沿帧触发一次）
/// <code>
///    void Start()
///    {
///        // 关心是谁按的：回调收到玩家下标
///        InputManager.Instance.Bind(InputAction.Confirm, KeyEvent.Down, OnConfirm);
///
///        // 不关心是谁按的：任意玩家触发都会调进来
///        InputManager.Instance.Bind(InputAction.GenerateQR, KeyEvent.Click, OnGenerateQR);
///    }
///
///    void OnDestroy()
///    {
///        // 订阅方销毁前记得解绑，避免回调打到已销毁对象
///        InputManager.Instance.Unbind(InputAction.Confirm, KeyEvent.Down, OnConfirm);
///    }
///
///    void OnConfirm(int player) { Debug.Log($"玩家 {player} 按下确认"); }
///    void OnGenerateQR() { Debug.Log("有人触发生成"); }
/// </code>
///
/// 3. 轮询查询（适合持续按住类逻辑，放在 Update 中）
///    轮询一律要显式给玩家下标 —— "哪个玩家"含糊不得，所以不提供省略版。
/// <code>
///    if (InputManager.Instance.IsDown(0, InputAction.Right))
///        transform.Translate(Vector3.right * speed * Time.deltaTime);
///
///    if (InputManager.Instance.IsPressed(1, InputAction.Confirm)) { /* 1P 本帧刚按下 */ }
///    if (InputManager.Instance.IsLongMulti(0, InputAction.Confirm)) { /* 0P 长按连发 */ }
/// </code>
///
/// 4. 全局开关（如设置面板打开时屏蔽全局快捷键）
/// <code>
///    InputManager.Instance.Enabled = false;   // 关闭期间不派发任何事件；重新启用时对齐
///                                             // 每个玩家当前的按住状态，禁用窗口内的
///                                             // 按下/松开不会补发
/// </code>
///
/// 5. 新增一个动作：在 InputAction 枚举追加位标志 + 在 InputMap.Bindings 加一行绑定
/// <code>
///    // InputAction.cs:  Jump = 1u << 8,
///    // InputMap.cs:     new InputBinding { action = InputAction.Jump, keys = new[] { KeyCode.Space } },
/// </code>
///
/// 6. 接入自定义输入源（实现 IInputSource 后注册；FakeSource 需自行编写，见 EditMode 测试用例）
/// <code>
///    InputManager.Instance.AddSource(new FakeSource());
///    InputManager.Instance.ClearSources();    // 清空内置源后可用 Tick() 手动驱动
///    InputManager.Instance.Tick(1f / 60f);    // 手动推进一帧（测试用）
/// </code>
///
/// 7. 外部灌入按键（串口 / IO 板 / 网络，按键不经过 Unity 键盘）
/// <code>
///    var src = InputManager.Instance.GetSource<KeyControllerSource>();
///    src.BindExternal(0, KeyCode.Return, AppKeyCode.KeyCode_OK);   // 登记 0 号玩家的反查表
///    src.BindExternal(1, KeyCode.Return, AppKeyCode.KeyCode_OK);   // 1 号玩家同理
///
///    // 之后每帧按需给对应玩家的位缓冲置位 / 清位（边沿由状态机自动分析）
///    src.Controller.g_player_keybuf[0] |= (uint)AppKeyCode.KeyCode_OK;    // 0P 按下
///    src.Controller.g_player_keybuf[1] &= ~(uint)AppKeyCode.KeyCode_OK;   // 1P 抬起
/// </code>
///
/// 注意：Bind 只接受单个位标志的 action；重复绑定同一 handler 会被忽略并告警。
/// </summary>
[DefaultExecutionOrder(-100)]   // 保证在业务代码的 Update 之前完成本帧解析
public class InputManager : SingletonMono<InputManager>
{
    [Header("玩家")]
    [Tooltip("支持的玩家数，每个玩家一份独立的状态机。运行中修改会在下一帧重建状态机并清空当前进度")]
    [Min(1)] public int PlayerCount = 8;

    /// <summary>总开关（如设置面板打开时暂停全局快捷键）</summary>
    public bool Enabled = true;

    /// <summary>已注册的输入源，每帧轮询后取并集</summary>
    readonly List<IInputSource> sources = new List<IInputSource>();
    /// <summary>每个玩家一份状态机，按住状态与长按/双击进度完全隔离</summary>
    InputStateMachine[] machines = new InputStateMachine[0];
    /// <summary>订阅表：动作位 -> 事件类型 -> 回调列表（回调带玩家下标）</summary>
    readonly Dictionary<uint, Dictionary<KeyEvent, List<Action<int>>>> subscribers =
        new Dictionary<uint, Dictionary<KeyEvent, List<Action<int>>>>();
    /// <summary>
    /// 不带 player 的 Action 转 Action<int> 的包装缓存。
    /// 必须复用同一个包装实例，否则 Unbind 时按原 handler 找不到当初加进去的那个委托。
    /// </summary>
    readonly Dictionary<Action, Action<int>> playerAgnosticWrappers =
        new Dictionary<Action, Action<int>>();

    /// <summary>分发用的回调快照缓冲，复用以避免每帧 GC</summary>
    readonly List<Action<int>> dispatchBuffer = new List<Action<int>>();
    int dispatchDepth;
    /// <summary>经历过禁用，下一帧恢复时需先与当前按住状态对齐</summary>
    bool needsResync;

    /// <summary>单例化，注册内置输入源，并把 InputMap 的长按参数写进所有状态机</summary>
    protected override void Awake()
    {
        base.Awake();

        Initialize();

        sources.Add(new KeyboardSource());
        sources.Add(new MouseSource());
        sources.Add(new GamepadSource());
        sources.Add(new TouchSource());
        sources.Add(new KeyControllerSource(PlayerCount));
    }

    /// <summary>
    /// 按 PlayerCount 建好每玩家一份的状态机，并套用 InputMap 的长按参数。幂等。
    /// Awake 与首次 Tick 都会走这条路径 —— EditMode 测试里 Unity 不调用 Awake，全靠它兜底。
    /// 运行中改 PlayerCount 会重建状态机、丢弃全部进度，因此标 needsResync 让下一帧对齐按住状态。
    /// </summary>
    void Initialize()
    {
        PlayerCount = Mathf.Max(1, PlayerCount);
        if (machines.Length == PlayerCount) return;

        // 首次建表不算"重置"：启动那一刻就按住的键，仍应正常产生一次 Down
        bool rebuilding = machines.Length > 0;

        machines = new InputStateMachine[PlayerCount];
        for (int p = 0; p < PlayerCount; p++) machines[p] = new InputStateMachine();
        ConfigureBindings();

        if (rebuilding) needsResync = true;
    }

    /// <summary>每帧解析一次。Enabled 判断放在 Tick 内部，测试可脱离 Update 手动驱动</summary>
    void Update() => Tick(Time.deltaTime);

    // ---------- 源管理（测试与扩展用） ----------

    /// <summary>注册输入源。传 null 或重复注册同一实例都会被忽略</summary>
    public void AddSource(IInputSource source)
    {
        if (source == null || sources.Contains(source)) return;
        sources.Add(source);
    }

    /// <summary>清空全部输入源（测试改用 Tick() 手动驱动前先调这个）</summary>
    public void ClearSources() => sources.Clear();

    /// <summary>清除所有订阅（Shutdown 时调用）</summary>
    public void ClearContexts() => subscribers.Clear();

    /// <summary>取出已注册的某个输入源（如 KeyControllerSource），未注册返回 null</summary>
    public T GetSource<T>() where T : class, IInputSource
    {
        foreach (var source in sources)
        {
            if (source is T typed) return typed;
        }
        return null;
    }

    // ---------- 订阅 API ----------

    /// <summary>
    /// 订阅某动作的某类事件，回调收到触发它的玩家下标。成功返回 true；
    /// action 不是单个位标志、handler 为 null、或重复绑定同一 handler 时返回 false 并告警。
    /// </summary>
    public bool Bind(InputAction action, KeyEvent keyEvent, Action<int> handler)
    {
        uint bit = (uint)action;
        if (bit == 0 || (bit & (bit - 1)) != 0)
        {
            Debug.LogWarning($"[InputManager] Bind 失败：action 必须是单个位标志，收到 {action}");
            return false;
        }
        if (handler == null) return false;

        if (!subscribers.TryGetValue(bit, out var byEvent))
        {
            byEvent = new Dictionary<KeyEvent, List<Action<int>>>();
            subscribers[bit] = byEvent;
        }
        if (!byEvent.TryGetValue(keyEvent, out var list))
        {
            list = new List<Action<int>>();
            byEvent[keyEvent] = list;
        }
        if (list.Contains(handler))
        {
            Debug.LogWarning($"[InputManager] 重复绑定被忽略：{action} / {keyEvent}");
            return false;
        }
        list.Add(handler);
        return true;
    }

    /// <summary>
    /// 订阅但不需要知道是哪个玩家触发 —— 内部包成 Action<int>，每个触发的玩家各回调一次。
    /// 注意传 null 会因 Action / Action<int> 重载歧义编译不过，需显式写 (Action)null。
    /// </summary>
    public bool Bind(InputAction action, KeyEvent keyEvent, Action handler)
    {
        if (handler == null) return false;
        return Bind(action, keyEvent, Wrap(handler));
    }

    /// <summary>解绑，真的移除到才返回 true。订阅方销毁前应调用，避免回调打到已销毁对象</summary>
    public bool Unbind(InputAction action, KeyEvent keyEvent, Action<int> handler)
    {
        uint bit = (uint)action;
        if (subscribers.TryGetValue(bit, out var byEvent)
            && byEvent.TryGetValue(keyEvent, out var list))
        {
            return list.Remove(handler);
        }
        return false;
    }

    /// <summary>解绑 Bind(action, keyEvent, Action) 的便捷版本</summary>
    public bool Unbind(InputAction action, KeyEvent keyEvent, Action handler)
    {
        if (handler == null) return false;
        return playerAgnosticWrappers.TryGetValue(handler, out var wrapped)
            && Unbind(action, keyEvent, wrapped);
    }

    /// <summary>取出（必要时新建）该 handler 的包装，保证 Bind/Unbind 拿到同一个实例</summary>
    Action<int> Wrap(Action handler)
    {
        if (!playerAgnosticWrappers.TryGetValue(handler, out var wrapped))
        {
            wrapped = _ => handler();
            playerAgnosticWrappers[handler] = wrapped;
        }
        return wrapped;
    }

    // ---------- 轮询 API ----------

    /// <summary>该玩家该动作当前是否按住（持续为 true，不是边沿）</summary>
    public bool IsDown(int player, InputAction action)
    {
        return Valid(player) && machines[player].IsDown((uint)action);
    }

    /// <summary>该玩家本帧刚按下（边沿，只在按下的那一帧为 true）</summary>
    public bool IsPressed(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameDown & (uint)action) != 0;
    }

    /// <summary>该玩家本帧刚抬起（边沿，只在松开的那一帧为 true）</summary>
    public bool IsReleased(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameUp & (uint)action) != 0;
    }

    /// <summary>该玩家本帧构成一次完整短按（非长按时抬起）</summary>
    public bool IsClick(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameClick & (uint)action) != 0;
    }

    /// <summary>该玩家本帧构成双击（双击间隔内的第二次单击）</summary>
    public bool IsDoubleClick(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameDoubleClick & (uint)action) != 0;
    }

    /// <summary>该玩家本帧刚达到长按阈值，一次按住只触发一次</summary>
    public bool IsLongOnce(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameLongOnce & (uint)action) != 0;
    }

    /// <summary>该玩家本帧产生一次长按连发</summary>
    public bool IsLongMulti(int player, InputAction action)
    {
        return Valid(player) && (machines[player].FrameLongMulti & (uint)action) != 0;
    }

    /// <summary>该玩家本帧 Down 位的原始掩码（测试断言用）</summary>
    public uint FrameDown(int player) => Valid(player) ? machines[player].FrameDown : 0u;

    /// <summary>玩家下标越界时返回 false 并告警，不让调用方拿到莫名其妙的默认值</summary>
    bool Valid(int player)
    {
        Initialize();
        if (player >= 0 && player < machines.Length) return true;
        Debug.LogWarning($"[InputManager] 玩家下标 {player} 越界，当前 PlayerCount = {machines.Length}");
        return false;
    }

    // ---------- 每帧解析 ----------

    /// <summary>
    /// 推进一帧解析：每个玩家各轮询一次输入源 -> 翻译为动作掩码 -> 状态机做边沿分析，
    /// 全部玩家都算完再逐个分发事件。禁用期间只清瞬态位不推进，并标记下次恢复时需先 Resync。
    /// </summary>
    public void Tick(float deltaTime)
    {
        Initialize();

        if (!Enabled)
        {
            needsResync = true;
            for (int p = 0; p < machines.Length; p++) machines[p].ClearFrameEdges();
            return;
        }

        // 先把所有玩家都推进完再分发：回调里重入 Tick 时，各玩家状态已是一致的
        for (int p = 0; p < machines.Length; p++)
        {
            uint mask = BuildActionMask(p);
            if (needsResync)
            {
                // 从禁用恢复：先把按住状态对齐，否则禁用窗口内的松手/按下会被算成 Up/Down 边沿
                machines[p].Resync(mask);
            }
            machines[p].Tick(mask, deltaTime);
        }
        needsResync = false;

        for (int p = 0; p < machines.Length; p++) Dispatch(p);
    }

    /// <summary>
    /// 遍历 InputMap.Bindings，把"某玩家视角下物理键是否按住"翻译成动作位。
    /// 同一动作的多个键、以及 touchGesture 绑定之间都是 OR 关系。
    /// </summary>
    uint BuildActionMask(int player)
    {
        uint mask = 0;
        foreach (var binding in InputMap.Bindings)
        {
            bool held = false;
            foreach (var code in binding.keys)
            {
                foreach (var source in sources)
                {
                    if (source.IsAvailable() && source.GetKey(player, code))
                    {
                        held = true;
                        break;
                    }
                }
                if (held) break;
            }
            if (!held && binding.touchGesture && TouchSource.IsTouching())
            {
                held = true;
            }
            if (held) mask |= (uint)binding.action;
        }
        return mask;
    }

    /// <summary>把该玩家本帧六类瞬态位各自分发出去</summary>
    void Dispatch(int player)
    {
        var m = machines[player];
        Dispatch(player, m.FrameDown, KeyEvent.Down);
        Dispatch(player, m.FrameUp, KeyEvent.Up);
        Dispatch(player, m.FrameClick, KeyEvent.Click);
        Dispatch(player, m.FrameDoubleClick, KeyEvent.DoubleClick);
        Dispatch(player, m.FrameLongOnce, KeyEvent.LongOnce);
        Dispatch(player, m.FrameLongMulti, KeyEvent.LongMulti);
    }

    /// <summary>
    /// 分发某玩家的一类事件。遍历 frameBits 的置位而非订阅表，
    /// 因此只访问本帧真正触发的动作，也不分配临时集合（热路径零 GC）。
    /// </summary>
    void Dispatch(int player, uint frameBits, KeyEvent keyEvent)
    {
        if (frameBits == 0 || subscribers.Count == 0) return;

        // 遍历 frameBits 的置位，而不是 subscribers 的键：
        // 只访问本帧真正触发的动作（通常 1 个），也不像字典枚举那样会被回调内的 Bind/Unbind 打乱
        uint remaining = frameBits;
        while (remaining != 0)
        {
            uint bit = remaining & (0u - remaining);   // 取最低置位（x & -x）
            remaining &= remaining - 1;                // 清掉该位

            if (!subscribers.TryGetValue(bit, out var byEvent)) continue;
            if (!byEvent.TryGetValue(keyEvent, out var list)) continue;

            // 拷贝后调用：回调内 Unbind/Rebind 不影响本次遍历。
            // 复用 dispatchBuffer 避免每帧分配；回调内重入 Tick 时让出共享缓冲，退化为临时分配
            dispatchDepth++;
            List<Action<int>> handlers;
            if (dispatchDepth == 1)
            {
                dispatchBuffer.Clear();
                dispatchBuffer.AddRange(list);
                handlers = dispatchBuffer;
            }
            else
            {
                handlers = new List<Action<int>>(list);
            }

            try
            {
                for (int i = 0; i < handlers.Count; i++) handlers[i](player);
            }
            finally
            {
                dispatchDepth--;
            }
        }
    }

    /// <summary>
    /// 启动时把 InputMap 的长按参数写进每个玩家的状态机，并检查是否有 KeyCode 被绑到多个动作。
    /// 后者是合法的（一次按下同时驱动两个动作），这里只打日志提醒确认是否符合预期。
    /// </summary>
    void ConfigureBindings()
    {
        var seen = new Dictionary<KeyCode, InputAction>();
        foreach (var binding in InputMap.Bindings)
        {
            foreach (var m in machines)
            {
                m.SetLongPressTime((uint)binding.action, binding.longPressTime);
                m.SetRepeatInterval((uint)binding.action, binding.repeatInterval);
            }
            foreach (var code in binding.keys)
            {
                if (seen.TryGetValue(code, out var other) && other != binding.action)
                {
                    Debug.Log($"[InputManager] 键码 {code} 同时绑定了 {other} 与 {binding.action}（合法但请确认是否符合预期）");
                }
                else
                {
                    seen[code] = binding.action;
                }
            }
        }
    }
}