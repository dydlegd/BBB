using System;
using UnityEngine;

namespace GameFish
{

#region ===================== 数据结构 =====================

/// <summary>
/// 伤害信息，承载一次命中事件的完整上下文
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// var info = new DamageInfo
/// {
///     Damage = 10,
///     HitPoint = hit.point,
///     Attacker = gameObject
/// };
/// target.TakeDamage(info);
/// </code>
/// </example>
public struct DamageInfo
{
    /// <summary>伤害数值</summary>
    public int Damage;

    /// <summary>命中点世界坐标</summary>
    public Vector3 HitPoint;

    /// <summary>命中方向（法线）</summary>
    public Vector3 HitNormal;

    /// <summary>攻击者GameObject</summary>
    public GameObject Attacker;
}

#endregion

#region ===================== 功能接口 =====================

/// <summary>
/// 移动能力接口，提供朝向、速度和位移功能
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// IMovable movable = GetComponent<IMovable>();
/// movable.Forward = Vector3.right;
/// movable.MoveSpeed = 5f;
/// movable.Move(dir * speed, Time.deltaTime);
/// movable.ResetMoveSpeed();
/// </code>
/// </example>
public interface IMovable
{
    /// <summary>当前朝向</summary>
    Vector3 Forward { get; set; }

    /// <summary>当前移动速度</summary>
    float MoveSpeed { get; set; }

    /// <summary>沿指定速度向量移动</summary>
    /// <param name="velocity">速度向量（方向 * 速度值）</param>
    /// <param name="dt">帧间隔</param>
    void Move(Vector3 velocity, float dt);

    /// <summary>将当前移动速度重置为基础速度</summary>
    void ResetMoveSpeed();
}

/// <summary>
/// 受伤/死亡数据层接口，管理HP和伤害判定，通过事件驱动表现层
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// IDamageable target = hit.collider.GetComponent<IDamageable>();
/// target.OnDamaged += (info) => Debug.Log($"受伤 {info.Damage} 点");
/// target.OnDeath += () => Debug.Log("死亡");
/// target.TakeDamage(new DamageInfo { Damage = 10, Attacker = gameObject });
/// </code>
/// </example>
public interface IDamageable
{
    /// <summary>当前生命值</summary>
    int HP { get; }

    /// <summary>最大生命值</summary>
    int MaxHP { get; }

    /// <summary>是否存活</summary>
    bool IsAlive { get; }

    /// <summary>受到伤害</summary>
    void TakeDamage(DamageInfo info);

    /// <summary>受击事件（DamageInfo）</summary>
    event Action<DamageInfo> OnDamaged;

    /// <summary>死亡事件</summary>
    event Action OnDeath;
}

/// <summary>
/// 受伤/死亡表现效果接口，负责播放视觉、音效等反馈
/// <para>与 IDamageable（数据层）分离，通过事件驱动连接</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 在Fish内部，Awake中绑定数据层事件到表现层
/// _onDamaged += (info) => PlayHitEffect(info);
/// _onDeath += () => PlayDeathEffect();
///
/// // 实现表现效果
/// public void PlayHitEffect(DamageInfo info)
/// {
///     flashEffect.Play();
///     audioSource.PlayOneShot(hitSound);
/// }
/// </code>
/// </example>
public interface IDamageEffect
{
    /// <summary>播放受击效果（闪白、粒子、音效等）</summary>
    void PlayHitEffect(DamageInfo info);

    /// <summary>播放死亡效果（死亡动画、爆炸粒子、掉落等）</summary>
    void PlayDeathEffect();
}

/// <summary>
/// 动画控制接口，封装动画播放
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// IAnimatable anim = GetComponent<IAnimatable>();
/// anim.PlayAnim("swim");
/// anim.PlayAnim("dead");
/// </code>
/// </example>
public interface IAnimatable
{
    /// <summary>播放指定名称的动画</summary>
    void PlayAnim(string animName);

    /// <summary>当前动画是否已播完（normalizedTime >= 1.0）</summary>
    bool IsAnimFinished();
}

/// <summary>
/// 游动策略运行时数据上下文，策略通过此接口读写动态参数
/// <para>每个策略只访问自己需要的字段，互不干扰</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 策略通过 ISwimContext 读写参数
/// public Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt)
/// {
///     context.WavePhase += dt * context.WaveFrequency;
///     return currentRotation;
/// }
///
/// // 切换策略时重置所有运行时数据
/// context.ResetAll();
/// </code>
/// </example>
public interface ISwimContext
{
    /// <summary>SineWave：当前相位</summary>
    float WavePhase { get; set; }

    /// <summary>SineWave：频率</summary>
    float WaveFrequency { get; set; }

    /// <summary>SineWave：振幅</summary>
    float WaveAmplitude { get; set; }

    /// <summary>RandomTurn：转向倒计时</summary>
    float TurnCountdown { get; set; }

    /// <summary>RandomTurn：最小转向间隔</summary>
    float TurnIntervalMin { get; set; }

    /// <summary>RandomTurn：最大转向间隔</summary>
    float TurnIntervalMax { get; set; }

    /// <summary>RandomTurn：偏转角度范围（±值）</summary>
    float TurnAngleRange { get; set; }

    /// <summary>Circle：绕圈旋转角度（已废弃，不再使用）</summary>
    float CircleAngle { get; set; }

    /// <summary>Circle：绕圈旋转速度</summary>
    float CircleRotateSpeed { get; set; }

    /// <summary>重置所有策略运行时参数</summary>
    void ResetAll();
}

#endregion

#region ===================== SwimData 实现 =====================

/// <summary>
/// 游动策略运行时数据，ISwimContext的默认实现
/// <para>纯数据类，被Fish持有，在OnSpawn时从FishConfig初始化参数</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// var data = new SwimData();
/// data.WaveFrequency = 3f;
/// data.WaveAmplitude = 0.5f;
/// data.ResetAll(); // 切换策略前重置
/// </code>
/// </example>
public class SwimData : ISwimContext
{
    /// <summary>SineWave：当前相位</summary>
    public float WavePhase { get; set; }

    /// <summary>SineWave：频率</summary>
    public float WaveFrequency { get; set; }

    /// <summary>SineWave：振幅</summary>
    public float WaveAmplitude { get; set; }

    /// <summary>RandomTurn：转向倒计时</summary>
    public float TurnCountdown { get; set; }

    /// <summary>RandomTurn：最小转向间隔</summary>
    public float TurnIntervalMin { get; set; }

    /// <summary>RandomTurn：最大转向间隔</summary>
    public float TurnIntervalMax { get; set; }

    /// <summary>RandomTurn：偏转角度范围（±值）</summary>
    public float TurnAngleRange { get; set; }

    /// <summary>Circle：当前绕圈角度（已废弃，CircleSwim改为基于Forward相对旋转）</summary>
    public float CircleAngle { get; set; }

    /// <summary>Circle：绕圈旋转速度</summary>
    public float CircleRotateSpeed { get; set; }

    /// <summary>重置所有策略运行时参数</summary>
    public void ResetAll()
    {
        WavePhase = 0f;
        TurnCountdown = 0f;
        CircleAngle = 0f;
    }
}

#endregion
}