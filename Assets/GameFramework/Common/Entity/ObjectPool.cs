using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用 GameObject 组件对象池，用于池化挂载在 Prefab 上的 Component（如 EntityView）。
    /// <para>池内对象保持 deactive 状态，取出时激活并回调 IPoolable.OnSpawn，回收时反激活并回调 IPoolable.OnDespawn。</para>
    /// </summary>
    /// <typeparam name="T">组件类型，必须继承自 Component</typeparam>
    /// <example>
    /// 使用示例：
    /// <code>
    /// var pool = new ObjectPool<EntityView>(prefab, parent, 10);
    /// var view = pool.Get();
    /// pool.Release(view);
    /// </code>
    /// </example>
    public class ObjectPool<T> where T : Component
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _pool;

        /// <summary>池中可用（已回收）的对象数量。</summary>
        public int AvailableCount => _pool.Count;

        /// <summary>从池创建过的对象总数（含已取出和池中）。</summary>
        public int TotalCount { get; private set; }

        /// <summary>当前活跃（已取出未回收）的对象数量。</summary>
        public int ActiveCount => TotalCount - _pool.Count;

        /// <summary>
        /// 创建对象池。
        /// </summary>
        /// <param name="prefab">预制体，必须挂载 T 组件</param>
        /// <param name="parent">池对象的父节点，回收时对象会移回此节点</param>
        /// <param name="preloadCount">预创建数量，创建后对象保持 deactive</param>
        public ObjectPool(GameObject prefab, Transform parent = null, int preloadCount = 0)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new Stack<T>(Mathf.Max(preloadCount, 4));

            if (preloadCount > 0)
            {
                Prewarm(preloadCount);
            }
        }

        /// <summary>
        /// 预先创建指定数量的实例放入池中。
        /// </summary>
        /// <param name="count">预创建数量</param>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance();
                if (instance == null)
                {
                    // Prefab 缺少 T 组件或 Prefab 为空，CreateInstance 已输出错误日志。
                    // 必须中止：否则 null 会被压进 _pool，后续 Get() 弹出 null 再解引用。
                    break;
                }

                instance.gameObject.SetActive(false);
                // 必须 worldPositionStays=false：UI 池对象的父节点是 ScreenSpaceCamera 画布（缩放非 1），
                // 若保留世界缩放，Unity 会把画布缩放烘焙进 localScale，导致复用一次缩小一截。
                instance.transform.SetParent(_parent, false);
                _pool.Push(instance);
                TotalCount++;
            }
        }

        /// <summary>
        /// 从池中取出一个对象。池空时会创建新实例。
        /// <para>取出后自动激活 GameObject 并回调 IPoolable.OnSpawn。</para>
        /// </summary>
        /// <returns>可用的 T 组件实例；当 Prefab 缺少 T 组件或 Prefab 为空时返回 null</returns>
        public T Get()
        {
            T instance = null;

            while (_pool.Count > 0)
            {
                instance = _pool.Pop();

                // 池中对象可能已被外部 Destroy，此时 Unity 重载的 == null 为 true
                if (instance != null)
                {
                    break;
                }

                instance = null;
                TotalCount--; // 该实例已不存在，同步计数，避免 ActiveCount 虚高
            }

            if (instance == null)
            {
                instance = CreateInstance();
                if (instance == null)
                {
                    // 创建失败（Prefab 缺组件 / Prefab 为空），返回 null 交由调用方处理。
                    // 注意此处不能累加 TotalCount，否则计数会与真实对象数脱节。
                    return null;
                }

                TotalCount++;
            }

            instance.gameObject.SetActive(true);

            if (instance is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            return instance;
        }

        /// <summary>
        /// 将对象回收到池中。
        /// <para>回收后自动 deactive GameObject、移回父节点并回调 IPoolable.OnDespawn。</para>
        /// </summary>
        /// <param name="instance">要回收的实例</param>
        public void Release(T instance)
        {
            if (instance == null)
            {
                Debug.LogError("[ObjectPool] Cannot release null instance.");
                return;
            }

            if (instance is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            instance.gameObject.SetActive(false);
            // 不能用默认的 worldPositionStays=true：回收时若对象还挂在 ScreenSpaceCamera 画布下，
            // Unity 会以池根节点（缩放 1）为基准重算 localScale，把画布缩放（如 0.1069）乘进去；
            // 下次 Show() 以 false 挂回画布时保留这个被缩小过的 localScale，于是每复用一次就更小。
            instance.transform.SetParent(_parent, false);
            _pool.Push(instance);
        }

        /// <summary>
        /// 清空池中所有对象（销毁 GameObject）。
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var instance = _pool.Pop();
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }

            _pool.Clear();
            TotalCount = 0;
        }

        private T CreateInstance()
        {
            if (_prefab == null)
            {
                Debug.LogError("[ObjectPool] Prefab is null, cannot create instance.");
                return null;
            }

            var go = Object.Instantiate(_prefab, _parent);
            var component = go.GetComponent<T>();

            if (component == null)
            {
                Debug.LogError($"[ObjectPool] Prefab '{_prefab.name}' does not have component of type {typeof(T).Name}. Instance destroyed.");
                // 实例化出来的 GameObject 没有任何引用，必须销毁，否则每失败一次就泄漏一个对象
                Object.Destroy(go);
                return null;
            }

            return component;
        }
    }
}