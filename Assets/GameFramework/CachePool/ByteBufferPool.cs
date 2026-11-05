using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 通用字节数组缓存池，适用于高频 byte[] 创建/销毁场景，零 GC 压力。
/// <para>
/// 内部按 2 的幂次分桶管理，同一桶内所有缓冲区长度相等，线程安全。
/// 支持容量限制防止无限制膨胀，内置统计信息便于性能分析。
/// </para>
/// <para>使用示例：</para>
/// <code>
/// // ===== 创建池（全局单例或局部实例均可） =====
/// ByteBufferPool pool = new ByteBufferPool(maxPerBucket: 64);
///
/// // ===== 租用缓冲区 =====
/// byte[] buffer = pool.Rent(512); // 实际返回长度 >= 512 的2的幂次（此处为512）
/// try
/// {
///     // ... 使用 buffer，关注逻辑长度而非 buffer.Length ...
/// }
/// finally
/// {
///     pool.Return(buffer); // 归还，默认自动清零
/// }
///
/// // ===== 不关心数据残留时跳过清零 =====
/// pool.Return(buffer, clearArray: false);
///
/// // ===== 调试 =====
/// Debug.Log(pool.GetStats()); // "ByteBufferPool Cached=32 HitRate=94.3% ..."
/// </code>
/// </summary>
public class ByteBufferPool
{
    #region Fields

    private readonly Dictionary<int, Stack<byte[]>> _buckets = new Dictionary<int, Stack<byte[]>>();
    private readonly object _lock = new object();
    private readonly int _maxPerBucket;

    #endregion

    #region Statistics

    private long _totalRented;
    private long _totalReturned;
    private long _poolHitCount;
    private long _poolMissCount;

    /// <summary>池中当前缓存的缓冲区总数（O(n)）</summary>
    public int CachedCount
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (var bucket in _buckets.Values)
                    count += bucket.Count;
                return count;
            }
        }
    }

    /// <summary>累计租用次数</summary>
    public long TotalRented => _totalRented;

    /// <summary>累计归还次数</summary>
    public long TotalReturned => _totalReturned;

    /// <summary>池命中率（命中次数 / 租用次数），返回 0~1</summary>
    public float HitRate => _totalRented == 0 ? 0f : (float)_poolHitCount / _totalRented;

    /// <summary>不同大小的桶的数量</summary>
    public int BucketCount
    {
        get { lock (_lock) return _buckets.Count; }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// 创建字节缓存池
    /// </summary>
    /// <param name="maxPerBucket">每个大小桶最多缓存多少个缓冲区。-1 表示无限制。默认 64。</param>
    public ByteBufferPool(int maxPerBucket = 64)
    {
        _maxPerBucket = maxPerBucket;
    }

    #endregion

    #region Rent / TryRent

    /// <summary>
    /// 从池中租用一个长度 >= minimumLength 的字节数组。
    /// 返回的数组长度一定是 2 的幂次，可能大于请求值。
    /// </summary>
    /// <param name="minimumLength">所需最小长度</param>
    /// <returns>长度 >= minimumLength 的字节数组</returns>
    public byte[] Rent(int minimumLength)
    {
        if (minimumLength < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));

        if (minimumLength == 0)
            return Array.Empty<byte>();

        _totalRented++;

        int bucketSize = NextPowerOf2(minimumLength);

        lock (_lock)
        {
            if (_buckets.TryGetValue(bucketSize, out Stack<byte[]> stack) && stack.Count > 0)
            {
                _poolHitCount++;
                return stack.Pop();
            }
        }

        _poolMissCount++;
        return new byte[bucketSize];
    }

    /// <summary>
    /// 尝试从池中获取缓冲区。池空时返回 null 而非分配新数组。
    /// 适用于不希望触发分配的降级路径。
    /// </summary>
    /// <param name="minimumLength">所需最小长度</param>
    /// <param name="result">命中时返回缓冲区，否则 null</param>
    /// <returns>是否命中</returns>
    public bool TryRent(int minimumLength, out byte[] result)
    {
        if (minimumLength < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));

        result = null;

        if (minimumLength == 0)
        {
            result = Array.Empty<byte>();
            return true;
        }

        int bucketSize = NextPowerOf2(minimumLength);

        lock (_lock)
        {
            if (_buckets.TryGetValue(bucketSize, out Stack<byte[]> stack) && stack.Count > 0)
            {
                _totalRented++;
                _poolHitCount++;
                result = stack.Pop();
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Return

    /// <summary>
    /// 将字节数组归还池中以便复用。
    /// </summary>
    /// <param name="array">之前通过 Rent 租用的数组。null 或零长度数组将被忽略。</param>
    /// <param name="clearArray">归还前是否清零数组内容，默认 true</param>
    public void Return(byte[] array, bool clearArray = true)
    {
        if (array == null || array.Length == 0) return;

        _totalReturned++;

        if (clearArray)
        {
            Array.Clear(array, 0, array.Length);
        }

        int bucketSize = array.Length;

        lock (_lock)
        {
            if (!_buckets.TryGetValue(bucketSize, out Stack<byte[]> stack))
            {
                stack = new Stack<byte[]>();
                _buckets[bucketSize] = stack;
            }

            if (_maxPerBucket > 0 && stack.Count >= _maxPerBucket)
            {
                return;
            }

            stack.Push(array);
        }
    }

    #endregion

    #region Prewarm / Clear

    /// <summary>
    /// 预填充指定大小的桶，避免运行时的冷启动分配。
    /// </summary>
    /// <param name="bucketSize">目标数组大小（会被对齐到 2 的幂次）</param>
    /// <param name="count">预填充数量</param>
    public void Prewarm(int bucketSize, int count)
    {
        int alignedSize = NextPowerOf2(bucketSize);

        lock (_lock)
        {
            if (!_buckets.TryGetValue(alignedSize, out Stack<byte[]> stack))
            {
                stack = new Stack<byte[]>();
                _buckets[alignedSize] = stack;
            }

            int toCreate = _maxPerBucket > 0
                ? Math.Min(count, _maxPerBucket - stack.Count)
                : count;

            for (int i = 0; i < toCreate; i++)
            {
                stack.Push(new byte[alignedSize]);
            }
        }
    }

    /// <summary>
    /// 清空所有已缓存的缓冲区，让 GC 回收。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buckets.Clear();
        }
    }

    /// <summary>
    /// 缩小池大小：丢弃每个桶中超过指定比例的缓冲区。
    /// </summary>
    /// <param name="trimRatio">保留比例，0~1。如 0.5 表示每个桶只保留一半。</param>
    public void Trim(float trimRatio = 0.5f)
    {
        if (trimRatio < 0f) trimRatio = 0f;
        if (trimRatio > 1f) trimRatio = 1f;

        lock (_lock)
        {
            foreach (var bucket in _buckets.Values)
            {
                int keepCount = Math.Max(1, (int)(bucket.Count * trimRatio));
                while (bucket.Count > keepCount)
                {
                    bucket.Pop();
                }
            }
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// 获取池统计信息的可读字符串，用于调试。
    /// </summary>
    public string GetStats()
    {
        var sb = new StringBuilder();
        sb.Append("ByteBufferPool ");
        sb.Append($"Cached={CachedCount} ");
        sb.Append($"Buckets={BucketCount} ");
        sb.Append($"Rented={TotalRented} ");
        sb.Append($"Returned={TotalReturned} ");
        sb.Append($"HitRate={HitRate:P1}");
        sb.Append($" Misses={_poolMissCount}");

        lock (_lock)
        {
            if (_buckets.Count > 0)
            {
                sb.Append(" | ");
                foreach (var kv in _buckets)
                {
                    if (kv.Value.Count > 0)
                    {
                        sb.Append($"{kv.Key}:{kv.Value.Count} ");
                    }
                }
            }
        }

        return sb.ToString();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 计算大于等于 v 的最小 2 的幂次方（如 10 -> 16，8 -> 8）。
    /// 通过位运算将最高位以下全部填 1，再 +1 得到结果。
    /// </summary>
    /// <param name="v">输入值，<= 0 时返回 1</param>
    /// <returns>大于等于 v 的最小 2 的幂次方</returns>
    private static int NextPowerOf2(int v)
    {
        if (v <= 0) return 1;
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        return v + 1;
    }

    #endregion
}