using System;
using System.Collections.Generic;

/// <summary>
/// 位标记的操作类型
/// </summary>
public enum BitFlagOperateType
{
    /// <summary>添加单个标记（按位或）</summary>
    Add,
    /// <summary>移除单个标记（按位与非）</summary>
    Remove,
    /// <summary>清空全部标记</summary>
    Clear,
    /// <summary>整体覆盖为指定掩码</summary>
    Set
}

/// <summary>
/// 位标记变更事件参数
/// </summary>
/// <typeparam name="T">标记枚举类型</typeparam>
public struct BitFlagChangedEvent<T> where T : Enum
{
    /// <summary>本次变更的操作类型</summary>
    public BitFlagOperateType OperateType;
    /// <summary>被操作的标记：Add/Remove 为对应标记，Set 为目标值，Clear 为 default</summary>
    public T Flag;
    /// <summary>变更前的掩码</summary>
    public ulong OldMask;
    /// <summary>变更后的掩码</summary>
    public ulong NewMask;
    /// <summary>变更来源，由调用方传入，可为 null</summary>
    public object Sender;

    /// <summary>构造变更事件参数</summary>
    /// <param name="operateType">操作类型</param>
    /// <param name="flag">被操作的标记</param>
    /// <param name="oldMask">变更前掩码</param>
    /// <param name="newMask">变更后掩码</param>
    /// <param name="sender">变更来源</param>
    public BitFlagChangedEvent(BitFlagOperateType operateType, T flag, ulong oldMask, ulong newMask, object sender = null)
    {
        OperateType = operateType;
        Flag = flag;
        OldMask = oldMask;
        NewMask = newMask;
        Sender = sender;
    }
}

/// <summary>
/// 位标记管理器，内部C#原生委托事件，不依赖QFramework消息总线
/// <para>约定：</para>
/// <para>1. T 为 [Flags] 枚举，取值 1 &lt;&lt; n</para>
/// <para>2. Add / Remove 单次只修改一个标记</para>
/// <para>使用示例：</para>
/// <code>
/// // ===== 1. 定义标记枚举 =====
/// [Flags]
/// public enum EntityState
/// {
///     None    = 0,
///     Idle    = 1 << 0,  // 1
///     Moving  = 1 << 1,  // 2
///     Dead    = 1 << 2,  // 4
///     Invincible = 1 << 3,  // 8
/// }
///
/// // ===== 2. 创建 BitFlag 实例 =====
/// var state = new BitFlag<EntityState>();           // 默认无标记
/// var state2 = new BitFlag<EntityState>(EntityState.Idle); // 初始包含 Idle
///
/// // ===== 3. 添加 / 移除标记 =====
/// state.Add(EntityState.Idle);      // 添加 Idle 标记
/// state.Add(EntityState.Moving);    // 添加 Moving 标记（可同时拥有多个标记）
/// state.Remove(EntityState.Idle);   // 移除 Idle 标记
///
/// // ===== 4. 判断标记 =====
/// bool isMoving = state.Has(EntityState.Moving);
/// bool hasAll = state.HasAll(EntityState.Idle, EntityState.Moving);
/// bool hasAny = state.HasAny(EntityState.Dead, EntityState.Invincible);
///
/// // ===== 5. 清空 / 整体设置 =====
/// state.Clear();                        // 清空所有标记
/// state.Set(EntityState.Idle | EntityState.Invincible); // 一次性设置多个标记
///
/// // ===== 6. 订阅变更事件 =====
/// state.OnChanged += evt =>
/// {
///     Debug.Log($"[{evt.OperateType}] {evt.Flag}: {evt.OldMask} -> {evt.NewMask}");
///
///     // 使用扩展方法精确判断
///     if (evt.IsAddFlag(EntityState.Dead))
///         Debug.Log("角色死亡！");
///     if (evt.IsRemoveFlag(EntityState.Invincible))
///         Debug.Log("无敌结束！");
/// };
///
/// // ===== 7. 批量操作（逐个触发事件） =====
/// state.AddMany(EntityState.Idle, EntityState.Moving);
/// state.RemoveMany(EntityState.Dead, EntityState.Invincible);
///
/// // ===== 8. 获取当前值 =====
/// EntityState current = state.Value;
/// ulong mask = state.Mask;
/// Debug.Log(state.ToString());
/// </code>
/// </summary>
/// <typeparam name="T">标记枚举类型，需为 [Flags] 枚举</typeparam>
public class BitFlag<T> where T : Enum
{
    [Newtonsoft.Json.JsonProperty("Value")]
    private ulong _mask;
    private readonly object _lockObj = new object();

    /// <summary>枚举值 → 掩码的转换器，静态初始化一次，避免每次调用走反射与装箱</summary>
    private static readonly Func<T, ulong> ToMaskConverter;
    /// <summary>掩码 → 枚举值的反向转换器，按底层类型做 unchecked 转换，兼容负数常量</summary>
    private static readonly Func<ulong, T> FromMaskConverter;

    /// <summary>已警告过的复合掩码，避免同一非法用法重复刷日志</summary>
    private static readonly HashSet<ulong> WarnedCompositeMasks = new HashSet<ulong>();

    /// <summary>
    /// 静态初始化：校验 [Flags] 约定并生成双向转换器。
    /// <para>按枚举底层类型转换，兼容 int 底层枚举的负数常量（如 int.MinValue、All = -1），
    /// 避免 Convert.ToUInt64 在合法枚举值上抛 OverflowException。</para>
    /// </summary>
    static BitFlag()
    {
        Type enumType = typeof(T);
        if (enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length == 0)
        {
            UnityEngine.Debug.LogWarning($"[BitFlag] {enumType.Name} 未标记 [Flags]，位组合语义可能不符合预期");
        }

        switch (Type.GetTypeCode(Enum.GetUnderlyingType(enumType)))
        {
            case TypeCode.SByte:
                ToMaskConverter = v => (ulong)(sbyte)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((sbyte)m);
                break;
            case TypeCode.Byte:
                ToMaskConverter = v => (byte)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((byte)m);
                break;
            case TypeCode.Int16:
                ToMaskConverter = v => (ulong)(short)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((short)m);
                break;
            case TypeCode.UInt16:
                ToMaskConverter = v => (ushort)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((ushort)m);
                break;
            case TypeCode.Int32:
                ToMaskConverter = v => (uint)(int)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((int)(uint)m);
                break;
            case TypeCode.UInt32:
                ToMaskConverter = v => (uint)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((uint)m);
                break;
            case TypeCode.Int64:
                ToMaskConverter = v => (ulong)(long)(object)v;
                FromMaskConverter = m => (T)(object)unchecked((long)m);
                break;
            default:
                ToMaskConverter = v => (ulong)(object)v;
                FromMaskConverter = m => (T)(object)m;
                break;
        }
    }

    /// <summary>标记发生实际变更时触发，回调在锁外执行</summary>
    public event Action<BitFlagChangedEvent<T>> OnChanged;

    /// <summary>当前标记值（枚举视图，只读快照）</summary>
    [Newtonsoft.Json.JsonIgnore]
    public T Value
    {
        get
        {
            lock (_lockObj)
            {
                return FromMaskConverter(_mask);
            }
        }
    }

    /// <summary>当前标记掩码（只读快照）</summary>
    [Newtonsoft.Json.JsonIgnore]
    public ulong Mask
    {
        get
        {
            lock (_lockObj)
            {
                return _mask;
            }
        }
    }

    /// <summary>创建空标记集合</summary>
    public BitFlag()
    {
        _mask = 0;
    }

    /// <summary>以指定初始值创建标记集合</summary>
    /// <param name="initValue">初始标记，允许复合值</param>
    public BitFlag(T initValue)
    {
        _mask = ToMaskConverter(initValue);
    }

    /// <summary>
    /// 添加单个标记（按位或）。
    /// <para>标记已存在时不触发事件。</para>
    /// <para>传入复合掩码（多个位）时只发一个 Add 事件，订阅者按单标记判断会漏掉部分位，
    /// 因此首次传入时打印警告；需要一次设置多个位请用 <see cref="Set"/>。</para>
    /// </summary>
    /// <param name="flag">要添加的标记，约定为单个位</param>
    /// <param name="sender">变更来源，随事件回传</param>
    /// <returns>掩码发生实际变更返回 true；标记已存在返回 false</returns>
    public bool Add(T flag, object sender = null)
    {
        ulong val = ToMaskConverter(flag);
        WarnIfComposite(val);

        BitFlagChangedEvent<T>? evt = null;
        lock (_lockObj)
        {
            ulong newMask = _mask | val;
            if (newMask == _mask) return false;

            ulong oldMask = _mask;
            _mask = newMask;
            evt = new BitFlagChangedEvent<T>(BitFlagOperateType.Add, flag, oldMask, newMask, sender);
        }
        if (evt.HasValue) OnChanged?.Invoke(evt.Value);
        return true;
    }

    /// <summary>
    /// 移除单个标记（按位与非）。
    /// <para>标记不存在时不触发事件。</para>
    /// <para>传入复合掩码（多个位）时只发一个 Remove 事件，订阅者按单标记判断会漏掉部分位，
    /// 因此首次传入时打印警告。</para>
    /// </summary>
    /// <param name="flag">要移除的标记，约定为单个位</param>
    /// <param name="sender">变更来源，随事件回传</param>
    /// <returns>掩码发生实际变更返回 true；标记本来就不存在返回 false</returns>
    public bool Remove(T flag, object sender = null)
    {
        ulong val = ToMaskConverter(flag);
        WarnIfComposite(val);

        BitFlagChangedEvent<T>? evt = null;
        lock (_lockObj)
        {
            ulong newMask = _mask & ~val;
            if (newMask == _mask) return false;

            ulong oldMask = _mask;
            _mask = newMask;
            evt = new BitFlagChangedEvent<T>(BitFlagOperateType.Remove, flag, oldMask, newMask, sender);
        }
        if (evt.HasValue) OnChanged?.Invoke(evt.Value);
        return true;
    }

    /// <summary>
    /// 判断是否包含指定标记的全部位（传入复合值时要求每一位都存在）。
    /// <para>等价于单参数版本的 HasAll。</para>
    /// </summary>
    /// <param name="flag">要判断的标记</param>
    /// <returns>全部位都存在返回 true</returns>
    public bool Has(T flag)
    {
        lock (_lockObj)
        {
            ulong val = ToMaskConverter(flag);
            return (_mask & val) == val;
        }
    }

    /// <summary>
    /// 清空全部标记。
    /// <para>原本为空时不触发事件。</para>
    /// </summary>
    /// <param name="sender">变更来源，随事件回传</param>
    public void Clear(object sender = null)
    {
        BitFlagChangedEvent<T>? evt = null;
        lock (_lockObj)
        {
            if (_mask == 0) return;
            ulong oldMask = _mask;
            _mask = 0;
            evt = new BitFlagChangedEvent<T>(BitFlagOperateType.Clear, default, oldMask, 0, sender);
        }
        if (evt.HasValue) OnChanged?.Invoke(evt.Value);
    }

    /// <summary>
    /// 整体覆盖为指定掩码。
    /// <para>与当前值相同则不触发事件。</para>
    /// </summary>
    /// <param name="value">目标标记，允许复合值</param>
    /// <param name="sender">变更来源，随事件回传</param>
    public void Set(T value, object sender = null)
    {
        BitFlagChangedEvent<T>? evt = null;
        lock (_lockObj)
        {
            ulong newMask = ToMaskConverter(value);
            if (newMask == _mask) return;

            ulong oldMask = _mask;
            _mask = newMask;
            evt = new BitFlagChangedEvent<T>(BitFlagOperateType.Set, value, oldMask, newMask, sender);
        }
        if (evt.HasValue) OnChanged?.Invoke(evt.Value);
    }

    /// <summary>
    /// 订阅变更事件，返回可反注册的句柄。
    /// <para>推荐用法：<c>.Subscribe(handler).UnRegisterWhenGameObjectDestroyed(gameObject)</c>，
    /// 避免长期存活的 BitFlag 持有已销毁的 MonoBehaviour。</para>
    /// </summary>
    /// <param name="handler">变更回调</param>
    /// <returns>订阅句柄；handler 为 null 时返回 null</returns>
    public BitFlagSubscription<T> Subscribe(Action<BitFlagChangedEvent<T>> handler)
    {
        if (handler == null) return null;
        OnChanged += handler;
        return new BitFlagSubscription<T>(this, handler);
    }

    /// <summary>
    /// 校验 Add / Remove 的入参是否为单个位，复合掩码首次出现时打印警告。
    /// <para>只警告一次，避免热路径刷屏。</para>
    /// </summary>
    /// <param name="mask">待校验的掩码</param>
    private static void WarnIfComposite(ulong mask)
    {
        if (mask == 0 || (mask & (mask - 1)) == 0) return;

        bool firstTime;
        lock (WarnedCompositeMasks)
        {
            firstTime = WarnedCompositeMasks.Add(mask);
        }
        if (firstTime)
        {
            UnityEngine.Debug.LogWarning(
                $"[BitFlag<{typeof(T).Name}>] Add/Remove 收到复合掩码 {mask}：约定只接收单个位，" +
                $"事件订阅者按单标记判断会漏掉其中部分位，需要一次设置多个位请改用 Set。");
        }
    }

    /// <summary>批量添加标记，sender=null</summary>
    public void AddMany(params T[] flags)
    {
        AddMany(null, flags);
    }

    /// <summary>批量添加标记，统一指定sender，内部逐个触发Add事件</summary>
    public void AddMany(object sender, params T[] flags)
    {
        if (flags == null || flags.Length == 0) return;
        foreach (var f in flags)
        {
            Add(f, sender);
        }
    }

    /// <summary>批量移除标记，sender=null</summary>
    public void RemoveMany(params T[] flags)
    {
        RemoveMany(null, flags);
    }

    /// <summary>批量移除标记，统一指定sender，内部逐个触发Remove事件</summary>
    public void RemoveMany(object sender, params T[] flags)
    {
        if (flags == null || flags.Length == 0) return;
        foreach (var f in flags)
        {
            Remove(f, sender);
        }
    }

    /// <summary>
    /// 判断是否同时包含全部指定标记（复合值要求每一位都存在）。
    /// <para>空参数视为 true；内部零分配，可用于每帧查询。</para>
    /// </summary>
    /// <param name="flags">要判断的标记集合</param>
    /// <returns>全部标记都存在返回 true</returns>
    public bool HasAll(params T[] flags)
    {
        if (flags == null) return true;
        lock (_lockObj)
        {
            ulong mask = _mask;
            for (int i = 0; i < flags.Length; i++)
            {
                ulong val = ToMaskConverter(flags[i]);
                if ((mask & val) != val) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 判断是否包含任意一个指定标记（复合值只要命中其中任意一位即为 true）。
    /// <para>空参数视为 false；内部零分配，可用于每帧查询。</para>
    /// </summary>
    /// <param name="flags">要判断的标记集合</param>
    /// <returns>命中任意一位返回 true</returns>
    public bool HasAny(params T[] flags)
    {
        if (flags == null) return false;
        lock (_lockObj)
        {
            ulong mask = _mask;
            for (int i = 0; i < flags.Length; i++)
            {
                if ((mask & ToMaskConverter(flags[i])) != 0) return true;
            }
            return false;
        }
    }

    /// <summary>返回当前标记的枚举名称，未定义组合值时返回数值</summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}

/// <summary>
/// BitFlag 订阅句柄，反注册后释放对订阅者与 BitFlag 的引用。
/// <para>实现 <see cref="QFramework.IUnRegister"/>，因此可以直接接
/// <c>UnRegisterWhenGameObjectDestroyed(gameObject)</c>，在 GameObject 销毁时自动反注册。</para>
/// </summary>
/// <typeparam name="T">标记枚举类型</typeparam>
public sealed class BitFlagSubscription<T> : QFramework.IUnRegister, IDisposable where T : Enum
{
    private BitFlag<T> _owner;
    private Action<BitFlagChangedEvent<T>> _handler;

    /// <summary>由 <see cref="BitFlag{T}.Subscribe"/> 创建，外部不要直接 new</summary>
    /// <param name="owner">事件所属的 BitFlag</param>
    /// <param name="handler">已注册的回调</param>
    internal BitFlagSubscription(BitFlag<T> owner, Action<BitFlagChangedEvent<T>> handler)
    {
        _owner = owner;
        _handler = handler;
    }

    /// <summary>反注册事件并释放引用，重复调用安全</summary>
    public void UnRegister()
    {
        BitFlag<T> owner = _owner;
        Action<BitFlagChangedEvent<T>> handler = _handler;

        _owner = null;
        _handler = null;

        if (owner != null && handler != null)
        {
            owner.OnChanged -= handler;
        }
    }

    /// <summary>等同于 <see cref="UnRegister"/></summary>
    public void Dispose()
    {
        UnRegister();
    }
}

/// <summary>
/// BitFlag事件扩展方法
/// </summary>
public static class BitFlagExtensions
{
    /// <summary>判断本次事件是否是【指定标记新增】</summary>
    public static bool IsAddFlag<T>(this BitFlagChangedEvent<T> evt, T flag) where T : Enum
    {
        return evt.OperateType == BitFlagOperateType.Add && EqualityComparer<T>.Default.Equals(evt.Flag, flag);
    }

    /// <summary>判断本次事件是否是【指定标记移除】</summary>
    public static bool IsRemoveFlag<T>(this BitFlagChangedEvent<T> evt, T flag) where T : Enum
    {
        return evt.OperateType == BitFlagOperateType.Remove && EqualityComparer<T>.Default.Equals(evt.Flag, flag);
    }

    /// <summary>判断本次事件是【指定标记变动】（Add或Remove）</summary>
    public static bool IsFlagChanged<T>(this BitFlagChangedEvent<T> evt, T flag) where T : Enum
    {
        return (evt.OperateType == BitFlagOperateType.Add || evt.OperateType == BitFlagOperateType.Remove)
            && EqualityComparer<T>.Default.Equals(evt.Flag, flag);
    }

    /// <summary>判断本次事件是否是 Clear</summary>
    public static bool IsClear<T>(this BitFlagChangedEvent<T> evt) where T : Enum
    {
        return evt.OperateType == BitFlagOperateType.Clear;
    }

    /// <summary>判断本次事件是否是 Set</summary>
    public static bool IsSet<T>(this BitFlagChangedEvent<T> evt) where T : Enum
    {
        return evt.OperateType == BitFlagOperateType.Set;
    }
}