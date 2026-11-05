using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 通用 JSON 工具类，基于 Newtonsoft.Json（Json.NET）。
/// <para>相较 Unity JsonUtility 的优势：</para>
/// <para>  · 无需 [Serializable] 标记，任意对象均可序列化</para>
/// <para>  · 原生支持 Dictionary、List、Array 等集合类型</para>
/// <para>  · 内置 Formatting.Indented 美化输出（零性能开销的正反向查找算法）</para>
/// <para>  · LINQ to JSON：JObject / JArray / JToken 动态操作</para>
/// <para>  · 丰富的配置项：驼峰命名、忽略 null、引用处理等</para>
/// <para>使用示例：</para>
/// <code>
/// // ===== 1. 对象 ↔ JSON =====
/// string json = JsonUtil.ToJson(playerData);
/// PlayerData data = JsonUtil.FromJson<PlayerData>(json);
///
/// // ===== 2. Dictionary / List 原生支持 =====
/// var dict = new Dictionary<string, int> { { "gold", 100 } };
/// string dictJson = JsonUtil.ToJson(dict);
/// var loadedDict = JsonUtil.FromJson<Dictionary<string, int>>(dictJson);
///
/// // ===== 3. 文件读写 =====
/// JsonUtil.WriteToFile(data, savePath, pretty: true);
/// PlayerData loaded = JsonUtil.ReadFromFile<PlayerData>(savePath);
///
/// // ===== 4. LINQ to JSON =====
/// JObject obj = JsonUtil.ParseObject(json);
/// int gold = obj["player"].Value<int>("gold");
/// obj["player"]["level"] = 10;
/// string modified = JsonUtil.ToJson(obj);
///
/// // ===== 5. 驼峰命名 =====
/// JsonUtil.ToJson(data, JsonUtil.CamelCaseSettings);
/// </code>
/// </summary>
public static class JsonUtil
{
    private const string Tag = "[JsonUtil]";

    #region 默认序列化配置

    /// <summary>
    /// 默认配置：紧凑输出，忽略 null 值，不处理循环引用。
    /// </summary>
    public static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Converters = { new BindablePropertyJsonConverter() }
    };

    /// <summary>
    /// 美化输出配置：带缩进，忽略 null 值。
    /// </summary>
    public static readonly JsonSerializerSettings PrettySettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Converters = { new BindablePropertyJsonConverter() }
    };

    /// <summary>
    /// 驼峰命名配置：输出属性名采用 camelCase（如 playerName），适合对接 Web API。
    /// </summary>
    public static readonly JsonSerializerSettings CamelCaseSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        Converters = { new BindablePropertyJsonConverter() }
    };

    /// <summary>
    /// 保留所有字段（包括 null）。适合需要完整结构的场景（如配置文件模板）。
    /// </summary>
    public static readonly JsonSerializerSettings PreserveAllSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Converters = { new BindablePropertyJsonConverter() }
    };

    #endregion

    #region 对象 ↔ JSON

    /// <summary>
    /// 将对象序列化为 JSON 字符串。
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="settings">序列化配置，为 null 时使用 DefaultSettings</param>
    /// <returns>JSON 字符串，失败返回 null</returns>
    public static string ToJson(object obj, JsonSerializerSettings settings = null)
    {
        if (obj == null)
        {
            Debug.LogWarning($"{Tag} ToJson: 对象为 null");
            return null;
        }

        try
        {
            return JsonConvert.SerializeObject(obj, settings ?? DefaultSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ToJson 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型的对象。
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="settings">序列化配置，为 null 时使用 DefaultSettings</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public static T FromJson<T>(string json, JsonSerializerSettings settings = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"{Tag} FromJson: JSON 字符串为空");
            return default;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} FromJson 失败: {e.Message}");
            return default;
        }
    }

    /// <summary>
    /// 尝试将 JSON 字符串反序列化为指定类型的对象，通过返回值区分「成功」与「失败」。
    /// <para>与 <see cref="FromJson<T>"/> 的区别：解析失败返回 false，可安全用于存档加载，
    /// 避免把 default(T) 误判为成功结果。</para>
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">解析结果，失败为 default(T)</param>
    /// <param name="settings">序列化配置，为 null 时使用 DefaultSettings</param>
    /// <returns>解析成功返回 true，JSON 为空或解析异常返回 false</returns>
    public static bool TryFromJson<T>(string json, out T result, JsonSerializerSettings settings = null)
    {
        result = default;

        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            result = JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} TryFromJson 失败: {e.Message}");
            result = default;
            return false;
        }
    }

    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型的对象（非泛型版本）。
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="type">目标类型</param>
    /// <param name="settings">序列化配置</param>
    /// <returns>反序列化后的对象，失败返回 null</returns>
    public static object FromJson(string json, Type type, JsonSerializerSettings settings = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"{Tag} FromJson: JSON 字符串为空");
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject(json, type, settings ?? DefaultSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} FromJson 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region LINQ to JSON（JObject / JArray / JToken）

    /// <summary>
    /// 将 JSON 字符串解析为 JObject（JSON 对象）。
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JObject 实例，失败返回 null</returns>
    public static JObject ParseObject(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"{Tag} ParseObject: JSON 字符串为空");
            return null;
        }

        try
        {
            return JObject.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ParseObject 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JArray（JSON 数组）。
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JArray 实例，失败返回 null</returns>
    public static JArray ParseArray(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"{Tag} ParseArray: JSON 字符串为空");
            return null;
        }

        try
        {
            return JArray.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ParseArray 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JToken（自动识别对象或数组）。
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JToken 实例（可能是 JObject 或 JArray），失败返回 null</returns>
    public static JToken ParseToken(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"{Tag} ParseToken: JSON 字符串为空");
            return null;
        }

        try
        {
            return JToken.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ParseToken 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从 JObject 中安全获取指定路径的值。
    /// <para>支持点号分隔的路径，如 "player.inventory.items[0].id"</para>
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="jObject">JObject 实例</param>
    /// <param name="path">属性路径</param>
    /// <returns>路径对应的值，失败返回 default(T)</returns>
    public static T GetValue<T>(JObject jObject, string path)
    {
        if (jObject == null || string.IsNullOrEmpty(path))
            return default;

        try
        {
            JToken token = jObject.SelectToken(path);
            return token != null ? token.Value<T>() : default;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} GetValue 失败 [{path}]: {e.Message}");
            return default;
        }
    }

    /// <summary>
    /// 尝试从 JSON 中按 JSONPath 查询 JToken。
    /// </summary>
    /// <param name="jToken">根 JToken</param>
    /// <param name="jsonPath">JSONPath 表达式，如 "$.items[?(@.price > 100)]"</param>
    /// <returns>匹配的 JToken 集合，失败返回空集合</returns>
    public static IEnumerable<JToken> SelectTokens(JToken jToken, string jsonPath)
    {
        if (jToken == null || string.IsNullOrEmpty(jsonPath))
            return Array.Empty<JToken>();

        try
        {
            return jToken.SelectTokens(jsonPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} SelectTokens 失败 [{jsonPath}]: {e.Message}");
            return Array.Empty<JToken>();
        }
    }

    #endregion

    #region 文件读写

    /// <summary>
    /// 将对象序列化为 JSON 并写入文件（UTF-8）。
    /// </summary>
    /// <param name="obj">要保存的对象</param>
    /// <param name="filePath">文件完整路径</param>
    /// <param name="pretty">是否格式化输出，默认 false</param>
    /// <returns>是否成功写入</returns>
    public static bool WriteToFile(object obj, string filePath, bool pretty = false)
    {
        if (obj == null)
        {
            Debug.LogError($"{Tag} WriteToFile: 对象为 null");
            return false;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError($"{Tag} WriteToFile: 文件路径为空");
            return false;
        }

        try
        {
            var settings = pretty ? PrettySettings : DefaultSettings;
            string json = JsonConvert.SerializeObject(obj, settings);
            if (json == null) return false;

            return FileUtility.WriteAllTextAtomic(filePath, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} WriteToFile 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件中读取 JSON 并反序列化为指定类型对象。
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePath">文件完整路径</param>
    /// <param name="settings">序列化配置</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public static T ReadFromFile<T>(string filePath, JsonSerializerSettings settings = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError($"{Tag} ReadFromFile: 文件路径为空");
            return default;
        }

        try
        {
            string json = FileUtility.ReadAllTextSafe(filePath);
            if (json == null) return default;
            return FromJson<T>(json, settings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ReadFromFile 失败 [{filePath}]: {e.Message}");
            return default;
        }
    }

    #endregion

    #region Resources 加载

    /// <summary>
    /// 从 Resources 目录加载 .json 文件（不带扩展名），并反序列化。
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="resourcePath">Resources 下路径，不带扩展名</param>
    /// <param name="settings">序列化配置</param>
    /// <returns>反序列化后的对象，失败返回 default(T)</returns>
    public static T LoadFromResources<T>(string resourcePath, JsonSerializerSettings settings = null)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError($"{Tag} LoadFromResources: 资源路径为空");
            return default;
        }

        try
        {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                Debug.LogError($"{Tag} LoadFromResources: 资源不存在 [{resourcePath}]");
                return default;
            }
            return FromJson<T>(textAsset.text, settings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} LoadFromResources 失败 [{resourcePath}]: {e.Message}");
            return default;
        }
    }

    #endregion

    #region JSON 合并

    /// <summary>
    /// 将 source 对象合并到 target JObject 中（浅合并，同名字段覆盖）。
    /// <para>常用于：加载默认配置后，再用用户配置覆盖部分字段。</para>
    /// </summary>
    /// <param name="target">目标 JObject</param>
    /// <param name="source">源对象</param>
    /// <returns>合并后的 target（同一引用）</returns>
    public static JObject Merge(JObject target, object source)
    {
        if (target == null)
        {
            Debug.LogError($"{Tag} Merge: target 为 null");
            return null;
        }

        if (source == null) return target;

        try
        {
            JObject sourceObj = source as JObject ?? JObject.FromObject(source);
            target.Merge(sourceObj, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore,
            });
            return target;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} Merge 失败: {e.Message}");
            return target;
        }
    }

    /// <summary>
    /// 合并两个 JSON 字符串（source 的字段覆盖 target 的同名字段）。
    /// </summary>
    /// <param name="targetJson">目标 JSON 字符串</param>
    /// <param name="sourceJson">源 JSON 字符串</param>
    /// <returns>合并后的 JSON 字符串，失败返回 null</returns>
    public static string MergeJson(string targetJson, string sourceJson)
    {
        if (string.IsNullOrEmpty(targetJson)) return sourceJson;
        if (string.IsNullOrEmpty(sourceJson)) return targetJson;

        try
        {
            JObject target = JObject.Parse(targetJson);
            JObject source = JObject.Parse(sourceJson);
            target.Merge(source, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore,
            });
            return target.ToString(Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} MergeJson 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region 验证

    /// <summary>
    /// 检查字符串是否为合法 JSON 格式。
    /// </summary>
    /// <param name="json">待验证字符串</param>
    /// <returns>是否合法</returns>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            JToken.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查字符串是否为合法 JSON 对象格式（以 '{' 开头的对象）。
    /// </summary>
    /// <param name="json">待验证字符串</param>
    /// <returns>是否为合法 JSON 对象</returns>
    public static bool IsValidJsonObject(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            JObject.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查字符串是否为合法 JSON 数组格式（以 '[' 开头的数组）。
    /// </summary>
    /// <param name="json">待验证字符串</param>
    /// <returns>是否为合法 JSON 数组</returns>
    public static bool IsValidJsonArray(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            JArray.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 实用方法

    /// <summary>
    /// 通过 JSON 序列化/反序列化实现对象的深拷贝。
    /// <para>注意：仅拷贝可被 Newtonsoft 序列化的成员，循环引用会被忽略。</para>
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <returns>深拷贝后的新对象，失败返回 default(T)</returns>
    public static T DeepCopy<T>(T source)
    {
        if (source == null) return default;
        string json = ToJson(source);
        if (json == null) return default;
        return FromJson<T>(json);
    }

    /// <summary>
    /// 将 UTF-8 字节数组反序列化为指定类型对象。
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="bytes">UTF-8 编码的 JSON 字节数组</param>
    /// <param name="settings">序列化配置</param>
    /// <returns>反序列化后的对象</returns>
    public static T FromBytes<T>(byte[] bytes, JsonSerializerSettings settings = null)
    {
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogWarning($"{Tag} FromBytes: 字节数组为空");
            return default;
        }

        try
        {
            string json = Encoding.UTF8.GetString(bytes);
            return FromJson<T>(json, settings);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} FromBytes 失败: {e.Message}");
            return default;
        }
    }

    /// <summary>
    /// 将对象序列化为 UTF-8 字节数组。
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="settings">序列化配置</param>
    /// <returns>UTF-8 编码的 JSON 字节数组</returns>
    public static byte[] ToBytes(object obj, JsonSerializerSettings settings = null)
    {
        if (obj == null) return null;
        string json = ToJson(obj, settings);
        if (json == null) return null;
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// 将 JSON 字符串中的指定字段（支持点号路径）的值替换为新值。
    /// <para>示例：JsonUtil.SetValue(json, "player.gold", 999)</para>
    /// </summary>
    /// <param name="json">原始 JSON 字符串</param>
    /// <param name="path">字段路径（点号分隔）</param>
    /// <param name="value">新值</param>
    /// <returns>修改后的 JSON 字符串，失败返回 null</returns>
    public static string SetValue(string json, string path, object value)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(path))
            return json;

        try
        {
            JObject obj = JObject.Parse(json);
            JToken token = obj.SelectToken(path);
            if (token != null)
            {
                token.Replace(JToken.FromObject(value));
                return obj.ToString(Formatting.None);
            }
            Debug.LogWarning($"{Tag} SetValue: 路径 [{path}] 不存在");
            return json;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} SetValue 失败 [{path}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将对象转换为 JObject，方便动态添加/修改字段。
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <returns>JObject 实例，失败返回 null</returns>
    public static JObject ToJObject(object obj)
    {
        if (obj == null) return null;

        try
        {
            return obj as JObject ?? JObject.FromObject(obj);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ToJObject 失败: {e.Message}");
            return null;
        }
    }

    #endregion
}