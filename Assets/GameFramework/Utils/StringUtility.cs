using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 通用字符串工具类。
/// 提供空值判断、截断、Base64编码、格式化、数字转换、富文本标签移除等常用字符串操作。
/// 所有方法均为扩展方法，可直接在 string 实例上调用。
/// </summary>
/// <example>空值判断
/// <code>
/// string name = null;
/// bool isEmpty = name.IsNullOrEmpty();                    // true
/// bool isWhitespace = "  ".IsNullOrWhiteSpace();          // true
/// string safe = name.OrDefault("未知");                    // "未知"
/// </code>
/// </example>
/// <example>截断与格式化
/// <code>
/// string s = "Hello World".Truncate(5);                   // "Hello"
/// string s2 = "Hello World".Truncate(5, "...");           // "He..."
/// string s3 = "hello_world".ToTitleCase();                 // "Hello_world"
/// string s4 = StringUtility.FormatBytes(2048);             // "2.00 KB"
/// </code>
/// </example>
/// <example>编码与加密
/// <code>
/// string base64 = "Hello".ToBase64();                     // "SGVsbG8="
/// string plain = base64.FromBase64();                     // "Hello"
/// string bytesStr = StringUtility.ToBase64(bytes);
/// byte[] bytes = StringUtility.FromBase64ToBytes(base64);
/// </code>
/// </example>
/// <example>字符串处理
/// <code>
/// string joined = StringUtility.Join(", ", "A", "B", "C"); // "A, B, C"
/// string[] parts = "a,b,c".SplitAndTrim(',');              // ["a", "b", "c"]
/// string clean = "<color=red>text</color>".StripRichText(); // "text"
/// bool isEmail = "test@qq.com".IsValidEmail();             // true
/// </code>
/// </example>
public static class StringUtility
{
    #region 空值判断

    /// <summary>
    /// 判断字符串是否为 null 或空。
    /// </summary>
    public static bool IsNullOrEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// 判断字符串是否为 null、空或仅包含空白字符。
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// 判断字符串是否非空（等效于 !string.IsNullOrEmpty）。
    /// </summary>
    public static bool IsNotNullOrEmpty(this string value)
    {
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// 判断字符串是否非空且非空白（等效于 !string.IsNullOrWhiteSpace）。
    /// </summary>
    public static bool IsNotNullOrWhiteSpace(this string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// 返回字符串本身，若为 null 或空则返回默认值。
    /// </summary>
    public static string OrDefault(this string value, string defaultValue)
    {
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// 若字符串为 null 则返回空字符串，否则返回自身。
    /// </summary>
    public static string OrEmpty(this string value)
    {
        return value ?? string.Empty;
    }

    #endregion

    #region 截断与填充

    /// <summary>
    /// 截断字符串到指定长度，超长部分直接丢弃。
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return value.Substring(0, maxLength);
    }

    /// <summary>
    /// 截断字符串到指定长度，超长部分用 suffix 替换。
    /// </summary>
    public static string Truncate(this string value, int maxLength, string suffix)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        if (string.IsNullOrEmpty(suffix)) return value.Substring(0, maxLength);
        if (maxLength <= suffix.Length) return suffix.Substring(0, maxLength);
        return value.Substring(0, maxLength - suffix.Length) + suffix;
    }

    /// <summary>
    /// 将字符串重复指定次数。
    /// </summary>
    public static string Repeat(this string value, int count)
    {
        if (string.IsNullOrEmpty(value) || count <= 0) return string.Empty;
        if (count == 1) return value;

        StringBuilder sb = new StringBuilder(value.Length * count);
        for (int i = 0; i < count; i++)
            sb.Append(value);
        return sb.ToString();
    }

    /// <summary>
    /// 确保字符串以指定前缀开头。
    /// </summary>
    public static string EnsureStartsWith(this string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return prefix ?? string.Empty;
        if (string.IsNullOrEmpty(prefix)) return value;
        return value.StartsWith(prefix) ? value : prefix + value;
    }

    /// <summary>
    /// 确保字符串以指定后缀结尾。
    /// </summary>
    public static string EnsureEndsWith(this string value, string suffix)
    {
        if (string.IsNullOrEmpty(value)) return suffix ?? string.Empty;
        if (string.IsNullOrEmpty(suffix)) return value;
        return value.EndsWith(suffix) ? value : value + suffix;
    }

    #endregion

    #region 大小写转换

    /// <summary>
    /// 将字符串首字母大写。
    /// </summary>
    public static string ToFirstUpper(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return value.ToUpper();
        return char.ToUpper(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// 将字符串首字母小写。
    /// </summary>
    public static string ToFirstLower(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return value.ToLower();
        return char.ToLower(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// 将下划线/连字符分隔转换为驼峰命名（camelCase）。
    /// </summary>
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string[] parts = value.Split('_', '-');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            parts[i] = i == 0 ? parts[i].ToFirstLower() : parts[i].ToFirstUpper();
        }
        return string.Join(string.Empty, parts);
    }

    /// <summary>
    /// 将下划线/连字符分隔转换为帕斯卡命名（PascalCase）。
    /// </summary>
    public static string ToPascalCase(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string[] parts = value.Split('_', '-');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            parts[i] = parts[i].ToFirstUpper();
        }
        return string.Join(string.Empty, parts);
    }

    #endregion

    #region 分割与合并

    /// <summary>
    /// 按分隔符分割字符串，并自动去除每个元素的空白和空条目。
    /// </summary>
    public static string[] SplitAndTrim(this string value, char separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<string>();

        string[] parts = value.Split(new[] { separator }, options);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();
        return parts;
    }

    /// <summary>
    /// 按分隔符分割字符串，并自动去除每个元素的空白和空条目。
    /// </summary>
    public static string[] SplitAndTrim(this string value, string separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<string>();

        string[] parts = value.Split(new[] { separator }, options);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();
        return parts;
    }

    /// <summary>
    /// 将多个字符串用分隔符连接。
    /// </summary>
    public static string Join(string separator, params string[] values)
    {
        if (values == null || values.Length == 0) return string.Empty;
        return string.Join(separator, values);
    }

    /// <summary>
    /// 将集合元素用分隔符连接。
    /// </summary>
    public static string Join<T>(string separator, IEnumerable<T> values)
    {
        if (values == null) return string.Empty;
        return string.Join(separator, values);
    }

    #endregion

    #region Base64 编码

    /// <summary>
    /// 将字符串编码为 Base64 字符串。
    /// </summary>
    public static string ToBase64(this string value, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        encoding = encoding ?? Encoding.UTF8;
        return Convert.ToBase64String(encoding.GetBytes(value));
    }

    /// <summary>
    /// 将 Base64 字符串解码为原字符串。
    /// </summary>
    public static string FromBase64(this string base64, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;
        try
        {
            encoding = encoding ?? Encoding.UTF8;
            byte[] bytes = Convert.FromBase64String(base64);
            return encoding.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 将字节数组编码为 Base64 字符串。
    /// </summary>
    public static string ToBase64(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 将 Base64 字符串解码为字节数组。
    /// </summary>
    public static byte[] FromBase64ToBytes(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return Array.Empty<byte>();
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    #endregion

    #region 格式化显示

    /// <summary>
    /// 将字节数格式化为人类可读的大小（KB、MB、GB等）。
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        int unitIndex = 0;
        double size = bytes;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
    }

    /// <summary>
    /// 将数字格式化为带千分位分隔符的字符串（如 1,234,567）。
    /// </summary>
    public static string FormatNumber(long number)
    {
        return number.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 将数字格式化为中文大数字（如 1.2万、3.4亿）。
    /// </summary>
    public static string FormatChineseNumber(long number)
    {
        if (number < 0) return "-" + FormatChineseNumber(-number);
        if (number < 10000) return number.ToString();

        if (number < 100000000)
            return (number / 10000.0).ToString("F1") + "万";

        return (number / 100000000.0).ToString("F1") + "亿";
    }

    #endregion

    #region 数字与字符串互转

    /// <summary>
    /// 安全地将字符串转换为 int，失败返回 defaultValue。
    /// </summary>
    public static int ToInt(this string value, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
    }

    /// <summary>
    /// 安全地将字符串转换为 long，失败返回 defaultValue。
    /// </summary>
    public static long ToLong(this string value, long defaultValue = 0)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : defaultValue;
    }

    /// <summary>
    /// 安全地将字符串转换为 float，失败返回 defaultValue。
    /// </summary>
    public static float ToFloat(this string value, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : defaultValue;
    }

    /// <summary>
    /// 安全地将字符串转换为 double，失败返回 defaultValue。
    /// </summary>
    public static double ToDouble(this string value, double defaultValue = 0.0)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : defaultValue;
    }

    /// <summary>
    /// 安全地将字符串转换为 bool，失败返回 defaultValue。
    /// 支持 "true"/"false"、"1"/"0"、"yes"/"no"。
    /// </summary>
    public static bool ToBool(this string value, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;

        string lower = value.Trim().ToLower();
        if (lower == "true" || lower == "1" || lower == "yes") return true;
        if (lower == "false" || lower == "0" || lower == "no") return false;

        return defaultValue;
    }

    #endregion

    #region 富文本与特殊字符

    /// <summary>
    /// 移除 UGUI 富文本标签（如 <color>、<size>、<b> 等）。
    /// </summary>
    public static string StripRichText(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value, @"<[^>]*>", string.Empty);
    }

    /// <summary>
    /// 转义 HTML 特殊字符（< > &amp;）。
    /// </summary>
    public static string EscapeHtml(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("&", "&amp;")
                    .Replace("<", "<")
                    .Replace(">", ">")
                    .Replace("\"", "&quot;");
    }

    #endregion

    #region 校验

    /// <summary>
    /// 检查是否为有效的邮箱地址（简单校验）。
    /// </summary>
    public static bool IsValidEmail(this string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    /// <summary>
    /// 检查是否为有效的手机号码（中国大陆 11 位）。
    /// </summary>
    public static bool IsValidChineseMobile(this string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return Regex.IsMatch(value, @"^1[3-9]\d{9}$");
    }

    /// <summary>
    /// 检查字符串是否只包含英文字母和数字。
    /// </summary>
    public static bool IsAlphanumeric(this string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return Regex.IsMatch(value, @"^[a-zA-Z0-9]+$");
    }

    #endregion
}