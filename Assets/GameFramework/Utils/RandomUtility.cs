using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 通用随机工具类。
/// 提供带权随机、数组/列表随机抽取、洗牌、随机布尔、随机颜色等高级随机功能。
/// 基于 UnityEngine.Random。
/// </summary>
/// <example>基础随机
/// <code>
/// float f = RandomUtility.Range(0f, 10f);
/// int i = RandomUtility.Range(0, 100);
/// bool b = RandomUtility.RandomBool();     // 50% 概率
/// bool b2 = RandomUtility.RandomBool(0.3f); // 30% 概率为 true
/// </code>
/// </example>
/// <example>集合随机抽取
/// <code>
/// string[] names = { "A", "B", "C" };
/// string pick = RandomUtility.FromArray(names);
/// int index = RandomUtility.RandomIndex(names.Length);
/// T item = RandomUtility.FromList(list);
/// </code>
/// </example>
/// <example>带权随机
/// <code>
/// var weighted = new Dictionary<string, float>
/// {
///     { "普通", 70f },
///     { "稀有", 25f },
///     { "史诗", 5f }
/// };
/// string result = RandomUtility.WeightedRandom(weighted);
/// string defaultResult = RandomUtility.WeightedRandom(weighted, "普通");
/// </code>
/// </example>
/// <example>洗牌与乱序
/// <code>
/// int[] nums = { 1, 2, 3, 4, 5 };
/// RandomUtility.Shuffle(nums);
///
/// List<string> names = new List<string> { "A", "B", "C" };
/// RandomUtility.Shuffle(names);
/// </code>
/// </example>
/// <example>更多随机
/// <code>
/// Color color = RandomUtility.RandomColor();
/// Vector3 pos = RandomUtility.InsideUnitCircle();
/// Vector3 pos3d = RandomUtility.InsideUnitSphere();
/// float angle = RandomUtility.RandomAngle();
/// string guid = RandomUtility.GenerateId(8);
/// </code>
/// </example>
public static class RandomUtility
{
    #region 基础随机

    /// <summary>
    /// 返回 [minInclusive, maxInclusive] 范围内的随机整数。
    /// </summary>
    public static int Range(int minInclusive, int maxInclusive)
    {
        return Random.Range(minInclusive, maxInclusive + 1);
    }

    /// <summary>
    /// 返回 [minInclusive, maxInclusive) 范围内的随机浮点数。
    /// </summary>
    public static float Range(float minInclusive, float maxInclusive)
    {
        return Random.Range(minInclusive, maxInclusive);
    }

    /// <summary>
    /// 返回 0~1 之间的随机浮点数。
    /// </summary>
    public static float Value()
    {
        return Random.value;
    }

    /// <summary>
    /// 返回随机布尔值，true 概率为 probability（0~1）。
    /// </summary>
    public static bool RandomBool(float probability = 0.5f)
    {
        return Random.value < Mathf.Clamp01(probability);
    }

    /// <summary>
    /// 返回随机符号：1 或 -1。
    /// </summary>
    public static float RandomSign()
    {
        return Random.value < 0.5f ? 1f : -1f;
    }

    #endregion

    #region 数组/列表随机

    /// <summary>
    /// 从数组中随机抽取一个元素。
    /// </summary>
    public static T FromArray<T>(T[] array)
    {
        if (array == null || array.Length == 0)
            throw new ArgumentException("数组为空或长度为零");
        return array[Random.Range(0, array.Length)];
    }

    /// <summary>
    /// 从数组中随机抽取一个元素，可为空返回 default。
    /// </summary>
    public static T FromArraySafe<T>(T[] array)
    {
        if (array == null || array.Length == 0) return default;
        return array[Random.Range(0, array.Length)];
    }

    /// <summary>
    /// 从列表中随机抽取一个元素。
    /// </summary>
    public static T FromList<T>(IList<T> list)
    {
        if (list == null || list.Count == 0)
            throw new ArgumentException("列表为空或长度为零");
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 从列表中随机抽取一个元素，可为空返回 default。
    /// </summary>
    public static T FromListSafe<T>(IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 获取随机索引 [0, count)。
    /// </summary>
    public static int RandomIndex(int count)
    {
        if (count <= 0) return 0;
        return Random.Range(0, count);
    }

    /// <summary>
    /// 从数组中随机抽取 N 个不重复的元素。
    /// </summary>
    public static T[] Sample<T>(T[] array, int count)
    {
        if (array == null) return Array.Empty<T>();
        if (count <= 0) return Array.Empty<T>();
        if (count >= array.Length) return (T[])array.Clone();

        List<int> indices = new List<int>(array.Length);
        for (int i = 0; i < array.Length; i++) indices.Add(i);

        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, indices.Count);
            result[i] = array[indices[idx]];
            indices.RemoveAt(idx);
        }
        return result;
    }

    #endregion

    #region 带权随机

    /// <summary>
    /// 根据权重字典随机抽取一个键。权重值越大，被抽到的概率越高。
    /// </summary>
    /// <param name="weightedItems">键值对：键 = 选项，值 = 权重（必须 > 0）</param>
    /// <returns>抽中的键</returns>
    public static T WeightedRandom<T>(Dictionary<T, float> weightedItems)
    {
        if (weightedItems == null || weightedItems.Count == 0)
            throw new ArgumentException("权重字典为空");

        float totalWeight = 0f;
        foreach (var kv in weightedItems)
        {
            if (kv.Value < 0f)
                throw new ArgumentException($"权重不能为负数: {kv.Key} = {kv.Value}");
            totalWeight += kv.Value;
        }

        if (totalWeight <= 0f)
            throw new ArgumentException("总权重必须大于0");

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var kv in weightedItems)
        {
            cumulative += kv.Value;
            if (randomValue <= cumulative)
                return kv.Key;
        }

        // 容错：返回最后一个
        foreach (var kv in weightedItems) return kv.Key;
        return default;
    }

    /// <summary>
    /// 根据权重字典随机抽取一个键。若总权重为0或字典为空，返回默认值。
    /// </summary>
    public static T WeightedRandomSafe<T>(Dictionary<T, float> weightedItems, T defaultValue = default)
    {
        if (weightedItems == null || weightedItems.Count == 0) return defaultValue;

        float totalWeight = 0f;
        foreach (var kv in weightedItems)
        {
            if (kv.Value < 0f) return defaultValue;
            totalWeight += kv.Value;
        }

        if (totalWeight <= 0f) return defaultValue;

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var kv in weightedItems)
        {
            cumulative += kv.Value;
            if (randomValue <= cumulative)
                return kv.Key;
        }

        foreach (var kv in weightedItems) return kv.Key;
        return defaultValue;
    }

    /// <summary>
    /// 根据权重数组随机抽取一个索引。
    /// </summary>
    /// <param name="weights">权重数组</param>
    /// <returns>抽中的索引</returns>
    public static int WeightedRandomIndex(float[] weights)
    {
        if (weights == null || weights.Length == 0)
            throw new ArgumentException("权重数组为空");

        float totalWeight = 0f;
        foreach (float w in weights)
        {
            if (w < 0f) throw new ArgumentException($"权重不能为负数: {w}");
            totalWeight += w;
        }

        if (totalWeight <= 0f)
            throw new ArgumentException("总权重必须大于0");

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (randomValue <= cumulative)
                return i;
        }

        return weights.Length - 1;
    }

    #endregion

    #region 洗牌

        public static void Shuffle<T>(T[] array)
    {
        if (array == null || array.Length <= 1) return;

        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    public static void Shuffle<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1) return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    #endregion

    #region 颜色随机

    public static Color RandomColor(bool randomAlpha = false)
    {
        return new Color(Random.value, Random.value, Random.value, randomAlpha ? Random.value : 1f);
    }

    public static Color RandomBrightColor()
    {
        return Color.HSVToRGB(Random.value, 0.7f + Random.value * 0.3f, 1f);
    }

    #endregion

    #region 位置随机

    public static Vector2 InsideUnitCircle() => Random.insideUnitCircle;
    public static Vector3 InsideUnitSphere() => Random.insideUnitSphere;
    public static Vector3 OnUnitSphere() => Random.onUnitSphere;

    public static Vector2 PointInRect(Rect rect)
    {
        return new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
    }

    public static float RandomAngle() => Random.Range(0f, 360f);

    #endregion

    #region ID生成

    private const string IdChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string GenerateId(int length = 8)
    {
        if (length <= 0) return string.Empty;
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = IdChars[Random.Range(0, IdChars.Length)];
        return new string(result);
    }

    public static string GenerateNumericId(int length = 6)
    {
        if (length <= 0) return string.Empty;
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = (char)('0' + Random.Range(0, 10));
        return new string(result);
    }

    #endregion
}