using System;
using System.Collections.Generic;
using UnityEngine;

/*  ============================================================
 *  使用示例 (Usage Examples)
 *  ============================================================
 *
 *  1. 无参事件
 *  -----------------------------------------------------------------
 *  // 注册
 *  EventManager.Instance.AddListener("OnGameStart", OnGameStart);
 *  // 触发
 *  EventManager.Instance.Dispatch("OnGameStart");
 *  // 移除
 *  EventManager.Instance.RemoveListener("OnGameStart", OnGameStart);
 *
 *  2. 单参事件
 *  -----------------------------------------------------------------
 *  EventManager.Instance.AddListener<int>("OnScoreChanged", OnScoreChanged);
 *  EventManager.Instance.Dispatch("OnScoreChanged", 100);
 *  EventManager.Instance.RemoveListener<int>("OnScoreChanged", OnScoreChanged);
 *
 *  3. 双参事件
 *  -----------------------------------------------------------------
 *  EventManager.Instance.AddListener<string, int>("OnItemCollected", OnItemCollected);
 *  EventManager.Instance.Dispatch("OnItemCollected", "金币", 50);
 *  EventManager.Instance.RemoveListener<string, int>("OnItemCollected", OnItemCollected);
 *
 *  4. 三参事件
 *  -----------------------------------------------------------------
 *  EventManager.Instance.AddListener<int, float, bool>("OnDamage", OnDamage);
 *  EventManager.Instance.Dispatch("OnDamage", 50, 0.5f, true);
 *  EventManager.Instance.RemoveListener<int, float, bool>("OnDamage", OnDamage);
 *
 *  5. 四参事件
 *  -----------------------------------------------------------------
 *  EventManager.Instance.AddListener<string, int, float, Vector3>("OnSpawn", OnSpawn);
 *  EventManager.Instance.Dispatch("OnSpawn", "Enemy", 3, 1.5f, Vector3.zero);
 *  EventManager.Instance.RemoveListener<string, int, float, Vector3>("OnSpawn", OnSpawn);
 *
 *  ============================================================
 */

/// <summary>
/// 通用事件管理器，支持 0~4 个参数的全局事件注册、触发与移除。
/// 通过 EventManager.Instance 单例访问。
/// </summary>
public class EventManager
{
    private static EventManager instance;

    /// <summary>
    /// 单例实例
    /// </summary>
    public static EventManager Instance
    {
        get
        {
            if (instance == null)
                instance = new EventManager();
            return instance;
        }
    }

    /// <summary>
    /// 事件字典，key 为事件名，value 为对应的委托链
    /// </summary>
    private Dictionary<string, Delegate> eventDict = new Dictionary<string, Delegate>();

    #region AddListener（注册事件）

    /// <summary>
    /// 注册无参事件监听
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">回调函数</param>
    public void AddListener(string eventName, Action handler)
    {
        if (handler == null) return;
        eventDict[eventName] = eventDict.TryGetValue(eventName, out Delegate existing)
            ? (Action)existing + handler
            : handler;
    }

    /// <summary>
    /// 注册单参事件监听
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">回调函数，接收一个参数</param>
    public void AddListener<T>(string eventName, Action<T> handler)
    {
        if (handler == null) return;
        eventDict[eventName] = eventDict.TryGetValue(eventName, out Delegate existing)
            ? (Action<T>)existing + handler
            : handler;
    }

    /// <summary>
    /// 注册双参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">回调函数，接收两个参数</param>
    public void AddListener<T1, T2>(string eventName, Action<T1, T2> handler)
    {
        if (handler == null) return;
        eventDict[eventName] = eventDict.TryGetValue(eventName, out Delegate existing)
            ? (Action<T1, T2>)existing + handler
            : handler;
    }

    /// <summary>
    /// 注册三参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">回调函数，接收三个参数</param>
    public void AddListener<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler)
    {
        if (handler == null) return;
        eventDict[eventName] = eventDict.TryGetValue(eventName, out Delegate existing)
            ? (Action<T1, T2, T3>)existing + handler
            : handler;
    }

    /// <summary>
    /// 注册四参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">回调函数，接收四个参数</param>
    public void AddListener<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> handler)
    {
        if (handler == null) return;
        eventDict[eventName] = eventDict.TryGetValue(eventName, out Delegate existing)
            ? (Action<T1, T2, T3, T4>)existing + handler
            : handler;
    }

    #endregion

    #region RemoveListener（移除事件）

    /// <summary>
    /// 移除无参事件监听
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的回调函数，必须与注册时是同一个委托实例</param>
    public void RemoveListener(string eventName, Action handler)
    {
        if (handler == null) return;
        RemoveDelegate(eventName, handler);
    }

    /// <summary>
    /// 移除单参事件监听
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的回调函数，必须与注册时是同一个委托实例</param>
    public void RemoveListener<T>(string eventName, Action<T> handler)
    {
        if (handler == null) return;
        RemoveDelegate(eventName, handler);
    }

    /// <summary>
    /// 移除双参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的回调函数，必须与注册时是同一个委托实例</param>
    public void RemoveListener<T1, T2>(string eventName, Action<T1, T2> handler)
    {
        if (handler == null) return;
        RemoveDelegate(eventName, handler);
    }

    /// <summary>
    /// 移除三参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的回调函数，必须与注册时是同一个委托实例</param>
    public void RemoveListener<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler)
    {
        if (handler == null) return;
        RemoveDelegate(eventName, handler);
    }

    /// <summary>
    /// 移除四参事件监听
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的回调函数，必须与注册时是同一个委托实例</param>
    public void RemoveListener<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> handler)
    {
        if (handler == null) return;
        RemoveDelegate(eventName, handler);
    }

    /// <summary>
    /// 移除指定事件名下所有监听
    /// </summary>
    /// <param name="eventName">事件名</param>
    public void RemoveAllListeners(string eventName)
    {
        eventDict.Remove(eventName);
    }

    /// <summary>
    /// 移除全部事件的所有监听，清空事件字典
    /// </summary>
    public void RemoveAllListeners()
    {
        eventDict.Clear();
    }

    /// <summary>
    /// 从事件字典中移除指定委托（内部通用实现）
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="handler">要移除的委托</param>
    private void RemoveDelegate(string eventName, Delegate handler)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Delegate result = Delegate.Remove(existing, handler);
            if (result == null)
                eventDict.Remove(eventName);
            else
                eventDict[eventName] = result;
        }
    }

    #endregion

    #region Dispatch（触发事件）

    /// <summary>
    /// 触发无参事件
    /// </summary>
    /// <param name="eventName">事件名</param>
    public void Dispatch(string eventName)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Action handler = existing as Action;
            if (handler != null)
                handler.Invoke();
            else
                Debug.LogError(string.Format("EventManager: Event \"{0}\" has incompatible delegate type.", eventName));
        }
    }

    /// <summary>
    /// 触发单参事件
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="arg">事件参数</param>
    public void Dispatch<T>(string eventName, T arg)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Action<T> handler = existing as Action<T>;
            if (handler != null)
                handler.Invoke(arg);
            else
                Debug.LogError(string.Format("EventManager: Event \"{0}\" has incompatible delegate type.", eventName));
        }
    }

    /// <summary>
    /// 触发双参事件
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="arg1">事件参数1</param>
    /// <param name="arg2">事件参数2</param>
    public void Dispatch<T1, T2>(string eventName, T1 arg1, T2 arg2)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Action<T1, T2> handler = existing as Action<T1, T2>;
            if (handler != null)
                handler.Invoke(arg1, arg2);
            else
                Debug.LogError(string.Format("EventManager: Event \"{0}\" has incompatible delegate type.", eventName));
        }
    }

    /// <summary>
    /// 触发三参事件
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="arg1">事件参数1</param>
    /// <param name="arg2">事件参数2</param>
    /// <param name="arg3">事件参数3</param>
    public void Dispatch<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Action<T1, T2, T3> handler = existing as Action<T1, T2, T3>;
            if (handler != null)
                handler.Invoke(arg1, arg2, arg3);
            else
                Debug.LogError(string.Format("EventManager: Event \"{0}\" has incompatible delegate type.", eventName));
        }
    }

    /// <summary>
    /// 触发四参事件
    /// </summary>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="eventName">事件名</param>
    /// <param name="arg1">事件参数1</param>
    /// <param name="arg2">事件参数2</param>
    /// <param name="arg3">事件参数3</param>
    /// <param name="arg4">事件参数4</param>
    public void Dispatch<T1, T2, T3, T4>(string eventName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (eventDict.TryGetValue(eventName, out Delegate existing))
        {
            Action<T1, T2, T3, T4> handler = existing as Action<T1, T2, T3, T4>;
            if (handler != null)
                handler.Invoke(arg1, arg2, arg3, arg4);
            else
                Debug.LogError(string.Format("EventManager: Event \"{0}\" has incompatible delegate type.", eventName));
        }
    }

    #endregion

    /// <summary>
    /// 检查指定事件名是否有监听者
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <returns>存在监听者返回 true，否则返回 false</returns>
    public bool HasListener(string eventName)
    {
        return eventDict.ContainsKey(eventName);
    }
}