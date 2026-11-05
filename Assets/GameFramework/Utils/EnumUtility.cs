using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

/// <summary>
/// 通用枚举扩展工具类。
/// 提供枚举解析、名称/描述获取、Flags 位运算、随机取值、遍历等常用枚举操作。
/// 名称/描述查询结果有缓存，适合重复调用。
/// </summary>
/// <example>解析与定义检查
/// <code>
/// bool ok = "Run".TryParseEnum<AnimationState>(out var state);   // state = AnimationState.Run
/// var value = EnumUtility.ParseEnum<AnimationState>(2);           // 按数值解析，非法返回 default
/// bool has = EnumUtility.IsDefined<AnimationState>(state);
/// </code>
/// </example>
/// <example>名称与描述
/// <code>
/// string name = state.GetEnumName();        // "Run"
/// string desc = state.GetDescription();     // [Description("奔跑")] 时返回 "奔跑"，否则返回名称
/// </code>
/// </example>
/// <example>Flags 位运算
/// <code>
/// var mask = ItemEffects.Fire | ItemEffects.Ice;
/// bool all = mask.HasAllFlags(ItemEffects.Fire | ItemEffects.Ice);  // true
/// bool any = mask.HasAnyFlag(ItemEffects.Wind);                     // false
/// mask = mask.AddFlag(ItemEffects.Wind).RemoveFlag(ItemEffects.Fire);
/// string s = mask.ToFlagString();           // "Ice, Wind"
/// </code>
/// </example>
/// <example>遍历与随机
/// <code>
/// foreach (var v in EnumUtility.GetValues<AnimationState>()) { }
/// var random = EnumUtility.GetRandom<AnimationState>();
/// </code>
/// </example>
/// <example>索引访问与循环切换
/// <code>
/// // 假设 enum Quality { Low, Medium, High }（声明顺序即索引顺序）
/// int idx = Quality.Medium.IndexOf();                       // 1
/// var q1 = EnumUtility.GetValueAt<Quality>(2);              // Quality.High
/// var q2 = EnumUtility.GetValueAt<Quality>(5);              // default（越界安全）
/// int count = EnumUtility.GetCount<Quality>();              // 3
/// var next = Quality.High.GetNext();                        // Quality.Low（循环回第一个）
/// var prev = Quality.Low.GetPrevious();                     // Quality.High（循环回最后一个）
/// </code>
/// </example>
public static class EnumUtility
{
    #region 解析

    /// <summary>
    /// 将字符串解析为枚举（忽略大小写），解析失败返回 false 并输出 default。
    /// <para>优先按名称解析，若字符串是纯数字则按数值解析。</para>
    /// </summary>
    /// <typeparam name="T">目标枚举类型</typeparam>
    /// <param name="value">枚举名称或数值字符串，可为 null/空</param>
    /// <param name="result">解析结果，失败时为 default</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParseEnum<T>(this string value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrEmpty(value)) return false;

        // 纯数字字符串按数值解析，避免名称匹配失败后整串报错
        if (char.IsDigit(value[0]) || value[0] == '-')
        {
            if (Enum.TryParse(value, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                result = parsed;
                return true;
            }
            return false;
        }

        if (Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(typeof(T), result))
        {
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// 按数值解析枚举，未定义的数值返回 default（不抛异常）。
    /// </summary>
    /// <typeparam name="T">目标枚举类型</typeparam>
    /// <param name="value">枚举底层值</param>
    /// <returns>对应的枚举值；数值未定义时返回 default</returns>
    public static T ParseEnum<T>(int value) where T : struct, Enum
    {
        var parsed = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(typeof(T), parsed) ? parsed : default;
    }

    /// <summary>
    /// 判断枚举值是否为该类型中已定义的成员（组合 Flags 值返回 false）。
    /// </summary>
    public static bool IsDefined<T>(this T value) where T : struct, Enum
    {
        return Enum.IsDefined(typeof(T), value);
    }

    #endregion

    #region 名称与描述

    /// <summary>
    /// 获取枚举成员名称（等价于 ToString，语义更明确）。
    /// </summary>
    public static string GetEnumName<T>(this T value) where T : struct, Enum
    {
        return value.ToString();
    }

    /// <summary>
    /// 获取枚举成员的 [Description("...")] 描述文本。
    /// <para>未标注 Description 时回退为成员名称。</para>
    /// </summary>
    public static string GetDescription<T>(this T value) where T : struct, Enum
    {
        string name = value.ToString();
        return GetDescriptionCache<T>().TryGetValue(name, out string desc) ? desc : name;
    }

    #endregion

    #region Flags 位运算

    /// <summary>
    /// 判断是否包含指定所有标志位（全部命中才算 true）。
    /// </summary>
    public static bool HasAllFlags<T>(this T value, T flags) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value);
        ulong f = Convert.ToUInt64(flags);
        return (v & f) == f;
    }

    /// <summary>
    /// 判断是否包含任一指定标志位（命中任意一个即 true）。
    /// </summary>
    public static bool HasAnyFlag<T>(this T value, T flags) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value);
        ulong f = Convert.ToUInt64(flags);
        return (v & f) != 0;
    }

    /// <summary>
    /// 添加标志位。
    /// </summary>
    public static T AddFlag<T>(this T value, T flag) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value) | Convert.ToUInt64(flag);
        return (T)Enum.ToObject(typeof(T), v);
    }

    /// <summary>
    /// 移除标志位。
    /// </summary>
    public static T RemoveFlag<T>(this T value, T flag) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value) & ~Convert.ToUInt64(flag);
        return (T)Enum.ToObject(typeof(T), v);
    }

    /// <summary>
    /// 翻转标志位（有则移除，无则添加）。
    /// </summary>
    public static T ToggleFlag<T>(this T value, T flag) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value) ^ Convert.ToUInt64(flag);
        return (T)Enum.ToObject(typeof(T), v);
    }

    /// <summary>
    /// 将 Flags 组合值格式化为逗号分隔的成员名列表，如 "Ice, Wind"。
    /// <para>无匹配成员的组合值（如 0 或未定义位）原样输出数值字符串。</para>
    /// </summary>
    public static string ToFlagString<T>(this T value) where T : struct, Enum
    {
        ulong v = Convert.ToUInt64(value);
        if (v == 0) return value.ToString();

        var names = new List<string>();
        foreach (var member in GetValues<T>())
        {
            ulong m = Convert.ToUInt64(member);
            if (m != 0 && (v & m) == m) names.Add(member.ToString());
        }
        return names.Count > 0 ? string.Join(", ", names) : value.ToString();
    }

    #endregion

    #region 遍历与随机

    /// <summary>
    /// 获取枚举所有成员（结果有缓存，不会重复分配）。
    /// </summary>
    public static IReadOnlyList<T> GetValues<T>() where T : struct, Enum
    {
        return ValueCache<T>.Values;
    }

    /// <summary>
    /// 获取枚举所有成员名称（结果有缓存，不会重复分配）。
    /// </summary>
    public static IReadOnlyList<string> GetNames<T>() where T : struct, Enum
    {
        return NameCache<T>.Names;
    }

    /// <summary>
    /// 随机获取一个枚举成员（成员数量为 0 时返回 default）。
    /// </summary>
    public static T GetRandom<T>() where T : struct, Enum
    {
        var values = ValueCache<T>.Values;
        if (values.Length == 0) return default;
        return values[UnityEngine.Random.Range(0, values.Length)];
    }

    /// <summary>
    /// 获取枚举成员总数。
    /// </summary>
    public static int GetCount<T>() where T : struct, Enum
    {
        return ValueCache<T>.Values.Length;
    }

    #endregion

    #region 索引访问

    /// <summary>
    /// 获取枚举值在所有成员（按声明顺序）中的索引。
    /// <para>Flags 组合值或未定义的值返回 -1。索引可安全用于数组/存档序号等场景。</para>
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>声明顺序索引；未定义时返回 -1</returns>
    public static int IndexOf<T>(this T value) where T : struct, Enum
    {
        return IndexCache<T>.ValueToIndex.TryGetValue(value.ToString(), out int index) ? index : -1;
    }

    /// <summary>
    /// 获取枚举声明顺序中第 index 个成员。
    /// <para>索引越界返回 default（不抛异常）。与 <see cref="IndexOf{T}(T)"/> 互为逆操作。</para>
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="index">声明顺序索引，从 0 开始</param>
    /// <returns>对应的枚举成员；越界时返回 default</returns>
    public static T GetValueAt<T>(int index) where T : struct, Enum
    {
        var values = ValueCache<T>.Values;
        if (index < 0 || index >= values.Length) return default;
        return values[index];
    }

    /// <summary>
    /// 获取枚举声明顺序中的下一个成员，到末尾后循环回第一个。
    /// <para>常见用途：枚举切换按钮（画质、难度等）。</para>
    /// </summary>
    public static T GetNext<T>(this T value) where T : struct, Enum
    {
        var values = ValueCache<T>.Values;
        if (values.Length == 0) return default;
        int index = value.IndexOf();
        if (index < 0) return values[0];
        return values[(index + 1) % values.Length];
    }

    /// <summary>
    /// 获取枚举声明顺序中的上一个成员，到开头后循环回最后一个。
    /// </summary>
    public static T GetPrevious<T>(this T value) where T : struct, Enum
    {
        var values = ValueCache<T>.Values;
        if (values.Length == 0) return default;
        int index = value.IndexOf();
        if (index < 0) return values[values.Length - 1];
        return values[(index - 1 + values.Length) % values.Length];
    }

    #endregion

    #region 缓存

    // 枚举成员/名称/描述在运行期不变，按枚举类型各缓存一份，避免反射重复开销

    private static class ValueCache<T> where T : struct, Enum
    {
        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
    }

    private static class NameCache<T> where T : struct, Enum
    {
        public static readonly string[] Names = Enum.GetNames(typeof(T));
    }

    private static Dictionary<string, string> GetDescriptionCache<T>() where T : struct, Enum
    {
        if (DescriptionCache<T>.Descriptions != null) return DescriptionCache<T>.Descriptions;

        var dict = new Dictionary<string, string>();
        var type = typeof(T);
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = field.GetCustomAttribute<DescriptionAttribute>();
            if (attr != null) dict[field.Name] = attr.Description;
        }
        DescriptionCache<T>.Descriptions = dict;
        return dict;
    }

    private static class DescriptionCache<T> where T : struct, Enum
    {
        public static Dictionary<string, string> Descriptions;
    }

    // 枚举成员名 -> 声明顺序索引（成员名唯一，无需去重）
    private static class IndexCache<T> where T : struct, Enum
    {
        public static readonly Dictionary<string, int> ValueToIndex = BuildIndex();

        private static Dictionary<string, int> BuildIndex()
        {
            string[] names = Enum.GetNames(typeof(T));
            var dict = new Dictionary<string, int>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                dict[names[i]] = i;
            }
            return dict;
        }
    }

    #endregion
}
