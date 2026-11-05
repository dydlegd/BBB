using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 统一实体配置管理器，管理所有 EntityConfig 子类的加载、缓存和查询。
    /// <para>所有实体配置放在同一个 Resources/Configs 目录下，EntityId 按类型隔离，不同类型可重复。</para>
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// // 按类型+ID获取
    /// FishConfig fish = EntityConfigManager.GetById<FishConfig>(1001);
    /// BulletConfig bullet = EntityConfigManager.GetById<BulletConfig>(1001);
    ///
    /// // 获取某类型全部配置
    /// foreach (var cfg in EntityConfigManager.GetAll<FishConfig>())
    /// {
    ///     // 预加载
///     fishFactory.Prewarm(cfg, 5);
    /// }
    ///
    /// // 预加载
    /// EntityConfigManager.Preload();
    /// </code>
    /// </example>
    public static class EntityConfigManager
    {
        private const string ConfigPath = "Configs";

        private static Dictionary<Type, Dictionary<int, EntityConfig>> _byId;
        private static Dictionary<Type, List<EntityConfig>> _byType;
        private static List<EntityConfig> _all;
        private static bool _loaded;

        /// <summary>已加载的配置总数。</summary>
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _all.Count;
            }
        }

        /// <summary>
        /// 按类型和 EntityId 精确查询。
        /// </summary>
        /// <typeparam name="T">EntityConfig 子类型</typeparam>
        /// <param name="entityId">实体ID（同类型内唯一即可）</param>
        /// <returns>配置资产，未找到返回 null</returns>
        public static T GetById<T>(int entityId) where T : EntityConfig
        {
            EnsureLoaded();
            if (_byId.TryGetValue(typeof(T), out var typeDict) && typeDict.TryGetValue(entityId, out var config))
            {
                return config as T;
            }
            return null;
        }

        /// <summary>
        /// 按类型和 EntityName 精确查询。
        /// </summary>
        /// <typeparam name="T">EntityConfig 子类型</typeparam>
        /// <param name="entityName">实体名称（同类型内唯一即可）</param>
        /// <returns>配置资产，未找到返回 null</returns>
        public static T GetByName<T>(string entityName) where T : EntityConfig
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(entityName)) return null;
            if (_byType.TryGetValue(typeof(T), out var list))
            {
                foreach (var c in list)
                {
                    if (c.EntityName == entityName)
                        return (T)c;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定类型的所有配置。
        /// </summary>
        public static List<T> GetAll<T>() where T : EntityConfig
        {
            EnsureLoaded();
            var result = new List<T>();
            if (_byType.TryGetValue(typeof(T), out var list))
            {
                foreach (var c in list)
                {
                    result.Add((T)c);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有配置（基类列表）。
        /// </summary>
        public static List<EntityConfig> GetAll()
        {
            EnsureLoaded();
            return _all;
        }

        /// <summary>强制预加载所有配置，避免首次查询卡顿。</summary>
        public static void Preload()
        {
            EnsureLoaded();
        }

        /// <summary>重新加载所有配置（热更新或编辑器刷新）。</summary>
        public static void Reload()
        {
            _loaded = false;
            _all = null;
            _byId = null;
            _byType = null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            _byId = new Dictionary<Type, Dictionary<int, EntityConfig>>();
            _byType = new Dictionary<Type, List<EntityConfig>>();
            _all = new List<EntityConfig>();

            EntityConfig[] configs = Resources.LoadAll<EntityConfig>(ConfigPath);
            foreach (var config in configs)
            {
                _all.Add(config);

                var type = config.GetType();

                if (!_byType.ContainsKey(type))
                {
                    _byType[type] = new List<EntityConfig>();
                }
                _byType[type].Add(config);

                if (config.ConfigId != 0)
                {
                    if (!_byId.ContainsKey(type))
                    {
                        _byId[type] = new Dictionary<int, EntityConfig>();
                    }

                    if (_byId[type].ContainsKey(config.ConfigId))
                    {
                        Debug.LogWarning($"[EntityConfigManager] Duplicate EntityId={config.ConfigId} in {type.Name}: '{config.EntityName}'");
                    }
                    else
                    {
                        _byId[type][config.ConfigId] = config;
                    }
                }
            }

            _loaded = true;
            Debug.Log($"[EntityConfigManager] Loaded {_all.Count} configs from Resources/{ConfigPath}");
        }
    }
}