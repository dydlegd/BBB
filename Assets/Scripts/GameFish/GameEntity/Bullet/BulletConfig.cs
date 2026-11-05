using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== 子弹相关枚举 =====================

    /// <summary>子弹运动轨迹类型</summary>
    public enum BulletTrajectoryType
    {
        /// <summary>直线飞行，朝初始方向匀速前进</summary>
        Straight,

        /// <summary>正弦波S形，沿主方向左右摆动</summary>
        SineWave,

        /// <summary>追踪目标，每帧修正方向指向目标</summary>
        Homing,

        /// <summary>重力抛物线，受重力加速度影响下坠</summary>
        Parabolic,
    }

    /// <summary>子弹碰撞效果类型</summary>
    public enum BulletCollisionType
    {
        /// <summary>普通碰撞，命中单个目标后子弹销毁</summary>
        Normal,

        /// <summary>穿透效果，命中后继续飞行，可穿透N个目标</summary>
        Pierce,

        /// <summary>范围爆炸，命中后对周围目标造成AOE伤害</summary>
        Explosion,

        /// <summary>弹射效果，命中后弹向附近随机目标</summary>
        Bounce,
    }

    #endregion

    #region ===================== BulletConfig ScriptableObject =====================

    /// <summary>
    /// 子弹配置数据，继承 EntityConfig，添加子弹特有的战斗、弹道和碰撞参数。
    /// <para>EntityId / EntityName / Prefab / PreloadCount 继承自基类。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// // 1. 右键 → Create → 捕鱼 → BulletConfig 创建配置资产
    /// // 2. 在Inspector中配置弹道类型、碰撞效果等参数
    /// // 3. 代码中使用
    /// BulletConfig config = Resources.Load<BulletConfig>("Configs/Bullet_Normal");
    /// BulletEntity bullet = BulletFactory.SpawnByName(config.EntityName, firePos, fireRotation);
    /// </code>
    /// </example>
    [CreateAssetMenu(fileName = "BulletConfig", menuName = "捕鱼游戏/BulletConfig")]
    public class BulletConfig : EntityConfig
    {
        /// <summary>基础伤害值</summary>
        [Header("战斗属性")]
        [Tooltip("子弹命中时造成的基础伤害")]
        public int Damage = 10;

        /// <summary>子弹飞行速度（UI像素/秒 或 世界单位/秒，取决于View坐标系）</summary>
        public float Speed = 800f;

        /// <summary>最大存活时间（秒），超时自动回收，0表示不限制</summary>
        [Tooltip("超时自动回收，0表示不限制")]
        public float MaxLifetime = 5f;

        /// <summary>碰撞检测半径（UI像素 或 世界单位）</summary>
        [Header("碰撞属性")]
        [Tooltip("用于检测子弹与目标重叠的半径")]
        public float CollideRadius = 30f;

        /// <summary>碰撞效果类型</summary>
        public BulletCollisionType CollisionType = BulletCollisionType.Normal;

        /// <summary>穿透次数（仅Pierce类型有效，0表示无限穿透）</summary>
        [Tooltip("仅Pierce类型有效，0表示无限穿透")]
        public int PierceCount = 1;

        /// <summary>爆炸AOE半径（仅Explosion类型有效）</summary>
        [Tooltip("仅Explosion类型有效")]
        public float ExplosionRadius = 100f;

        /// <summary>爆炸AOE伤害系数（仅Explosion类型有效，=1时等于原始Damage）</summary>
        [Tooltip("仅Explosion类型有效，AOE伤害 = Damage * 此系数")]
        public float ExplosionDamageFactor = 0.5f;

        /// <summary>弹射次数（仅Bounce类型有效）</summary>
        [Tooltip("仅Bounce类型有效")]
        public int BounceCount = 3;

        /// <summary>弹射搜索半径（仅Bounce类型有效）</summary>
        [Tooltip("仅Bounce类型有效，搜索下一个目标的半径")]
        public float BounceSearchRadius = 200f;

        /// <summary>弹道类型</summary>
        [Header("弹道属性")]
        public BulletTrajectoryType TrajectoryType = BulletTrajectoryType.Straight;

        /// <summary>正弦波摆动频率（仅SineWave类型有效）</summary>
        [Tooltip("仅SineWave类型有效")]
        public float WaveFrequency = 4f;

        /// <summary>正弦波摆动振幅（仅SineWave类型有效，UI像素）</summary>
        [Tooltip("仅SineWave类型有效")]
        public float WaveAmplitude = 30f;

        /// <summary>追踪目标转向速度（仅Homing类型有效，度/秒）</summary>
        [Tooltip("仅Homing类型有效，度/秒，值越大转向越快")]
        public float HomingTurnRate = 180f;

        /// <summary>重力加速度（仅Parabolic类型有效，UI像素/秒²）</summary>
        [Tooltip("仅Parabolic类型有效，正值向下坠落")]
        public float Gravity = 200f;

        /// <summary>是否穿透墙壁/边界</summary>
        [Header("环境交互")]
        [Tooltip("是否在碰到屏幕边界时销毁，false则不检查边界")]
        public bool DestroyOnBoundary = true;

        /// <summary>碰到屏幕边界时反弹，与 DestroyOnBoundary 互斥（此项优先）</summary>
        [Tooltip("碰到屏幕边界反弹转向，与DestroyOnBoundary互斥")]
        public bool BounceOnBoundary;

        /// <summary>最大反弹次数，0表示无限反弹（仅BounceOnBoundary有效）</summary>
        [Tooltip("仅BounceOnBoundary有效，0表示无限反弹")]
        public int MaxBounceCount = 5;

        /// <summary>命中特效预制体（可选，命中时在命中点生成）</summary>
        [Header("表现效果")]
        public GameObject HitEffectPrefab;

        /// <summary>销毁特效预制体（可选，子弹被回收时生成）</summary>
        public GameObject DestroyEffectPrefab;

        /// <summary>命中音效名称（可选，通过AudioManager播放）</summary>
        public string HitSound;

        /// <summary>子弹飞行时自身旋转速度（度/秒，0表示不旋转）</summary>
        [Tooltip("子弹自转速度，0表示不旋转")]
        public float SpinSpeed;

        /// <summary>视图旋转补偿（欧拉角）。
        /// <para>框架约定弹头朝 +X（右），每帧会用 (0,0,朝向角) 覆盖预制体旋转；
        /// 美术预制体若自带初始姿态（如带粒子的 3D 弹），在此填入预制体原始欧拉角做补偿，
        /// 分量逐项相加，例如美术平躺姿态填 (-88.4, 0, 0)。</para>
        /// </summary>
        [Tooltip("视图旋转补偿，填美术预制体原始欧拉角，补偿框架的朝向覆盖")]
        public Vector3 ViewRotationOffset = Vector3.zero;

        /// <summary>
        /// 根据配置创建对应的子弹实体实例。
        /// <para>如果 Config 中指定了不同的子弹子类型，可在此处通过字段判断后返回不同实体。</para>
        /// </summary>
        /// <returns>BulletEntity 或其子类实例</returns>
        public override EntityBase CreateEntity()
        {
            return ConfigId switch
            {
                1001 => new BulletEntity(),
                _ => new BulletEntity(),
            };
            //return new BulletEntity();
        }
    }

    #endregion
}