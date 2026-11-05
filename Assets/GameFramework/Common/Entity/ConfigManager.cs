using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用配置管理器，负责 EntityConfig 子类的加载、缓存和查询。
    /// <para>泛型参数 T 必须是 EntityConfig 的子类，通过 Resources 目录自动加载。</para>
    /// </summary>
    /// <typeparam name="T">EntityConfig 子类型</typeparam>
    /// <example>
    /// 使用示例：
    /// <code>
    /// // 直接使用
    /// var fishMgr = new ConfigManager<FishConfig>("FishConfigs");
    /// var config = fishMgr.GetById(1001);
    ///
    /// // 封装为静态门面（推荐）
    /// public static class FishConfigMgr
    /// {
    ///     private static readonly ConfigManager<FishConfig> _instance = new("FishConfigs");
    ///     public static FishConfig GetById(int id) => _instance.GetById(id);
    ///     public static FishConfig GetByName(string name) => _instance.GetByName(name);
    ///     public static List<FishConfig> GetAll() => _instance.GetAll();
    /// }
    /// </code>
    /// </example>
    public class ConfigManager<T> where T : EntityConfig
    {
        private readonly string _resourcesPath;
        private Dictionary<int, T> _byId;
        private Dictionary<string, T> _byName;
        private List<T> _all;
        private bool _loaded;

        /// <summary>
        /// 创建配置管理器。
        /// </summary>
        /// <param name="resourcesPath">Resources 目录下的相对路径，如 "FishConfigs"</param>
        public ConfigManager(string resourcesPath)
        {
            _resourcesPath = resourcesPath;
        }

        /// <summary>已加载的配置总数。</summary>
        public int Count
        {
            get
            {
                EnsureLoaded();
                return _all.Count;
            }
        }

        /// <summary>
        /// 按 EntityId 获取配置。
        /// </summary>
        /// <returns>配置资产，未找到返回 null</returns>
        public T GetById(int entityId)
        {
            EnsureLoaded();
            _byId.TryGetValue(entityId, out var config);
            return config;
        }

        /// <summary>
        /// 按 EntityName 获取配置。
        /// </summary>
        /// <returns>配置资产，未找到返回 null</returns>
        public T GetByName(string entityName)
        {
            EnsureLoaded();
            _byName.TryGetValue(entityName, out var config);
            return config;
        }

        /// <summary>
        /// 获取所有已加载的配置列表。
        /// </summary>
        public List<T> GetAll()
        {
            EnsureLoaded();
            return _all;
        }

        /// <summary>强制预加载所有配置，可在场景加载时调用避免首次查询卡顿。</summary>
        public void Preload()
        {
            EnsureLoaded();
        }

        /// <summary>重新加载配置，热更新或编辑器刷新时使用。</summary>
        public void Reload()
        {
            _loaded = false;
            _all = null;
            _byId = null;
            _byName = null;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;

            _byId = new Dictionary<int, T>();
            _byName = new Dictionary<string, T>();
            _all = new List<T>();

            T[] configs = Resources.LoadAll<T>(_resourcesPath);
            foreach (var config in configs)
            {
                _all.Add(config);

                if (config.ConfigId != 0 && !_byId.ContainsKey(config.ConfigId))
                {
                    _byId[config.ConfigId] = config;
                }

                if (!string.IsNullOrEmpty(config.EntityName) && !_byName.ContainsKey(config.EntityName))
                {
                    _byName[config.EntityName] = config;
                }
            }

            _loaded = true;
            Debug.Log($"[ConfigManager<{typeof(T).Name}>] Loaded {_all.Count} configs from Resources/{_resourcesPath}");
        }
    }
}