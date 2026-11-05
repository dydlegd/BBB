using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InputManager 运行时冒烟测试。挂在场景任意 GameObject 上即可，按 F1 显示/隐藏面板。
/// 面板开关刻意使用旧版 Input.GetKeyDown，避免"用被测对象控制自己"。
///
/// 覆盖三类验证：
///   1. 自检（Start 时跑一遍，结果打到 Console）—— Bind/Unbind 返回值、非法 action、重复绑定
///   2. 实时状态面板 —— 每个 InputAction 的 按住/按下/抬起/点击/双击/长按/连发 七个位
///   3. 事件日志 —— 真的 Bind 一套回调，确认"物理键 → 动作 → 状态机 → 分发"整条链路通
///
/// 手动测试步骤：
///   方向键        → 看 Up/Down/Left/Right 行的"按住"列变绿
///   小键盘回车/左键 → Confirm：快速点两下看"双击"，按住 0.8s 看"长按"再到"连发"
///   回车键        → GenerateQR：本项目实际在用的绑定，应与二维码生成功能同时响应
///   Esc           → Cancel
///   面板上的开关按钮 → 关闭后所有状态列应变灰（边沿被清掉，不会补发）
///
/// 多玩家：把 InputManager 的 PlayerCount 调到 2 以上，面板顶部会出现玩家切换按钮。
/// 共享设备（键盘/鼠标/手柄/触摸）对所有玩家生效，所以方向键会让每个玩家的行都变绿；
/// 要验证玩家隔离得走 KeyControllerSource，往 g_player_keybuf[n] 灌对应玩家的位。
/// </summary>
public class InputTest : MonoBehaviour
{
    [Header("面板")]
    [Tooltip("是否显示测试面板，运行时按 F1 也可切换")]
    public bool showPanel = true;
    [Tooltip("面板当前查看的玩家，0 起。多玩家时用面板上的 +/- 按钮切换")]
    public int viewPlayer;
    [Tooltip("事件日志保留条数，超出后丢弃最旧的")]
    public int logCapacity = 12;

    // 七个状态列，顺序与 ColumnHeaders 一致
    const int ColumnCount = 7;
    static readonly string[] ColumnHeaders = { "按住", "按下", "抬起", "点击", "双击", "长按", "连发" };

    InputManager input;                                     // 被测对象
    InputAction[] watched;                                  // 面板要显示的全部动作（除 None）
    readonly Dictionary<InputAction, bool[]> states = new Dictionary<InputAction, bool[]>();
    readonly List<string> logs = new List<string>();        // Bind 回调实际触发记录

    int passCount;                                          // 自检通过数
    int failCount;                                          // 自检失败数
    Vector2 scroll;                                         // 面板滚动位置

    /// <summary>找到 InputManager、枚举动作、跑自检、挂上事件日志</summary>
    void Start()
    {
        input = InputManager.Instance;
        if (input == null)
        {
            Debug.LogError("[InputTest] 场景中未找到 InputManager，请先挂载 InputManager 组件。");
            enabled = false;
            return;
        }

        BuildWatchedActions();
        RunSelfChecks();
        BindEventLog();
    }

    // ---------- 自检：不依赖按键，结果打到 Console ----------

    void RunSelfChecks()
    {
        passCount = 0;
        failCount = 0;
        Debug.Log("========== InputManager 自检开始 ==========");

        Action handler = () => { };

        Check(input.Bind(InputAction.Menu, KeyEvent.Down, handler), "Bind 合法动作返回 true");
        Check(!input.Bind(InputAction.Menu, KeyEvent.Down, handler), "重复绑定返回 false 并告警");
        Check(input.Unbind(InputAction.Menu, KeyEvent.Down, handler), "Unbind 已绑定回调返回 true");
        Check(!input.Unbind(InputAction.Menu, KeyEvent.Down, handler), "Unbind 未绑定回调返回 false");

        Check(!input.Bind(InputAction.None, KeyEvent.Down, () => { }), "Bind(None) 被拒绝");
        Check(!input.Bind(InputAction.Up | InputAction.Down, KeyEvent.Down, () => { }),
            "Bind(多位置标志) 被拒绝");
        Check(!input.Bind(InputAction.Menu, KeyEvent.Down, (Action)null), "Bind(null 回调) 被拒绝");

        Debug.Log($"========== InputManager 自检结束: 通过 {passCount}, 失败 {failCount} ==========");
    }

    /// <summary>记一条自检结果并打到 Console</summary>
    void Check(bool condition, string testName)
    {
        if (condition)
        {
            passCount++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            failCount++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    // ---------- 事件链路：真的 Bind 一套回调 ----------

    /// <summary>枚举 InputAction 里除 None 外的全部动作，并为每个动作开好状态行</summary>
    void BuildWatchedActions()
    {
        var list = new List<InputAction>();
        foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
        {
            if (action == InputAction.None) continue;
            list.Add(action);
        }
        watched = list.ToArray();
        foreach (var action in watched) states[action] = new bool[ColumnCount];
    }

    /// <summary>给每个动作的每类事件都挂一个日志回调，验证"物理键 -> 分发"整条链路</summary>
    void BindEventLog()
    {
        foreach (var action in watched)
        {
            foreach (KeyEvent keyEvent in Enum.GetValues(typeof(KeyEvent)))
            {
                var a = action;
                var e = keyEvent;
                // 用带玩家下标的重载，日志里能看出是哪个玩家触发的
                input.Bind(a, e, p => LogEvent(p, a, e));
            }
        }
    }

    /// <summary>记录一条事件（含触发它的玩家），超出 logCapacity 时丢弃最旧的</summary>
    void LogEvent(int player, InputAction action, KeyEvent keyEvent)
    {
        logs.Add($"[{Time.frameCount,6}] P{player} {action,-10} {keyEvent}");
        if (logs.Count > logCapacity) logs.RemoveAt(0);
    }

    // ---------- 每帧刷新轮询状态 ----------

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) showPanel = !showPanel;
        if (input == null) return;

        // PlayerCount 可能在 Inspector 里被改小，轮询前先夹住，避免触发越界告警
        viewPlayer = Mathf.Clamp(viewPlayer, 0, Mathf.Max(0, input.PlayerCount - 1));

        foreach (var action in watched)
        {
            var row = states[action];
            row[0] = input.IsDown(viewPlayer, action);
            row[1] = input.IsPressed(viewPlayer, action);
            row[2] = input.IsReleased(viewPlayer, action);
            row[3] = input.IsClick(viewPlayer, action);
            row[4] = input.IsDoubleClick(viewPlayer, action);
            row[5] = input.IsLongOnce(viewPlayer, action);
            row[6] = input.IsLongMulti(viewPlayer, action);
        }
    }

    // ---------- 面板 ----------

    void OnGUI()
    {
        if (!showPanel || input == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 520, 420), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label($"InputManager 冒烟测试   [F1] 隐藏面板   自检 {passCount} 通过 / {failCount} 失败");

        // 玩家切换：PlayerCount > 1 时才显示
        if (input.PlayerCount > 1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"查看玩家 P{viewPlayer}（共 {input.PlayerCount} 人）", GUILayout.Width(220));
            if (GUILayout.Button("◀ 上一个", GUILayout.Width(80)) && viewPlayer > 0) viewPlayer--;
            if (GUILayout.Button("下一个 ▶", GUILayout.Width(80)) && viewPlayer < input.PlayerCount - 1) viewPlayer++;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        // 表头
        GUILayout.BeginHorizontal();
        GUILayout.Label("动作", GUILayout.Width(110));
        foreach (var header in ColumnHeaders)
        {
            GUILayout.Label(header, GUILayout.Width(46));
        }
        GUILayout.EndHorizontal();

        // 每个动作一行
        foreach (var action in watched)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(action.ToString(), GUILayout.Width(110));
            var row = states[action];
            for (int i = 0; i < row.Length; i++)
            {
                Cell(row[i]);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        // 全局开关：关闭后所有状态列应变灰
        string switchText = input.Enabled ? "InputManager.Enabled = true（点击关闭）" : "InputManager.Enabled = false（点击开启）";
        if (GUILayout.Button(switchText, GUILayout.Width(300)))
        {
            input.Enabled = !input.Enabled;
        }

        GUILayout.Space(8);
        GUILayout.Label("事件日志（Bind 回调实际触发记录）");
        if (logs.Count == 0)
        {
            GUILayout.Label("  暂无事件，按一下方向键试试");
        }
        foreach (var line in logs)
        {
            GUILayout.Label("  " + line);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>画一格状态指示灯，用 GUI.color 临时改色后还原</summary>
    void Cell(bool on)
    {
        Color old = GUI.color;
        GUI.color = on ? Color.green : new Color(1f, 1f, 1f, 0.3f);
        GUILayout.Label(on ? "●" : "○", GUILayout.Width(46));
        GUI.color = old;
    }
}
