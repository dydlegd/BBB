using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    /// <summary>
    /// 炮台核心接口，定义旋转、发射和生命周期。
    /// <para>每个玩家持有一个 ICannon 实例，通过 CannonManager 管理。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// ICannon cannon = new Cannon(config, transform, playerId);
    /// cannon.AimAt(screenTouchPos);
    /// if (cannon.CanFire)
    /// {
    ///     BulletEntity bullet = cannon.Fire();
    ///     bullet?.Launch(muzzlePos, aimDir, cannon.PlayerId);
    /// }
    /// </code>
    /// </example>
    public interface ICannon
    {
        /// <summary>玩家ID，用于子弹归属和计分</summary>
        int PlayerId { get; }

        /// <summary>炮口世界坐标</summary>
        Vector3 MuzzlePosition { get; }

        /// <summary>瞄准方向（归一化）</summary>
        Vector3 AimDirection { get; }

        /// <summary>当前冷却剩余时间（秒），0 表示可发射</summary>
        float CooldownRemaining { get; }

        /// <summary>是否能开火</summary>
        bool CanFire { get; }

        /// <summary>炮台等级（影响子弹类型、威力等）</summary>
        int CannonLevel { get; }

        /// <summary>威力倍率（等级加成）</summary>
        float PowerMultiplier { get; }

        /// <param name="direction">旋转方向：正值右转，负值左转，0不转</param>
        void Rotate(float direction);

        /// <summary>发射子弹</summary>
        /// <returns>生成的子弹实体，冷却中返回 null</returns>
        BulletEntity Fire();

        /// <summary>子弹命中回调，用于计分等</summary>
        void OnBulletHit(EntityBase hitTarget, int damage);
    }
}