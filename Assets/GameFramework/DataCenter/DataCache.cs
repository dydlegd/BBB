using System;
using System.Collections.Generic;

/// <summary>缓存变更类型</summary>
public enum CacheChangeType
{
    /// <summary>新增条目</summary>
    Added,
    /// <summary>更新已有条目</summary>
    Updated,
    /// <summary>移除单个条目</summary>
    Remove,
    /// <summary>清空全部</summary>
    Clear
}

/// <summary>
/// 通用数据缓存类，键值对存取。
/// <para>int/float/bool/long/double 走强类型重载，零装箱零拆箱，零 GC。</para>
/// <para>class/struct/数组/List/Dictionary 等走泛型方法，class 无装箱，struct 会装箱。</para>
/// <para>同一 Key 只保留最后一次写入的类型：用不同类型覆盖同一 Key 时，旧类型字典中的值会被自动清理。</para>
/// <para>线程安全，基于懒加载单例模式。</para>
/// <para>支持变更事件 <see cref="OnChanged"/>Added/Updated/Remove/Clear 时触发。</para>
/// <para>高频写入场景使用 SetSilent / RemoveSilent 压制事件，避免 GC。</para>
/// <para>使用示例：</para>
/// <code>
/// // ===== 监听变更（零 GC，零装箱）=====
/// DataCache.Instance.OnChanged += (key, type) =>
/// {
///     if (type == CacheChangeType.Added)
///         Debug.Log($"新增: {key}");
///     else if (type == CacheChangeType.Updated)
///         Debug.Log($"更新: {key} = {DataCache.Instance.GetInt(key)}");
/// };
///
/// // ===== 值类型（走重载，零 GC）=====
/// DataCache.Instance.Set("Score", 100);
/// DataCache.Instance.Set("Volume", 0.8f);
/// int score = DataCache.Instance.GetInt("Score");
///
/// // ===== 批量写入不触发事件 =====
/// DataCache.Instance.SetSilent("A", 1);
/// DataCache.Instance.SetSilent("B", 2);
/// DataCache.Instance.SetSilent("C", 3);
///
/// // ===== 引用类型（走泛型，class 无装箱）=====
/// DataCache.Instance.Set("Player", playerData);
/// DataCache.Instance.Set("Scores", new List<int> { 1, 2, 3 });
/// var data = DataCache.Instance.Get<PlayerConfig>("Player");
///
/// // ===== 懒加载 =====
/// var cfg = DataCache.Instance.GetOrCreate("Cfg", () => LoadConfig());
///
/// // ===== 管理 =====
/// DataCache.Instance.Remove("TempData");
/// DataCache.Instance.Clear();
/// </code>
/// </summary>
public class DataCache
{
    #region Singleton

    private static readonly Lazy<DataCache> _instance = new Lazy<DataCache>(() => new DataCache());

    public static DataCache Instance => _instance.Value;

    private DataCache() { }

    #endregion

    #region Typed Storage

    private readonly Dictionary<string, int> _intDict = new Dictionary<string, int>();
    private readonly Dictionary<string, float> _floatDict = new Dictionary<string, float>();
    private readonly Dictionary<string, bool> _boolDict = new Dictionary<string, bool>();
    private readonly Dictionary<string, long> _longDict = new Dictionary<string, long>();
    private readonly Dictionary<string, double> _doubleDict = new Dictionary<string, double>();
    private readonly Dictionary<string, object> _objectDict = new Dictionary<string, object>();

    /// <summary>Key → 当前存储类型，用于 O(1) 判断存在性并只清理冲突的字典</summary>
    private readonly Dictionary<string, ValueKind> _keyKinds = new Dictionary<string, ValueKind>();

    #endregion

    #region Fields

    private readonly object _lock = new object();
    private readonly object _factoryLock = new object();
    private long _hitCount;
    private long _missCount;

    #endregion

    #region Events

    /// <summary>
    /// 缓存变更事件。Set/Remove/Clear 时触发，在锁外回调以避免死锁。
    /// <para>回调签名 (string key, CacheChangeType type)，零堆分配，零装箱。</para>
    /// <para>回调中可通过 GetInt/GetFloat/Get{T} 等强类型方法取值，同样是 O(1) 零 GC。</para>
    /// <para>高频写入走 SetSilent / RemoveSilent 完全压制事件。</para>
    /// </summary>
    public event Action<string, CacheChangeType> OnChanged;

    private void NotifyChange(string key, CacheChangeType changeType)
    {
        OnChanged?.Invoke(key, changeType);
    }

    #endregion

    #region Properties

    /// <summary>
    /// 当前缓存条目总数
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
                return _keyKinds.Count;
        }
    }

    /// <summary>累计命中次数</summary>
    public long HitCount { get { lock (_lock) return _hitCount; } }
    /// <summary>累计未命中次数</summary>
    public long MissCount { get { lock (_lock) return _missCount; } }

    /// <summary>
    /// 缓存命中率（0~1），命中次数 / 总访问次数
    /// </summary>
    public float HitRate
    {
        get
        {
            lock (_lock)
            {
                long total = _hitCount + _missCount;
                return total > 0 ? (float)_hitCount / total : 0f;
            }
        }
    }

    #endregion

    #region Set — int / float / bool / long / double (zero boxing)

    /// <summary>存入 int 值，零装箱。区分新增和更新事件</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, int value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Int);
            _intDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    /// <summary>存入 float 值，零装箱。区分新增和更新事件</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, float value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Float);
            _floatDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    /// <summary>存入 bool 值，零装箱。区分新增和更新事件</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, bool value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Bool);
            _boolDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    /// <summary>存入 long 值，零装箱。区分新增和更新事件</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, long value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Long);
            _longDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    /// <summary>存入 double 值，零装箱。区分新增和更新事件</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, double value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Double);
            _doubleDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    #endregion

    #region Set — Generic (class/struct/array/List/etc.)

    /// <summary>
    /// 存入任意类型的值。class 无装箱，struct 会装箱。区分新增和更新事件。
    /// <para>仅当调用方的静态类型 T 就是 int/float/bool/long/double 时才路由到强类型重载，
    /// 避免同一 Key 在泛型与强类型字典各存一份。</para>
    /// <para>以 object / 可空类型 / 基类形式写入的值仍保存在 object 字典，泛型读取语义不变。</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set<T>(string key, T value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        bool routed = false;
        if (typeof(T) == typeof(int)) { Set(key, (int)(object)value); routed = true; }
        else if (typeof(T) == typeof(float)) { Set(key, (float)(object)value); routed = true; }
        else if (typeof(T) == typeof(bool)) { Set(key, (bool)(object)value); routed = true; }
        else if (typeof(T) == typeof(long)) { Set(key, (long)(object)value); routed = true; }
        else if (typeof(T) == typeof(double)) { Set(key, (double)(object)value); routed = true; }
        if (routed) return;

        CacheChangeType changeType;
        lock (_lock)
        {
            bool existed = PrepareWriteLocked(key, ValueKind.Object);
            _objectDict[key] = value;
            changeType = existed ? CacheChangeType.Updated : CacheChangeType.Added;
        }
        NotifyChange(key, changeType);
    }

    #endregion

    #region SetSilent — zero event (batch / hot path)

    /// <summary>存入 int 值，不触发 OnChanged 事件</summary>
    public void SetSilent(string key, int value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Int);
            _intDict[key] = value;
        }
    }

    /// <summary>存入 float 值，不触发 OnChanged 事件</summary>
    public void SetSilent(string key, float value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Float);
            _floatDict[key] = value;
        }
    }

    /// <summary>存入 bool 值，不触发 OnChanged 事件</summary>
    public void SetSilent(string key, bool value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Bool);
            _boolDict[key] = value;
        }
    }

    /// <summary>存入 long 值，不触发 OnChanged 事件</summary>
    public void SetSilent(string key, long value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Long);
            _longDict[key] = value;
        }
    }

    /// <summary>存入 double 值，不触发 OnChanged 事件</summary>
    public void SetSilent(string key, double value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Double);
            _doubleDict[key] = value;
        }
    }

    /// <summary>
    /// 存入任意类型的值，不触发 OnChanged 事件。
    /// <para>仅当调用方的静态类型 T 就是 int/float/bool/long/double 时才路由到强类型重载；
    /// object / 可空类型 / 基类形式写入的值保存在 object 字典。</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void SetSilent<T>(string key, T value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        bool routed = false;
        if (typeof(T) == typeof(int)) { SetSilent(key, (int)(object)value); routed = true; }
        else if (typeof(T) == typeof(float)) { SetSilent(key, (float)(object)value); routed = true; }
        else if (typeof(T) == typeof(bool)) { SetSilent(key, (bool)(object)value); routed = true; }
        else if (typeof(T) == typeof(long)) { SetSilent(key, (long)(object)value); routed = true; }
        else if (typeof(T) == typeof(double)) { SetSilent(key, (double)(object)value); routed = true; }
        if (routed) return;

        lock (_lock)
        {
            PrepareWriteLocked(key, ValueKind.Object);
            _objectDict[key] = value;
        }
    }

    #endregion

    #region RemoveSilent

    /// <summary>移除指定 key，不触发 OnChanged 事件</summary>
    public bool RemoveSilent(string key)
    {
        if (key == null) return false;
        lock (_lock)
        {
            if (!_keyKinds.TryGetValue(key, out ValueKind kind)) return false;
            RemoveKindLocked(key, kind);
            _keyKinds.Remove(key);
            return true;
        }
    }

    #endregion

    #region Get — int / float / bool / long / double (zero unboxing)

    /// <summary>读取 int 值，未命中返回 defaultValue</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            if (_intDict.TryGetValue(key, out int v)) { _hitCount++; return v; }
            _missCount++; return defaultValue;
        }
    }

    /// <summary>尝试读取 int 值，返回是否命中</summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 0</param>
    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            if (_intDict.TryGetValue(key, out value)) { _hitCount++; return true; }
            _missCount++; return false;
        }
    }

    /// <summary>读取 float 值，未命中返回 defaultValue</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            if (_floatDict.TryGetValue(key, out float v)) { _hitCount++; return v; }
            _missCount++; return defaultValue;
        }
    }

    /// <summary>尝试读取 float 值，返回是否命中</summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 0</param>
    public bool TryGetFloat(string key, out float value)
    {
        value = 0f;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            if (_floatDict.TryGetValue(key, out value)) { _hitCount++; return true; }
            _missCount++; return false;
        }
    }

    /// <summary>读取 bool 值，未命中返回 defaultValue</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            if (_boolDict.TryGetValue(key, out bool v)) { _hitCount++; return v; }
            _missCount++; return defaultValue;
        }
    }

    /// <summary>尝试读取 bool 值，返回是否命中</summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 false</param>
    public bool TryGetBool(string key, out bool value)
    {
        value = false;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            if (_boolDict.TryGetValue(key, out value)) { _hitCount++; return true; }
            _missCount++; return false;
        }
    }

    /// <summary>读取 long 值，未命中返回 defaultValue</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public long GetLong(string key, long defaultValue = 0L)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            if (_longDict.TryGetValue(key, out long v)) { _hitCount++; return v; }
            _missCount++; return defaultValue;
        }
    }

    /// <summary>尝试读取 long 值，返回是否命中</summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 0</param>
    public bool TryGetLong(string key, out long value)
    {
        value = 0L;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            if (_longDict.TryGetValue(key, out value)) { _hitCount++; return true; }
            _missCount++; return false;
        }
    }

    /// <summary>读取 double 值，未命中返回 defaultValue</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public double GetDouble(string key, double defaultValue = 0.0)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            if (_doubleDict.TryGetValue(key, out double v)) { _hitCount++; return v; }
            _missCount++; return defaultValue;
        }
    }

    /// <summary>尝试读取 double 值，返回是否命中</summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 0</param>
    public bool TryGetDouble(string key, out double value)
    {
        value = 0.0;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            if (_doubleDict.TryGetValue(key, out value)) { _hitCount++; return true; }
            _missCount++; return false;
        }
    }

    #endregion

    #region Get — Generic (class/struct)

    /// <summary>
    /// 读取任意类型的值，未命中返回 defaultValue。
    /// <para>当 T 为 int/float/bool/long/double 时，会回退读取对应的强类型字典，
    /// 因此用强类型重载写入的值也能被泛型读取到（回退命中会产生一次装箱）。</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public T Get<T>(string key, T defaultValue = default)
    {
        if (key == null) { lock (_lock) { _missCount++; return defaultValue; } }
        lock (_lock)
        {
            // 类型不匹配（如用 Get<int> 读取已存的 class）按未命中处理，避免 InvalidCastException
            if (_objectDict.TryGetValue(key, out object obj))
            {
                if (obj is T typed) { _hitCount++; return typed; }
                _missCount++; return defaultValue;
            }

            if (typeof(T) == typeof(int) && _intDict.TryGetValue(key, out int intValue)) { _hitCount++; return (T)(object)intValue; }
            if (typeof(T) == typeof(float) && _floatDict.TryGetValue(key, out float floatValue)) { _hitCount++; return (T)(object)floatValue; }
            if (typeof(T) == typeof(bool) && _boolDict.TryGetValue(key, out bool boolValue)) { _hitCount++; return (T)(object)boolValue; }
            if (typeof(T) == typeof(long) && _longDict.TryGetValue(key, out long longValue)) { _hitCount++; return (T)(object)longValue; }
            if (typeof(T) == typeof(double) && _doubleDict.TryGetValue(key, out double doubleValue)) { _hitCount++; return (T)(object)doubleValue; }

            _missCount++; return defaultValue;
        }
    }

    /// <summary>
    /// 尝试读取任意类型的值，返回是否命中。
    /// <para>当 T 为 int/float/bool/long/double 时，会回退读取对应的强类型字典。</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 default(T)</param>
    public bool TryGet<T>(string key, out T value)
    {
        value = default;
        if (key == null) { lock (_lock) { _missCount++; return false; } }
        lock (_lock)
        {
            // 类型不匹配（如用 TryGet<int> 读取已存的 class）按未命中处理，避免 InvalidCastException
            if (_objectDict.TryGetValue(key, out object obj))
            {
                if (obj is T typed) { _hitCount++; value = typed; return true; }
                _missCount++; return false;
            }

            if (typeof(T) == typeof(int) && _intDict.TryGetValue(key, out int intValue)) { _hitCount++; value = (T)(object)intValue; return true; }
            if (typeof(T) == typeof(float) && _floatDict.TryGetValue(key, out float floatValue)) { _hitCount++; value = (T)(object)floatValue; return true; }
            if (typeof(T) == typeof(bool) && _boolDict.TryGetValue(key, out bool boolValue)) { _hitCount++; value = (T)(object)boolValue; return true; }
            if (typeof(T) == typeof(long) && _longDict.TryGetValue(key, out long longValue)) { _hitCount++; value = (T)(object)longValue; return true; }
            if (typeof(T) == typeof(double) && _doubleDict.TryGetValue(key, out double doubleValue)) { _hitCount++; value = (T)(object)doubleValue; return true; }

            _missCount++; return false;
        }
    }

    #endregion

    #region GetOrCreate

    /// <summary>
    /// 读取缓存，未命中时通过工厂方法创建并自动存入缓存。
    /// <para>线程安全：使用双检锁，并发时工厂方法只会执行一次。</para>
    /// <para>工厂在独立的全局锁内执行，耗时较长的工厂会阻塞其它 Key 的 GetOrCreate，请保持工厂轻量。</para>
    /// <para>锁内只做静默写入，OnChanged 回调在释放锁之后触发，避免用户回调持有工厂锁导致死锁。</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">未命中时的创建工厂，不能为 null</param>
    public T GetOrCreate<T>(string key, Func<T> factory)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        if (TryGet(key, out T value)) return value;

        lock (_factoryLock)
        {
            // 双检：等待锁期间可能已被其它线程写入
            if (TryGet(key, out value)) return value;

            value = factory();
            SetSilent(key, value);
        }

        // 出锁后再通知订阅者，事件回调不会在 _factoryLock 内执行
        NotifyChange(key, CacheChangeType.Added);
        return value;
    }

    #endregion

    #region HasKey / Remove / Clear

    /// <summary>检查指定 key 是否存在于缓存中</summary>
    /// <param name="key">缓存键</param>
    public bool HasKey(string key)
    {
        if (key == null) return false;
        lock (_lock)
        {
            return HasKeyLocked(key);
        }
    }

    /// <summary>
    /// 从缓存中移除指定 key。返回 true 表示成功移除，false 表示 key 不存在。
    /// </summary>
    /// <param name="key">缓存键</param>
    public bool Remove(string key)
    {
        if (key == null) return false;
        bool removed;
        lock (_lock)
        {
            removed = _keyKinds.TryGetValue(key, out ValueKind kind);
            if (removed)
            {
                RemoveKindLocked(key, kind);
                _keyKinds.Remove(key);
            }
        }
        if (removed) NotifyChange(key, CacheChangeType.Remove);
        return removed;
    }

    /// <summary>清空所有缓存条目</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _intDict.Clear();
            _floatDict.Clear();
            _boolDict.Clear();
            _longDict.Clear();
            _doubleDict.Clear();
            _objectDict.Clear();
            _keyKinds.Clear();
        }
        NotifyChange(null, CacheChangeType.Clear);
    }

    #endregion

    #region Private — Type Uniqueness

    /// <summary>同一 Key 在缓存中允许存在的存储类型</summary>
    private enum ValueKind
    {
        Int,
        Float,
        Bool,
        Long,
        Double,
        Object
    }

    /// <summary>在锁内判断 Key 是否存在于缓存中（O(1)，只查类型表）</summary>
    /// <param name="key">缓存键</param>
    /// <returns>Key 存在时返回 true</returns>
    private bool HasKeyLocked(string key)
    {
        return _keyKinds.ContainsKey(key);
    }

    /// <summary>
    /// 在锁内准备写入：登记 Key 的类型，并且仅在类型发生变化时清理旧类型字典中的值。
    /// <para>相比每次盲删 5 个类型字典，这里通常只有 1 次查找、0 次删除，保持 SetSilent 热路径的开销可控。</para>
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="kind">即将写入的类型</param>
    /// <returns>该 Key 在本次写入前是否已存在</returns>
    private bool PrepareWriteLocked(string key, ValueKind kind)
    {
        bool existed = _keyKinds.TryGetValue(key, out ValueKind previous);

        if (existed && previous != kind)
        {
            RemoveKindLocked(key, previous);
        }

        _keyKinds[key] = kind;
        return existed;
    }

    /// <summary>在锁内按类型删除对应字典中的值</summary>
    /// <param name="key">缓存键</param>
    /// <param name="kind">该 Key 当前登记的类型</param>
    private void RemoveKindLocked(string key, ValueKind kind)
    {
        switch (kind)
        {
            case ValueKind.Int: _intDict.Remove(key); break;
            case ValueKind.Float: _floatDict.Remove(key); break;
            case ValueKind.Bool: _boolDict.Remove(key); break;
            case ValueKind.Long: _longDict.Remove(key); break;
            case ValueKind.Double: _doubleDict.Remove(key); break;
            case ValueKind.Object: _objectDict.Remove(key); break;
        }
    }

    #endregion

    #region Stats

    /// <summary>获取缓存运行统计信息（供调试）</summary>
    public string GetStats()
    {
        lock (_lock)
        {
            long total = _hitCount + _missCount;
            float rate = total > 0 ? (float)_hitCount / total : 0f;
            return $"[DataCache] Count={Count} Hits={_hitCount} Misses={_missCount} HitRate={rate:P1}";
        }
    }

    #endregion
}