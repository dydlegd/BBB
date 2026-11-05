using UnityEngine;

/// <summary>
/// 通用游戏时间工具类。
/// 提供时间缩放控制、FPS 统计、计时器、秒数格式化等与 Unity Time 系统相关的常用功能。
/// 所有对 Time.timeScale 的修改均通过本工具类进行，便于统一管理和调试。
/// </summary>
/// <example>时间缩放
/// <code>
/// TimeUtility.SetTimeScale(0.5f);          // 慢动作
/// TimeUtility.Pause();                      // 暂停游戏
/// TimeUtility.Resume();                     // 恢复游戏
/// bool paused = TimeUtility.IsPaused;       // 是否暂停
/// TimeUtility.TogglePause();                // 切换暂停状态
/// </code>
/// </example>
/// <example>FPS 统计
/// <code>
/// float fps = TimeUtility.CurrentFPS;       // 当前帧率
/// float avgFps = TimeUtility.AverageFPS;    // 平均帧率
/// float smoothDelta = TimeUtility.SmoothDeltaTime; // 平滑帧间隔
/// </code>
/// </example>
/// <example>秒数格式化
/// <code>
/// string s = TimeUtility.FormatSeconds(3661f);              // "01:01:01"
/// string s2 = TimeUtility.FormatSeconds(125f, "mm:ss");     // "02:05"
/// string s3 = TimeUtility.FormatSecondsCompact(3661f);      // "1h1m1s"
/// string s4 = TimeUtility.FormatSecondsChinese(3661f);      // "1小时1分钟1秒"
/// </code>
/// </example>
/// <example>计时器
/// <code>
/// // 在 Update 中驱动
/// float timer = 0f;
/// if (TimeUtility.TickTimer(ref timer, 3f))
/// {
///     Debug.Log("3秒到了！");
/// }
/// </code>
/// </example>
public static class TimeUtility
{
    #region 时间缩放

    /// <summary>
    /// 当前游戏是否处于暂停状态。
    /// </summary>
    public static bool IsPaused => Mathf.Approximately(Time.timeScale, 0f);

    /// <summary>
    /// 记录暂停前的 timeScale，用于 Resume 时恢复。
    /// </summary>
    private static float _timeScaleBeforePause = 1f;

    /// <summary>
    /// 设置时间缩放比例。自动 clamped 到 [0, 100] 范围。
    /// </summary>
    /// <param name="scale">时间缩放比例，0 为暂停，1 为正常速度</param>
    public static void SetTimeScale(float scale)
    {
        Time.timeScale = Mathf.Clamp(scale, 0f, 100f);
    }

    /// <summary>
    /// 暂停游戏（设置 timeScale 为 0）。会记录暂停前的 timeScale。
    /// </summary>
    public static void Pause()
    {
        if (IsPaused) return;
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 恢复游戏到暂停前的 timeScale。
    /// </summary>
    public static void Resume()
    {
        if (!IsPaused) return;
        Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
    }

    /// <summary>
    /// 切换暂停状态。
    /// </summary>
    public static void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    #endregion

    #region Delta Time

    /// <summary>
    /// 当前帧的 DeltaTime（受 timeScale 影响）。等同于 Time.deltaTime。
    /// </summary>
    public static float DeltaTime => Time.deltaTime;

    /// <summary>
    /// 当前帧的非缩放 DeltaTime（不受 timeScale 影响）。等同于 Time.unscaledDeltaTime。
    /// </summary>
    public static float UnscaledDeltaTime => Time.unscaledDeltaTime;

    /// <summary>
    /// FixedUpdate 的固定时间间隔。等同于 Time.fixedDeltaTime。
    /// </summary>
    public static float FixedDeltaTime
    {
        get => Time.fixedDeltaTime;
        set => Time.fixedDeltaTime = value;
    }

    /// <summary>
    /// 使用 Time.smoothDeltaTime 平滑后的 DeltaTime，适合用于 FPS 显示。
    /// </summary>
    public static float SmoothDeltaTime => Time.smoothDeltaTime;

    /// <summary>
    /// 游戏自启动以来经过的时间（受 timeScale 影响）。
    /// </summary>
    public static float TimeSinceStartup => Time.time;

    /// <summary>
    /// 游戏自启动以来经过的时间（不受 timeScale 影响）。
    /// </summary>
    public static float UnscaledTimeSinceStartup => Time.unscaledTime;

    /// <summary>
    /// 总渲染帧数。
    /// </summary>
    public static int FrameCount => Time.frameCount;

    #endregion

    #region FPS 统计

    private static float _fpsAccumulator;
    private static int _fpsFrameCount;
    private static float _fpsUpdateInterval = 0.5f;
    private static float _fpsTimer;
    private static float _currentFPS;
    private static float _averageFPS;

    /// <summary>
    /// 获取当前 FPS。需在每帧调用 <see cref="UpdateFPS"/> 后才准确。
    /// </summary>
    public static float CurrentFPS => _currentFPS;

    /// <summary>
    /// 获取累计平均 FPS。
    /// </summary>
    public static float AverageFPS => _averageFPS;

    /// <summary>
    /// 更新 FPS 统计，应在 MonoBehaviour 的 Update 中每帧调用。
    /// 内部每 0.5 秒刷新一次 FPS 值。
    /// </summary>
    public static void UpdateFPS()
    {
        _fpsTimer += Time.unscaledDeltaTime;
        _fpsAccumulator += 1f / Time.unscaledDeltaTime;
        _fpsFrameCount++;

        if (_fpsTimer >= _fpsUpdateInterval)
        {
            _currentFPS = _fpsAccumulator / _fpsFrameCount;
            _fpsTimer = 0f;
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;
        }
    }

    /// <summary>
    /// 计算指定时间段内的平均 FPS。使用 unscaledDeltaTime，不受时间缩放影响。
    /// </summary>
    /// <param name="interval">统计间隔（秒）</param>
    public static float CalculateFPSOverInterval(float interval)
    {
        if (interval <= 0f) return 0f;
        return 1f / interval;
    }

    /// <summary>
    /// 获取当前累计平均 FPS。需在每帧调用 <see cref="UpdateCumulativeAverageFPS"/> 后才准确。
    /// </summary>
    public static void UpdateCumulativeAverageFPS()
    {
        if (Time.unscaledDeltaTime > 0f)
        {
            float currentFrameFPS = 1f / Time.unscaledDeltaTime;
            _currentFPS = currentFrameFPS;
            _fpsFrameCount++;
            _averageFPS = (_averageFPS * (_fpsFrameCount - 1) + currentFrameFPS) / _fpsFrameCount;
        }
    }

    /// <summary>
    /// 重置平均 FPS 统计。
    /// </summary>
    public static void ResetAverageFPS()
    {
        _averageFPS = 0f;
        _fpsFrameCount = 0;
        _fpsAccumulator = 0f;
        _fpsTimer = 0f;
    }

    #endregion

    #region 秒数格式化

    /// <summary>
    /// 将秒数格式化为 HH:mm:ss 或 mm:ss 格式。
    /// </summary>
    /// <param name="seconds">总秒数</param>
    /// <param name="format">格式："HH:mm:ss" 或 "mm:ss"</param>
    public static string FormatSeconds(float seconds, string format = "HH:mm:ss")
    {
        if (seconds <= 0f)
            return format == "HH:mm:ss" ? "00:00:00" : "00:00";

        int totalSeconds = Mathf.FloorToInt(seconds);
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;

        if (format == "mm:ss")
            return $"{m:D2}:{s:D2}";

        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    /// <summary>
    /// 将秒数格式化为紧凑中文描述，如 "1h30m15s"。
    /// </summary>
    public static string FormatSecondsCompact(float seconds)
    {
        if (seconds <= 0f) return "0s";

        int totalSeconds = Mathf.FloorToInt(seconds);
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;

        string result = string.Empty;
        if (h > 0) result += $"{h}h";
        if (m > 0) result += $"{m}m";
        if (s > 0 || string.IsNullOrEmpty(result)) result += $"{s}s";

        return result;
    }

    /// <summary>
    /// 将秒数格式化为中文可读时长，如 "1小时30分钟15秒"。
    /// </summary>
    public static string FormatSecondsChinese(float seconds)
    {
        if (seconds <= 0f) return "0秒";

        int totalSeconds = Mathf.FloorToInt(seconds);
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;

        string result = string.Empty;
        if (h > 0) result += $"{h}小时";
        if (m > 0) result += $"{m}分钟";
        if (s > 0 || string.IsNullOrEmpty(result)) result += $"{s}秒";

        return result;
    }

    /// <summary>
    /// 将秒数格式化为倒计时显示（mm:ss），小于10分钟时精度到十分之一秒。
    /// </summary>
    /// <param name="seconds">剩余秒数</param>
    /// <param name="highPrecisionThreshold">高精度阈值（秒），低于此值显示 .f 格式</param>
    public static string FormatCountdown(float seconds, float highPrecisionThreshold = 600f)
    {
        if (seconds <= 0f) return "00:00";

        if (seconds < highPrecisionThreshold)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds % 60f;
            return $"{m:D2}:{s:04.1f}";
        }

        int totalSeconds = Mathf.FloorToInt(seconds);
        int mm = totalSeconds / 60;
        int ss = totalSeconds % 60;
        return $"{mm:D2}:{ss:D2}";
    }

    #endregion

    #region 计时器

    /// <summary>
    /// 驱动一个简单倒计时器，到达目标时间后返回 true 并自动重置。
    /// 使用 Time.deltaTime，受 timeScale 影响。
    /// </summary>
    /// <param name="timer">计时器累计值（引用传递，会被修改）</param>
    /// <param name="interval">目标间隔（秒）</param>
    /// <returns>到达间隔时返回 true，否则 false</returns>
    public static bool TickTimer(ref float timer, float interval)
    {
        if (interval <= 0f) return true;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 驱动一个简单倒计时器（不受 timeScale 影响），到达目标时间后返回 true 并自动重置。
    /// </summary>
    /// <param name="timer">计时器累计值（引用传递，会被修改）</param>
    /// <param name="interval">目标间隔（秒）</param>
    /// <returns>到达间隔时返回 true，否则 false</returns>
    public static bool TickTimerUnscaled(ref float timer, float interval)
    {
        if (interval <= 0f) return true;

        timer += Time.unscaledDeltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 驱动一个不自动重置的计时器，到达目标时间后返回 true。
    /// 需手动重置 timer 来重新开始计时。
    /// </summary>
    /// <param name="timer">计时器累计值（引用传递，会被修改）</param>
    /// <param name="interval">目标间隔（秒）</param>
    /// <returns>累计时间 >= interval 时返回 true</returns>
    public static bool TickTimerOnce(ref float timer, float interval)
    {
        if (interval <= 0f) return true;

        timer += Time.deltaTime;
        return timer >= interval;
    }

    /// <summary>
    /// 驱动一个不自动重置的计时器（不受 timeScale 影响），到达目标时间后返回 true。
    /// </summary>
    public static bool TickTimerOnceUnscaled(ref float timer, float interval)
    {
        if (interval <= 0f) return true;

        timer += Time.unscaledDeltaTime;
        return timer >= interval;
    }

    /// <summary>
    /// 获取计时器的进度（0~1），到达间隔时始终返回 1。
    /// </summary>
    /// <param name="timer">当前累计时间</param>
    /// <param name="interval">目标间隔</param>
    /// <returns>0~1 之间的进度值</returns>
    public static float GetTimerProgress(float timer, float interval)
    {
        if (interval <= 0f) return 1f;
        return Mathf.Clamp01(timer / interval);
    }

    #endregion

    #region 时间单位转换

    /// <summary>
    /// 分钟转秒。
    /// </summary>
    public static float MinutesToSeconds(float minutes) => minutes * 60f;

    /// <summary>
    /// 小时转秒。
    /// </summary>
    public static float HoursToSeconds(float hours) => hours * 3600f;

    /// <summary>
    /// 秒转分钟。
    /// </summary>
    public static float SecondsToMinutes(float seconds) => seconds / 60f;

    /// <summary>
    /// 秒转小时。
    /// </summary>
    public static float SecondsToHours(float seconds) => seconds / 3600f;

    /// <summary>
    /// 毫秒转秒。
    /// </summary>
    public static float MillisecondsToSeconds(float milliseconds) => milliseconds / 1000f;

    /// <summary>
    /// 秒转毫秒。
    /// </summary>
    public static float SecondsToMilliseconds(float seconds) => seconds * 1000f;

    #endregion
}