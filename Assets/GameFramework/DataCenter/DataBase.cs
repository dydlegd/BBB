using System;
using UnityEngine;

/// <summary>
/// 通用数据基类（CRTP 泛型），带生命周期与数据变更事件。
///
/// <para>设计约定：</para>
/// <list type="bullet">
/// <item><description>泛型参数 T 为子类自身（CRTP），约束 where T : DataBase<T>，回调直接携带强类型数据。</description></item>
/// <item><description>构造即完成初始化（OnInit 在构造函数中回调）；Release 幂等，重复调用为空操作，释放时自动清空所有订阅。</description></item>
/// <item><description>子类通过 NotifyChanged 上报变更，不要直接暴露 event 的 Invoke。</description></item>
/// </list>
///
/// <para>使用示例：</para>
/// <code>
/// // ===== 定义业务数据 =====
/// public class PlayerData : DataBase<PlayerData>
/// {
///     private int _level;
///     public int Level => _level;
///
///     public void AddLevel(int delta)
///     {
///         _level += delta;
///         NotifyChanged();  // 上报变更，触发事件
///     }
///
///     protected override void OnRelease() { /* 存档/清理逻辑 */ }
/// }
///
/// // ===== 生命周期 =====
/// PlayerData data = new PlayerData();  // 构造即初始化
/// data.Release();     // 释放：回调 OnRelease，清空所有订阅
/// data.Release();     // 重复调用：空操作
///
/// // ===== 订阅变更（回调直接携带强类型数据，注册时立即回调一次，刷新 UI 无需额外初始化代码）=====
/// data.RegisterChanged((d) =&gt; uiLevel.text = d.Level.ToString());
///
/// // 常规订阅 / 撤销
/// Action&lt;PlayerData&gt; onChanged = (d) =&gt; Debug.Log($"Level: {d.Level}");
/// data.RegisterChanged(onChanged);
/// data.UnregisterChanged(onChanged);
/// </code>
/// </summary>
public abstract class DataBase<T> where T : DataBase<T>
{
    /// <summary>数据变更事件（订阅者集合，Release 时清空），回调签名携带强类型数据本身</summary>
    private event Action<T> OnChanged;

    /// <summary>是否已释放（Release 幂等守卫）</summary>
    private bool _isReleased;

    /// <summary>
    /// 构造函数，完成初始化并回调 <see cref="OnInit"/>。
    /// <para>注意：OnInit 在基类构造函数中回调，此时子类字段尚未初始化（子类构造体之后才执行），
    /// 覆写 OnInit 时不要读取子类实例字段，仅做不依赖子类状态的工作。</para>
    /// </summary>
    protected DataBase()
    {
        OnInit();
    }

    /// <summary>
    /// 释放数据。
    /// <para>清空所有变更事件订阅，重复调用为空操作（幂等守卫）。</para>
    /// <para>仅首次调用时回调 <see cref="OnRelease"/>。</para>
    /// </summary>
    public void Release()
    {
        if (_isReleased) return;
        _isReleased = true;

        OnChanged = null;
        OnRelease();
    }

    /// <summary>初始化回调，构造函数中触发。子类覆写加载/恢复逻辑，注意此时子类字段尚未初始化</summary>
    protected virtual void OnInit() { }

    /// <summary>释放回调，Release 时触发。子类覆写存档/清理逻辑</summary>
    protected virtual void OnRelease() { }

    #region Change Events

    /// <summary>
    /// 注册数据变更回调。
    /// <para>回调携带强类型数据本身，订阅者无需外部闭包捕获。</para>
    /// <para>invokeImmediately 为 true 时，注册后立即回调一次，常用于 UI 初始刷新，避免重复的初始化代码。</para>
    /// <para>Release 后调用本方法为空操作（输出错误日志），已释放的实体不再接受订阅。</para>
    /// </summary>
    /// <param name="onChanged">变更回调，签名 (T data)，不能为 null</param>
    /// <param name="invokeImmediately">注册后是否立即回调一次</param>
    public void RegisterChanged(Action<T> onChanged, bool invokeImmediately = true)
    {
        if (_isReleased)
        {
            Debug.LogError($"[DataBase] RegisterChanged: 实体已释放，拒绝订阅 | Type: {GetType().Name}");
            return;
        }

        if (onChanged == null)
        {
            Debug.LogError($"[DataBase] RegisterChanged: 回调为 null | Type: {GetType().Name}");
            return;
        }

        OnChanged += onChanged;

        if (invokeImmediately)
        {
            onChanged((T)this);
        }
    }

    /// <summary>
    /// 撤销数据变更回调。
    /// <para>未注册时为空操作，重复撤销安全。</para>
    /// </summary>
    /// <param name="onChanged">要撤销的回调</param>
    public void UnregisterChanged(Action<T> onChanged)
    {
        if (onChanged == null) return;
        OnChanged -= onChanged;
    }

    /// <summary>
    /// 上报数据变更，触发所有订阅回调，回调参数为数据本身（this）。
    /// <para>子类在数据被修改后调用此方法。</para>
    /// <para>Release 后订阅已清空，调用本方法为空操作。</para>
    /// </summary>
    protected void NotifyChanged()
    {
        // 拷贝到局部变量再回调：回调内 UnregisterChanged 时不会导致遍历异常
        Action<T> handler = OnChanged;
        handler?.Invoke((T)this);
    }

    #endregion
}
