using UnityEngine;

/// <summary>
/// 实体视图基类，挂载在 GameObject 上作为实体的视觉表现。
/// <para>运行时自动检测 RectTransform（UI）/ Transform（3D）决定坐标系。</para>
/// <para>显式子类 <see cref="ScreenEntityView"/> / <see cref="WorldEntityView"/> 跳过运行时检测。</para>
/// </summary>
namespace GameFramework.Common
{
    public class EntityView : MonoBehaviour, IEntityView
    {
        /// <summary>绑定的逻辑实体</summary>
        public IEntity Entity { get; private set; }

        /// <summary>
        /// 渲染旋转偏移（欧拉角），只作用于显示，不影响逻辑朝向。
        /// <para>美术朝向约定不统一时（头部朝 -X、模型直立/俯卧等）在此补偿，由实体在 OnSpawn 时写入。</para>
        /// </summary>
        public Vector3 RotationOffset { get; set; }

        /// <summary>
        /// 自定义父节点（一次性），Show() 时优先使用此节点作为父节点。
        /// <para>由 Spawn 方法在 OnSpawn 前设置，Show 执行后自动清零。</para>
        /// </summary>
        public Transform CustomParent { get; set; }

        protected RectTransform RectTransform;

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 绑定实体，同时将 View 反向注入 Entity，一次调用完成双向绑定。
        /// </summary>
        public void BindEntity(IEntity entity)
        {
            Entity = entity;
            if (entity is EntityBase eb)
            {
                eb.SetView(this);
            }
        }

        /// <summary>
        /// 设置视图位置（运行时检测 RectTransform 区分 UI/3D，子类显式覆盖跳过检测）。
        /// </summary>
        public virtual void SetPosition(Vector3 pos)
        {
            if (RectTransform != null)
            {
                RectTransform.position = pos;
            }
            else
            {
                transform.position = pos;
            }
        }

        /// <summary>
        /// 设置视图旋转（欧拉角），并叠加 <see cref="RotationOffset"/> 补偿美术朝向差异。
        /// </summary>
        public virtual void SetRotation(Vector3 eulerAngles)
        {
            transform.eulerAngles = eulerAngles + RotationOffset;
        }

        /// <summary>
        /// 显示视图。
        /// <para>优先使用 <see cref="CustomParent"/>，其次 UI 实体自动挂载到 ActiveCanvas。</para>
        /// </summary>
        public virtual void Show()
        {
            if (CustomParent != null)
            {
                transform.SetParent(CustomParent);
                CustomParent = null;
            }
            else if (RectTransform != null && ScreenEntityView.ActiveCanvas != null)
            {
                transform.SetParent(ScreenEntityView.ActiveCanvas.transform, false);
            }
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏视图。
        /// </summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 获取实体屏幕像素坐标（左下角原点）。
        /// <para>UI实体直接返回逻辑坐标XY，3D实体通过 <see cref="EntitySpace"/> 投影。</para>
        /// </summary>
        public virtual Vector2 GetScreenPosition()
        {
            if (RectTransform != null)
            {
                return new Vector2(Entity.Position.x, Entity.Position.y);
            }

            return EntitySpace.WorldToScreen(Entity.Position);
        }

        /// <summary>
        /// 播放命中特效（子类重写）。
        /// </summary>
        public void PlayHitEffect()
        {
        }

        /// <summary>
        /// 播放死亡动画（子类重写）。
        /// </summary>
        public void PlayDeathAnim()
        {
        }
    }
}