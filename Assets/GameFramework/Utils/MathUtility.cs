using UnityEngine;

/// <summary>
/// 通用数学工具类。
/// 提供数值重映射、夹紧、平滑、角度处理、插值、曲线计算等常用数学函数。
/// </summary>
/// <example>数值映射与夹紧
/// <code>
/// // 将 0.5 从 [0,1] 映射到 [0,100] → 50
/// float result = MathUtility.Remap(0.5f, 0f, 1f, 0f, 100f);
///
/// // 将 value 从旧范围映射到新范围并夹紧
/// float clamped = MathUtility.RemapClamped(value, 0f, 10f, 0f, 1f);
///
/// // 生成往返值（ping-pong）
/// float pingPong = MathUtility.PingPong(Time.time, 3f); // 在 [0, 3] 之间往返
/// </code>
/// </example>
/// <example>角度处理
/// <code>
/// float delta = MathUtility.AngleDelta(350f, 10f);    // 20°（最短路径）
/// float clamped = MathUtility.ClampAngle(400f);        // 40°（规范化到 [0, 360)）
/// float wrapped = MathUtility.WrapAngle180(270f);      // -90°
/// </code>
/// </example>
/// <example>平滑与插值
/// <code>
/// float smooth = MathUtility.SmoothStep(value, from, to);
/// float ease = MathUtility.EaseInOutQuad(0f, 100f, t);
/// float damped = MathUtility.SmoothDamp(current, target, ref velocity, 0.3f);
/// </code>
/// </example>
/// <example>实用函数
/// <code>
/// bool near = MathUtility.Approximately(0.001f, 0.00101f, 0.0001f);
/// int sign = MathUtility.SignWithZero(0f);  // 0
/// float snap = MathUtility.Snap(value, 0.5f);  // 吸附到最近0.5倍数值
/// float percent = MathUtility.PercentOf(3, 10); // 30%
/// </code>
/// </example>
public static class MathUtility
{
    #region 重映射

    /// <summary>
    /// 将 value 从 [fromMin, fromMax] 线性映射到 [toMin, toMax]。
    /// </summary>
    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float fromRange = fromMax - fromMin;
        if (Mathf.Abs(fromRange) < 0.0001f) return (toMin + toMax) * 0.5f;

        float t = (value - fromMin) / fromRange;
        return Mathf.Lerp(toMin, toMax, t);
    }

    /// <summary>
    /// 将 value 从 [fromMin, fromMax] 映射到 [toMin, toMax]，结果夹紧在目标范围内。
    /// </summary>
    public static float RemapClamped(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float fromRange = fromMax - fromMin;
        if (Mathf.Abs(fromRange) < 0.0001f) return (toMin + toMax) * 0.5f;

        float t = Mathf.Clamp01((value - fromMin) / fromRange);
        return Mathf.Lerp(toMin, toMax, t);
    }

    /// <summary>
    /// 获取 value 在 [min, max] 范围内的归一化值（0-1）。
    /// </summary>
    public static float Normalize(float value, float min, float max)
    {
        float range = max - min;
        if (Mathf.Abs(range) < 0.0001f) return 0.5f;
        return Mathf.Clamp01((value - min) / range);
    }

    #endregion

    #region 夹紧与限制

    /// <summary>
    /// 将值夹紧在 [min, max] 范围内。
    /// </summary>
    public static float Clamp(float value, float min, float max)
    {
        return Mathf.Clamp(value, min, max);
    }

    /// <summary>
    /// 将值夹紧在 [0, 1] 范围内。
    /// </summary>
    public static float Clamp01(float value)
    {
        return Mathf.Clamp01(value);
    }

    /// <summary>
    /// 将值夹紧在 [0, max] 范围内。
    /// </summary>
    public static float ClampPositive(float value, float max = float.MaxValue)
    {
        return Mathf.Clamp(value, 0f, max);
    }

    #endregion

    #region 近似判断

    /// <summary>
    /// 判断两个浮点数是否近似相等（默认容差 0.0001f）。
    /// </summary>
    public static bool Approximately(float a, float b, float tolerance = 0.0001f)
    {
        return Mathf.Abs(a - b) <= tolerance;
    }

    /// <summary>
    /// 判断两个 Vector3 是否近似相等。
    /// </summary>
    public static bool Approximately(Vector3 a, Vector3 b, float tolerance = 0.0001f)
    {
        return (a - b).sqrMagnitude <= tolerance * tolerance;
    }

    /// <summary>
    /// 判断两个 Vector2 是否近似相等。
    /// </summary>
    public static bool Approximately(Vector2 a, Vector2 b, float tolerance = 0.0001f)
    {
        return (a - b).sqrMagnitude <= tolerance * tolerance;
    }

    /// <summary>
    /// 判断一个值是否在指定范围内（含边界）。
    /// </summary>
    public static bool InRange(float value, float min, float max)
    {
        return value >= min && value <= max;
    }

    #endregion

    #region 角度处理

    /// <summary>
    /// 计算两个角度之间的最小差值（度）。
    /// </summary>
    public static float AngleDelta(float angleA, float angleB)
    {
        float delta = Mathf.Repeat(angleB - angleA, 360f);
        if (delta > 180f) delta -= 360f;
        return delta;
    }

    /// <summary>
    /// 将角度规范化到 [0, 360) 范围。
    /// </summary>
    public static float WrapAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }

    /// <summary>
    /// 将角度规范化到 [-180, 180) 范围。
    /// </summary>
    public static float WrapAngle180(float angle)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return angle;
    }

    /// <summary>
    /// 将角度夹紧在 [0, 360) 范围并规范化。
    /// </summary>
    public static float ClampAngle(float angle, float min, float max)
    {
        angle = WrapAngle(angle);
        return Mathf.Clamp(angle, min, max);
    }

    /// <summary>
    /// 向目标角度平滑旋转（处理 360° 环绕）。
    /// </summary>
    public static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        float delta = AngleDelta(current, target);
        if (Mathf.Abs(delta) <= maxDelta) return target;
        return current + Mathf.Sign(delta) * maxDelta;
    }

    #endregion

    #region 平滑与插值

    /// <summary>
    /// 平滑阶跃函数（SmoothStep），在 [from, to] 之间做 cubic 平滑。
    /// </summary>
    public static float SmoothStep(float value, float from, float to)
    {
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// 更平滑的阶跃函数（SmootherStep），在 [from, to] 之间做 quintic 平滑。
    /// </summary>
    public static float SmootherStep(float value, float from, float to)
    {
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>
    /// 缓入二次函数（EaseInQuad）。
    /// </summary>
    public static float EaseInQuad(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(from, to, t * t);
    }

    /// <summary>
    /// 缓出二次函数（EaseOutQuad）。
    /// </summary>
    public static float EaseOutQuad(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(from, to, t * (2f - t));
    }

    /// <summary>
    /// 缓入缓出二次函数（EaseInOutQuad）。
    /// </summary>
    public static float EaseInOutQuad(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
        return Mathf.Lerp(from, to, t);
    }

    /// <summary>
    /// 弹性缓出（EaseOutElastic）。
    /// </summary>
    public static float EaseOutElastic(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.0001f || t > 0.9999f) return Mathf.Lerp(from, to, t);

        float p = 0.3f;
        float s = p / 4f;
        float val = Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - s) * (2f * Mathf.PI) / p) + 1f;
        return Mathf.Lerp(from, to, val);
    }

    /// <summary>
    /// 往返值（PingPong），在 [0, length] 之间来回。
    /// </summary>
    public static float PingPong(float t, float length)
    {
        return Mathf.PingPong(t, length);
    }

    #endregion

    #region 取整与吸附

    /// <summary>
    /// 将值吸附到最近的步进倍数值。
    /// </summary>
    public static float Snap(float value, float step)
    {
        if (step <= 0f) return value;
        return Mathf.Round(value / step) * step;
    }

    /// <summary>
    /// 将 Vector3 各分量吸附到最近的步进倍数值。
    /// </summary>
    public static Vector3 Snap(Vector3 value, float step)
    {
        return new Vector3(
            Snap(value.x, step),
            Snap(value.y, step),
            Snap(value.z, step)
        );
    }

    /// <summary>
    /// 将 Vector2 各分量吸附到最近的步进倍数值。
    /// </summary>
    public static Vector2 Snap(Vector2 value, float step)
    {
        return new Vector2(
            Snap(value.x, step),
            Snap(value.y, step)
        );
    }

    /// <summary>
    /// 向下取整到最近的步进倍数值。
    /// </summary>
    public static float FloorToStep(float value, float step)
    {
        if (step <= 0f) return value;
        return Mathf.Floor(value / step) * step;
    }

    /// <summary>
    /// 向上取整到最近的步进倍数值。
    /// </summary>
    public static float CeilToStep(float value, float step)
    {
        if (step <= 0f) return value;
        return Mathf.Ceil(value / step) * step;
    }

    #endregion

    #region 方向与角度

    /// <summary>
    /// 将角度（度）转换为方向向量（XZ 平面）。
    /// </summary>
    public static Vector3 AngleToDirection(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }

    /// <summary>
    /// 将方向向量（XZ 平面）转换为角度（度）。
    /// </summary>
    public static float DirectionToAngle(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 计算两点之间的角度（度，XZ 平面）。
    /// </summary>
    public static float AngleBetweenPoints(Vector3 from, Vector3 to)
    {
        return DirectionToAngle(to - from);
    }

    #endregion

    #region 实用函数

    /// <summary>
    /// 获取数值的符号：正数返回 1，负数返回 -1，0 返回 0（不同于 Mathf.Sign 对 0 返回 1）。
    /// </summary>
    public static float SignWithZero(float value)
    {
        if (value > 0f) return 1f;
        if (value < 0f) return -1f;
        return 0f;
    }

    /// <summary>
    /// 计算 value 占 maxValue 的百分比（0-100）。
    /// </summary>
    public static float PercentOf(float value, float maxValue)
    {
        if (Mathf.Approximately(maxValue, 0f)) return 0f;
        return (value / maxValue) * 100f;
    }

    /// <summary>
    /// 计算 value 占 maxValue 的比例（0-1）。
    /// </summary>
    public static float RatioOf(float value, float maxValue)
    {
        if (Mathf.Approximately(maxValue, 0f)) return 0f;
        return Mathf.Clamp01(value / maxValue);
    }

    /// <summary>
    /// 循环索引：超出范围时回绕（如数组索引循环）。
    /// </summary>
    public static int WrapIndex(int index, int count)
    {
        if (count <= 0) return 0;
        index %= count;
        if (index < 0) index += count;
        return index;
    }

    /// <summary>
    /// 循环值：使 value 在 [min, max) 之间循环。
    /// </summary>
    public static float Wrap(float value, float min, float max)
    {
        float range = max - min;
        if (range <= 0f) return min;
        value = min + (value - min) % range;
        if (value < min) value += range;
        return value;
    }

    #endregion
}