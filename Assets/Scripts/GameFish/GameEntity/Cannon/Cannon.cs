using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    /// <summary>
    /// 炮台 MonoBehaviour 实现，处理瞄准旋转、冷却计时和子弹发射。
    /// <para>挂载在炮管 GameObject 上，通过 CannonConfig 配置参数。</para>
    /// </summary>
    public class Cannon : MonoBehaviour, ICannon
    {
        [SerializeField] private CannonConfig _config;
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private int _playerId;

        private float _cooldownTimer;
        private Vector3 _initialForward;
        private Canvas _canvas;

        public int PlayerId => _playerId;
        public int CannonLevel => _config != null ? _config.Level : 1;
        public float PowerMultiplier => _config != null ? _config.PowerMultiplier : 1f;
        public float CooldownRemaining => _cooldownTimer;
        public bool CanFire => _cooldownTimer <= 0f;
        public Vector3 MuzzlePosition => _muzzlePoint != null ? _muzzlePoint.position : transform.position;
        public Vector3 AimDirection => _muzzlePoint != null ? _muzzlePoint.up : transform.up;

        private void Awake()
        {
            _initialForward = transform.right;
            // 只有 UI 炮台（RectTransform）才需要画布坐标转换；3D 炮台返回 null，走世界投影
            _canvas = transform is RectTransform ? GetComponentInParent<Canvas>() : null;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// 初始化炮台。
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        public void Initialize(int playerId)
        {
            _playerId = playerId;
            _cooldownTimer = 0f;
            _initialForward = transform.right;
        }

        /// <summary>
        /// 旋转炮台（受 MaxAimAngle 限制，相对初始朝向）。
        /// </summary>
        /// <param name="direction">正值右转，负值左转，0不转</param>
        public void Rotate(float direction)
        {
            if (direction == 0f || _config == null) return;

            float step = _config.RotationSpeed * Time.deltaTime;
            float maxAngle = _config.MaxAimAngle;

            // 计算当前相对初始朝向的角度
            float currentAngle = Vector3.SignedAngle(_initialForward, transform.right, Vector3.forward);
            // 限制旋转量不超过边界
            float desiredAngle = currentAngle + direction * step;
            float clampedAngle = Mathf.Clamp(desiredAngle, -maxAngle, maxAngle);
            float actualStep = clampedAngle - currentAngle;

            transform.Rotate(_config.RotationAxis, actualStep);
        }

        /// <summary>
        /// 发射子弹。炮口与瞄准方向为世界坐标，发射前统一转换为屏幕像素坐标。
        /// </summary>
        /// <returns>生成的 BulletEntity，冷却中返回 null</returns>
        public BulletEntity Fire()
        {
            if (!CanFire) return null;
            if (_config == null)
            {
                Debug.LogError("[Cannon] CannonConfig is null.");
                return null;
            }

            // 子弹为屏幕像素实体：按炮台所在空间（UI 画布 / 3D 世界）转换
            Vector2 muzzleScreen = ToScreen(MuzzlePosition);
            Vector2 aimScreen = ToScreen(MuzzlePosition + AimDirection * 10f);
            Vector2 direction = (aimScreen - muzzleScreen).normalized;
            if (direction == Vector2.zero)
            {
                direction = Vector2.up;
            }

            BulletEntity bullet = FishGameLauncher.BulletFactory?.SpawnById(
                _config.DefaultBulletConfigId,
                muzzleScreen,
                Vector3.zero
            );

            if (bullet != null)
            {
                bullet.Launch(muzzleScreen, new Vector3(direction.x, direction.y, 0f), _playerId);

                _cooldownTimer = _config.FireInterval;
            }

            return bullet;
        }

        /// <summary>
        /// 将炮台所在空间的坐标转换为屏幕像素坐标。
        /// <para>UI 炮台（RectTransform，位于 Canvas 下）使用画布转换；3D 炮台按世界坐标投影。</para>
        /// </summary>
        /// <param name="worldPos">待转换的位置</param>
        /// <returns>屏幕像素坐标（左下角原点）</returns>
        private Vector2 ToScreen(Vector3 worldPos)
        {
            return _canvas != null
                ? EntitySpace.UiWorldToScreen(worldPos, _canvas)
                : EntitySpace.WorldToScreen(worldPos);
        }

        /// <summary>
        /// 子弹命中回调（暂无具体逻辑，预留扩展）。
        /// </summary>
        public void OnBulletHit(EntityBase hitTarget, int damage)
        {
        }
    }
}