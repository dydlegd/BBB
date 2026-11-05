using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 实体基类，提供 IEntity 的默认实现。所有具体实体类应继承此类。
    /// <para>自动分配递增ID。</para>
    /// </summary>
    public abstract class EntityBase : IEntity
    {
        private static int _nextId;

        /// <summary>
        /// 实体唯一标识ID。
        /// <para>池化实体每次生成都会重新分配，保证「一次生成」的ID全局唯一：
        /// 可以安全地用 EntityId 做伤害去重、字典键或 UI 绑定，不会与上一轮复用的同一实例冲突。</para>
        /// </summary>
        public int EntityId { get; private set; }

        /// <summary>实体类型标识，默认使用类名。</summary>
        public string EntityType { get; protected set; }

        /// <summary>实体是否处于活跃（已生成）状态。</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 逻辑位置。3D实体使用世界坐标，2D实体（子弹/UI特效）使用屏幕像素坐标（左下角原点）。
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>本帧屏幕像素坐标缓存，由 EntityManager 每帧刷新</summary>
        public Vector2 ScreenPosition { get; internal set; }

        /// <summary>本帧屏幕像素半径缓存，由 EntityManager 每帧刷新</summary>
        public float ScreenRadius { get; internal set; }

        /// <summary>世界旋转（欧拉角）。</summary>
        public Vector3 Rotation { get; set; }

        /// <summary>实体视图（可为 null，支持纯数据实体）。</summary>
        public IEntityView View { get; private set; }

        /// <summary>
        /// 使用自动递增ID创建实体。
        /// </summary>
        protected EntityBase()
        {
            AssignNewEntityId();
            EntityType = GetType().Name;
        }

        /// <summary>
        /// 为本次生成分配新的实体ID。
        /// <para>由 <see cref="EntityPoolManager"/> 在每次从池中取出实体后调用。
        /// 放在框架层而不是 <see cref="OnSpawn"/>，是为了不依赖子类是否调用了 base.OnSpawn()。</para>
        /// </summary>
        internal void AssignNewEntityId()
        {
            EntityId = _nextId++;
        }

        /// <summary>绑定视图，通常在 EntitySystem.Spawn 中调用。</summary>
        public void SetView(IEntityView view)
        {
            View = view;
        }

        /// <summary>
        /// View 是否仍可安全访问。
        /// <para>编辑器退出播放/场景卸载时，Unity 可能先销毁 View 再回调实体 OnDespawn，
        /// 此时接口引用非 null 但底层对象已被销毁，必须用 Unity 的 == null 语义判断。</para>
        /// </summary>
        private bool HasLiveView
        {
            get
            {
                if (View == null) return false;
                return !(View is UnityEngine.Object unityObject) || unityObject != null;
            }
        }

        /// <summary>将 Position/Rotation 同步到 View 的 Transform。</summary>
        public void SyncTransform()
        {
            if (!HasLiveView) return;
            View.SetPosition(Position);
            View.SetRotation(Rotation);
        }

        /// <summary>
        /// 每次循环的入口方法，包含更新逻辑和 Transform 同步。
        /// EntitySystem 应调用此方法而非直接调用 OnUpdate。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间（秒）</param>
        public void Tick(float deltaTime)
        {
            OnUpdate(deltaTime);
            SyncTransform();
        }

        /// <summary>
        /// 实体生成/从对象池取出时调用，子类重写以添加逻辑。
        /// <para>池化复用要求：子类必须在 OnSpawn 中初始化全部可变状态（血量、速度、计时器等扩展字段），
        /// 不能假设字段保留了上一轮 OnDespawn 的清理结果——框架只归零 Position/Rotation，
        /// 子类自定义状态的清零完全由子类自己负责。</para>
        /// </summary>
        public virtual void OnSpawn()
        {
            if (HasLiveView) View.Show();
            SyncTransform();
            IsActive = true;
        }

        /// <summary>
        /// 实体回收/回到对象池时调用，子类重写以添加逻辑。
        /// <para>与 OnSpawn 配对：子类扩展状态（血量、速度、事件订阅等）应在此清理或复位，
        /// 避免残留数据被下一次池化复用读到。</para>
        /// </summary>
        public virtual void OnDespawn()
        {
            if (HasLiveView) View.Hide();
            Position = Vector3.zero;
            Rotation = Vector3.zero;
            IsActive = false;
        }

        /// <summary>
        /// 计算本帧屏幕像素半径。
        /// <para>2D实体返回配置中的像素半径，3D实体重写为世界半径投影。</para>
        /// </summary>
        protected internal virtual float ComputeScreenRadius()
        {
            return 0f;
        }

        /// <summary>每帧更新，子类重写以添加逻辑。</summary>
        public virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// 实体绑定配置时调用（在 OnSpawn 之前）。
        /// <para>子类重写以存储具体类型的配置引用。</para>
        /// </summary>
        /// <param name="config">关联的实体配置</param>
        protected internal virtual void OnBindConfig(EntityConfig config)
        {
        }
    }
}