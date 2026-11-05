using UnityEngine;

namespace GameFish
{
    /// <summary>
    /// 炮台配置（ScriptableObject），策划在 Inspector 中调整参数。
    /// </summary>
    [CreateAssetMenu(fileName = "CannonConfig", menuName = "捕鱼游戏/Cannon Config")]
    public class CannonConfig : ScriptableObject
    {
        [Header("基础属性")]
        /// <summary>炮台等级</summary>
        public int Level;

        /// <summary>发射间隔（秒）</summary>
        public float FireInterval = 0.3f;

        /// <summary>威力倍率</summary>
        public float PowerMultiplier = 1f;

        /// <summary>默认发射的子弹配置ID</summary>
        public int DefaultBulletConfigId = 1;

        [Header("操控")]
        /// <summary>炮管旋转速度（度/秒）</summary>
        public float RotationSpeed = 360f;

        /// <summary>最大瞄准角度（相对于垂直方向）</summary>
        public float MaxAimAngle = 80f;

        [Header("表现")]
        /// <summary>炮管旋转轴（默认 Forward）</summary>
        public Vector3 RotationAxis = Vector3.forward;

        /// <summary>开火时炮管后座距离</summary>
        public float RecoilDistance = 5f;

        /// <summary>炮管回弹速度</summary>
        public float RecoilRecoverSpeed = 100f;
    }
}