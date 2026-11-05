using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用实体池管理器，维护不同类型的 IEntity 对象池。
    /// <para>根据 EntityConfig 配置自动管理实体逻辑对象（EntityBase 子类）与视图对象（EntityView/GameObject）的池化。</para>
    /// <para>实体创建委托给 config.CreateEntity()，子类可据此实现"一种 Config → 多种实体类型"。</para>
    /// <para>仅供 <see cref="EntityManager"/> 内部使用；外部请通过 EntityManager / EntityFactory 生成与回收，以保证实体被活跃追踪。</para>
    /// </summary>
    /// <example>
    /// 初始化预加载（由 EntityManager 调用）：
    /// <code>
    /// EntityManager.Instance.PrewarmAll();
    /// </code>
    /// </example>
    internal static class EntityPoolManager
    {
        /// <summary>
        /// 内部池条目，维护一种实体配置对应的逻辑实体栈与视图对象池。
        /// </summary>
        private class EntityPoolEntry
        {
            public EntityConfig Config;
            public ObjectPool<EntityView> ViewPool;
            public Transform Root;
            public readonly Stack<EntityBase> EntityStack = new Stack<EntityBase>();

            public Type ConfigType => Config.GetType();
            public int ConfigId => Config.ConfigId;
        }

        private static Transform _poolRoot;
        private static readonly Dictionary<(Type ConfigType, int ConfigId), EntityPoolEntry> Pools = new Dictionary<(Type, int), EntityPoolEntry>();
        private static readonly Dictionary<EntityBase, EntityPoolEntry> EntityToPool = new Dictionary<EntityBase, EntityPoolEntry>();

        /// <summary>
        /// 由 EntityManager 注入池根节点，使池对象生命周期与场景实体管理器一致。
        /// </summary>
        /// <param name="root">池根节点，通常为 EntityManager 自身 Transform</param>
        internal static void Initialize(Transform root)
        {
            _poolRoot = root;
        }

        /// <summary>
        /// 池根节点 Transform，所有池内 GameObject 均挂在此节点下。
        /// </summary>
        public static Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("EntityPoolRoot");
                    _poolRoot = go.transform;
                }

                return _poolRoot;
            }
        }

        /// <summary>已注册的池总数。</summary>
        public static int PoolCount => Pools.Count;

        /// <summary>
        /// 根据配置类型和 ConfigId 生成实体（返回基类）。
        /// <para>实体从池中取出或新建，自动绑定 View 并调用 OnSpawn。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public static EntityBase Spawn<TConfig>(int configId, Transform parent = null) where TConfig : EntityConfig
        {
            var config = EntityConfigManager.GetById<TConfig>(configId);
            if (config == null)
            {
                Debug.LogError($"[EntityPoolManager] Config not found: {typeof(TConfig).Name} ID={configId}");
                return null;
            }
            return SpawnWithConfig(config, null, null, parent);
        }

        /// <summary>
        /// 根据配置类型和 ConfigId 生成实体（返回指定实体类型）。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <typeparam name="TEntity">EntityBase 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public static TEntity Spawn<TConfig, TEntity>(int configId, Transform parent = null)
            where TConfig : EntityConfig
            where TEntity : EntityBase
        {
            return (TEntity)Spawn<TConfig>(configId, parent);
        }

        /// <summary>
        /// 根据配置类型和 EntityName 生成实体（返回基类）。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="entityName">实体名称</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public static EntityBase SpawnByName<TConfig>(string entityName, Transform parent = null) where TConfig : EntityConfig
        {
            var config = EntityConfigManager.GetByName<TConfig>(entityName);
            if (config == null)
            {
                Debug.LogError($"[EntityPoolManager] Config not found: {typeof(TConfig).Name} Name='{entityName}'");
                return null;
            }
            return SpawnWithConfig(config, null, null, parent);
        }

        /// <summary>
        /// 根据配置类型和 EntityName 生成实体（返回指定实体类型）。
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <typeparam name="TEntity">EntityBase 子类型</typeparam>
        /// <param name="entityName">实体名称</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        public static TEntity SpawnByName<TConfig, TEntity>(string entityName, Transform parent = null)
            where TConfig : EntityConfig
            where TEntity : EntityBase
        {
            var entity = SpawnByName<TConfig>(entityName, parent);
            if (entity == null) return null;

            if (entity is TEntity typed)
            {
                return typed;
            }

            Debug.LogError(
                $"[EntityPoolManager] Spawn type mismatch: {typeof(TConfig).Name} Name='{entityName}' 生成的是 {entity.GetType().Name}，" +
                $"调用方却要求 {typeof(TEntity).Name}。请检查该配置的 CreateEntity 实现。");

            // 该实体未经 EntityManager 追踪，直接归还池中即可，不会留下活跃列表残留
            Despawn(entity);
            return null;
        }

        /// <summary>
        /// 根据 EntityConfig 直接生成实体。
        /// <para>从池中取出或新建，先写入初始位置/朝向再调用 OnSpawn，保证 OnSpawn 内读到的是本次生成的正确数据。</para>
        /// </summary>
        /// <param name="config">实体配置资产</param>
        /// <param name="position">初始位置，null 时保留实体归零后的值</param>
        /// <param name="rotation">初始朝向（欧拉角），null 时保留归零值</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>生成的实体实例，失败返回 null</returns>
        internal static EntityBase Spawn(EntityConfig config, Vector3? position = null, Vector3? rotation = null, Transform parent = null)
        {
            if (config == null)
            {
                Debug.LogError("[EntityPoolManager] Cannot spawn with null config.");
                return null;
            }
            return SpawnWithConfig(config, position, rotation, parent);
        }

        /// <summary>
        /// 回收实体到池中。
        /// <para>自动调用 OnDespawn → 解绑 View → 回收 EntityView → 回收 EntityBase。</para>
        /// </summary>
        /// <param name="entity">要回收的实体</param>
        public static void Despawn(EntityBase entity)
        {
            if (entity == null)
            {
                Debug.LogError("[EntityPoolManager] Cannot despawn null entity.");
                return;
            }

            if (!EntityToPool.TryGetValue(entity, out var entry))
            {
                Debug.LogWarning($"[EntityPoolManager] Entity (ID={entity.EntityId}, Type={entity.EntityType}) is not managed by EntityPoolManager.");
                return;
            }

            EntityToPool.Remove(entity);

            entity.OnDespawn();

            // View 可能已被 Unity 先销毁（如退出播放时），此时跳过回收，避免访问已销毁对象
            if (entry.ViewPool != null && entity.View is EntityView view && view != null)
            {
                entry.ViewPool.Release(view);
            }

            // 解绑 View：避免池内逻辑实体继续持有已归还的视图引用
            entity.SetView(null);

            entry.EntityStack.Push(entity);
        }

        /// <summary>
        /// 根据 EntityConfig 直接预加载，使用 config.PreloadCount 作为预加载数量。
        /// <para>View 和 Entity 独立批量创建，各自入池，生成时再通过 BindEntity 配对。</para>
        /// </summary>
        /// <param name="config">实体配置资产</param>
        public static void Prewarm(EntityConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[EntityPoolManager] Cannot prewarm null config.");
                return;
            }

            PrewarmInternal(config, config.GetType(), config.PreloadCount);
        }

        /// <summary>
        /// 根据配置类型和 ConfigId 预加载指定数量的实体。
        /// <para>View 和 Entity 独立批量创建，各自入池。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="configId">实体配置ID</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public static void Prewarm<TConfig>(int configId, int count = 0) where TConfig : EntityConfig
        {
            var config = EntityConfigManager.GetById<TConfig>(configId);
            if (config == null)
            {
                Debug.LogError($"[EntityPoolManager] Config not found: {typeof(TConfig).Name} ID={configId}");
                return;
            }

            int preloadCount = count > 0 ? count : config.PreloadCount;
            PrewarmInternal(config, typeof(TConfig), preloadCount);
        }

        /// <summary>
        /// 预加载所有已注册配置的实体（使用每个 config.PreloadCount）。
        /// <para>会自动调用 EntityConfigManager.Preload 确保配置已加载。</para>
        /// </summary>
        public static void PrewarmAll()
        {
            EntityConfigManager.Preload();
            var allConfigs = EntityConfigManager.GetAll();
            foreach (var config in allConfigs)
            {
                Prewarm(config);
            }
        }

        /// <summary>
        /// 根据配置类型和 EntityName 预加载指定数量的实体。
        /// <para>View 和 Entity 独立批量创建，各自入池。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="entityName">实体名称</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public static void Prewarm<TConfig>(string entityName, int count = 0) where TConfig : EntityConfig
        {
            var config = EntityConfigManager.GetByName<TConfig>(entityName);
            if (config == null)
            {
                Debug.LogError($"[EntityPoolManager] Config not found: {typeof(TConfig).Name} Name='{entityName}'");
                return;
            }

            int preloadCount = count > 0 ? count : config.PreloadCount;
            PrewarmInternal(config, typeof(TConfig), preloadCount);
        }

        /// <summary>
        /// 预加载核心逻辑：确保池中 Entity 和 View 数量达到 targetCount。
        /// <para>只补充不足的数量，已有实例不受影响。</para>
        /// </summary>
        /// <param name="config">实体配置资产</param>
        /// <param name="configType">配置的具体类型</param>
        /// <param name="targetCount">目标预加载总数</param>
        private static void PrewarmInternal(EntityConfig config, Type configType, int targetCount)
        {
            if (targetCount <= 0) return;

            var entry = GetOrCreatePoolEntry(configType, config.ConfigId, config);

            if (entry.ViewPool != null)
            {
                int viewsNeeded = targetCount - entry.ViewPool.AvailableCount;
                if (viewsNeeded > 0)
                {
                    entry.ViewPool.Prewarm(viewsNeeded);
                }
            }

            int entitiesNeeded = targetCount - entry.EntityStack.Count;
            for (int i = 0; i < entitiesNeeded; i++)
            {
                var entity = CreateEntity(config);
                entity.OnBindConfig(config);
                entry.EntityStack.Push(entity);
            }
        }

        /// <summary>
        /// 清空指定类型的实体池（销毁所有已池化的 View 和 Entity）。
        /// <para>该池仍有活跃实体时拒绝清理并输出警告：先通过 EntityManager 回收活跃实体后再调用，
        /// 否则活跃实体后续 Despawn 将无法归还。</para>
        /// </summary>
        /// <typeparam name="TConfig">EntityConfig 子类型</typeparam>
        /// <param name="entityId">实体配置ID</param>
        public static void ClearPool<TConfig>(int entityId) where TConfig : EntityConfig
        {
            var key = (typeof(TConfig), entityId);
            if (!Pools.TryGetValue(key, out var entry)) return;

            // EntityToPool 中登记的是活跃实体：该池仍有实体在场上时不允许清池
            foreach (var activeEntry in EntityToPool.Values)
            {
                if (activeEntry == entry)
                {
                    Debug.LogWarning($"[EntityPoolManager] ClearPool ({typeof(TConfig).Name} ID={entityId}) 被拒绝：该池仍有活跃实体，请先回收。");
                    return;
                }
            }

            entry.ViewPool?.Clear();

            foreach (var entity in entry.EntityStack)
            {
                if (entity != null && EntityToPool.ContainsKey(entity))
                {
                    EntityToPool.Remove(entity);
                }
            }

            entry.EntityStack.Clear();
            Pools.Remove(key);

            if (entry.Root != null)
            {
                UnityEngine.Object.Destroy(entry.Root.gameObject);
            }
        }

        /// <summary>
        /// 清空所有实体池（销毁所有已池化的 View 和 Entity）。
        /// </summary>
        public static void ClearAll()
        {
            foreach (var kvp in Pools)
            {
                kvp.Value.ViewPool?.Clear();

                foreach (var entity in kvp.Value.EntityStack)
                {
                    if (entity != null && EntityToPool.ContainsKey(entity))
                    {
                        EntityToPool.Remove(entity);
                    }
                }

                kvp.Value.EntityStack.Clear();

                if (kvp.Value.Root != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.Root.gameObject);
                }
            }

            Pools.Clear();
            EntityToPool.Clear();
        }

        private static EntityPoolEntry GetOrCreatePoolEntry(Type configType, int entityId, EntityConfig config)
        {
            var key = (configType, entityId);
            if (Pools.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var entry = new EntityPoolEntry { Config = config };
            var parent = new GameObject($"{configType.Name}_{entityId}_Pool").transform;
            parent.SetParent(PoolRoot);

            entry.Root = parent;
            // Prefab 为 null 时为纯数据实体，不创建 View 池
            entry.ViewPool = config.Prefab != null ? new ObjectPool<EntityView>(config.Prefab, parent) : null;
            Pools[key] = entry;
            return entry;
        }

        private static EntityBase SpawnWithConfig(EntityConfig config, Vector3? position, Vector3? rotation, Transform parent)
        {
            var configType = config.GetType();
            var entry = GetOrCreatePoolEntry(configType, config.ConfigId, config);

            EntityBase entity;
            if (entry.EntityStack.Count > 0)
            {
                entity = entry.EntityStack.Pop();
            }
            else
            {
                entity = CreateEntity(config);
            }

            // 每次生成都分配新ID。池化复用返回的是同一个实例，若ID沿用构造时那一个，
            // 外部基于 EntityId 的去重/缓存（如子弹的 HitTargetIds）会把新一轮误判成上一轮已处理，
            // 导致目标「免疫」。放在 OnBindConfig 之前，保证后续绑定与日志读到的都是新ID。
            entity.AssignNewEntityId();

            entity.OnBindConfig(config);

            // Prefab 为 null 时为纯数据实体，跳过 View 绑定
            if (entry.ViewPool != null)
            {
                var view = entry.ViewPool.Get();
                if (view == null)
                {
                    // View 创建失败时不污染池，实体原样归还
                    entry.EntityStack.Push(entity);
                    return null;
                }

                view.BindEntity(entity);

                if (parent != null)
                {
                    view.CustomParent = parent;
                }
            }

            // 先写入本次生成的初始数据，再回调 OnSpawn，避免 OnSpawn 读到上一次的残留值
            if (position.HasValue)
            {
                entity.Position = position.Value;
            }

            if (rotation.HasValue)
            {
                entity.Rotation = rotation.Value;
            }

            EntityToPool[entity] = entry;
            entity.OnSpawn();

            return entity;
        }

        private static EntityBase CreateEntity(EntityConfig config)
        {
            return config.CreateEntity();
        }
    }
}