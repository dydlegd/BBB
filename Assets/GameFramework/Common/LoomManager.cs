using System;
using System.Collections.Generic;
using UnityEngine;

public class LoomManager : MonoBehaviour
{
    private static LoomManager instance;
    private static readonly object lockObj = new object();
    public static LoomManager Instance
    {
        get
        {
            if (instance == null)
            {
                lock (lockObj)
                {
                    if (instance == null)
                    {
                        instance = FindObjectOfType<LoomManager>();
                        if (instance == null)
                        {
                            GameObject obj = new GameObject("LoomManager");
                            instance = obj.AddComponent<LoomManager>();
                            DontDestroyOnLoad(obj);
                        }
                    }
                }
            }
            return instance;
        }
    }

    private Queue<Action> writeQueue = new Queue<Action>();
    private Queue<Action> readQueue = new Queue<Action>();
    private volatile bool hasActions = false;

    public static void QueueOnMainThread(Action action)
    {
        if (action == null) return;
        Instance.QueueAction(action);
    }

    public static void QueueOnMainThread(Action<object> action, object state)
    {
        if (action == null) return;
        Instance.QueueAction(() => action(state));
    }

    public static void QueueOnMainThread<T>(Action<T> action, T param)
    {
        if (action == null) return;
        Instance.QueueAction(() => action(param));
    }

    public static void QueueOnMainThread<T1, T2>(Action<T1, T2> action, T1 param1, T2 param2)
    {
        if (action == null) return;
        Instance.QueueAction(() => action(param1, param2));
    }

    private void QueueAction(Action action)
    {
        lock (lockObj)
        {
            writeQueue.Enqueue(action);
            hasActions = true;
        }
    }

    private void Update()
    {
        if (!hasActions) return;

        Queue<Action> toRun;
        lock (lockObj)
        {
            toRun = writeQueue;
            writeQueue = readQueue;
            readQueue = toRun;
            hasActions = false;
        }

        while (toRun.Count > 0)
        {
            try
            {
                toRun.Dequeue().Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    private void OnDestroy()
    {
        lock (lockObj)
        {
            writeQueue.Clear();
            readQueue.Clear();
            hasActions = false;
        }
        instance = null;
    }
}