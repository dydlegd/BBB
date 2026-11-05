using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 通用集合扩展工具类。
/// 提供列表交换、洗牌、随机抽取、安全访问、格式化输出等常用集合操作。
/// </summary>
/// <example>列表操作
/// <code>
/// List<int> list = new List<int> { 1, 2, 3, 4 };
/// list.Swap(0, 3);                           // { 4, 2, 3, 1 }
/// ListUtility.Shuffle(list);
/// int item = list.GetRandom();
/// </code>
/// </example>
/// <example>安全访问
/// <code>
/// int[] arr = { 10, 20, 30 };
/// int val = arr.TryGet(5, -1);               // -1（索引越界返回默认值）
/// int first = arr.FirstOrDefault();           // 10
/// int last = arr.LastOrDefault();             // 30
/// </code>
/// </example>
/// <example>格式化
/// <code>
/// string s = list.ToCommaString();            // "1, 2, 3, 4"
/// string s2 = list.JoinToString(" | ");       // "1 | 2 | 3 | 4"
/// </code>
/// </example>
public static class ListUtility
{
    #region 交换

    /// <summary>
    /// 交换列表中两个位置的元素。
    /// </summary>
    public static void Swap<T>(this IList<T> list, int indexA, int indexB)
    {
        if (list == null) return;
        if (indexA < 0 || indexA >= list.Count || indexB < 0 || indexB >= list.Count) return;
        T temp = list[indexA];
        list[indexA] = list[indexB];
        list[indexB] = temp;
    }

    /// <summary>
    /// 交换数组中两个位置的元素。
    /// </summary>
    public static void Swap<T>(this T[] array, int indexA, int indexB)
    {
        if (array == null) return;
        if (indexA < 0 || indexA >= array.Length || indexB < 0 || indexB >= array.Length) return;
        T temp = array[indexA];
        array[indexA] = array[indexB];
        array[indexB] = temp;
    }

    #endregion

    #region 洗牌

    /// <summary>
    /// Fisher-Yates 洗牌算法，原地打乱列表顺序。
    /// </summary>
    public static void Shuffle<T>(this IList<T> list)
    {
        if (list == null || list.Count <= 1) return;

        System.Random rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    /// <summary>
    /// Fisher-Yates 洗牌算法，原地打乱数组顺序。
    /// </summary>
    public static void Shuffle<T>(this T[] array)
    {
        if (array == null || array.Length <= 1) return;

        System.Random rng = new System.Random();
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    #endregion

    #region 随机抽取

    /// <summary>
    /// 从列表中随机获取一个元素。
    /// </summary>
    public static T GetRandom<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0)
            throw new ArgumentException("列表为空");
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 从列表中随机获取一个元素，为空返回 default。
    /// </summary>
    public static T GetRandomSafe<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 从数组中随机获取一个元素。
    /// </summary>
    public static T GetRandom<T>(this T[] array)
    {
        if (array == null || array.Length == 0)
            throw new ArgumentException("数组为空");
        return array[UnityEngine.Random.Range(0, array.Length)];
    }

    /// <summary>
    /// 从数组中随机获取一个元素，为空返回 default。
    /// </summary>
    public static T GetRandomSafe<T>(this T[] array)
    {
        if (array == null || array.Length == 0) return default;
        return array[UnityEngine.Random.Range(0, array.Length)];
    }

    #endregion

    #region 安全访问

    /// <summary>
    /// 安全获取列表元素，索引越界返回默认值。
    /// </summary>
    public static T TryGet<T>(this IList<T> list, int index, T defaultValue = default)
    {
        if (list == null || index < 0 || index >= list.Count) return defaultValue;
        return list[index];
    }

    /// <summary>
    /// 安全获取数组元素，索引越界返回默认值。
    /// </summary>
    public static T TryGet<T>(this T[] array, int index, T defaultValue = default)
    {
        if (array == null || index < 0 || index >= array.Length) return defaultValue;
        return array[index];
    }

    /// <summary>
    /// 获取列表第一个元素，为空返回默认值。
    /// </summary>
    public static T FirstOrDefault<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[0];
    }

    /// <summary>
    /// 获取列表最后一个元素，为空返回默认值。
    /// </summary>
    public static T LastOrDefault<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[list.Count - 1];
    }

    /// <summary>
    /// 获取数组第一个元素，为空返回默认值。
    /// </summary>
    public static T FirstOrDefault<T>(this T[] array)
    {
        if (array == null || array.Length == 0) return default;
        return array[0];
    }

    /// <summary>
    /// 获取数组最后一个元素，为空返回默认值。
    /// </summary>
    public static T LastOrDefault<T>(this T[] array)
    {
        if (array == null || array.Length == 0) return default;
        return array[array.Length - 1];
    }

    /// <summary>
    /// 安全移除列表元素（不存在时不报错）。
    /// </summary>
    public static bool RemoveSafe<T>(this IList<T> list, T item)
    {
        if (list == null) return false;
        return list.Remove(item);
    }

    /// <summary>
    /// 安全移除指定索引的元素（越界时不报错）。
    /// </summary>
    public static bool RemoveAtSafe<T>(this IList<T> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count) return false;
        list.RemoveAt(index);
        return true;
    }

    #endregion

    #region 格式化输出

    /// <summary>
    /// 将列表元素用逗号分隔连接为字符串。
    /// </summary>
    public static string ToCommaString<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0) return string.Empty;
        return JoinToString(list, ", ");
    }

    /// <summary>
    /// 将数组元素用逗号分隔连接为字符串。
    /// </summary>
    public static string ToCommaString<T>(this T[] array)
    {
        if (array == null || array.Length == 0) return string.Empty;
        return JoinToString(array, ", ");
    }

    /// <summary>
    /// 将列表元素用指定分隔符连接为字符串。
    /// </summary>
    public static string JoinToString<T>(this IList<T> list, string separator)
    {
        if (list == null || list.Count == 0) return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(separator);
            sb.Append(list[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 将数组元素用指定分隔符连接为字符串。
    /// </summary>
    public static string JoinToString<T>(this T[] array, string separator)
    {
        if (array == null || array.Length == 0) return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < array.Length; i++)
        {
            if (i > 0) sb.Append(separator);
            sb.Append(array[i]);
        }
        return sb.ToString();
    }

    #endregion

    #region 操作

    /// <summary>
    /// 判断列表是否为 null 或空。
    /// </summary>
    public static bool IsNullOrEmpty<T>(this IList<T> list)
    {
        return list == null || list.Count == 0;
    }

    /// <summary>
    /// 判断数组是否为 null 或空。
    /// </summary>
    public static bool IsNullOrEmpty<T>(this T[] array)
    {
        return array == null || array.Length == 0;
    }

    /// <summary>
    /// 判断列表是否非空（等效于 list != null && list.Count > 0）。
    /// </summary>
    public static bool IsNotNullOrEmpty<T>(this IList<T> list)
    {
        return list != null && list.Count > 0;
    }

    /// <summary>
    /// 判断数组是否非空（等效于 array != null && array.Length > 0）。
    /// </summary>
    public static bool IsNotNullOrEmpty<T>(this T[] array)
    {
        return array != null && array.Length > 0;
    }

    /// <summary>
    /// 向列表添加多个元素。
    /// </summary>
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        if (list == null || items == null) return;
        foreach (T item in items)
        {
            list.Add(item);
        }
    }

    /// <summary>
    /// 将列表元素复制到一个新数组中。
    /// </summary>
    public static T[] ToArraySafe<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0) return Array.Empty<T>();
        T[] result = new T[list.Count];
        list.CopyTo(result, 0);
        return result;
    }

    #endregion
}