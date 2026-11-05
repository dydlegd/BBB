using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 通用协程工具类。
/// 提供延迟执行、等待条件、序列协程、帧等待等常用协程封装。
/// 依赖 MonoBehaviour 运行协程，可通过 LoomManager 或任意 MonoBehaviour 调用。
/// </summary>
/// <example>延迟执行
/// <code>
/// // 需在 MonoBehaviour 中调用
/// StartCoroutine(CoroutineUtility.Delay(2f, () => Debug.Log("2秒后执行")));
/// StartCoroutine(CoroutineUtility.DelayRealtime(3f, () => Debug.Log("不受TimeScale影响")));
/// </code>
/// </example>
/// <example>等待条件
/// <code>
/// StartCoroutine(CoroutineUtility.WaitUntil(
///     () => player.HP <= 0,
///     () => Debug.Log("玩家死亡")
/// ));
///
/// StartCoroutine(CoroutineUtility.WaitWhile(
///     () => isAnimating,
///     () => Debug.Log("动画结束")
/// ));
/// </code>
/// </example>
/// <example>序列与并行
/// <code>
/// // 顺序执行
/// StartCoroutine(CoroutineUtility.Sequence(
///     CoroutineUtility.Delay(1f, () => Step1()),
///     CoroutineUtility.Delay(0.5f, () => Step2()),
///     CoroutineUtility.Delay(0.5f, () => Step3())
/// ));
/// </code>
/// </example>
/// <example>等待帧
/// <code>
/// StartCoroutine(CoroutineUtility.WaitForFrames(3, () => Debug.Log("3帧后")));
/// StartCoroutine(CoroutineUtility.WaitForEndOfFrame(() => Debug.Log("帧末")));
/// </code>
/// </example>
public static class CoroutineUtility
{
    #region 延迟执行

    /// <summary>
    /// 延迟指定秒数后执行回调（受 Time.timeScale 影响）。
    /// </summary>
    public static IEnumerator Delay(float seconds, Action callback)
    {
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        else
            yield return null;

        callback?.Invoke();
    }

    /// <summary>
    /// 延迟指定秒数后执行回调（不受 Time.timeScale 影响）。
    /// </summary>
    public static IEnumerator DelayRealtime(float seconds, Action callback)
    {
        if (seconds > 0f)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return null;

        callback?.Invoke();
    }

    /// <summary>
    /// 延迟到下一帧执行回调。
    /// </summary>
    public static IEnumerator DelayNextFrame(Action callback)
    {
        yield return null;
        callback?.Invoke();
    }

    #endregion

    #region 等待帧

    /// <summary>
    /// 等待指定帧数后执行回调。
    /// </summary>
    public static IEnumerator WaitForFrames(int frameCount, Action callback)
    {
        for (int i = 0; i < frameCount; i++)
            yield return null;

        callback?.Invoke();
    }

    /// <summary>
    /// 等待到当前帧结束（所有渲染完成后）执行回调。
    /// </summary>
    public static IEnumerator WaitForEndOfFrame(Action callback)
    {
        yield return new WaitForEndOfFrame();
        callback?.Invoke();
    }

    /// <summary>
    /// 等待到固定更新（FixedUpdate）后执行回调。
    /// </summary>
    public static IEnumerator WaitForFixedUpdate(Action callback)
    {
        yield return new WaitForFixedUpdate();
        callback?.Invoke();
    }

    #endregion

    #region 等待条件

    /// <summary>
    /// 等待直到条件为 true 时执行回调。
    /// </summary>
    public static IEnumerator WaitUntil(Func<bool> condition, Action callback)
    {
        if (condition == null) yield break;

        while (!condition())
            yield return null;

        callback?.Invoke();
    }

    /// <summary>
    /// 等待直到条件为 true，可设置超时时间。
    /// </summary>
    public static IEnumerator WaitUntil(Func<bool> condition, Action callback, float timeout, Action onTimeout = null)
    {
        if (condition == null) yield break;

        float elapsed = 0f;
        while (!condition())
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                onTimeout?.Invoke();
                yield break;
            }
            yield return null;
        }

        callback?.Invoke();
    }

    /// <summary>
    /// 等待条件为 false 时执行回调（与 WaitUntil 相反）。
    /// </summary>
    public static IEnumerator WaitWhile(Func<bool> condition, Action callback)
    {
        if (condition == null) yield break;

        while (condition())
            yield return null;

        callback?.Invoke();
    }

    /// <summary>
    /// 等待指定对象不为 null 时执行回调。
    /// </summary>
    public static IEnumerator WaitForNotNull<T>(Func<T> getter, Action<T> callback) where T : class
    {
        if (getter == null) yield break;

        T result = null;
        while (result == null)
        {
            result = getter();
            yield return null;
        }

        callback?.Invoke(result);
    }

    #endregion

    #region 序列

    /// <summary>
    /// 按顺序依次执行多个协程，每个完成后执行下一个。
    /// </summary>
    public static IEnumerator Sequence(params IEnumerator[] routines)
    {
        if (routines == null) yield break;

        foreach (IEnumerator routine in routines)
        {
            if (routine != null)
                yield return routine;
        }
    }

    /// <summary>
    /// 执行一个协程并在完成后调用回调。
    /// </summary>
    public static IEnumerator WithCallback(IEnumerator routine, Action onComplete)
    {
        if (routine != null)
            yield return routine;

        onComplete?.Invoke();
    }

    #endregion

    #region 重复

    /// <summary>
    /// 重复执行指定次数，每次间隔 interval 秒。
    /// </summary>
    public static IEnumerator Repeat(int count, float interval, Action<int> onEach, Action onComplete = null)
    {
        for (int i = 0; i < count; i++)
        {
            onEach?.Invoke(i);
            if (i < count - 1 && interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 无限循环执行，每次间隔 interval 秒。需外部通过 StopCoroutine 停止。
    /// </summary>
    public static IEnumerator Loop(float interval, Action onEach)
    {
        while (true)
        {
            onEach?.Invoke();
            if (interval > 0f)
                yield return new WaitForSeconds(interval);
            else
                yield return null;
        }
    }

    #endregion

    #region 渐变

    /// <summary>
    /// 在指定时间内执行渐变，每帧回调当前进度（0~1）。
    /// </summary>
    public static IEnumerator Lerp(float duration, Action<float> onUpdate, Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            onUpdate?.Invoke(t);
            yield return null;
        }

        onUpdate?.Invoke(1f);
        onComplete?.Invoke();
    }

    /// <summary>
    /// 在指定时间内执行渐变（不受 Time.timeScale 影响），每帧回调当前进度（0~1）。
    /// </summary>
    public static IEnumerator LerpRealtime(float duration, Action<float> onUpdate, Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            onUpdate?.Invoke(t);
            yield return null;
        }

        onUpdate?.Invoke(1f);
        onComplete?.Invoke();
    }

    #endregion
}