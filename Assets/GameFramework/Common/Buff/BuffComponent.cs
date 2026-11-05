using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFish
{
    #region ===================== Buff系统（纯C#类，无Mono）=====================

/// <summary>
/// Buff接口，定义Buff生命周期
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// public class MyBuff : IBuff
/// {
///     public float RemainTime { get; set; }
///
///     public void OnAdd(IBuffTarget target) { /* 添加Buff效果 */ }
///     public void OnTick(IBuffTarget target, float dt) { /* 每帧效果 */ }
///     public void OnRemove(IBuffTarget target) { /* 移除Buff效果 */ }
/// }
/// </code>
/// </example>
public interface IBuff
{
    /// <summary>剩余持续时间</summary>
    float RemainTime { get; set; }

    /// <summary>Buff添加时调用</summary>
    void OnAdd(IBuffTarget target);

    /// <summary>Buff每帧Tick</summary>
    void OnTick(IBuffTarget target, float deltaTime);

    /// <summary>Buff移除时调用</summary>
    void OnRemove(IBuffTarget target);
}

/// <summary>
/// Buff目标接口，暴露BuffComponent供Buff系统操作
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // FishEntity实现此接口
/// public class FishEntity : MonoBehaviour, IBuffTarget
/// {
///     public BuffComponent BuffComp { get; private set; }
/// }
///
/// // 通过接口添加Buff
/// IBuffTarget target = GetComponent<IBuffTarget>();
/// target.BuffComp.AddBuff(new FishFreezeBuff(3f));
/// </code>
/// </example>
public interface IBuffTarget
{
    /// <summary>Buff组件</summary>
    BuffComponent BuffComp { get; }
}

/// <summary>
/// Buff管理器，纯C#普通类，不继承Mono，实体内部持有
/// <para>管理Buff的添加、移除和Tick</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 实体内部创建
/// BuffComp = new BuffComponent(this);
///
/// // 添加Buff
/// BuffComp.AddBuff(new FishFreezeBuff(3f));
///
/// // 查询Buff
/// var freeze = BuffComp.GetBuff<FishFreezeBuff>();
///
/// // 每帧Tick
/// BuffComp.Tick(Time.deltaTime);
/// </code>
/// </example>
public class BuffComponent
{
    private readonly IBuffTarget _ownerTarget;
    private readonly List<IBuff> _buffList = new List<IBuff>();

    /// <summary>构造Buff管理器</summary>
    public BuffComponent(IBuffTarget ownerTarget)
    {
        _ownerTarget = ownerTarget;
    }

    /// <summary>
    /// 添加Buff
    /// <para>同类型Buff会刷新时长而非叠加</para>
    /// </summary>
    public void AddBuff(IBuff buff)
    {
        if (buff is FishFreezeBuff newFreeze)
        {
            var old = GetBuff<FishFreezeBuff>();
            if (old != null)
            {
                old.RemainTime = newFreeze.RemainTime;
                return;
            }
        }
        _buffList.Add(buff);
        buff.OnAdd(_ownerTarget);
    }

    /// <summary>移除指定Buff</summary>
    public void RemoveBuff(IBuff buff)
    {
        if (_buffList.Contains(buff))
        {
            buff.OnRemove(_ownerTarget);
            _buffList.Remove(buff);
        }
    }

    /// <summary>每帧Tick所有Buff，自动移除超时Buff</summary>
    public void Tick(float deltaTime)
    {
        List<IBuff> removeQueue = new List<IBuff>();
        foreach (var buff in _buffList)
        {
            buff.RemainTime -= deltaTime;
            buff.OnTick(_ownerTarget, deltaTime);
            if (buff.RemainTime <= 0)
            {
                removeQueue.Add(buff);
            }
        }
        foreach (var b in removeQueue)
        {
            RemoveBuff(b);
        }
    }

    /// <summary>清空所有Buff，依次调用OnRemove</summary>
    public void ClearAllBuffs()
    {
        for (int i = _buffList.Count - 1; i >= 0; i--)
        {
            _buffList[i].OnRemove(_ownerTarget);
            _buffList.RemoveAt(i);
        }
    }

    /// <summary>获取指定类型的Buff，不存在返回null</summary>
    public T GetBuff<T>() where T : class, IBuff
    {
        foreach (var b in _buffList)
        {
            if (b is T tb) return tb;
        }
        return null;
    }
}

/// <summary>
/// 鱼冰冻Buff（只作用鱼）
/// <para>添加时切换到冰冻状态，移除时恢复游动状态</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 对鱼添加3秒冰冻
/// fish.BuffComp.AddBuff(new FishFreezeBuff(3f));
///
/// // 冰冻结束时自动恢复游动
/// </code>
/// </example>
public class FishFreezeBuff : IBuff
{
    /// <summary>剩余时间</summary>
    public float RemainTime { get; set; }

    /// <summary>构造冰冻Buff</summary>
    /// <param name="duration">冰冻持续时间（秒）</param>
    public FishFreezeBuff(float duration) => RemainTime = duration;

    /// <summary>添加时切换到冰冻状态</summary>
    public void OnAdd(IBuffTarget target)
    {
        if (target is FishEntity fish)
        {
            fish.StateMachine.SwitchState(new FishFreezeState());
        }
    }

    /// <summary>Tick中无操作</summary>
    public void OnTick(IBuffTarget target, float deltaTime)
    {
    }

    /// <summary>移除时恢复游动状态</summary>
    public void OnRemove(IBuffTarget target)
    {
        if (target is FishEntity fish)
        {
            fish.StateMachine.SwitchState(new FishSwimState());
        }
    }
}

#endregion
}