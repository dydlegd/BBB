/// <summary>
/// DataCenter 数据键常量。统一管理缓存 Key 与磁盘文件名，避免硬编码字符串散落。
/// <para>所有 Save / Load / Get / Set / SaveFromCache 操作均引用此处常量。</para>
///
/// <para>新增数据时在此追加常量，格式：{Name}CacheKey / {Name}FileName 配对。</para>
/// </summary>
public static class DataDefine
{
    // ========================================
    //  PlayerData
    // ========================================

    /// <summary>获取指定玩家的缓存 Key（注意：每次调用会分配新字符串，请勿在热路径高频调用，需要时缓存结果）</summary>
    public static string PlayerDataCacheKey(int player) => $"PlayerData_{player}";
    /// <summary>获取指定玩家的磁盘文件名（注意：每次调用会分配新字符串，请勿在热路径高频调用，需要时缓存结果）</summary>
    public static string PlayerDataFileName(int player) => $"PlayerData_{player}.json";
}