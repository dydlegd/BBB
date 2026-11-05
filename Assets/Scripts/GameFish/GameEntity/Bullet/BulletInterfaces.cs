using System.Collections.Generic;
using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== 数据结构 =====================

    /// <summary>
    /// 子弹命中结果，承载一次碰撞检测的完整上下文
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// var result = new BulletHitResult
    /// {
    ///     HitTarget = fishEntity,
    ///     HitPoint = bullet.Position,
    ///     DamageDealt = config.Damage
    /// };
    /// if (result.HitTarget is IDamageable dmg)
    ///     dmg.TakeDamage(new DamageInfo { Damage = result.DamageDealt, Attacker = bullet.View?.GameObject });
    /// </code>
    /// </example>
    public struct BulletHitResult
    {
        /// <summary>命中的目标实体</summary>
        public EntityBase HitTarget;

        /// <summary>命中点世界坐标</summary>
        public Vector3 HitPoint;

        /// <summary>本次造成的伤害值</summary>
        public int DamageDealt;

        /// <summary>是否立即销毁子弹</summary>
        public bool ShouldDestroy;

        /// <summary>发射此子弹的玩家ID</summary>
        public int PlayerId;
    }

    /// <summary>
    /// 子弹运行时数据上下文，存储子弹飞行过程中的动态参数。
    /// <para>每种弹道策略只访问自己需要的字段，互不干扰。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// var data = new BulletTrajectoryData();
    /// data.Direction = Vector3.right;
    /// data.WaveFrequency = 4f;
    /// data.WaveAmplitude = 30f;
    /// data.ResetAll();
    /// </code>
    /// </example>
    public class BulletTrajectoryData
    {
        /// <summary>当前飞行方向（归一化，每帧更新）</summary>
        public Vector3 Direction;

        /// <summary>基准前进方向（发射时固化，正弦波等弹道以此为中心振荡）</summary>
        public Vector3 ForwardDirection;

        /// <summary>子弹当前位置（每帧由BulletEntity同步）</summary>
        public Vector3 CurrentPosition;

        /// <summary>已存活时间</summary>
        public float ElapsedTime;

        /// <summary>剩余穿透次数</summary>
        public int RemainingPierce;

        /// <summary>剩余弹射次数</summary>
        public int RemainingBounce;

        /// <summary>剩余边界反弹次数</summary>
        public int RemainingBounces;

        /// <summary>已命中的目标ID集合（用于穿透/弹射的去重）</summary>
        public System.Collections.Generic.HashSet<int> HitTargetIds = new System.Collections.Generic.HashSet<int>();

        // === SineWave ===
        /// <summary>正弦波摆动频率</summary>
        public float WaveFrequency;

        /// <summary>正弦波摆动振幅</summary>
        public float WaveAmplitude;

        /// <summary>正弦波当前相位</summary>
        public float WavePhase;

        // === Homing ===
        /// <summary>追踪目标转向速度（度/秒）</summary>
        public float HomingTurnRate;

        /// <summary>追踪目标的位置（由外部设置）</summary>
        public Vector3? HomingTargetPos;

        // === Parabolic ===
        /// <summary>重力加速度（正值向下）</summary>
        public float Gravity;

        /// <summary>重力方向上的当前速度分量</summary>
        public float VerticalVelocity;

        /// <summary>重置所有运行时参数</summary>
        public void ResetAll()
        {
            Direction = Vector3.zero;
            ForwardDirection = Vector3.zero;
            CurrentPosition = Vector3.zero;
            ElapsedTime = 0f;
            RemainingPierce = 0;
            RemainingBounce = 0;
            RemainingBounces = 0;
            HitTargetIds.Clear();
            WaveFrequency = 0f;
            WaveAmplitude = 0f;
            WavePhase = 0f;
            HomingTurnRate = 0f;
            HomingTargetPos = null;
            Gravity = 0f;
            VerticalVelocity = 0f;
        }
    }

    #endregion

    #region ===================== 功能接口 =====================

    /// <summary>
    /// 子弹投射物接口，提供发射、飞行和生命周期管理。
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// IProjectile bullet = bulletEntity;
    /// bullet.Launch(firePosition, fireDirection);
    ///
    /// // 每帧检查
    /// if (bullet.IsExpired)
    ///     bullet.Recycle();
    /// </code>
    /// </example>
    public interface IProjectile
    {
        /// <summary>子弹初始发射方向</summary>
        Vector3 LaunchDirection { get; }

        /// <summary>子弹飞行速度</summary>
        float Speed { get; }

        /// <summary>子弹伤害值</summary>
        int Damage { get; }

        /// <summary>最大存活时间</summary>
        float MaxLifetime { get; }

        /// <summary>已存活时间</summary>
        float ElapsedTime { get; }

        /// <summary>是否已超时/命中而应被回收</summary>
        bool IsExpired { get; }

        /// <summary>碰撞检测半径</summary>
        float CollideRadius { get; }

        /// <summary>发射子弹，设置起始位置和方向</summary>
        /// <param name="startPos">发射起始位置</param>
        /// <param name="direction">发射方向（归一化向量）</param>
        /// <param name="playerId">发射者玩家ID</param>
        void Launch(Vector3 startPos, Vector3 direction, int playerId = 0);

        /// <summary>回收子弹到对象池</summary>
        void Recycle();
    }

    /// <summary>
    /// 子弹碰撞效果接口，定义命中后的行为（普通销毁/穿透/爆炸/弹射）。
    /// <para>与 IProjectile 分离，通过策略模式支持不同碰撞类型。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// // NormalCollision: 命中即销毁
    /// // PierceCollision: 穿透N个目标后销毁
    /// // ExplosionCollision: 命中后AOE伤害
    /// // BounceCollision: 命中后弹向新目标
    ///
    /// IBulletCollision collision = bulletEntity.CollisionStrategy;
    /// collision.OnHit(bulletEntity, hitTarget, hitPoint);
    /// </code>
    /// </example>
    public interface IBulletCollisionStrategy
    {
        /// <summary>
        /// 子弹命中目标时的处理逻辑.
        /// </summary>
        /// <param name="bullet">子弹实体</param>
        /// <param name="hitTarget">命中的目标实体</param>
        /// <param name="hitPoint">命中点坐标</param>
        /// <returns>命中结果，指示是否应销毁子弹</returns>
        BulletHitResult OnHit(BulletEntity bullet, EntityBase hitTarget, Vector3 hitPoint);
    }

    /// <summary>
    /// 子弹弹道接口，计算每帧的飞行方向和位移。
    /// <para>不同弹道类型（直线/正弦波/追踪/抛物线）各自实现。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// IBulletTrajectory trajectory = bulletEntity.TrajectoryStrategy;
    /// Vector3 velocity = trajectory.CalculateVelocity(data, dt);
    /// bullet.Move(velocity, dt);
    /// </code>
    /// </example>
    public interface IBulletTrajectory
    {
        /// <summary>根据运行时数据计算当前帧的速度向量</summary>
        /// <param name="data">运行时数据上下文</param>
        /// <param name="dt">帧间隔</param>
        /// <returns>速度向量（方向 * 速度值）</returns>
        Vector3 CalculateVelocity(BulletTrajectoryData data, float dt);
    }

    /// <summary>
    /// 子弹命中/销毁表现效果接口，负责播放视觉和音效反馈。
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// bullet.PlayHitEffect(hitPoint);
    /// // 或
    /// bullet.PlayDestroyEffect();
    /// </code>
    /// </example>
    public interface IBulletEffect
    {
        /// <summary>在指定位置播放命中特效</summary>
        void PlayHitEffect(Vector3 hitPoint);

        /// <summary>播放子弹销毁/回收特效</summary>
        void PlayDestroyEffect();
    }

    #endregion
}