using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用实体管理器，场景单例，负责所有实体类型的生成调度、活跃列表维护与生命周期 Tick。
    /// <para>池化由 <see cref="EntityPoolManager"/> 统一管理，本类只维护活跃实体列表和提供生成/回收/查询接口。</para>
    /// <para>支持多种实体类型（FishEntity、BulletEntity 等），通过泛型方法按 Config 类型区分。</para>
    /// </summary>
    /// <example>
    /// 初始化和使用：
    /// <code>
    /// // 预加载（实体创建委托给 EntityConfig.CreateEntity，无需手动注册工厂）
    /// EntityManager.Instance.PrewarmAll();
    ///
    /// // 按ID生成
    /// FishEntity fish = em.SpawnById<FishConfig, FishEntity>(1001, position, rotation);
    ///
    /// // 按名称生成
    /// BulletEntity bullet = em.SpawnByName<BulletConfig, BulletEntity>("NormalBullet", position);
    ///
    /// // 回收
    /// em.Despawn(fish);
    ///
    /// // 查询所有活跃的鱼（零分配；已标记待移除的实体自动跳过）
    /// foreach (var entity in em.GetActive<FishConfig>())
    /// {
    ///     var fish = (FishEntity)entity;
    ///     Debug.Log(fish.GetConfig().EntityName);
    /// }
    ///
    /// // 查询指定ID的活跃实体（零分配）
    /// var bossFish = em.GetActiveById<FishConfig>(1001);
    ///
    /// // 回收所有鱼
    /// em.DespawnAll<FishConfig>();
    /// </code>
    /// </example>
    public class EntityManager : MonoBehaviour
    {
        /// <summary>场景单例</summary>
        public static EntityManager Instance { get; private set; }

        /// <summary>按 ConfigType 分组存储所有活跃实体</summary>
        private readonly Dictionary<Type, List<EntityBase>> _activeByConfigType = new Dictionary<Type, List<EntityBase>>();

        /// <summary>按 (ConfigType, ConfigId) 分组存储活跃实体，用于按ID精确查询</summary>
        private readonly Dictionary<(Type ConfigType, int ConfigId), List<EntityBase>> _activeByKey = new Dictionary<(Type, int), List<EntityBase>>();

        /// <summary>所有活跃实体的扁平列表，用于 Update 中快速遍历 Tick</summary>
        private readonly List<EntityBase> _allActive = new List<EntityBase>();

        /// <summary>Update 使用的稳定快照，避免在 Tick 遍历中因 Spawn 导致集合修改异常</summary>
        private readonly List<EntityBase> _updateSnapshot = new List<EntityBase>(1024);

        /// <summary>Flush 时使用的待移除快照，避免回收回调修改待移除集合时破坏遍历</summary>
        private readonly List<EntityBase> _pendingRemoveSnapshot = new List<EntityBase>();

        /// <summary>待移除集合（HashSet），支持 O(1) 查询和 TrackActive 取消</summary>
        private readonly HashSet<EntityBase> _pendingRemoveSet = new HashSet<EntityBase>();

        /// <summary>反向索引：实体 → (ConfigType, ConfigId)，使 FlushPendingRemove 中移除为 O(1)</summary>
        private readonly Dictionary<EntityBase, (Type ConfigType, int ConfigId)> _entityMeta = new Dictionary<EntityBase, (Type, int)>();

        private bool _isUpdating;
        private bool _isFlushingPendingRemove;

        /// <summary>
        /// 所有活跃实体的只读视图，用于外部遍历。
        /// <para>遍历时自动跳过已标记待移除的实体，调用方无需再手动判 <see cref="IsPendingRemove"/>。
        /// struct 枚举器，<c>foreach</c> 零分配。</para>
        /// </summary>
        public ActiveEntityEnumerable AllActive => new ActiveEntityEnumerable(_allActive, _pendingRemoveSet);

        /// <summary>
        /// 判断实体是否已进入待移除队列（帧末回收）。
        /// <para>遍历活跃列表做碰撞/查询时应跳过这类实体，避免同帧命中已被回收的实体。</para>
        /// </summary>
        /// <param name="entity">目标实体</param>
        /// <returns>已标记待移除返回 true</returns>
        public bool IsPendingRemove(EntityBase entity)
        {
            return entity != null && _pendingRemoveSet.Contains(entity);
        }

        /// <summary>
        /// 场景单例初始化。
        /// <para>若已存在其他实例则销毁自身并直接返回，保证场景内只保留一个 EntityManager。</para>
        /// <para>成为单例后把池根节点注入 <see cref="EntityPoolManager"/>，使池对象挂在本管理器下随场景一起销毁。</para>
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 池对象挂在实体管理器下，场景卸载时随管理器一起销毁
            EntityPoolManager.Initialize(transform);
        }

        private void Update()
        {
            _isUpdating = true;
            try
            {
                _updateSnapshot.Clear();
                _updateSnapshot.AddRange(_allActive);

                float dt = Time.deltaTime;
                foreach (var entity in _updateSnapshot)
                {
                    if (entity != null && entity.IsActive && !_pendingRemoveSet.Contains(entity))
                    {
                        // 先刷新屏幕缓存，保证本帧所有实体的命中判定读到同一份数据
                        entity.ScreenPosition = ResolveScreenPosition(entity);
                        entity.ScreenRadius = entity.ComputeScreenRadius();

                        entity.Tick(dt);
                    }
                }
            }
            finally
            {
                try
                {
                    FlushPendingRemove();
                }
                finally
                {
                    _updateSnapshot.Clear();
                    _isUpdating = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            // 先回收活跃实体，再清空池，避免留下悬空引用
            DespawnAll();
            EntityPoolManager.ClearAll();
            Instance = null;
        }

        #region Spawn

        /// <summary>
        /// 根据 EntityConfig 直接生成实体（返回基类），自动加入活跃列表。
        /// </summary>
        /// <param name="config">实体配置资产</param>
        /// <param name="position">出生位置，为 null 时不设置</param>
        /// <param name="rotation">出生旋转（欧拉角），为 null 时不设置</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public EntityBase Spawn(EntityConfig config, Vector3? position = null, Vector3? rotation = null, Transform parent = null)
        {
            if (config == null)
            {
                Debug.LogError("[EntityManager] Spawn failed: config is null");
                return null;
            }

            var entity = EntityPoolManager.Spawn(config, position, rotation, parent);
            if (entity == null) return null;

            // OnSpawn 内可能自行回收（如配置绑定失败，走「生成即回收」路径）。此时实体已进入待移除队列，
            // 但 base.OnSpawn 已经把 IsActive 置为 true，仅靠 IsActive 判断不出来，必须再看 IsPendingRemove。
            // 必须在 TrackActive 之前判断：TrackActive 会取消待移除标记，
            // 而这个实体从未被真正追踪过，取消后它既不在活跃列表也不会被帧末回收，将永久滞留在池外。
            if (!entity.IsActive || IsPendingRemove(entity))
            {
                // 留在待移除队列里，交给下一次 FlushPendingRemove 正常归还到对象池
                return null;
            }

            // 生成即刷新屏幕缓存，避免首帧命中判定读到默认值
            entity.ScreenPosition = ResolveScreenPosition(entity);
            entity.ScreenRadius = entity.ComputeScreenRadius();

            TrackActive(entity, config.GetType(), config.ConfigId);
            return entity;
        }

        /// <summary>
        /// 根据配置类型和 ConfigId 生成实体（返回指定实体类型），自动加入活跃列表。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <typeparam name="TEntity">EntityBase 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <param name="position">出生位置，为 null 时不设置</param>
        /// <param name="rotation">出生旋转（欧拉角），为 null 时不设置</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public TEntity SpawnById<TConfig, TEntity>(int configId, Vector3? position = null, Vector3? rotation = null, Transform parent = null)
            where TConfig : EntityConfig
            where TEntity : EntityBase
        {
            var config = EntityConfigManager.GetById<TConfig>(configId);
            if (config == null)
            {
                Debug.LogError($"[EntityManager] SpawnById failed: {typeof(TConfig).Name} ID={configId} not found");
                return null;
            }
            return SpawnAs<TConfig, TEntity>(config, position, rotation, parent, $"ID={configId}");
        }

        /// <summary>
        /// 根据配置类型和 EntityName 生成实体（返回指定实体类型），自动加入活跃列表。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <typeparam name="TEntity">EntityBase 子类型</typeparam>
        /// <param name="entityName">实体名称</param>
        /// <param name="position">出生位置，为 null 时不设置</param>
        /// <param name="rotation">出生旋转（欧拉角），为 null 时不设置</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public TEntity SpawnByName<TConfig, TEntity>(string entityName, Vector3? position = null, Vector3? rotation = null, Transform parent = null)
            where TConfig : EntityConfig
            where TEntity : EntityBase
        {
            var config = EntityConfigManager.GetByName<TConfig>(entityName);
            if (config == null)
            {
                Debug.LogError($"[EntityManager] SpawnByName failed: {typeof(TConfig).Name} Name='{entityName}' not found");
                return null;
            }
            return SpawnAs<TConfig, TEntity>(config, position, rotation, parent, $"Name='{entityName}'");
        }

        /// <summary>
        /// 生成实体并校验其运行时类型，供 SpawnById / SpawnByName 共用。
        /// <para>泛型参数只提供编译期约束，运行期实体类型由 <see cref="EntityConfig.CreateEntity"/> 决定。
        /// 二者不一致时，直接强转会抛 <see cref="System.InvalidCastException"/>，且堆栈落在框架内部、看不出是哪个配置写错了。
        /// 这里把错误提前成一条可读日志，并回收掉类型不符的实体。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <typeparam name="TEntity">调用方期望的实体类型</typeparam>
        /// <param name="config">已解析的配置资产</param>
        /// <param name="position">出生位置</param>
        /// <param name="rotation">出生旋转（欧拉角）</param>
        /// <param name="parent">自定义父节点</param>
        /// <param name="source">错误日志里标识配置来源的文本（ID 或 Name）</param>
        /// <returns>类型匹配的实体；类型不符或生成失败时返回 null</returns>
        internal TEntity SpawnAs<TConfig, TEntity>(EntityConfig config, Vector3? position, Vector3? rotation, Transform parent, string source)
            where TConfig : EntityConfig
            where TEntity : EntityBase
        {
            var entity = Spawn(config, position, rotation, parent);
            if (entity == null) return null;

            if (entity is TEntity typed)
            {
                return typed;
            }

            Debug.LogError(
                $"[EntityManager] Spawn type mismatch: {typeof(TConfig).Name}({source}) 生成的是 {entity.GetType().Name}，" +
                $"调用方却要求 {typeof(TEntity).Name}。请检查该配置的 CreateEntity 实现。实体已回收。");

            // 类型不符属于配置错误：实体已进入活跃列表，必须回收，否则调用方拿不到引用、
            // 但实体仍在被 Tick 且永不释放。
            Despawn(entity);
            return null;
        }

        #endregion

        #region Despawn

        /// <summary>
        /// 回收实体到对象池，自动从活跃列表中移除。
        /// <para>注意：回收是延迟到帧末统一执行的——本方法返回时 <see cref="EntityBase.OnDespawn"/>
        /// 尚未触发，实体也未真正回池。期间实体处于待移除状态，可用 <see cref="IsPendingRemove"/> 查询；
        /// 需要立即执行回收回调的场景请勿依赖本方法。</para>
        /// </summary>
        /// <param name="entity">要回收的实体</param>
        public void Despawn(EntityBase entity)
        {
            if (entity == null || !entity.IsActive) return;

            UntrackActive(entity);
            if (!_isUpdating && !_isFlushingPendingRemove)
            {
                FlushPendingRemove();
            }
        }

        /// <summary>
        /// 回收指定配置类型的所有活跃实体。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        public void DespawnAll<TConfig>() where TConfig : EntityConfig
        {
            var configType = typeof(TConfig);
            if (!_activeByConfigType.TryGetValue(configType, out var list)) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                UntrackActive(list[i]);
            }

            if (!_isUpdating && !_isFlushingPendingRemove)
            {
                FlushPendingRemove();
            }
        }

        /// <summary>回收所有活跃实体</summary>
        public void DespawnAll()
        {
            for (int i = _allActive.Count - 1; i >= 0; i--)
            {
                UntrackActive(_allActive[i]);
            }

            if (!_isUpdating && !_isFlushingPendingRemove)
            {
                FlushPendingRemove();
            }
        }

        #endregion

        #region Query

        /// <summary>
        /// 获取指定配置类型的所有活跃实体，零分配。
        /// <para>遍历时自动跳过已标记待移除的实体。调用方需自行对元素做类型转换，如 <c>(FishEntity)entity</c>。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <returns>活跃实体只读视图，无匹配时返回空集合</returns>
        public ActiveEntityEnumerable GetActive<TConfig>() where TConfig : EntityConfig
        {
            if (_activeByConfigType.TryGetValue(typeof(TConfig), out var list))
            {
                return new ActiveEntityEnumerable(list, _pendingRemoveSet);
            }
            return ActiveEntityEnumerable.Empty;
        }

        /// <summary>
        /// 获取指定配置类型和 ConfigId 的所有活跃实体，零分配。
        /// <para>遍历时自动跳过已标记待移除的实体。调用方需自行对元素做类型转换。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <returns>活跃实体只读视图，无匹配时返回空集合</returns>
        public ActiveEntityEnumerable GetActiveById<TConfig>(int configId) where TConfig : EntityConfig
        {
            if (_activeByKey.TryGetValue((typeof(TConfig), configId), out var list))
            {
                return new ActiveEntityEnumerable(list, _pendingRemoveSet);
            }
            return ActiveEntityEnumerable.Empty;
        }

        /// <summary>
        /// 获取指定配置类型的活跃实体数量。
        /// <para>与 <see cref="GetActive{TConfig}"/> 口径一致：已标记待移除的实体不计入。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        public int GetActiveCount<TConfig>() where TConfig : EntityConfig
        {
            return _activeByConfigType.TryGetValue(typeof(TConfig), out var list) ? CountLive(list) : 0;
        }

        /// <summary>获取所有活跃实体总数（已标记待移除的不计入）</summary>
        public int TotalActiveCount => CountLive(_allActive);

        /// <summary>
        /// 统计列表中仍存活且未被标记回收的实体数量。
        /// <para>O(n)，仅供 UI/调试查询，不要放进每帧热路径。</para>
        /// </summary>
        private int CountLive(List<EntityBase> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var entity = list[i];
                if (entity != null && entity.IsActive && !_pendingRemoveSet.Contains(entity))
                {
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region Prewarm

        /// <summary>
        /// 预热指定配置类型的对象池。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public void Prewarm<TConfig>(int configId, int count = 0) where TConfig : EntityConfig
        {
            EntityPoolManager.Prewarm<TConfig>(configId, count);
        }

        /// <summary>
        /// 根据配置类型和 EntityName 预热指定配置类型的对象池。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="entityName">实体名称</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public void Prewarm<TConfig>(string entityName, int count = 0) where TConfig : EntityConfig
        {
            EntityPoolManager.Prewarm<TConfig>(entityName, count);
        }

        /// <summary>
        /// 预加载所有已注册配置的实体。
        /// <para>会自动调用 EntityConfigManager.Preload 确保配置已加载。</para>
        /// </summary>
        public void PrewarmAll()
        {
            EntityPoolManager.PrewarmAll();
        }

        #endregion

        #region Internal Tracking

        /// <summary>将实体加入活跃追踪，同时取消其待移除状态（防止同帧回收再 Spawn 被误删或重复添加）</summary>
        private void TrackActive(EntityBase entity, Type configType, int configId)
        {
            if (_pendingRemoveSet.Remove(entity))
            {
                // 实体之前在待移除队列中，说明是同帧回收再生成。
                // 此时实体仍在追踪列表里（移除是延迟的），config 也未变，直接返回，避免重复添加。
                return;
            }

            if (!_activeByConfigType.TryGetValue(configType, out var typeList))
            {
                typeList = new List<EntityBase>();
                _activeByConfigType[configType] = typeList;
            }
            typeList.Add(entity);

            var key = (configType, configId);
            if (!_activeByKey.TryGetValue(key, out var keyList))
            {
                keyList = new List<EntityBase>();
                _activeByKey[key] = keyList;
            }
            keyList.Add(entity);

            _allActive.Add(entity);
            _entityMeta[entity] = (configType, configId);
        }

        /// <summary>将实体从活跃追踪中移除（延迟到帧末处理，避免在 Tick 遍历中修改集合）</summary>
        private void UntrackActive(EntityBase entity)
        {
            _pendingRemoveSet.Add(entity);
        }

        /// <summary>帧末统一清理追踪列表，并归还到对象池。反向索引保证 O(1) 移除</summary>
        private void FlushPendingRemove()
        {
            if (_pendingRemoveSet.Count == 0 || _isFlushingPendingRemove) return;

            _isFlushingPendingRemove = true;
            try
            {
                while (_pendingRemoveSet.Count > 0)
                {
                    _pendingRemoveSnapshot.Clear();
                    foreach (var entity in _pendingRemoveSet)
                    {
                        _pendingRemoveSnapshot.Add(entity);
                    }

                    foreach (var entity in _pendingRemoveSnapshot)
                    {
                        if (!_pendingRemoveSet.Remove(entity)) continue;

                        _allActive.Remove(entity);
                        if (_entityMeta.TryGetValue(entity, out var meta))
                        {
                            if (_activeByConfigType.TryGetValue(meta.ConfigType, out var typeList))
                                typeList.Remove(entity);
                            if (_activeByKey.TryGetValue((meta.ConfigType, meta.ConfigId), out var keyList))
                                keyList.Remove(entity);
                            _entityMeta.Remove(entity);
                        }
                        EntityPoolManager.Despawn(entity);
                    }
                }
            }
            finally
            {
                _pendingRemoveSnapshot.Clear();
                _isFlushingPendingRemove = false;
            }
        }

        /// <summary>
        /// 解析实体的屏幕像素坐标。
        /// <para>有 View 时由 View 提供；纯数据实体按空间语义回退：屏幕空间实体直接取 Position，3D 实体做世界投影。</para>
        /// </summary>
        /// <param name="entity">目标实体</param>
        /// <returns>屏幕像素坐标（左下角原点）</returns>
        private Vector2 ResolveScreenPosition(EntityBase entity)
        {
            if (entity.View != null)
            {
                return entity.View.GetScreenPosition();
            }

            if (entity is IScreenSpaceEntity)
            {
                return entity.Position;
            }

            return EntitySpace.WorldToScreen(entity.Position);
        }

        #endregion
    }
}