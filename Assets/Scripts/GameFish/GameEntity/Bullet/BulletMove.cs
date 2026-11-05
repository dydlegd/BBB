using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== 弹道策略接口（复用 BulletInterfaces.cs 中的 IBulletTrajectory）=====================

    // IBulletTrajectory 定义在 BulletInterfaces.cs 中

    #endregion

    #region ===================== 弹道策略实现（单例）=====================

    /// <summary>
    /// 直线弹道策略
    /// <para>沿初始发射方向匀速直线飞行，最基础的子弹类型</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// bullet.SetTrajectory(StraightTrajectory.Instance);
    /// </code>
    /// </example>
    public class StraightTrajectory : IBulletTrajectory
    {
        /// <summary>单例</summary>
        public static readonly StraightTrajectory Instance = new StraightTrajectory();
        private StraightTrajectory() { }

        /// <summary>返回沿发射方向的速度向量</summary>
        public Vector3 CalculateVelocity(BulletTrajectoryData data, float dt)
        {
            return data.Direction;
        }
    }

    /// <summary>
    /// 正弦波弹道策略
    /// <para>沿主方向前进的同时左右摆动，形成S形弹幕效果</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// bullet.SetTrajectory(SineWaveTrajectory.Instance);
    /// // 通过 BulletConfig 配置 WaveFrequency 和 WaveAmplitude
    /// </code>
    /// </example>
    public class SineWaveTrajectory : IBulletTrajectory
    {
        /// <summary>单例</summary>
        public static readonly SineWaveTrajectory Instance = new SineWaveTrajectory();
        private SineWaveTrajectory() { }

        /// <summary>正弦波方向偏移计算，以固化基准方向为中心振荡</summary>
        public Vector3 CalculateVelocity(BulletTrajectoryData data, float dt)
        {
            data.WavePhase += dt * data.WaveFrequency;

            Vector3 forward = data.ForwardDirection;
            Vector3 perpendicular = new Vector3(-forward.y, forward.x, 0f).normalized;
            float offset = Mathf.Sin(data.WavePhase) * data.WaveAmplitude;

            return (forward + perpendicular * offset).normalized;
        }
    }

    /// <summary>
    /// 追踪弹道策略
    /// <para>每帧根据目标位置修正飞行方向，转向速度受 HomingTurnRate 控制</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// bullet.SetTrajectory(HomingTrajectory.Instance);
    /// bullet.TrajectoryData.HomingTargetPos = targetFish.Position;
    /// // 通过 BulletConfig 配置 HomingTurnRate 控制转向速度
    /// </code>
    /// </example>
    public class HomingTrajectory : IBulletTrajectory
    {
        /// <summary>单例</summary>
        public static readonly HomingTrajectory Instance = new HomingTrajectory();
        private HomingTrajectory() { }

        /// <summary>追踪方向计算，平滑转向目标</summary>
        public Vector3 CalculateVelocity(BulletTrajectoryData data, float dt)
        {
            if (data.HomingTargetPos.HasValue)
            {
                Vector3 toTarget = (data.HomingTargetPos.Value - data.CurrentPosition).normalized;
                float maxAngle = data.HomingTurnRate > 0 ? data.HomingTurnRate * dt : 360f;
                data.Direction = Vector3.RotateTowards(data.Direction, toTarget, maxAngle * Mathf.Deg2Rad, 0f).normalized;
            }
            return data.Direction;
        }
    }

    /// <summary>
    /// 重力抛物线弹道策略
    /// <para>水平方向匀速前进，垂直方向受重力加速度影响持续下坠</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// bullet.SetTrajectory(ParabolicTrajectory.Instance);
    /// // 通过 BulletConfig 配置 Gravity 控制下坠速度
    /// </code>
    /// </example>
    public class ParabolicTrajectory : IBulletTrajectory
    {
        /// <summary>单例</summary>
        public static readonly ParabolicTrajectory Instance = new ParabolicTrajectory();
        private ParabolicTrajectory() { }

        /// <summary>抛物线速度计算，以固化发射方向为基准，Y轴累加重力</summary>
        public Vector3 CalculateVelocity(BulletTrajectoryData data, float dt)
        {
            data.VerticalVelocity += data.Gravity * dt;

            Vector3 result = data.ForwardDirection;
            result.y += data.VerticalVelocity;
            return result.normalized;
        }
    }

    #endregion

    #region ===================== 碰撞效果策略实现（单例）=====================

    /// <summary>
    /// 普通碰撞策略
    /// <para>命中目标后子弹立即销毁，最基础的碰撞类型</para>
    /// </summary>
    public class NormalCollision : IBulletCollisionStrategy
    {
        /// <summary>单例</summary>
        public static readonly NormalCollision Instance = new NormalCollision();
        private NormalCollision() { }

        /// <summary>对目标造成伤害并标记子弹应销毁</summary>
        public BulletHitResult OnHit(BulletEntity bullet, EntityBase hitTarget, Vector3 hitPoint)
        {
            int damage = bullet.Damage;

            if (hitTarget is IDamageable damageable && damageable.IsAlive)
            {
                damageable.TakeDamage(new DamageInfo
                {
                    Damage = damage,
                    HitPoint = hitPoint,
                    Attacker = bullet.View is Component comp ? comp.gameObject : null
                });
            }

            return new BulletHitResult
            {
                HitTarget = hitTarget,
                HitPoint = hitPoint,
                DamageDealt = damage,
                ShouldDestroy = true,
                PlayerId = bullet.PlayerId
            };
        }
    }

    /// <summary>
    /// 穿透碰撞策略</summary>/// </summary>
    public class PierceCollision : IBulletCollisionStrategy
    {
        /// <summary>单例</summary>
        public static readonly PierceCollision Instance = new PierceCollision();
        private PierceCollision() { }

        /// <summary>穿透伤害，减次数，穿透用完才销毁</summary>
        public BulletHitResult OnHit(BulletEntity bullet, EntityBase hitTarget, Vector3 hitPoint)
        {
            int damage = bullet.Damage;

            if (hitTarget is IDamageable damageable && damageable.IsAlive)
            {
                if (bullet.TrajectoryData.HitTargetIds.Contains(hitTarget.EntityId))
                {
                    return new BulletHitResult { HitTarget = hitTarget, HitPoint = hitPoint, DamageDealt = 0, ShouldDestroy = false, PlayerId = bullet.PlayerId };
                }

                bullet.TrajectoryData.HitTargetIds.Add(hitTarget.EntityId);
                damageable.TakeDamage(new DamageInfo
                {
                    Damage = damage,
                    HitPoint = hitPoint,
                    Attacker = bullet.View is Component comp ? comp.gameObject : null
                });
            }

            bullet.TrajectoryData.RemainingPierce--;
            bool shouldDestroy = bullet.TrajectoryData.RemainingPierce <= 0;

            return new BulletHitResult
            {
                HitTarget = hitTarget,
                HitPoint = hitPoint,
                DamageDealt = damage,
                ShouldDestroy = shouldDestroy,
                PlayerId = bullet.PlayerId
            };
        }
    }

    /// <summary>
    /// 范围爆炸碰撞策略</summary>
    /// </summary>
    public class ExplosionCollision : IBulletCollisionStrategy
    {
        /// <summary>单例</summary>
        public static readonly ExplosionCollision Instance = new ExplosionCollision();
        private ExplosionCollision() { }

        /// <summary>爆炸AOE伤害，范围内的所有IDamageable目标均受伤</summary>
        public BulletHitResult OnHit(BulletEntity bullet, EntityBase hitTarget, Vector3 hitPoint)
        {
            int damage = bullet.Damage;
            float explosionRadius = bullet.ExplosionRadius;
            float aoeFactor = bullet.ExplosionDamageFactor;
            int aoeDamage = Mathf.RoundToInt(damage * aoeFactor);

            var activeEntities = EntityManager.Instance.AllActive;
            int totalDealt = 0;
            Vector2 hitPoint2D = new Vector2(hitPoint.x, hitPoint.y);

            foreach (var entity in activeEntities)
            {
                if (entity == bullet) continue;
                if (!(entity is IDamageable damageable) || !damageable.IsAlive) continue;

                if (entity.View == null) continue;
                float dist = Vector2.Distance(entity.ScreenPosition, hitPoint2D);
                if (dist <= explosionRadius)
                {
                    int dmg = entity == hitTarget ? damage : aoeDamage;
                    damageable.TakeDamage(new DamageInfo
                    {
                        Damage = dmg,
                        HitPoint = entity.Position,
                        Attacker = bullet.View is Component comp ? comp.gameObject : null
                    });
                    totalDealt += dmg;
                }
            }

            return new BulletHitResult
            {
                HitTarget = hitTarget,
                HitPoint = hitPoint,
                DamageDealt = totalDealt,
                ShouldDestroy = true,
                PlayerId = bullet.PlayerId
            };
        }
    }

    /// <summary>
    /// 弹射碰撞策略</summary>
    public class BounceCollision : IBulletCollisionStrategy
    {
        /// <summary>单例</summary>
        public static readonly BounceCollision Instance = new BounceCollision();
        private BounceCollision() { }

        /// <summary>弹射伤害，搜索最近目标改变方向，弹射次数用完销毁</summary>
        public BulletHitResult OnHit(BulletEntity bullet, EntityBase hitTarget, Vector3 hitPoint)
        {
            int damage = bullet.Damage;
            float searchRadius = bullet.BounceSearchRadius;

            if (hitTarget is IDamageable damageable && damageable.IsAlive)
            {
                bullet.TrajectoryData.HitTargetIds.Add(hitTarget.EntityId);
                damageable.TakeDamage(new DamageInfo
                {
                    Damage = damage,
                    HitPoint = hitPoint,
                    Attacker = bullet.View is Component comp ? comp.gameObject : null
                });
            }

            bullet.TrajectoryData.RemainingBounce--;

            if (bullet.TrajectoryData.RemainingBounce > 0)
            {
                Vector2 hitPoint2D = new Vector2(hitPoint.x, hitPoint.y);
                EntityBase nearestTarget = null;
                float nearestDist = float.MaxValue;

                var activeEntities = EntityManager.Instance.AllActive;
                foreach (var entity in activeEntities)
                {
                    if (entity == bullet) continue;
                    if (!(entity is IDamageable d) || !d.IsAlive) continue;
                    if (bullet.TrajectoryData.HitTargetIds.Contains(entity.EntityId)) continue;

                    if (entity.View == null) continue;
                    float dist = Vector2.Distance(entity.ScreenPosition, hitPoint2D);
                    if (dist <= searchRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestTarget = entity;
                    }
                }

                if (nearestTarget != null)
                {
                    if (nearestTarget.View != null)
                    {
                        Vector2 targetScreenPos = nearestTarget.ScreenPosition;
                        Vector3 newDir = new Vector3(
                            targetScreenPos.x - bullet.Position.x,
                            targetScreenPos.y - bullet.Position.y,
                            0f).normalized;
                        bullet.TrajectoryData.Direction = newDir;
                        bullet.Forward = newDir;
                        return new BulletHitResult
                        {
                            HitTarget = hitTarget,
                            HitPoint = hitPoint,
                            DamageDealt = damage,
                            ShouldDestroy = false,
                            PlayerId = bullet.PlayerId
                        };
                    }
                }
            }

            return new BulletHitResult
            {
                HitTarget = hitTarget,
                HitPoint = hitPoint,
                DamageDealt = damage,
                ShouldDestroy = true,
                PlayerId = bullet.PlayerId
            };
        }
    }

    #endregion
}