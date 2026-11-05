using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== 实体：BulletEntity（继承 EntityBase，组合功能接口）=====================

    /// <summary>
    /// 子弹实体类，继承 EntityBase，通过组合弹道策略和碰撞策略实现多样化子弹行为。
    /// <para>实体创建由 BulletConfig.CreateEntity() 委托，无需手动注册工厂。</para>
    /// <para>实现接口：IProjectile（发射与控制）、IBulletEffect（命中表现）</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// BulletEntity bullet = BulletFactory.SpawnByName("NormalBullet", firePos, fireRotation);
    ///
    /// // 追踪弹需要设置目标
    /// bullet.TrajectoryData.HomingTargetPos = targetFish.Position;
    ///
    /// // 每帧Tick会自动检测碰撞、处理超时
    /// // 超时或命中后自动回收
    /// </code>
    /// </example>
    public class BulletEntity : EntityBase, IScreenSpaceEntity, IProjectile, IBulletEffect
    {
        private BulletConfig _config;

        /// <summary>发射此子弹的玩家ID</summary>
        public int PlayerId { get; set; }

        /// <summary>弹道运行时数据，供弹道策略和碰撞策略读写</summary>
        public readonly BulletTrajectoryData TrajectoryData = new BulletTrajectoryData();

        // === IProjectile ===
        /// <summary>发射方向</summary>
        public Vector3 LaunchDirection => TrajectoryData.Direction;

        /// <summary>当前飞行速度</summary>
        public float Speed => _config != null ? _config.Speed : 0f;

        /// <summary>伤害值</summary>
        public int Damage => _config != null ? _config.Damage : 0;

        /// <summary>最大存活时间</summary>
        public float MaxLifetime => _config != null ? _config.MaxLifetime : 0f;

        /// <summary>已存活时间</summary>
        public float ElapsedTime => TrajectoryData.ElapsedTime;

        /// <summary>是否应被回收（超时或命中销毁）</summary>
        public bool IsExpired => _config != null && _config.MaxLifetime > 0 && TrajectoryData.ElapsedTime >= _config.MaxLifetime;

        /// <summary>碰撞检测半径</summary>
        public float CollideRadius => _config != null ? _config.CollideRadius : 0f;

        /// <summary>爆炸AOE半径（仅Explosion类型）</summary>
        public float ExplosionRadius => _config != null ? _config.ExplosionRadius : 0f;

        /// <summary>爆炸AOE伤害系数（仅Explosion类型）</summary>
        public float ExplosionDamageFactor => _config != null ? _config.ExplosionDamageFactor : 0f;

        /// <summary>弹射搜索半径（仅Bounce类型）</summary>
        public float BounceSearchRadius => _config != null ? _config.BounceSearchRadius : 0f;

        /// <summary>当前弹道策略</summary>
        public IBulletTrajectory TrajectoryStrategy { get; private set; }

        /// <summary>当前碰撞效果策略</summary>
        public IBulletCollisionStrategy CollisionStrategy { get; private set; }

        /// <summary>当前朝向，由 Rotation 派生</summary>
        public Vector3 Forward
        {
            get => Quaternion.Euler(Rotation) * Vector3.right;
            set => Rotation = Quaternion.FromToRotation(Vector3.right, value).eulerAngles;
        }

        /// <summary>
        /// 发射子弹，设置初始位置、方向，初始化策略和运行时数据。
        /// </summary>
        /// <param name="startPos">发射起始位置</param>
        /// <param name="direction">发射方向（自动归一化）</param>
        /// <param name="playerId">发射者玩家ID</param>
        public void Launch(Vector3 startPos, Vector3 direction, int playerId = 0)
        {
            PlayerId = playerId;
            Position = startPos;
            TrajectoryData.Direction = direction.normalized;
            TrajectoryData.ForwardDirection = direction.normalized;
            TrajectoryData.ElapsedTime = 0f;
            Forward = direction.normalized;

            if (_config != null)
            {
                TrajectoryData.RemainingPierce = _config.PierceCount;
                TrajectoryData.RemainingBounce = _config.BounceCount;
                TrajectoryData.RemainingBounces = _config.BounceOnBoundary ? _config.MaxBounceCount : 0;
                TrajectoryData.HitTargetIds.Clear();
                TrajectoryData.WaveFrequency = _config.WaveFrequency;
                TrajectoryData.WaveAmplitude = _config.WaveAmplitude;
                TrajectoryData.HomingTurnRate = _config.HomingTurnRate;
                TrajectoryData.Gravity = _config.Gravity;

                SetTrajectoryFromType(_config.TrajectoryType);
                SetCollisionFromType(_config.CollisionType);
            }

            SyncTransform();
        }

        /// <summary>回收到对象池</summary>
        public void Recycle()
        {
            EntityManager.Instance.Despawn(this);
        }

        // === EntityBase Overrides ===

        /// <summary>池取出时初始化策略和运行时数据</summary>
        public override void OnSpawn()
        {
            // 同步美术朝向补偿：SetRotation 每帧用 (0,0,θ) 覆盖预制体旋转，
            // 美术预制体自带的初始姿态（如平躺的 3D 弹）需要通过 RotationOffset 找回来。
            // 必须在 base.OnSpawn()（内部会 SyncTransform）之前设置，避免首帧用旧补偿值。
            if (View is EntityView entityView && _config != null)
            {
                entityView.RotationOffset = _config.ViewRotationOffset;
            }

            if (_config != null)
            {
                TrajectoryData.ElapsedTime = 0f;
                TrajectoryData.RemainingPierce = _config.PierceCount;
                TrajectoryData.RemainingBounce = _config.BounceCount;
                TrajectoryData.RemainingBounces = _config.BounceOnBoundary ? _config.MaxBounceCount : 0;
                TrajectoryData.HitTargetIds.Clear();
                TrajectoryData.WaveFrequency = _config.WaveFrequency;
                TrajectoryData.WaveAmplitude = _config.WaveAmplitude;
                TrajectoryData.HomingTurnRate = _config.HomingTurnRate;
                TrajectoryData.Gravity = _config.Gravity;

                SetTrajectoryFromType(_config.TrajectoryType);
                SetCollisionFromType(_config.CollisionType);
            }

            base.OnSpawn();
        }

        /// <summary>回池时清理所有运行时状态</summary>
        public override void OnDespawn()
        {
            PlayerId = 0;
            TrajectoryStrategy = null;
            CollisionStrategy = null;
            TrajectoryData.ResetAll();

            base.OnDespawn();
        }

        /// <summary>屏幕半径：子弹为屏幕像素实体，直接使用配置的像素半径</summary>
        protected internal override float ComputeScreenRadius()
        {
            return CollideRadius;
        }

        /// <summary>每帧更新：弹道移动 + 碰撞检测 + 生命周期检查</summary>
        public override void OnUpdate(float deltaTime)
        {
            if (_config == null) return;

            TrajectoryData.ElapsedTime += deltaTime;

            // 超时检查
            if (IsExpired)
            {
                PlayDestroyEffect();
                Recycle();
                return;
            }

            // 首帧方向初始化（Spawn 时 Rotation 已设置，从中派生初始 Direction）
            if (TrajectoryData.Direction == Vector3.zero)
            {
                TrajectoryData.Direction = Forward;
            }

            // 弹道移动
            if (TrajectoryStrategy != null)
            {
                TrajectoryData.CurrentPosition = Position;
                Vector3 velocityDir = TrajectoryStrategy.CalculateVelocity(TrajectoryData, deltaTime);
                TrajectoryData.Direction = velocityDir.normalized;
                Forward = velocityDir.normalized;

                float effectiveSpeed = _config.Speed;
                Position += velocityDir * effectiveSpeed * deltaTime;
            }

            // 自旋
            if (_config.SpinSpeed != 0f)
            {
                Rotation += new Vector3(0f, 0f, _config.SpinSpeed * deltaTime);
            }

            // 边界检查
            if (HandleBoundary()) return;

            // 碰撞检测
            CheckAndProcessCollision();
        }

        /// <summary>绑定配置时存储引用并设置默认策略</summary>
        protected internal override void OnBindConfig(EntityConfig config)
        {
            _config = config as BulletConfig;
            if (_config != null)
            {
                SetTrajectoryFromType(_config.TrajectoryType);
                SetCollisionFromType(_config.CollisionType);
            }
        }

        // === 碰撞检测与处理 ===

        /// <summary>
        /// 每帧检测子弹与目标的碰撞。
        /// <para>统一使用屏幕像素坐标判定：子弹位置即屏幕像素，目标位置/半径读取 EntityManager 每帧刷新的缓存。</para>
        /// </summary>
        private void CheckAndProcessCollision()
        {
            if (CollisionStrategy == null || _config == null) return;
            if (EntitySpace.MainCamera == null) return;

            var activeEntities = EntityManager.Instance.AllActive;
            // 必须在移动后取当前位置：EntityManager 在 Tick 之前刷新的 ScreenPosition 还是上一帧的位置，
            // 高速子弹用它做判定会在低帧率下穿透目标。子弹是屏幕空间实体，Position 本身就是屏幕像素坐标。
            Vector2 bulletScreenPos = new Vector2(Position.x, Position.y);

            foreach (var entity in activeEntities)
            {
                if (entity == this) continue;
                if (!(entity is IDamageable damageable) || !damageable.IsAlive) continue;
                if (TrajectoryData.HitTargetIds.Contains(entity.EntityId)) continue;
                // 已失活/已标记待移除的实体由 AllActive 的枚举器统一过滤，这里只需挡掉纯数据实体
                if (entity.View == null) continue;

                float hitRadius = CollideRadius + entity.ScreenRadius;
                float sqrDist = (entity.ScreenPosition - bulletScreenPos).sqrMagnitude;

                if (sqrDist <= hitRadius * hitRadius)
                {
                    var result = CollisionStrategy.OnHit(this, entity, Position);

                    if (result.DamageDealt > 0)
                    {
                        PlayHitEffect(result.HitPoint);
                    }

                    if (result.ShouldDestroy)
                    {
                        PlayDestroyEffect();
                        Recycle();
                        return;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 边界处理：反弹或销毁。
        /// <para>BounceOnBoundary 优先：钳位到边界并反射方向，反弹次数用完则销毁。</para>
        /// <para>DestroyOnBoundary：越界直接销毁。</para>
        /// </summary>
        /// <returns>true 表示实体已被销毁，调用方应 return</returns>
        private bool HandleBoundary()
        {
            if (_config == null) return false;

            if (_config.BounceOnBoundary)
            {
                if (TryBounceOffScreen())
                {
                    if (_config.MaxBounceCount > 0 && TrajectoryData.RemainingBounces <= 0)
                    {
                        PlayDestroyEffect();
                        Recycle();
                        return true;
                    }
                }
                return false;
            }

            if (_config.DestroyOnBoundary && CheckOutOfScreen())
            {
                PlayDestroyEffect();
                Recycle();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检测越界并执行反弹：钳位 Position 到边界内，反射 Direction 和 ForwardDirection。
        /// </summary>
        /// <returns>是否发生了反弹</returns>
        private bool TryBounceOffScreen()
        {
            Rect bounds = EntitySpace.ScreenBounds;
            bool bounced = false;
            Vector3 pos = Position;

            if (pos.x < bounds.xMin)
            {
                pos.x = 2f * bounds.xMin - pos.x;
                TrajectoryData.Direction = new Vector3(-TrajectoryData.Direction.x, TrajectoryData.Direction.y, 0f);
                TrajectoryData.ForwardDirection = new Vector3(-TrajectoryData.ForwardDirection.x, TrajectoryData.ForwardDirection.y, 0f);
                bounced = true;
            }
            else if (pos.x > bounds.xMax)
            {
                pos.x = 2f * bounds.xMax - pos.x;
                TrajectoryData.Direction = new Vector3(-TrajectoryData.Direction.x, TrajectoryData.Direction.y, 0f);
                TrajectoryData.ForwardDirection = new Vector3(-TrajectoryData.ForwardDirection.x, TrajectoryData.ForwardDirection.y, 0f);
                bounced = true;
            }

            if (pos.y < bounds.yMin)
            {
                pos.y = 2f * bounds.yMin - pos.y;
                TrajectoryData.Direction = new Vector3(TrajectoryData.Direction.x, -TrajectoryData.Direction.y, 0f);
                TrajectoryData.ForwardDirection = new Vector3(TrajectoryData.ForwardDirection.x, -TrajectoryData.ForwardDirection.y, 0f);
                bounced = true;
            }
            else if (pos.y > bounds.yMax)
            {
                pos.y = 2f * bounds.yMax - pos.y;
                TrajectoryData.Direction = new Vector3(TrajectoryData.Direction.x, -TrajectoryData.Direction.y, 0f);
                TrajectoryData.ForwardDirection = new Vector3(TrajectoryData.ForwardDirection.x, -TrajectoryData.ForwardDirection.y, 0f);
                bounced = true;
            }

            if (bounced)
            {
                Position = pos;
                TrajectoryData.Direction.Normalize();
                TrajectoryData.ForwardDirection.Normalize();
                TrajectoryData.RemainingBounces--;
                SyncTransform();
            }

            return bounced;
        }

        /// <summary>检查子弹是否超出屏幕边界</summary>
        private bool CheckOutOfScreen()
        {
            const float margin = 100f;
            Rect bounds = EntitySpace.ScreenBounds;

            return Position.x < bounds.xMin - margin || Position.x > bounds.xMax + margin ||
                   Position.y < bounds.yMin - margin || Position.y > bounds.yMax + margin;
        }

        // === 弹道策略切换 ===

        /// <summary>根据弹道类型设置策略</summary>
        private void SetTrajectoryFromType(BulletTrajectoryType type)
        {
            switch (type)
            {
                case BulletTrajectoryType.SineWave:
                    TrajectoryStrategy = SineWaveTrajectory.Instance;
                    break;
                case BulletTrajectoryType.Homing:
                    TrajectoryStrategy = HomingTrajectory.Instance;
                    break;
                case BulletTrajectoryType.Parabolic:
                    TrajectoryStrategy = ParabolicTrajectory.Instance;
                    break;
                default:
                    TrajectoryStrategy = StraightTrajectory.Instance;
                    break;
            }
        }

        /// <summary>根据碰撞类型设置策略</summary>
        private void SetCollisionFromType(BulletCollisionType type)
        {
            switch (type)
            {
                case BulletCollisionType.Pierce:
                    CollisionStrategy = PierceCollision.Instance;
                    break;
                case BulletCollisionType.Explosion:
                    CollisionStrategy = ExplosionCollision.Instance;
                    break;
                case BulletCollisionType.Bounce:
                    CollisionStrategy = BounceCollision.Instance;
                    break;
                default:
                    CollisionStrategy = NormalCollision.Instance;
                    break;
            }
        }

        // === 表现效果 ===

        /// <summary>在命中点生成命中特效</summary>
        public void PlayHitEffect(Vector3 hitPoint)
        {
            if (_config != null && _config.HitEffectPrefab != null)
            {
                Object.Instantiate(_config.HitEffectPrefab, hitPoint, Quaternion.identity);
            }

            if (_config != null && !string.IsNullOrEmpty(_config.HitSound))
            {
                // AudioManager.Play(_config.HitSound);
            }
        }

        /// <summary>在当前位置生成销毁特效</summary>
        public void PlayDestroyEffect()
        {
            if (_config != null && _config.DestroyEffectPrefab != null)
            {
                Object.Instantiate(_config.DestroyEffectPrefab, Position, Quaternion.identity);
            }
        }
    }

    #endregion
}