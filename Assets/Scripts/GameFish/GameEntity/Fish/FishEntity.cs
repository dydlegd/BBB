using System;
using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== 实体：FishEntity（继承 EntityBase，组合功能接口）=====================

/// <summary>
/// 鱼实体类，继承 EntityBase，通过组合多个功能接口实现完整游戏逻辑。
/// <para>实体创建由 FishConfig.CreateEntity() 委托，无需手动注册工厂。</para>
/// <para>实现接口：IMovable（移动）、IDamageable（受伤数据）、IDamageEffect（受伤表现）、IBuffTarget（Buff）、IAnimatable（动画）</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// FishEntity fish = EntityManager.Instance.SpawnById<FishConfig, FishEntity>(1001, spawnPoint);
///
/// fish.TakeDamage(new DamageInfo { Damage = 10, Attacker = bullet });
/// fish.OnDamaged += (info) => Debug.Log($"受伤: {info.Damage}");
/// fish.OnDeath += () => Debug.Log("鱼死了");
///
/// fish.SetSwimStrategy(SineWaveSwim.Instance);
/// fish.BuffComp.AddBuff(new FishFreezeBuff(3f));
///
/// EntityManager.Instance.Despawn(fish);
/// </code>
/// </example>
public class FishEntity : EntityBase, IMovable, IDamageable, IDamageEffect, IBuffTarget, IAnimatable
{
    /// <summary>未配置边界时的默认半边长</summary>
    private const float k_defaultBoundaryLimit = 30f;

    private FishConfig _config;
    private int _hp;
    private float _moveSpeed;
    private Animator _anim;
    private float _boundaryLimit = k_defaultBoundaryLimit;
    private float _depthAmplitude;
    private float _depthFrequency;
    private float _depthPhase;
    private float _baseZ;

    /// <summary>Buff组件</summary>
    public BuffComponent BuffComp { get; private set; }

    /// <summary>状态机</summary>
    public FishStateMachine StateMachine { get; private set; }

    /// <summary>游动策略运行时数据</summary>
    public SwimData SwimData { get; private set; }

    /// <summary>当前游动策略</summary>
    public ISwimStrategy SwimStrategy { get; private set; }

    private event Action<DamageInfo> _onDamaged;
    private event Action _onDeath;

    /// <summary>构造时初始化组件和状态机</summary>
    public FishEntity()
    {
        BuffComp = new BuffComponent(this);
        StateMachine = new FishStateMachine(this);
        SwimData = new SwimData();
    }

    /// <summary>外部设置配置引用（兼容旧API，新流程通过 OnBindConfig 自动绑定）</summary>
    public void SetConfig(FishConfig config)
    {
        _config = config;
    }

    /// <summary>获取当前配置</summary>
    public FishConfig GetConfig()
    {
        return _config;
    }

    /// <summary>屏幕半径：将世界空间碰撞半径按当前相机投影为屏幕像素</summary>
    protected internal override float ComputeScreenRadius()
    {
        if (_config == null) return 0f;
        return EntitySpace.WorldRadiusToScreen(Position, _config.CollideRadius);
    }

    /// <summary>EntityPoolManager 生成时自动绑定配置</summary>
    protected internal override void OnBindConfig(EntityConfig config)
    {
        _config = config as FishConfig;
    }

    /// <summary>从对象池取出时初始化状态</summary>
    public override void OnSpawn()
    {
        if (_config == null)
        {
            // 不能直接 return：此时 View 已从池中取出并 BindEntity，跳过 base.OnSpawn 会让它永久泄漏。
            // 先建立活跃状态，再走框架约定的“生成即回收”路径立即回池。
            Debug.LogError($"[FishEntity] OnSpawn failed: config is null (EntityId={EntityId}), entity recycled.");
            base.OnSpawn();
            RecycleToPool();
            return;
        }

        _hp = _config.MaxHP;
        _moveSpeed = _config.BaseMoveSpeed;
        _boundaryLimit = _config.BoundaryLimit;
        _depthAmplitude = _config.DepthAmplitude;
        _depthFrequency = _config.DepthFrequency;
        _baseZ = Position.z;

        // 随机初始相位，避免同批生成的鱼前后同步摆动
        _depthPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        SwimData.WaveFrequency = _config.WaveFrequency;
        SwimData.WaveAmplitude = _config.WaveAmplitude;
        SwimData.TurnIntervalMin = _config.TurnIntervalMin;
        SwimData.TurnIntervalMax = _config.TurnIntervalMax;
        SwimData.TurnAngleRange = _config.TurnAngleRange;
        SwimData.CircleRotateSpeed = _config.CircleRotateSpeed;
        SwimData.ResetAll();

        if (View is EntityView entityView)
        {
            _anim = entityView.GetComponent<Animator>();
            // 美术朝向不统一时靠配置补偿，逻辑 Rotation 保持纯运动方向
            entityView.RotationOffset = _config.ViewRotationOffset;
        }

        // 内部表现由 TakeDamage 直接调用，不再占用公开事件，
        // 避免 OnDespawn 清空事件时把外部订阅者也一并静默丢弃。
        SetSwimStrategy(GetStrategyFromType(_config.DefaultStrategy));
        SwimData.TurnCountdown = UnityEngine.Random.Range(SwimData.TurnIntervalMin, SwimData.TurnIntervalMax);
        StateMachine.SwitchState(new FishSwimState());

        base.OnSpawn();
    }

    /// <summary>回收到对象池时清理所有运行时状态</summary>
    public override void OnDespawn()
    {
        StateMachine.SwitchState(null);
        SwimStrategy = null;
        BuffComp.ClearAllBuffs();
        SwimData.ResetAll();
        _hp = 0;
        _moveSpeed = 0f;
        _anim = null;
        _boundaryLimit = k_defaultBoundaryLimit;
        _depthAmplitude = 0f;
        _depthFrequency = 0f;
        _depthPhase = 0f;
        _baseZ = 0f;
        // 注意：_onDamaged / _onDeath 不在此清空。池化实体是同一实例复用，
        // 清空会让外部订阅无声失效；订阅者应在不再需要时自行取消订阅。

        base.OnDespawn();
    }

    /// <summary>每帧 Tick（由 EntityPoolManager 或外部调用）</summary>
    public override void OnUpdate(float deltaTime)
    {
        BuffComp.Tick(deltaTime);
        StateMachine.Tick(deltaTime);
    }

    #region IMovable

    /// <summary>当前朝向，由 Rotation 派生，Rotation 为唯一数据源（绕 Z 轴，运动在 XY 平面）</summary>
    public Vector3 Forward
    {
        get => Quaternion.Euler(Rotation) * Vector3.right;
        set => Rotation = Quaternion.FromToRotation(Vector3.right, value).eulerAngles;
    }

    /// <summary>当前移动速度</summary>
    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = value;
    }

    /// <summary>沿速度向量移动</summary>
    public void Move(Vector3 velocity, float dt)
    {
        Position += velocity * dt;
    }

    /// <summary>将移动速度重置为配置的基础速度</summary>
    public void ResetMoveSpeed()
    {
        _moveSpeed = _config != null ? _config.BaseMoveSpeed : 0f;
    }

    #endregion

    #region IDamageable

    /// <summary>当前生命值</summary>
    public int HP
    {
        get => _hp;
    }

    /// <summary>最大生命值</summary>
    public int MaxHP
    {
        get => _config != null ? _config.MaxHP : 0;
    }

    /// <summary>是否存活</summary>
    public bool IsAlive
    {
        get => _hp > 0;
    }

    /// <summary>受到伤害</summary>
    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive) return;

        _hp -= info.Damage;

        // 内部表现先执行，再通知外部订阅者（保持与原有顺序一致）
        PlayHitEffect(info);
        _onDamaged?.Invoke(info);

        if (_hp <= 0)
        {
            _hp = 0;
            PlayDeathEffect();
            _onDeath?.Invoke();
        }
    }

    /// <summary>
    /// 受击事件。
    /// <para>订阅在实体回池后依然保留（池化实体为同一实例复用），不再需要时请自行取消订阅。</para>
    /// </summary>
    public event Action<DamageInfo> OnDamaged
    {
        add => _onDamaged += value;
        remove => _onDamaged -= value;
    }

    /// <summary>
    /// 死亡事件。
    /// <para>订阅在实体回池后依然保留（池化实体为同一实例复用），不再需要时请自行取消订阅。</para>
    /// </summary>
    public event Action OnDeath
    {
        add => _onDeath += value;
        remove => _onDeath -= value;
    }

    #endregion

    #region IDamageEffect

    /// <summary>播放受击效果（闪白、粒子、音效等）</summary>
    public void PlayHitEffect(DamageInfo info)
    {
    }

    /// <summary>播放死亡效果（掉落、切换死亡状态）</summary>
    public void PlayDeathEffect()
    {
        DropGold();
        StateMachine.SwitchState(new FishDeadState());
    }

    #endregion

    #region IAnimatable

    /// <summary>播放指定名称的动画</summary>
    public void PlayAnim(string animName)
    {
        if (_anim != null)
        {
            _anim.Play(animName);
        }
    }

    /// <summary>当前动画是否已播完</summary>
    public bool IsAnimFinished()
    {
        if (_anim == null) return true;
        var stateInfo = _anim.GetCurrentAnimatorStateInfo(0);
        return stateInfo.normalizedTime >= 1f && !_anim.IsInTransition(0);
    }

    #endregion

    #region 移动逻辑

    /// <summary>
    /// 移动逻辑：策略计算新旋转 → 派生朝向 → 应用位移 → 边界检测
    /// <para>每帧由状态机Tick调用</para>
    /// </summary>
    public void MoveLogic(float dt)
    {
        if (SwimStrategy == null) return;

        // 2D 鱼与 3D 鱼都在 XY 平面游动，区别只在渲染载体，航向统一由绕 Z 轴的角度表示
        Rotation = CalculateRotationOnPlaneXY(dt);

        float finalSpeed = _moveSpeed;
        Move(Forward * finalSpeed, dt);
        ApplyDepthOscillation(dt);

        CheckBoundary();
    }

    /// <summary>
    /// XY 平面上的转向计算。
    /// <para>策略接口的约定是「在 Y 分量上累加航向角」，这里把 Rotation.z 当作航向送进去，
    /// 再把结果写回 Z 分量，这样 4 个游动策略无需改动即可复用。</para>
    /// </summary>
    /// <param name="dt">帧间隔（秒）</param>
    /// <returns>绕 Z 轴的旋转欧拉角</returns>
    private Vector3 CalculateRotationOnPlaneXY(float dt)
    {
        Vector3 heading = new Vector3(0f, Rotation.z, 0f);
        float angle = SwimStrategy.CalculateRotation(SwimData, heading, dt).y;
        return new Vector3(0f, 0f, Mathf.Repeat(angle, 360f));
    }

    /// <summary>
    /// Z 轴（景深）波动：围绕生成时的 Z 做正弦摆动，供 3D 鱼制造前后位移；幅度为 0 时不动。
    /// </summary>
    /// <param name="dt">帧间隔（秒）</param>
    private void ApplyDepthOscillation(float dt)
    {
        if (_depthAmplitude <= 0f)
            return;

        _depthPhase += Mathf.PI * 2f * _depthFrequency * dt;
        if (_depthPhase > Mathf.PI * 2f)
            _depthPhase -= Mathf.PI * 2f;

        Position = new Vector3(Position.x, Position.y, _baseZ + Mathf.Sin(_depthPhase) * _depthAmplitude);
    }

    /// <summary>边界检测：超出 XY 范围的鱼掉头</summary>
    private void CheckBoundary()
    {
        float limit = _boundaryLimit;

        if (Position.x < -limit || Position.x > limit || Position.y < -limit || Position.y > limit)
        {
            // 掉头即绕 Z 轴转 180°，保持运动平面不变
            Rotation = new Vector3(0f, 0f, Mathf.Repeat(Rotation.z + 180f, 360f));
        }
    }

    #endregion

    #region 游动策略

    /// <summary>切换游动策略，自动重置运行时参数</summary>
    public void SetSwimStrategy(ISwimStrategy strategy)
    {
        if (strategy != null)
        {
            SwimData.ResetAll();
        }
        SwimStrategy = strategy;
    }

    /// <summary>根据策略类型枚举获取对应的策略单例</summary>
    private static ISwimStrategy GetStrategyFromType(SwimStrategyType type)
    {
        switch (type)
        {
            case SwimStrategyType.Straight: return StraightSwim.Instance;
            case SwimStrategyType.SineWave: return SineWaveSwim.Instance;
            case SwimStrategyType.RandomTurn: return RandomTurnSwim.Instance;
            case SwimStrategyType.Circle: return CircleSwim.Instance;
            default: return StraightSwim.Instance;
        }
    }

    #endregion

    #region 掉落

    /// <summary>生成掉落金币</summary>
    public void DropGold()
    {
    }

    #endregion

    #region 池回收

    /// <summary>将自己回收到对象池</summary>
    public void RecycleToPool()
    {
        EntityManager.Instance.Despawn(this);
    }

    #endregion
}

#endregion
}