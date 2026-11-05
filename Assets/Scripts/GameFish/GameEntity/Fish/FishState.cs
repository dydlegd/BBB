using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFish
{
    #region ===================== 鱼 状态体系（独立，不和炮台共享）=====================

/// <summary>
/// 鱼的状态接口，定义状态生命周期
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 切换状态
/// stateMachine.SwitchState(new FishSwimState());
/// stateMachine.SwitchState(new FishFreezeState());
///
/// // 每帧Tick
/// stateMachine.Tick(Time.deltaTime);
///
/// // 检查当前状态类型
/// if (stateMachine.IsCurrentState<FishFreezeState>())
///     // 处理冰冻逻辑
/// </code>
/// </example>
public interface IFishState
{
    /// <summary>进入状态时调用</summary>
    void OnEnter(FishEntity fish);

    /// <summary>状态每帧Tick</summary>
    void OnTick(FishEntity fish, float deltaTime);

    /// <summary>退出状态时调用</summary>
    void OnExit(FishEntity fish);
}

/// <summary>
/// 鱼：游动状态
/// <para>正常游动，每帧调用MoveLogic根据当前策略移动</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// stateMachine.SwitchState(new FishSwimState());
/// // 自动播放swim动画、重置速度为BaseMoveSpeed
/// // 每帧根据当前ISwimStrategy计算方向并移动
/// </code>
/// </example>
public class FishSwimState : IFishState
{
    /// <summary>进入游动：重置速度、播放swim动画</summary>
    public void OnEnter(FishEntity fish)
    {
        fish.ResetMoveSpeed();
        fish.PlayAnim("swim");
    }

    /// <summary>每帧执行移动逻辑</summary>
    public void OnTick(FishEntity fish, float deltaTime)
    {
        fish.MoveLogic(deltaTime);
    }

    /// <summary>退出游动</summary>
    public void OnExit(FishEntity fish)
    {
    }
}

/// <summary>
/// 鱼：冰冻状态
/// <para>被冰冻Buff触发，冻结期间不移动</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 通过Buff系统自动切换
/// buffComp.AddBuff(new FishFreezeBuff(3f));
/// // Buff添加时自动 → SwitchState(FishFreezeState)
/// // Buff结束时自动 → SwitchState(FishSwimState)
/// </code>
/// </example>
public class FishFreezeState : IFishState
{
    /// <summary>进入冰冻：播放freeze动画</summary>
    public void OnEnter(FishEntity fish)
    {
        fish.PlayAnim("freeze");
    }

    /// <summary>冰冻期间不移动</summary>
    public void OnTick(FishEntity fish, float deltaTime)
    {
    }

    /// <summary>退出冰冻：恢复swim动画</summary>
    public void OnExit(FishEntity fish)
    {
        fish.PlayAnim("swim");
    }
}

/// <summary>
/// 鱼：死亡状态
/// <para>死亡时触发，播放死亡动画，动画播完后直接回收</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // HP归零时由 IDamageable 自动触发
/// // PlayDeathEffect() → DropGold() → SwitchState(FishDeadState)
/// // 动画播完后自动回收
/// </code>
/// </example>
public class FishDeadState : IFishState
{
    /// <summary>进入死亡：播放dead动画</summary>
    public void OnEnter(FishEntity fish)
    {
        fish.PlayAnim("dead");
    }

    /// <summary>等待动画播完后回收</summary>
    public void OnTick(FishEntity fish, float deltaTime)
    {
        if (fish.IsAnimFinished())
        {
            fish.RecycleToPool();
        }
    }

    /// <summary>退出死亡</summary>
    public void OnExit(FishEntity fish)
    {
    }
}

/// <summary>
/// 鱼专用状态机，纯C#，非Mono
/// <para>持有当前状态，提供SwitchState/Tick/IsCurrentState方法</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // FishEntity内部创建
/// StateMachine = new FishStateMachine(this);
///
/// // 切换状态
/// StateMachine.SwitchState(new FishSwimState());
///
/// // 每帧更新
/// StateMachine.Tick(Time.deltaTime);
///
/// // 检查状态
/// bool isDead = StateMachine.IsCurrentState<FishDeadState>();
/// </code>
/// </example>
public class FishStateMachine
{
    private IFishState _currentState;
    private readonly FishEntity _owner;

    /// <summary>构造状态机</summary>
    public FishStateMachine(FishEntity owner)
    {
        _owner = owner;
    }

    /// <summary>切换状态，旧状态OnExit → 新状态OnEnter，传null可清空状态</summary>
    public void SwitchState(IFishState newState)
    {
        if (_currentState == newState) return;
        _currentState?.OnExit(_owner);
        _currentState = newState;
        _currentState?.OnEnter(_owner);
    }

    /// <summary>每帧Tick当前状态</summary>
    public void Tick(float deltaTime)
    {
        _currentState?.OnTick(_owner, deltaTime);
    }

    /// <summary>判断当前状态是否为指定类型</summary>
    public bool IsCurrentState<T>() where T : class, IFishState
    {
        return _currentState is T;
    }
}

#endregion
}