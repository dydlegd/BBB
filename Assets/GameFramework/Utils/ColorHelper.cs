using System;
using System.Globalization;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 通用颜色工具类。
/// 提供十六进制转换、HSV 操作、颜色混合、随机生成、渐变采样等常用功能。
/// 命名使用 Helper 后缀以避免与 Unity 内置的 ColorUtility 冲突。
/// </summary>
/// <example>十六进制转换
/// <code>
/// Color c1 = ColorHelper.ParseHex("#FF5733");              // 6位RGB
/// Color c2 = ColorHelper.ParseHex("#FF573399");            // 8位RGBA
/// Color c3 = ColorHelper.ParseHex("#F53");                 // 3位短格式（→ #FF5533）
/// string hex = ColorHelper.ToHex(myColor);                  // "#FF5733"
/// string hexA = ColorHelper.ToHex(myColor, includeAlpha: true);  // "#FF5733FF"
/// </code>
/// </example>
/// <example>HSV 操作
/// <code>
/// float h, s, v;
/// ColorHelper.ToHSV(color, out h, out s, out v);
/// Color adjusted = ColorHelper.FromHSV(h + 0.1f, s, v);
/// Color hueShifted = color.AdjustHue(0.05f);
/// Color desaturated = color.AdjustSaturation(0.5f);
/// </code>
/// </example>
/// <example>颜色混合与调整
/// <code>
/// Color lerp = ColorHelper.Lerp(colorA, colorB, 0.5f);
/// Color avg = ColorHelper.Mix(color1, color2, color3);
/// Color bright = color.Brighten(0.2f);
/// Color dark = color.Darken(0.2f);
/// Color grey = color.Grayscale();
/// Color inverted = color.Invert();
/// </code>
/// </example>
/// <example>随机颜色
/// <code>
/// Color random = ColorHelper.RandomColor();
/// Color pastel = ColorHelper.RandomPastel();
/// Color vibrant = ColorHelper.RandomVibrant(0.7f);
/// Color gold = ColorHelper.RandomGolden();
/// Color themed = ColorHelper.RandomFromHueRange(0.5f, 0.7f); // 蓝紫色系
/// </code>
/// </example>
/// <example>渐变与比较
/// <code>
/// Color c = ColorHelper.Evaluate(gradient, 0.5f);
/// Color rainbow = ColorHelper.Rainbow(Time.time * 0.2f);
/// float diff = ColorHelper.Distance(c1, c2);
/// bool similar = ColorHelper.Approximately(c1, c2, 0.01f);
/// </code>
/// </example>
public static class ColorHelper
{
    #region 十六进制转换

    /// <summary>
    /// 从十六进制字符串解析颜色。支持 #RGB, #RRGGBB, #RGBA, #RRGGBBAA，可带或不带 # 前缀。
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
    /// <returns>解析得到的 Color，失败返回白色</returns>
    public static Color ParseHex(string hex)
    {
        return TryParseHexInternal(hex, out Color result) ? result : Color.white;
    }

    /// <summary>
    /// 尝试从十六进制字符串解析颜色，返回是否成功。
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
    /// <param name="color">解析结果（失败时为白色）</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParseHex(string hex, out Color color)
    {
        return TryParseHexInternal(hex, out color);
    }

    /// <summary>
    /// Color 转 #RRGGBB 格式十六进制字符串。
    /// </summary>
    public static string ToHex(this Color color)
    {
        return $"#{(int)(color.r * 255f):X2}{(int)(color.g * 255f):X2}{(int)(color.b * 255f):X2}";
    }

    /// <summary>
    /// Color 转十六进制字符串，可选包含 alpha 通道和 # 前缀。
    /// </summary>
    /// <param name="color">颜色</param>
    /// <param name="includeAlpha">是否包含 alpha（#RRGGBBAA）</param>
    /// <param name="withHash">是否带 # 前缀</param>
    public static string ToHex(this Color color, bool includeAlpha, bool withHash = true)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);

        string prefix = withHash ? "#" : string.Empty;

        if (!includeAlpha)
            return $"{prefix}{r:X2}{g:X2}{b:X2}";

        int a = Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
        return $"{prefix}{r:X2}{g:X2}{b:X2}{a:X2}";
    }

    /// <summary>
    /// 将常见 HTML 颜色名解析为 Color。不区分大小写。
    /// 未知名称会尝试作为十六进制解析。
    /// </summary>
    /// <param name="colorName">颜色名称</param>
    /// <returns>对应的 Color，失败返回白色</returns>
    public static Color FromName(string colorName)
    {
        if (string.IsNullOrEmpty(colorName)) return Color.white;

        string name = colorName.Trim().ToLowerInvariant();
        switch (name)
        {
            case "red": return new Color(1f, 0f, 0f);
            case "green": return new Color(0f, 0.502f, 0f);
            case "blue": return new Color(0f, 0f, 1f);
            case "white": return Color.white;
            case "black": return Color.black;
            case "yellow": return new Color(1f, 0.922f, 0.016f);
            case "cyan": return new Color(0f, 1f, 1f);
            case "magenta": return new Color(1f, 0f, 1f);
            case "gray": return new Color(0.5f, 0.5f, 0.5f);
            case "clear": return Color.clear;
            case "orange": return new Color(1f, 0.647f, 0f);
            case "pink": return new Color(1f, 0.753f, 0.796f);
            case "purple": return new Color(0.502f, 0f, 0.502f);
            case "brown": return new Color(0.647f, 0.165f, 0.165f);
            case "gold": return new Color(1f, 0.843f, 0f);
            case "silver": return new Color(0.753f, 0.753f, 0.753f);
            case "navy": return new Color(0f, 0f, 0.502f);
            case "teal": return new Color(0f, 0.502f, 0.502f);
            case "lime": return new Color(0f, 1f, 0f);
            case "maroon": return new Color(0.502f, 0f, 0f);
            case "olive": return new Color(0.502f, 0.502f, 0f);
            case "coral": return new Color(1f, 0.498f, 0.314f);
            case "salmon": return new Color(0.98f, 0.502f, 0.447f);
            case "turquoise": return new Color(0.251f, 0.878f, 0.816f);
            case "indigo": return new Color(0.294f, 0f, 0.51f);
            case "violet": return new Color(0.933f, 0.51f, 0.933f);
            default: return ParseHex(colorName);
        }
    }

    /// <summary>
    /// 内部统一的十六进制解析逻辑，避免 ParseHex / TryParseHex 代码重复。
    /// </summary>
    private static bool TryParseHexInternal(string hex, out Color color)
    {
        color = Color.white;

        if (string.IsNullOrEmpty(hex)) return false;

        hex = hex.Trim().TrimStart('#');

        switch (hex.Length)
        {
            case 3: return ParseHex3(hex, false, out color);
            case 4: return ParseHex3(hex, true, out color);
            case 6: return ParseHex6(hex, false, out color);
            case 8: return ParseHex6(hex, true, out color);
            default: return false;
        }
    }

    private static bool ParseHex3(string hex, bool hasAlpha, out Color color)
    {
        color = Color.white;
        int r, g, b, a = 255;

        if (!TryParseByte(hex[0].ToString() + hex[0], out r)) return false;
        if (!TryParseByte(hex[1].ToString() + hex[1], out g)) return false;
        if (!TryParseByte(hex[2].ToString() + hex[2], out b)) return false;

        if (hasAlpha && !TryParseByte(hex[3].ToString() + hex[3], out a)) return false;

        color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        return true;
    }

    private static bool ParseHex6(string hex, bool hasAlpha, out Color color)
    {
        color = Color.white;
        int r, g, b, a = 255;

        if (!TryParseByte(hex.Substring(0, 2), out r)) return false;
        if (!TryParseByte(hex.Substring(2, 2), out g)) return false;
        if (!TryParseByte(hex.Substring(4, 2), out b)) return false;

        if (hasAlpha && !TryParseByte(hex.Substring(6, 2), out a)) return false;

        color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        return true;
    }

    private static bool TryParseByte(string hexByte, out int value)
    {
        return int.TryParse(hexByte, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    #endregion

    #region 透明度操作

    /// <summary>
    /// 返回修改了 alpha 的新颜色。
    /// </summary>
    public static Color WithAlpha(this Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    /// <summary>
    /// 返回不透明版本（alpha = 1）。
    /// </summary>
    public static Color Opaque(this Color color)
    {
        color.a = 1f;
        return color;
    }

    /// <summary>
    /// 返回完全透明版本（alpha = 0），保留 RGB 通道值。
    /// </summary>
    public static Color Transparent(this Color color)
    {
        color.a = 0f;
        return color;
    }

    #endregion

    #region 颜色混合

    /// <summary>
    /// 线性插值混合两个颜色，t ∈ [0, 1]。
    /// </summary>
    public static Color Lerp(Color a, Color b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color(
            a.r + (b.r - a.r) * t,
            a.g + (b.g - a.g) * t,
            a.b + (b.b - a.b) * t,
            a.a + (b.a - a.a) * t
        );
    }

    /// <summary>
    /// 多颜色等权重混合（取平均色）。
    /// </summary>
    public static Color Mix(params Color[] colors)
    {
        if (colors == null || colors.Length == 0) return Color.white;

        float r = 0f, g = 0f, b = 0f, a = 0f;
        for (int i = 0; i < colors.Length; i++)
        {
            r += colors[i].r;
            g += colors[i].g;
            b += colors[i].b;
            a += colors[i].a;
        }

        float inv = 1f / colors.Length;
        return new Color(r * inv, g * inv, b * inv, a * inv);
    }

    /// <summary>
    /// 使用 smoothstep 平滑混合，端点附近过渡更柔和。
    /// </summary>
    public static Color SmoothLerp(Color a, Color b, float t)
    {
        float st = t * t * (3f - 2f * t);
        return Lerp(a, b, st);
    }

    /// <summary>
    /// 在两种颜色之间叠加混合（类似 Photoshop 的叠加模式）。
    /// base 为底色，overlay 为叠加色，factor 为叠加强度。
    /// </summary>
    public static Color Overlay(Color baseColor, Color overlay, float factor = 1f)
    {
        factor = Mathf.Clamp01(factor);
        return new Color(
            Mathf.Lerp(baseColor.r, OverlayChannel(baseColor.r, overlay.r), factor),
            Mathf.Lerp(baseColor.g, OverlayChannel(baseColor.g, overlay.g), factor),
            Mathf.Lerp(baseColor.b, OverlayChannel(baseColor.b, overlay.b), factor),
            baseColor.a
        );
    }

    private static float OverlayChannel(float baseVal, float overlayVal)
    {
        return baseVal < 0.5f
            ? 2f * baseVal * overlayVal
            : 1f - 2f * (1f - baseVal) * (1f - overlayVal);
    }

    #endregion

    #region 亮度与对比度

    /// <summary>
    /// 计算感知亮度（ITU-R BT.601 相对亮度公式）。
    /// </summary>
    public static float GetLuminance(this Color color)
    {
        return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
    }

    /// <summary>
    /// 调亮颜色（给 RGB 等量加值）。
    /// </summary>
    public static Color Brighten(this Color color, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Clamp01(color.r + amount),
            Mathf.Clamp01(color.g + amount),
            Mathf.Clamp01(color.b + amount),
            color.a
        );
    }

    /// <summary>
    /// 调暗颜色（给 RGB 等量减值）。
    /// </summary>
    public static Color Darken(this Color color, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Clamp01(color.r - amount),
            Mathf.Clamp01(color.g - amount),
            Mathf.Clamp01(color.b - amount),
            color.a
        );
    }

    /// <summary>
    /// 颜色转灰度。
    /// </summary>
    public static Color Grayscale(this Color color)
    {
        float lum = color.GetLuminance();
        return new Color(lum, lum, lum, color.a);
    }

    /// <summary>
    /// 反转颜色。
    /// </summary>
    public static Color Invert(this Color color, bool includeAlpha = false)
    {
        return new Color(
            1f - color.r,
            1f - color.g,
            1f - color.b,
            includeAlpha ? 1f - color.a : color.a
        );
    }

    /// <summary>
    /// 调整饱和度。factor = 0 完全去色，1 不变，>1 增强。
    /// </summary>
    public static Color AdjustSaturation(this Color color, float factor)
    {
        float lum = color.GetLuminance();
        return new Color(
            Mathf.Clamp01(lum + (color.r - lum) * factor),
            Mathf.Clamp01(lum + (color.g - lum) * factor),
            Mathf.Clamp01(lum + (color.b - lum) * factor),
            color.a
        );
    }

    /// <summary>
    /// 将颜色的 RGB 乘以一个系数。常用于将颜色调暗（factor < 1）或曝光补偿。
    /// </summary>
    public static Color Multiply(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }

    #endregion

    #region HSV 操作

    /// <summary>
    /// 从 Color 中提取 HSV 分量。
    /// </summary>
    public static void ToHSV(this Color color, out float h, out float s, out float v)
    {
        Color.RGBToHSV(color, out h, out s, out v);
    }

    /// <summary>
    /// 从 HSV 分量构造 Color。
    /// </summary>
    public static Color FromHSV(float h, float s, float v, float a = 1f)
    {
        Color c = Color.HSVToRGB(h, s, v);
        c.a = Mathf.Clamp01(a);
        return c;
    }

    /// <summary>
    /// 调整色调（色相偏移）。
    /// </summary>
    /// <param name="color">原颜色</param>
    /// <param name="delta">色调偏移量，结果自动 wrap 到 [0,1)</param>
    public static Color AdjustHue(this Color color, float delta)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        h = (h + delta) % 1f;
        if (h < 0f) h += 1f;
        Color result = Color.HSVToRGB(h, s, v);
        result.a = color.a;
        return result;
    }

    /// <summary>
    /// 调整明度（Value），factor = 1 不变。
    /// </summary>
    public static Color AdjustValue(this Color color, float factor)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        v = Mathf.Clamp01(v * factor);
        Color result = Color.HSVToRGB(h, s, v);
        result.a = color.a;
        return result;
    }

    #endregion

    #region 颜色比较

    /// <summary>
    /// 计算两个颜色在 RGB 空间中的欧几里得距离。
    /// 返回值 ∈ [0, √3]，越小越接近。
    /// </summary>
    public static float Distance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// 判断两个颜色是否近似相等（在给定容差范围内）。
    /// </summary>
    /// <param name="tolerance">容差，默认 0.01f</param>
    public static bool Approximately(Color a, Color b, float tolerance = 0.01f)
    {
        return Distance(a, b) <= tolerance;
    }

    #endregion

    #region 渐变工具

    /// <summary>
    /// 从 Unity Gradient 对象中采样颜色。
    /// </summary>
    public static Color Evaluate(Gradient gradient, float t)
    {
        if (gradient == null) return Color.white;
        return gradient.Evaluate(Mathf.Clamp01(t));
    }

    /// <summary>
    /// 在 Color 数组中进行均匀渐变采样。
    /// </summary>
    /// <param name="colors">颜色数组，至少包含 2 个颜色</param>
    /// <param name="t">采样位置（0~1）</param>
    public static Color Evaluate(Color[] colors, float t)
    {
        if (colors == null || colors.Length == 0) return Color.white;
        if (colors.Length == 1) return colors[0];

        t = Mathf.Clamp01(t);
        float segment = 1f / (colors.Length - 1);
        int idx = Mathf.FloorToInt(t / segment);

        if (idx >= colors.Length - 1)
            return colors[colors.Length - 1];

        float localT = (t - idx * segment) / segment;
        return Lerp(colors[idx], colors[idx + 1], localT);
    }

    /// <summary>
    /// 彩虹色：t ∈ [0, 1) 遍历完整色相环（红→橙→黄→绿→青→蓝→紫→红）。
    /// </summary>
    public static Color Rainbow(float t, float alpha = 1f)
    {
        t = (t % 1f + 1f) % 1f;
        return FromHSV(t, 1f, 1f, alpha);
    }

    /// <summary>
    /// 在两个颜色之间循环脉动，基于 Time.time 变化，适合呼吸灯等 UI 特效。
    /// </summary>
    /// <param name="speed">速度系数，越大越快</param>
    public static Color Pulse(Color a, Color b, float speed = 1f)
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        return Lerp(a, b, t);
    }

    #endregion

    #region 随机颜色

    /// <summary>
    /// 生成完全随机的 RGB 颜色。
    /// </summary>
    public static Color RandomColor(float alpha = 1f)
    {
        return new Color(Random.value, Random.value, Random.value, Mathf.Clamp01(alpha));
    }

    /// <summary>
    /// 生成柔和的粉彩色（高明度、低饱和度）。
    /// </summary>
    public static Color RandomPastel(float alpha = 1f)
    {
        return Color.HSVToRGB(
            Random.value,
            Random.Range(0.2f, 0.5f),
            Random.Range(0.75f, 1f)
        ).WithAlpha(alpha);
    }

    /// <summary>
    /// 生成鲜艳的高饱和度颜色。
    /// </summary>
    public static Color RandomVibrant(float alpha = 1f)
    {
        return Color.HSVToRGB(
            Random.value,
            Random.Range(0.7f, 1f),
            Random.Range(0.7f, 1f)
        ).WithAlpha(alpha);
    }

    /// <summary>
    /// 生成金色系随机颜色。
    /// </summary>
    public static Color RandomGolden(float alpha = 1f)
    {
        return Color.HSVToRGB(
            Random.Range(0.1f, 0.2f),
            Random.Range(0.7f, 1f),
            Random.Range(0.7f, 1f)
        ).WithAlpha(alpha);
    }

    /// <summary>
    /// 生成指定色相范围内的随机颜色。
    /// </summary>
    /// <param name="hueMin">色调最小值（0~1）</param>
    /// <param name="hueMax">色调最大值（0~1）</param>
    /// <param name="saturationMin">饱和度最小值</param>
    /// <param name="saturationMax">饱和度最大值</param>
    /// <param name="valueMin">明度最小值</param>
    /// <param name="valueMax">明度最大值</param>
    public static Color RandomFromHueRange(
        float hueMin, float hueMax,
        float saturationMin = 0.5f, float saturationMax = 1f,
        float valueMin = 0.7f, float valueMax = 1f,
        float alpha = 1f)
    {
        return Color.HSVToRGB(
            Random.Range(hueMin, hueMax),
            Random.Range(saturationMin, saturationMax),
            Random.Range(valueMin, valueMax)
        ).WithAlpha(alpha);
    }

    #endregion
}