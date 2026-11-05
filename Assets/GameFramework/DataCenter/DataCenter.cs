using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 数据管理中心，统一协调 DataCache（内存缓存）、JsonUtil（序列化）、CryptoUtility（加解密）、FileUtility（磁盘IO）。
/// <para>作为整个数据框架的唯一对外入口，业务代码只需与 DataCenter 交互。</para>
///
/// <para>架构分层：</para>
/// <code>
/// DataCenter (编排层)
///   ├── DataCache     (内存层) — 运行时 KV 缓存，零 GC，线程安全
///   ├── JsonUtil      (序列化层) — 对象 ↔ JSON，基于 Newtonsoft.Json
///   ├── CryptoUtility (安全层) — AES-256 加解密，可选开关
///   └── FileUtility   (持久化层) — 磁盘读写，原子写入，断电安全
/// </code>
///
/// <para>数据流：</para>
/// <code>
/// // ===== 保存 =====
/// object → JsonUtil.ToJson → CryptoUtility.AesEncrypt → FileUtility.WriteAllBytesAtomic
///                ↓ (同时)
///         DataCache.Set (同步更新缓存)
///
/// // ===== 加载 =====
/// DataCache.TryGet (命中→直接返回)
///     ↓ (未命中)
/// FileUtility.ReadAllBytesSafe → CryptoUtility.AesDecrypt → JsonUtil.FromJson
///                ↓ (回填)
///         DataCache.Set
/// </code>
///
/// <para>使用示例：</para>
/// <code>
/// // ===== 初始化（游戏启动时调用一次）=====
/// DataCenter.Instance.Initialize(
///     basePath: Application.persistentDataPath + "/Data",
///     passphrase: SystemInfo.deviceUniqueIdentifier
/// );
///
/// // ===== 磁盘读写 — JSON 路径 =====
/// PlayerData player = new PlayerData { Name = "Hero", Level = 10, Gold = 999 };
/// DataCenter.Instance.Save("PlayerData.json", player);
///
/// // TryLoad — 明确知道是否成功（class / struct 通用）
/// if (DataCenter.Instance.TryLoad("PlayerData.json", out PlayerData data))
///     Debug.Log($"加载成功: {data.Name}");
///
/// // LoadOrDefault — 文件不存在时自动创建默认实例
/// GameConfig cfg = DataCenter.Instance.LoadOrDefault("Config.json", () => new GameConfig());
///
/// // 异步加载
/// var (ok, asyncData) = await DataCenter.Instance.TryLoadAsync<PlayerData>("Player.json");
///
/// // Refresh — 强制从磁盘刷新（绕过缓存）
/// PlayerData fresh = DataCenter.Instance.Refresh<PlayerData>("Player.json");
///
/// // Delete — 删除磁盘文件及缓存
/// DataCenter.Instance.Delete("Player.json");
///
/// // Exists / GetFilePath / GetFileSize / GetFileMD5 — 文件工具
/// if (DataCenter.Instance.Exists("Player.json"))
///     Debug.Log(DataCenter.Instance.GetFileSize("Player.json"));
///
/// // 纯缓存 — Remove / HasKey / ClearCache
/// DataCenter.Instance.Remove("Gold");
/// bool hasGold = DataCenter.Instance.HasKey("Gold");
/// DataCenter.Instance.ClearCache();  // 清空所有缓存，不删磁盘文件
///
/// // ===== 磁盘读写 — 二进制路径 =====
/// Texture2D tex = CaptureScreenshot();
/// DataCenter.Instance.SaveRaw("Screenshot.dat", tex.EncodeToPNG());
/// byte[] raw = DataCenter.Instance.LoadRaw("Screenshot.dat");
///
/// // ===== 纯内存缓存（零 GC，不写磁盘）=====
/// DataCenter.Instance.Set("Gold", 10000);
/// DataCenter.Instance.Set("Volume", 0.8f);
/// int gold = DataCenter.Instance.Get("Gold", 0);
///
/// // 批量写入不触发事件
/// DataCenter.Instance.SetSilent("A", 1);
/// DataCenter.Instance.SetSilent("B", 2);
///
/// // 缓存尝试读取
/// if (DataCenter.Instance.TryGet("Cfg", out Config cached))
///     Use(cached);
///
/// // 懒加载
/// var cfg2 = DataCenter.Instance.GetOrCreate("LazyCfg", () => LoadExpensive());
///
/// // ===== 缓存 ↔ 磁盘 桥接 =====
/// DataCenter.Instance.SetSilent("Session", 999);
/// DataCenter.Instance.SaveFromCache<int>("Session", "Session.json");  // 缓存→落盘
/// DataCenter.Instance.SaveDirect("Config", myConfig);                 // 一步到位，文件名自动为 Config.json
///
/// // ===== 运行时开关 =====
/// DataCenter.Instance.SetEncryptionEnabled(false);  // 关闭加密（调试用）
/// DataCenter.Instance.SetCacheEnabled(false);       // 关闭缓存
///
/// // ===== 生命周期 =====
/// DataCenter.Instance.Shutdown();                    // 清缓存，停止服务
/// </code>
/// </summary>
public class DataCenter : Singleton<DataCenter>
{
    #region Constants

    /// <summary>日志标签前缀，用于 Debug.Log 输出</summary>
    private const string Tag = "[DataCenter]";

    #endregion

    #region Fields

    /// <summary>配置级线程锁，保护 _basePath / _passphrase / _enableEncryption / _enableCache 的读写</summary>
    private readonly object _configLock = new object();

    /// <summary>数据文件存储根目录</summary>
    private string _basePath;

    /// <summary>AES 加密口令</summary>
    private string _passphrase;

    /// <summary>是否启用 AES 加密（volatile 确保多线程可见性）</summary>
    private volatile bool _enableEncryption = false;

    /// <summary>是否启用内存缓存（volatile 确保多线程可见性）</summary>
    private volatile bool _enableCache = true;

    /// <summary>是否已关闭（volatile 确保多线程可见性）</summary>
    private volatile bool _isShutdown;

    /// <summary>是否已有数据成功落盘，用于加密开关切换时提示旧数据无法读取</summary>
    private volatile bool _hasWrittenData;

    /// <summary>
    /// 读取时是否校验内容完整性（解密 + 反序列化）。
    /// <para>开启后，能打开但内容已损坏的文件会被判为无效，从而触发 .bak / .tmp 回滚，
    /// 而不是把坏数据交给业务层。</para>
    /// </summary>
    private volatile bool _verifyOnRead = true;

    #endregion

    #region Constructor

    /// <summary>
    /// 私有构造函数，确保单例模式。
    /// <para>默认使用 Application.persistentDataPath 作为根目录。</para>
    /// <para>默认口令与 <see cref="Initialize"/> 的兜底规则一致，避免未调用 Initialize 就 Save 时
    /// 因口令为空导致加密失败。</para>
    /// </summary>
    private DataCenter()
    {
        _basePath = Application.persistentDataPath;
        _passphrase = SystemInfo.deviceUniqueIdentifier;
    }

    #endregion

    #region Properties

    /// <summary>当前数据根目录</summary>
    public string BasePath => _basePath;

    /// <summary>是否启用加密</summary>
    public bool IsEncryptionEnabled => _enableEncryption;

    /// <summary>读取时是否校验内容完整性（默认开启）</summary>
    public bool IsVerifyOnReadEnabled => _verifyOnRead;

    /// <summary>
    /// 内存缓存当前是否实际生效。
    /// <para>Shutdown 后即使调用方未用 <see cref="SetCacheEnabled"/> 关闭缓存，此处也返回 false；
    /// 重新 Initialize 后恢复。</para>
    /// </summary>
    public bool IsCacheEnabled => _enableCache && !_isShutdown;

    /// <summary>当前缓存条目数</summary>
    public int CacheCount => DataCache.Instance.Count;

    /// <summary>缓存命中率（0~1）</summary>
    public float CacheHitRate => DataCache.Instance.HitRate;

    /// <summary>是否已调用 Shutdown</summary>
    public bool IsShutdown => _isShutdown;

    /// <summary>
    /// 缓存当前是否实际生效：用户未关闭缓存且未调用 Shutdown。
    /// <para>Shutdown 后即使 _enableCache 仍为 true，也不会读写内存缓存。</para>
    /// </summary>
    private bool IsCacheActive => _enableCache && !_isShutdown;

    #endregion

    #region Events

    /// <summary>
    /// 数据变更事件，Set / Remove / Clear 时触发。
    /// <para>回调签名 (string key, CacheChangeType type)，在 DataCache 的锁外回调以避免死锁。</para>
    /// <para>高频批量写入场景请使用 SetSilent / RemoveSilent 等压制事件的方法，避免 GC。</para>
    /// <para>使用示例：</para>
    /// <code>
    /// DataCenter.Instance.OnDataChanged += (key, type) =>
    /// {
    ///     if (type == CacheChangeType.Updated && key == "Volume")
    ///         UpdateVolume(DataCenter.Instance.Get<float>("Volume"));
    /// };
    /// </code>
    /// </summary>
    public event Action<string, CacheChangeType> OnDataChanged;

    #endregion

    #region Initialize

    /// <summary>
    /// 初始化数据中心。
    /// <para>建议在游戏启动时调用一次，设置持久化根目录和加密口令。</para>
    /// <para>可重复调用：内部的 DataCache 事件订阅会先解绑再绑定，不会产生多份订阅。</para>
    /// </summary>
    /// <param name="basePath">
    /// 数据文件存储根目录。
    /// 为 null 则使用 Application.persistentDataPath。
    /// </param>
    /// <param name="passphrase">
    /// AES 加密口令。
    /// 为 null 或空则使用 SystemInfo.deviceUniqueIdentifier 作为默认口令。
    /// </param>
    public void Initialize(string basePath = null, string passphrase = null)
    {
        lock (_configLock)
        {
            // basePath 变化时缓存 Key（dc:完整路径）全部失效，先清掉避免命中旧目录的脏数据
            bool basePathChanged = !string.IsNullOrEmpty(basePath) && basePath != _basePath;
            if (!string.IsNullOrEmpty(basePath))
            {
                _basePath = basePath;
            }

            if (!string.IsNullOrEmpty(passphrase))
            {
                _passphrase = passphrase;
            }
            else if (string.IsNullOrEmpty(_passphrase))
            {
                _passphrase = SystemInfo.deviceUniqueIdentifier;
            }

            // 重新初始化后清掉 Shutdown 标记，缓存随 IsCacheActive 一起恢复
            _isShutdown = false;

            if (basePathChanged)
            {
                DataCache.Instance.Clear();
            }
        }

        FileUtility.CreateDirectory(_basePath);

        // 先解绑再绑定：重复 Initialize 只会保留一份订阅
        DataCache.Instance.OnChanged -= HandleCacheChanged;
        DataCache.Instance.OnChanged += HandleCacheChanged;

        Debug.Log($"{Tag} 初始化完成 | BasePath: {_basePath} | Encryption: {_enableEncryption} | Cache: {_enableCache}");
    }

    /// <summary>DataCache 变更事件的转发处理器</summary>
    private void HandleCacheChanged(string key, CacheChangeType type)
    {
        OnDataChanged?.Invoke(key, type);
    }

    /// <summary>
    /// 落盘成功后同步文件缓存并触发变更事件，保证「磁盘数据已变」对订阅者是可见的。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="cacheKey">文件维度的缓存 Key（dc: 前缀）</param>
    /// <param name="data">最新数据</param>
    /// <param name="fileExisted">写入前磁盘文件是否已存在，用于区分 Added / Updated 事件</param>
    private void UpdateCacheAndNotify<T>(string cacheKey, T data, bool fileExisted)
    {
        if (IsCacheActive)
        {
            DataCache.Instance.SetSilent(cacheKey, data);
        }

        OnDataChanged?.Invoke(cacheKey, fileExisted ? CacheChangeType.Updated : CacheChangeType.Added);
    }

    #endregion

    #region Toggle

    /// <summary>
    /// 设置是否启用 AES 加密。
    /// <para>加密状态不会自动迁移历史文件：已有密文/明文数据在切换后将无法读取。</para>
    /// <para>建议仅在 Initialize 之前设定；运行中切换会输出警告。</para>
    /// </summary>
    /// <param name="enabled">是否启用加密</param>
    public void SetEncryptionEnabled(bool enabled)
    {
        bool changed;

        lock (_configLock)
        {
            changed = _enableEncryption != enabled;
            _enableEncryption = enabled;
        }

        if (!changed) return;

        if (_hasWrittenData)
        {
            Debug.LogWarning($"{Tag} 加密已切换为 {(enabled ? "启用" : "关闭")}，但磁盘上已有数据文件。" +
                             "历史文件不会被自动迁移，切换后可能无法读取，请先迁移或清理数据目录。");
        }

        Debug.Log($"{Tag} 加密: {(enabled ? "启用" : "关闭")}");
    }

    /// <summary>设置是否启用内存缓存（Shutdown 后需重新 Initialize 才会恢复生效）</summary>
    public void SetCacheEnabled(bool enabled)
    {
        lock (_configLock) { _enableCache = enabled; }
        if (!enabled)
        {
            DataCache.Instance.Clear();
        }
        Debug.Log($"{Tag} 缓存: {(enabled ? "启用" : "关闭")}");
    }

    /// <summary>
    /// 设置读取时是否校验内容完整性。
    /// <para>开启（默认）：读到「能打开但解密/反序列化失败」的文件时，视为损坏并回滚到 .bak / .tmp。</para>
    /// <para>关闭：退化为「能打开就算读取成功」，仅用于排查校验误判。</para>
    /// </summary>
    /// <param name="enabled">是否启用读取校验</param>
    public void SetVerifyOnRead(bool enabled)
    {
        lock (_configLock) { _verifyOnRead = enabled; }
        Debug.Log($"{Tag} 读取校验: {(enabled ? "启用" : "关闭")}");
    }

    #endregion

    #region Shutdown

    /// <summary>
    /// 关闭数据中心：停止缓存读写、清除内存缓存并解绑变更事件。
    /// <para>不会等待已发起的异步 IO：这些操作会独立完成，但不会回填缓存。</para>
    /// <para>关闭后仍可读写磁盘文件；再次调用 Initialize 可恢复缓存。</para>
    /// </summary>
    public void Shutdown()
    {
        lock (_configLock)
        {
            // 只置关闭标记，不改写用户的缓存开关：Initialize 后缓存状态可原样恢复
            _isShutdown = true;
        }

        DataCache.Instance.OnChanged -= HandleCacheChanged;
        OnDataChanged = null;
        DataCache.Instance.Clear();
        Debug.Log($"{Tag} 已关闭 | BasePath: {_basePath}");
    }

    #endregion

    #region Load (Sync)

    /// <summary>
    /// 从磁盘加载数据并反序列化，命中缓存时直接返回。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public T Load<T>(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} Load: 文件名为空");
            return default;
        }

        TryLoad(fileName, out T result);
        return result;
    }

    /// <summary>
    /// 尝试从磁盘加载数据，返回是否成功。
    /// <para>命中缓存时 out result 为缓存值，返回 true。</para>
    /// <para>加载失败时 out result 为 default(T)，返回 false。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径）</param>
    /// <param name="result">加载结果，失败为 default(T)</param>
    /// <returns>是否加载成功</returns>
    public bool TryLoad<T>(string fileName, out T result)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            result = default;
            return false;
        }

        string cacheKey = MakeCacheKey(fileName);

        if (IsCacheActive && DataCache.Instance.TryGet(cacheKey, out T cached))
        {
            result = cached;
            return true;
        }

        bool success = TryReadFromDisk(fileName, out result);

        if (IsCacheActive && success)
        {
            DataCache.Instance.SetSilent(cacheKey, result);
        }

        return success;
    }

    /// <summary>
    /// 从磁盘加载数据，文件不存在或加载失败时返回默认工厂创建的实例。
    /// <para>默认工厂为 null 则返回 default(T)。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <param name="defaultFactory">加载失败时的默认值工厂，为 null 则返回 default(T)</param>
    /// <returns>反序列化后的对象或默认实例</returns>
    public T LoadOrDefault<T>(string fileName, Func<T> defaultFactory = null)
    {
        if (TryLoad(fileName, out T result)) return result;
        return defaultFactory != null ? defaultFactory() : default;
    }

    /// <summary>
    /// 从磁盘加载数据并写入文件缓存，一步完成。
    /// 文件名由约定自动推导：<c>{cacheKey}.json</c>。
    /// <para>缓存统一使用文件维度 Key（<c>dc:{cacheKey}.json</c>），不再额外写入裸 Key，
    /// 避免同一数据在缓存中出现两份互不同步的副本。</para>
    /// <para>文件不存在时自动创建默认实例。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="cacheKey">缓存 Key，如 "GlobalData"，文件名自动为 "GlobalData.json"</param>
    /// <param name="defaultFactory">加载失败时的默认值工厂，为 null 则 new T()</param>
    /// <returns>反序列化后的对象或默认实例</returns>
    public T LoadAndCache<T>(string cacheKey, Func<T> defaultFactory = null) where T : class, new()
    {
        string fileName = $"{cacheKey}.json";
        return LoadOrDefault(fileName, defaultFactory ?? (() => new T()));
    }

    /// <summary>
    /// 从磁盘加载 JSON 并放入缓存（首次）或从缓存直接返回（已缓存）。key 和文件名自动使用 typeof(T).Name。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="defaultFactory">加载失败时的默认值工厂，为 null 则 new T()</param>
    /// <returns>反序列化后的对象或默认实例</returns>
    public T LoadAndCache<T>(Func<T> defaultFactory = null) where T : class, new()
    {
        return LoadAndCache(typeof(T).Name, defaultFactory);
    }

    #endregion

    #region LoadAsync

    /// <summary>
    /// 异步从磁盘加载数据并反序列化，不阻塞 UI 线程。
    /// 命中缓存时直接同步返回，跳过异步 IO。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public async Task<T> LoadAsync<T>(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} LoadAsync: 文件名为空");
            return default;
        }

        var (_, result) = await TryLoadAsync<T>(fileName);
        return result;
    }

    /// <summary>
    /// 异步尝试从磁盘加载数据，返回是否成功。
    /// <para>命中缓存时直接同步返回，跳过异步 IO。</para>
    /// <para>加载失败时 result 为 default(T)，success 为 false。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径）</param>
    /// <returns>(是否成功, 加载结果)</returns>
    public async Task<(bool success, T result)> TryLoadAsync<T>(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return (false, default);
        }

        string cacheKey = MakeCacheKey(fileName);

        if (IsCacheActive && DataCache.Instance.TryGet(cacheKey, out T cached))
        {
            return (true, cached);
        }

        var (success, diskResult) = await TryReadFromDiskAsync<T>(fileName);

        if (IsCacheActive && success)
        {
            DataCache.Instance.SetSilent(cacheKey, diskResult);
        }

        return (success, diskResult);
    }

    #endregion

    #region Save (Sync)

    /// <summary>
    /// 将对象序列化并写入磁盘（原子写，断电安全），成功后同步内存缓存并触发变更事件。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <param name="data">要保存的数据对象</param>
    /// <returns>是否保存成功</returns>
    public bool Save<T>(string fileName, T data)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} Save: 文件名为空");
            return false;
        }

        string path = GetFullPath(fileName);
        bool fileExisted = FileUtility.FileExists(path);
        bool success = WriteToDisk(path, data);

        if (success)
        {
            _hasWrittenData = true;
            UpdateCacheAndNotify(MakeCacheKey(fileName), data, fileExisted);
        }

        return success;
    }

    #endregion

    #region SaveAsync

    /// <summary>
    /// 异步将对象序列化并写入磁盘（原子写，断电安全），成功后同步内存缓存并触发变更事件。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <param name="data">要保存的数据对象</param>
    /// <returns>是否保存成功</returns>
    public async Task<bool> SaveAsync<T>(string fileName, T data)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} SaveAsync: 文件名为空");
            return false;
        }

        string path = GetFullPath(fileName);
        bool fileExisted = FileUtility.FileExists(path);
        bool success = await WriteToDiskAsync(path, data);

        if (success)
        {
            _hasWrittenData = true;
            UpdateCacheAndNotify(MakeCacheKey(fileName), data, fileExisted);
        }

        return success;
    }

    /// <summary>
    /// 从缓存读取数据并写入磁盘（持久化纯缓存数据）。文件名自动推导为 <c>{cacheKey}.json</c>。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="cacheKey">缓存键，如 "FishGameConfig"，文件名自动为 "FishGameConfig.json"</param>
    /// <returns>是否成功（缓存未命中或写入失败均返回 false）</returns>
    public bool SaveFromCache<T>(string cacheKey)
    {
        if (!DataCache.Instance.TryGet(cacheKey, out T data)) return false;
        return Save($"{cacheKey}.json", data);
    }

    /// <summary>从缓存读取数据并写入磁盘，key 和文件名自动使用 typeof(T).Name</summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <returns>是否成功（缓存未命中或写入失败均返回 false）</returns>
    public bool SaveFromCache<T>()
    {
        return SaveFromCache<T>(typeof(T).Name);
    }

    /// <summary>
    /// 从缓存读取数据并写入磁盘，使用自定义文件名。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="fileName">目标文件名（相对路径或绝对路径），如 "CustomName.json"</param>
    /// <returns>是否成功（缓存未命中或写入失败均返回 false）</returns>
    public bool SaveFromCache<T>(string cacheKey, string fileName)
    {
        if (!DataCache.Instance.TryGet(cacheKey, out T data)) return false;
        return Save(fileName, data);
    }

    /// <summary>
    /// 一步到位：持久化到磁盘，成功后自动进入文件缓存并触发变更事件。文件名自动推导为 <c>{key}.json</c>。
    /// <para>不再额外写入裸 Key，缓存中始终只有 <c>dc:{key}.json</c> 这一份副本。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">缓存键，同时作为文件名</param>
    /// <param name="value">要写入的数据</param>
    /// <returns>是否保存成功</returns>
    public bool SaveDirect<T>(string key, T value)
    {
        return Save($"{key}.json", value);
    }

    #endregion

    #region Raw (Binary, no serialization)

    /// <summary>
    /// 将原始字节数组写入磁盘（绕过 JSON 序列化，适用于截图、protobuf 等二进制数据）。
    /// <para>同样受加密开关控制。</para>
    /// </summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "Screenshot.dat" 或 "C:/Data/Raw.bin"</param>
    /// <param name="data">原始字节数组</param>
    /// <returns>是否保存成功</returns>
    public bool SaveRaw(string fileName, byte[] data)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} SaveRaw: 文件名为空");
            return false;
        }
        if (data == null)
        {
            Debug.LogError($"{Tag} SaveRaw: data 为 null");
            return false;
        }

        string path = GetFullPath(fileName);
        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        bool success;
        if (enableEncryption)
        {
            string base64 = Convert.ToBase64String(data);
            string encrypted = CryptoUtility.AesEncrypt(base64, passphrase);
            if (encrypted == null) return false;
            byte[] encryptedBytes = Encoding.UTF8.GetBytes(encrypted);
            success = FileUtility.WriteAllBytesAtomic(path, encryptedBytes);
        }
        else
        {
            success = FileUtility.WriteAllBytesAtomic(path, data);
        }

        // 与 Save 保持一致：写入过数据后切换加密时需要提示历史文件不会迁移
        if (success) _hasWrittenData = true;
        return success;
    }

    /// <summary>异步将原始字节数组写入磁盘（绕过 JSON 序列化）</summary>
    public async Task<bool> SaveRawAsync(string fileName, byte[] data)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} SaveRawAsync: 文件名为空");
            return false;
        }
        if (data == null)
        {
            Debug.LogError($"{Tag} SaveRawAsync: data 为 null");
            return false;
        }

        string path = GetFullPath(fileName);
        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        bool success;
        if (enableEncryption)
        {
            string base64 = Convert.ToBase64String(data);
            string encrypted = await Task.Run(() => CryptoUtility.AesEncrypt(base64, passphrase));
            if (encrypted == null) return false;
            byte[] encryptedBytes = Encoding.UTF8.GetBytes(encrypted);
            success = await FileUtility.WriteAllBytesAtomicAsync(path, encryptedBytes);
        }
        else
        {
            success = await FileUtility.WriteAllBytesAtomicAsync(path, data);
        }

        if (success) _hasWrittenData = true;
        return success;
    }

    /// <summary>从磁盘读取原始字节数组（绕过 JSON 反序列化）。同样受加密开关控制</summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "Screenshot.dat"</param>
    /// <returns>原始字节数组，失败返回 null</returns>
    public byte[] LoadRaw(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} LoadRaw: 文件名为空");
            return null;
        }

        string path = GetFullPath(fileName);
        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        if (enableEncryption)
        {
            byte[] encryptedBytes = FileUtility.ReadAllBytesSafe(
                path, validate: _verifyOnRead ? BuildRawValidator(passphrase) : null);
            if (encryptedBytes == null) return null;

            string encrypted = Encoding.UTF8.GetString(encryptedBytes);
            string base64 = CryptoUtility.AesDecrypt(encrypted, passphrase);
            if (base64 == null) return null;

            return DecodeBase64(base64);
        }
        else
        {
            // 明文原始数据没有结构可校验，但至少要挡住 0 字节：断电截断最常见的结果就是空文件
            Func<byte[], bool> nonEmptyValidator = null;
            if (_verifyOnRead)
                nonEmptyValidator = bytes => bytes != null && bytes.Length > 0;

            return FileUtility.ReadAllBytesSafe(path, validate: nonEmptyValidator);
        }
    }

    /// <summary>异步从磁盘读取原始字节数组（绕过 JSON 反序列化）</summary>
    public async Task<byte[]> LoadRawAsync(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} LoadRawAsync: 文件名为空");
            return null;
        }

        string path = GetFullPath(fileName);
        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        if (enableEncryption)
        {
            byte[] encryptedBytes = await FileUtility.ReadAllBytesSafeAsync(
                path, validate: _verifyOnRead ? BuildRawValidator(passphrase) : null);
            if (encryptedBytes == null) return null;

            return await Task.Run(() =>
            {
                string encrypted = Encoding.UTF8.GetString(encryptedBytes);
                string base64 = CryptoUtility.AesDecrypt(encrypted, passphrase);
                return DecodeBase64(base64);
            });
        }
        else
        {
            // 明文原始数据没有结构可校验，但至少要挡住 0 字节：断电截断最常见的结果就是空文件
            Func<byte[], bool> nonEmptyValidator = null;
            if (_verifyOnRead)
                nonEmptyValidator = bytes => bytes != null && bytes.Length > 0;

            return await FileUtility.ReadAllBytesSafeAsync(path, validate: nonEmptyValidator);
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// 删除指定数据文件及其内存缓存，并触发 <see cref="CacheChangeType.Remove"/> 变更事件。
    /// <para>先删磁盘文件，成功后再清缓存并通知，避免删除失败时事件与真实状态不一致。</para>
    /// <para>同时清理 .bak 备份与残留 .tmp：只删主文件时安全读取链会从备份回滚，
    /// 导致已删除的数据再次被读取到。</para>
    /// </summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>是否删除成功（文件不存在也返回 true）</returns>
    public bool Delete(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} Delete: 文件名为空");
            return false;
        }

        string path = GetFullPath(fileName);
        if (!FileUtility.DeleteFileWithBackup(path)) return false;

        // 磁盘删除成功后再清缓存：使用 Remove 让 OnDataChanged 订阅者感知删档
        DataCache.Instance.Remove(MakeCacheKey(fileName));
        return true;
    }

    /// <summary>
    /// 异步删除指定数据文件及其内存缓存，并触发 <see cref="CacheChangeType.Remove"/> 变更事件。
    /// <para>先删磁盘文件，成功后再清缓存并通知。</para>
    /// <para>同时清理 .bak 备份与残留 .tmp，避免安全读取链回滚使已删除的数据复活。</para>
    /// </summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> DeleteAsync(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} DeleteAsync: 文件名为空");
            return false;
        }

        string path = GetFullPath(fileName);
        bool success = await Task.Run(() => FileUtility.DeleteFileWithBackup(path));
        if (!success) return false;

        DataCache.Instance.Remove(MakeCacheKey(fileName));
        return true;
    }

    #endregion

    #region Refresh

    /// <summary>
    /// 强制从磁盘重新读取并刷新缓存，绕过已有缓存。
    /// 适用于需要确保数据为磁盘最新版本的场景（如读取其他模块写入的文件）。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public T Refresh<T>(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError($"{Tag} Refresh: 文件名为空");
            return default;
        }

        string cacheKey = MakeCacheKey(fileName);

        if (TryReadFromDisk(fileName, out T result))
        {
            if (IsCacheActive) DataCache.Instance.SetSilent(cacheKey, result);
            return result;
        }

        if (IsCacheActive) DataCache.Instance.RemoveSilent(cacheKey);
        return default;
    }

    #endregion

    #region Preload

    /// <summary>
    /// 预加载数据到缓存（不返回数据），用于提前预热。
    /// 后续 Load 调用将直接命中缓存，无需磁盘 IO。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>是否加载成功</returns>
    public bool Preload<T>(string fileName)
    {
        return TryLoad<T>(fileName, out _);
    }

    /// <summary>
    /// 异步预加载数据到缓存，不阻塞 UI。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>是否加载成功</returns>
    public async Task<bool> PreloadAsync<T>(string fileName)
    {
        var (success, _) = await TryLoadAsync<T>(fileName);
        return success;
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// 清除指定文件的内存缓存，下次 Load 将重新从磁盘读取。
    /// </summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    public void InvalidateCache(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        DataCache.Instance.RemoveSilent(MakeCacheKey(fileName));
    }

    /// <summary>清除所有内存缓存（不影响磁盘文件）</summary>
    public void ClearCache()
    {
        DataCache.Instance.Clear();
        Debug.Log($"{Tag} 缓存已全部清除");
    }

    #endregion

    #region Set (Cache only, no disk I/O)

    /// <summary>直接写入缓存（不写磁盘），零装箱</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, int value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），零装箱</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, float value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），零装箱</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, bool value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），零装箱</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, long value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），零装箱</summary>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set(string key, double value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），class 无装箱，struct 会装箱</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键，不能为 null</param>
    /// <param name="value">要缓存的值</param>
    public void Set<T>(string key, T value)
    {
        DataCache.Instance.Set(key, value);
    }

    /// <summary>直接写入缓存（不写磁盘），key 自动使用 typeof(T).Name</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="value">要缓存的值</param>
    public void Set<T>(T value)
    {
        DataCache.Instance.Set(typeof(T).Name, value);
    }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），零装箱。高频批量写入场景使用</summary>
    public void SetSilent(string key, int value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），零装箱。高频批量写入场景使用</summary>
    public void SetSilent(string key, float value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），零装箱。高频批量写入场景使用</summary>
    public void SetSilent(string key, bool value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），零装箱。高频批量写入场景使用</summary>
    public void SetSilent(string key, long value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），零装箱。高频批量写入场景使用</summary>
    public void SetSilent(string key, double value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件）。高频批量写入场景使用</summary>
    public void SetSilent<T>(string key, T value) { DataCache.Instance.SetSilent(key, value); }

    /// <summary>直接写入缓存（不写磁盘，不触发事件），key 自动使用 typeof(T).Name。高频批量写入场景使用</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="value">要缓存的值</param>
    public void SetSilent<T>(T value) { DataCache.Instance.SetSilent(typeof(T).Name, value); }

    #endregion

    #region Get (Cache only, no disk I/O)

    /// <summary>直接从缓存读取 int 值，未命中返回 defaultValue，零拆箱</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public int Get(string key, int defaultValue = 0)
    {
        return DataCache.Instance.GetInt(key, defaultValue);
    }

    /// <summary>直接从缓存读取 float 值，未命中返回 defaultValue，零拆箱</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public float Get(string key, float defaultValue = 0f)
    {
        return DataCache.Instance.GetFloat(key, defaultValue);
    }

    /// <summary>直接从缓存读取 bool 值，未命中返回 defaultValue，零拆箱</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public bool Get(string key, bool defaultValue = false)
    {
        return DataCache.Instance.GetBool(key, defaultValue);
    }

    /// <summary>直接从缓存读取 long 值，未命中返回 defaultValue，零拆箱</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public long Get(string key, long defaultValue = 0L)
    {
        return DataCache.Instance.GetLong(key, defaultValue);
    }

    /// <summary>直接从缓存读取 double 值，未命中返回 defaultValue，零拆箱</summary>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public double Get(string key, double defaultValue = 0.0)
    {
        return DataCache.Instance.GetDouble(key, defaultValue);
    }

    /// <summary>直接从缓存读取任意类型的值，未命中返回 defaultValue。class 无拆箱，struct 会拆箱</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public T Get<T>(string key, T defaultValue = default)
    {
        return DataCache.Instance.Get(key, defaultValue);
    }

    /// <summary>直接从缓存读取任意类型的值，key 自动使用 typeof(T).Name，未命中返回 defaultValue</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="defaultValue">未命中时的默认返回值</param>
    public T Get<T>(T defaultValue = default)
    {
        return DataCache.Instance.Get(typeof(T).Name, defaultValue);
    }

    /// <summary>尝试从缓存读取任意类型的值，返回是否命中。class 无拆箱，struct 会拆箱</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">命中的值，未命中为 default(T)</param>
    public bool TryGet<T>(string key, out T value)
    {
        return DataCache.Instance.TryGet(key, out value);
    }

    /// <summary>尝试从缓存读取任意类型的值，key 自动使用 typeof(T).Name，返回是否命中</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="value">命中的值，未命中为 default(T)</param>
    public bool TryGet<T>(out T value)
    {
        return DataCache.Instance.TryGet(typeof(T).Name, out value);
    }

    #endregion

    #region Remove / HasKey / GetOrCreate (Cache only)

    /// <summary>从缓存中移除指定 key。返回 true 表示成功移除，false 表示 key 不存在</summary>
    /// <param name="key">缓存键</param>
    public bool Remove(string key)
    {
        return DataCache.Instance.Remove(key);
    }

    /// <summary>从缓存中移除指定 key，不触发 OnChanged 事件。高频场景使用</summary>
    public bool RemoveSilent(string key)
    {
        return DataCache.Instance.RemoveSilent(key);
    }

    /// <summary>检查指定 key 是否存在于缓存中</summary>
    /// <param name="key">缓存键</param>
    public bool HasKey(string key)
    {
        return DataCache.Instance.HasKey(key);
    }

    /// <summary>读取缓存，未命中时通过工厂方法创建并自动存入缓存。线程安全，工厂方法仅在锁内调用一次</summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">未命中时的创建工厂，不能为 null</param>
    public T GetOrCreate<T>(string key, Func<T> factory)
    {
        return DataCache.Instance.GetOrCreate(key, factory);
    }

    #endregion

    #region File Info

    /// <summary>检查数据文件是否存在</summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>是否存在</returns>
    public bool Exists(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        return FileUtility.FileExists(GetFullPath(fileName));
    }

    /// <summary>获取文件完整路径</summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>完整文件路径</returns>
    public string GetFilePath(string fileName)
    {
        return GetFullPath(fileName);
    }

    /// <summary>获取文件大小（字节），不存在返回 -1</summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>文件大小，-1 表示不存在</returns>
    public long GetFileSize(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return -1;
        return FileUtility.GetFileSize(GetFullPath(fileName));
    }

    /// <summary>获取文件 MD5，不存在返回 null</summary>
    /// <param name="fileName">文件名（相对路径或绝对路径），如 "PlayerData.json" 或 "C:/Data/PlayerData.json"</param>
    /// <returns>MD5 字符串，不存在或失败返回 null</returns>
    public string GetFileMD5(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        return FileUtility.GetFileMD5(GetFullPath(fileName));
    }

    /// <summary>获取所有数据文件列表</summary>
    /// <param name="searchPattern">搜索模式，默认 "*.json"</param>
    /// <returns>文件名数组（不含路径）</returns>
    public string[] GetFileList(string searchPattern = "*.json")
    {
        string[] fullPaths = FileUtility.GetFiles(_basePath, searchPattern);
        if (fullPaths == null) return Array.Empty<string>();

        for (int i = 0; i < fullPaths.Length; i++)
        {
            fullPaths[i] = Path.GetFileName(fullPaths[i]);
        }
        return fullPaths;
    }

    #endregion

    #region Stats

    /// <summary>获取数据中心运行统计（供调试）</summary>
    public string GetStats()
    {
        return $"{Tag} BasePath: {_basePath}\n" +
               $"  Encryption: {_enableEncryption} | Cache: {_enableCache}\n" +
               $"  {DataCache.Instance.GetStats()}";
    }

    #endregion

    #region Private — Path Helpers

    /// <summary>
    /// 解析文件路径：若已是绝对路径则直接归一化，否则相对于 BasePath 拼接后再归一化。
    /// <para>路径含非法字符或过长时 <see cref="Path.GetFullPath(string)"/> 会抛异常，
    /// 这里捕获后回退为简单拼接，让上层仍能按“文件不存在”处理，而不是把异常抛给调用方。</para>
    /// </summary>
    /// <param name="fileName">文件名（相对路径）或绝对路径</param>
    /// <returns>标准化的完整文件路径</returns>
    private string GetFullPath(string fileName)
    {
        try
        {
            return Path.IsPathRooted(fileName)
                ? Path.GetFullPath(fileName)
                : Path.GetFullPath(Path.Combine(_basePath, fileName));
        }
        catch (Exception e) when (e is ArgumentException || e is PathTooLongException || e is NotSupportedException)
        {
            Debug.LogError($"{Tag} 路径不合法，已回退为拼接路径: {fileName} ({e.Message})");
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(_basePath, fileName);
        }
    }

    /// <summary>
    /// 生成内存缓存的 Key，统一添加 "dc:" 前缀以避免与其他模块冲突。
    /// <para>始终基于归一化后的完整路径生成，确保同一文件的相对路径与绝对路径写法共用同一份缓存，
    /// 避免同一文件出现两份互不同步的缓存。</para>
    /// </summary>
    /// <param name="fileName">文件名或绝对路径</param>
    /// <returns>缓存 Key</returns>
    private string MakeCacheKey(string fileName)
    {
        return $"dc:{GetFullPath(fileName)}";
    }

    #endregion

    #region Private — Disk Read

    /// <summary>
    /// 安全地将 Base64 字符串解码为字节数组。
    /// <para>损坏文件或口令变更时 AES 可能解出非 Base64 内容，
    /// 若直接调用 <see cref="Convert.FromBase64String"/> 会抛出异常，这里统一捕获并返回 null。</para>
    /// </summary>
    /// <param name="base64">Base64 字符串，可为 null</param>
    /// <returns>解码后的字节数组，失败返回 null</returns>
    private static byte[] DecodeBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException e)
        {
            Debug.LogError($"{Tag} Base64 解码失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 同步从磁盘读取文件并反序列化，通过 out 参数返回是否成功。
    /// <para>文件不存在、解密失败、JSON 为空或解析异常时返回 false，避免把 default(T) 当成加载成功。</para>
    /// </summary>
    private bool TryReadFromDisk<T>(string fileName, out T result)
    {
        result = default;
        string path = GetFullPath(fileName);

        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        // 内容校验：让「能打开但内容已损坏」的文件参与 .bak / .tmp 回滚，而不是直接交给业务层
        Func<byte[], bool> bytesValidator = _verifyOnRead ? BuildBytesValidator<T>(passphrase) : null;
        Func<string, bool> textValidator = _verifyOnRead ? BuildTextValidator<T>(passphrase) : null;

        if (enableEncryption)
        {
            byte[] bytes = FileUtility.ReadAllBytesSafe(path, validate: bytesValidator);
            if (bytes == null) return false;

            string content = Encoding.UTF8.GetString(bytes);
            string json = CryptoUtility.AesDecrypt(content, passphrase);
            if (json == null)
            {
                // 兼容「先写明文、后开启加密」的历史文件：解密失败时按明文再解析一次
                return TryParseJson(content, out result);
            }

            return TryParseJson(json, out result);
        }
        else
        {
            string json = FileUtility.ReadAllTextSafe(path, validate: textValidator);
            if (json == null) return false;

            if (TryParseJson(json, out result)) return true;

            // 兼容「先加密写入、后关闭加密」的历史文件：按密文再尝试解密解析
            if (!LooksLikeBase64(json)) return false;

            string decrypted = CryptoUtility.AesDecrypt(json, passphrase);
            if (decrypted == null || !TryParseJson(decrypted, out T decryptedResult)) return false;

            Debug.LogWarning($"{Tag} 文件为加密格式但当前加密已关闭，已解密加载: {fileName}");
            result = decryptedResult;
            return true;
        }
    }

    /// <summary>
    /// 构造文本层内容校验委托：能否直接反序列化为 T，或作为密文解密后再反序列化。
    /// <para>判定顺序与 <see cref="TryReadFromDisk{T}"/> 的历史兼容逻辑完全一致，
    /// 不会把「先写明文、后开启加密」或「先加密、后关闭加密」的历史文件误判为损坏。</para>
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="passphrase">AES 口令</param>
    /// <returns>校验委托，内容可用返回 true</returns>
    private static Func<string, bool> BuildTextValidator<T>(string passphrase)
    {
        return text =>
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (TryParseJson(text, out T _)) return true;
            if (!LooksLikeBase64(text)) return false;

            string decrypted = CryptoUtility.AesDecrypt(text, passphrase);
            return decrypted != null && TryParseJson(decrypted, out T _);
        };
    }

    /// <summary>
    /// 构造字节层内容校验委托：解码为文本后走 <see cref="BuildTextValidator{T}"/> 的同一套判定。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="passphrase">AES 口令</param>
    /// <returns>校验委托，内容可用返回 true</returns>
    private static Func<byte[], bool> BuildBytesValidator<T>(string passphrase)
    {
        Func<string, bool> textValidator = BuildTextValidator<T>(passphrase);
        return bytes =>
        {
            if (bytes == null || bytes.Length == 0) return false;
            return textValidator(Encoding.UTF8.GetString(bytes));
        };
    }

    /// <summary>
    /// 构造原始字节的内容校验委托：只验证「能否解密并解出 Base64」，不解析业务结构。
    /// <para>用于 <see cref="LoadRaw"/> 这类不经过 JSON 的数据。</para>
    /// </summary>
    /// <param name="passphrase">AES 口令</param>
    /// <returns>校验委托，内容可用返回 true</returns>
    private static Func<byte[], bool> BuildRawValidator(string passphrase)
    {
        return bytes =>
        {
            if (bytes == null || bytes.Length == 0) return false;

            string encrypted = Encoding.UTF8.GetString(bytes);
            string base64 = CryptoUtility.AesDecrypt(encrypted, passphrase);
            return base64 != null && DecodeBase64(base64) != null;
        };
    }

    /// <summary>
    /// 解析 JSON 文本，返回是否成功（空串 / 解析失败均为 false）。
    /// </summary>
    private static bool TryParseJson<T>(string json, out T result)
    {
        if (string.IsNullOrEmpty(json))
        {
            result = default;
            return false;
        }

        return JsonUtil.TryFromJson(json, out result);
    }

    /// <summary>
    /// 粗判字符串是否可能是 Base64 密文：明文 JSON 一定包含 { } " : 等非法字符，会被快速排除，
    /// 避免对普通明文文件做无意义的解密尝试并刷出错误日志。
    /// <para>另需满足最小有效长度：纯字母的短文本（如 "True"、"Yes"）字符集合法但绝不可能是密文，
    /// 靠长度下限把它们挡在解密之外，避免合法短文本被误判为损坏。</para>
    /// </summary>
    /// <param name="text">待判断文本</param>
    /// <returns>可能为 Base64 时返回 true</returns>
    private static bool LooksLikeBase64(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // AES 加密后至少数十字节，Base64 编码后必然远长于该下限
        const int k_minBase64Length = 8;

        int meaningfulLength = 0;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
                continue;

            bool isValid = (c >= 'A' && c <= 'Z')
                           || (c >= 'a' && c <= 'z')
                           || (c >= '0' && c <= '9')
                           || c == '+' || c == '/' || c == '=';

            if (!isValid) return false;

            meaningfulLength++;
        }

        return meaningfulLength >= k_minBase64Length;
    }

    /// <summary>
    /// 异步从磁盘读取文件并反序列化，通过元组返回是否成功。
    /// <para>文件不存在、解密失败、JSON 为空或解析异常时 success 为 false。</para>
    /// </summary>
    private async Task<(bool success, T result)> TryReadFromDiskAsync<T>(string fileName)
    {
        string path = GetFullPath(fileName);

        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        // 内容校验：让「能打开但内容已损坏」的文件参与 .bak / .tmp 回滚，而不是直接交给业务层
        Func<byte[], bool> bytesValidator = _verifyOnRead ? BuildBytesValidator<T>(passphrase) : null;
        Func<string, bool> textValidator = _verifyOnRead ? BuildTextValidator<T>(passphrase) : null;

        if (enableEncryption)
        {
            byte[] bytes = await FileUtility.ReadAllBytesSafeAsync(path, validate: bytesValidator);
            if (bytes == null) return (false, default);

            return await Task.Run(() =>
            {
                string content = Encoding.UTF8.GetString(bytes);
                string json = CryptoUtility.AesDecrypt(content, passphrase);

                // 兼容「先写明文、后开启加密」的历史文件
                string target = json ?? content;
                return TryParseJson(target, out T parsed)
                    ? (true, parsed)
                    : (false, default(T));
            });
        }
        else
        {
            string json = await FileUtility.ReadAllTextSafeAsync(path, validate: textValidator);
            if (json == null) return (false, default);

            return await Task.Run(() =>
            {
                if (TryParseJson(json, out T parsed)) return (true, parsed);

                // 兼容「先加密写入、后关闭加密」的历史文件：按密文再尝试解密解析
                if (!LooksLikeBase64(json)) return (false, default(T));

                string decrypted = CryptoUtility.AesDecrypt(json, passphrase);
                if (decrypted == null || !TryParseJson(decrypted, out T decryptedResult))
                {
                    return (false, default(T));
                }

                Debug.LogWarning($"{Tag} 文件为加密格式但当前加密已关闭，已解密加载: {fileName}");
                return (true, decryptedResult);
            });
        }
    }

    #endregion

    #region Private — Disk Write

    /// <summary>
    /// 同步将对象序列化并写入磁盘（原子写入，断电安全）。
    /// 根据加密开关决定是否先序列化再加密。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="path">目标文件完整路径</param>
    /// <param name="data">要保存的数据对象</param>
    /// <returns>是否写入成功</returns>
    private bool WriteToDisk<T>(string path, T data)
    {
        string json = JsonUtil.ToJson(data);
        if (json == null) return false;

        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        if (enableEncryption)
        {
            string encrypted = CryptoUtility.AesEncrypt(json, passphrase);
            if (encrypted == null) return false;

            byte[] bytes = Encoding.UTF8.GetBytes(encrypted);
            return FileUtility.WriteAllBytesAtomic(path, bytes);
        }
        else
        {
            return FileUtility.WriteAllTextAtomic(path, json);
        }
    }

    /// <summary>
    /// 异步将对象序列化并写入磁盘（原子写入，断电安全）。
    /// 序列化和加密操作在 Task.Run 中执行以避免阻塞调用线程。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="path">目标文件完整路径</param>
    /// <param name="data">要保存的数据对象</param>
    /// <returns>是否写入成功</returns>
    private async Task<bool> WriteToDiskAsync<T>(string path, T data)
    {
        bool enableEncryption = _enableEncryption;
        string passphrase = _passphrase;

        // 序列化与加密都在线程池执行，大对象不会阻塞调用线程
        string content = await Task.Run(() =>
        {
            string json = JsonUtil.ToJson(data);
            if (json == null) return null;
            return enableEncryption ? CryptoUtility.AesEncrypt(json, passphrase) : json;
        });

        if (content == null) return false;

        return enableEncryption
            ? await FileUtility.WriteAllBytesAtomicAsync(path, Encoding.UTF8.GetBytes(content))
            : await FileUtility.WriteAllTextAtomicAsync(path, content);
    }

    #endregion
}