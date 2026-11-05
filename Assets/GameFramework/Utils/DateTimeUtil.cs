using System;

/// <summary>
/// 通用日期时间工具类。
/// 提供格式化输出、Unix 时间戳转换、日期计算、相对时间描述等常用功能。
/// 所有方法均为纯函数，无状态、无依赖，可直接在任何环境中调用。
/// </summary>
/// <example>格式化输出
/// <code>
/// string s1 = DateTimeUtil.Now.ToString("yyyy-MM-dd HH:mm:ss");  // 2026-09-21 14:30:00
/// string s2 = DateTimeUtil.FormatNow("yyyy/MM/dd");               // 2026/09/21
/// string iso = DateTimeUtil.Now.ToIso8601();                      // 2026-09-21T14:30:00.000Z
/// </code>
/// </example>
/// <example>时间戳
/// <code>
/// long seconds = DateTimeUtil.ToUnixSeconds(DateTime.UtcNow);  // 获取当前秒级时间戳
/// long ms = DateTimeUtil.ToUnixMilliseconds(DateTime.UtcNow);  // 获取当前毫秒级时间戳
/// DateTime dt = DateTimeUtil.FromUnixSeconds(1726900000);      // 时间戳转 DateTime
/// DateTime dt2 = DateTimeUtil.FromUnixMilliseconds(1726900000000);
/// </code>
/// </example>
/// <example>日期边界
/// <code>
/// DateTime today = DateTimeUtil.Today;                        // 2026-09-21 00:00:00
/// DateTime dayStart = DateTimeUtil.Now.StartOfDay();           // 今天 00:00:00
/// DateTime dayEnd = DateTimeUtil.Now.EndOfDay();               // 今天 23:59:59.999
/// DateTime weekStart = DateTimeUtil.Now.StartOfWeek();         // 本周一 00:00:00
/// DateTime monthEnd = DateTimeUtil.Now.EndOfMonth();           // 本月最后一天 23:59:59
/// </code>
/// </example>
/// <example>相对时间
/// <code>
/// string desc = DateTimeUtil.GetRelativeTimeDescription(pastTime);  // "3分钟前" / "昨天 14:30"
/// string duration = DateTimeUtil.FormatDuration(TimeSpan.FromMinutes(90)); // "1小时30分钟"
/// int age = DateTimeUtil.CalculateAge(birthDate);                   // 年龄
/// int days = DateTimeUtil.DaysBetween(start, end);                  // 间隔天数
/// </code>
/// </example>
public static class DateTimeUtil
{
    /// <summary>
    /// 当前本地时间（等同于 DateTime.Now，统一入口便于 mock）。
    /// </summary>
    public static DateTime Now => DateTime.Now;

    /// <summary>
    /// 当前 UTC 时间（等同于 DateTime.UtcNow）。
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// 今天的日期（本地时间 00:00:00）。
    /// </summary>
    public static DateTime Today => DateTime.Today;

    #region 格式化

    /// <summary>
    /// 将 DateTime 格式化为通用日期时间字符串（yyyy-MM-dd HH:mm:ss）。
    /// </summary>
    public static string ToStandardString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 将 DateTime 格式化为日期字符串（yyyy-MM-dd）。
    /// </summary>
    public static string ToDateString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// 将 DateTime 格式化为时间字符串（HH:mm:ss）。
    /// </summary>
    public static string ToTimeString(this DateTime dateTime)
    {
        return dateTime.ToString("HH:mm:ss");
    }

    /// <summary>
    /// 将 DateTime 格式化为紧凑字符串（yyyyMMddHHmmss），适合做文件名。
    /// </summary>
    public static string ToCompactString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyyMMddHHmmss");
    }

    /// <summary>
    /// 将 DateTime 格式化为 ISO 8601 字符串（yyyy-MM-ddTHH:mm:ss.fffZ）。
    /// 始终以 UTC 形式输出，适合 API 通信和日志。
    /// </summary>
    public static string ToIso8601(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    /// <summary>
    /// 使用指定格式格式化当前本地时间。
    /// </summary>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化后的时间字符串</returns>
    public static string FormatNow(string format)
    {
        if (string.IsNullOrEmpty(format))
            format = "yyyy-MM-dd HH:mm:ss";
        return DateTime.Now.ToString(format);
    }

    /// <summary>
    /// 将 DateTime 转换为中国习惯的周几名称（星期一~星期日）。
    /// </summary>
    /// <param name="dateTime">日期</param>
    /// <returns>周几名称</returns>
    public static string ToChineseDayOfWeek(this DateTime dateTime)
    {
        switch (dateTime.DayOfWeek)
        {
            case DayOfWeek.Monday: return "星期一";
            case DayOfWeek.Tuesday: return "星期二";
            case DayOfWeek.Wednesday: return "星期三";
            case DayOfWeek.Thursday: return "星期四";
            case DayOfWeek.Friday: return "星期五";
            case DayOfWeek.Saturday: return "星期六";
            case DayOfWeek.Sunday: return "星期日";
            default: return string.Empty;
        }
    }

    #endregion

    #region Unix 时间戳

    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 将 DateTime 转换为 Unix 秒级时间戳。DateTime 内部会转为 UTC 计算。
    /// </summary>
    /// <param name="dateTime">日期时间</param>
    /// <returns>秒级时间戳</returns>
    public static long ToUnixSeconds(this DateTime dateTime)
    {
        return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
    }

    /// <summary>
    /// 将 DateTime 转换为 Unix 毫秒级时间戳。DateTime 内部会转为 UTC 计算。
    /// </summary>
    /// <param name="dateTime">日期时间</param>
    /// <returns>毫秒级时间戳</returns>
    public static long ToUnixMilliseconds(this DateTime dateTime)
    {
        return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
    }

    /// <summary>
    /// 从 Unix 秒级时间戳转换为 UTC DateTime。
    /// </summary>
    /// <param name="seconds">秒级时间戳</param>
    /// <returns>UTC DateTime</returns>
    public static DateTime FromUnixSeconds(long seconds)
    {
        return UnixEpoch.AddSeconds(seconds);
    }

    /// <summary>
    /// 从 Unix 毫秒级时间戳转换为 UTC DateTime。
    /// </summary>
    /// <param name="milliseconds">毫秒级时间戳</param>
    /// <returns>UTC DateTime</returns>
    public static DateTime FromUnixMilliseconds(long milliseconds)
    {
        return UnixEpoch.AddMilliseconds(milliseconds);
    }

    /// <summary>
    /// 获取当前时间的秒级时间戳。
    /// </summary>
    public static long NowUnixSeconds => (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;

    /// <summary>
    /// 获取当前时间的毫秒级时间戳。
    /// </summary>
    public static long NowUnixMilliseconds => (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;

    #endregion

    #region 日期边界

    /// <summary>
    /// 获取当天的起始时刻（00:00:00.000）。
    /// </summary>
    public static DateTime StartOfDay(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    /// 获取当天的结束时刻（23:59:59.999）。
    /// </summary>
    public static DateTime EndOfDay(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddMilliseconds(-1);
    }

    /// <summary>
    /// 获取本周一的起始时刻（以周一为每周第一天）。
    /// </summary>
    public static DateTime StartOfWeek(this DateTime dateTime)
    {
        int diff = dateTime.DayOfWeek - DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return dateTime.Date.AddDays(-diff);
    }

    /// <summary>
    /// 获取本周日的结束时刻。
    /// </summary>
    public static DateTime EndOfWeek(this DateTime dateTime)
    {
        return dateTime.StartOfWeek().AddDays(7).AddMilliseconds(-1);
    }

    /// <summary>
    /// 获取当月第一天的起始时刻。
    /// </summary>
    public static DateTime StartOfMonth(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, 1);
    }

    /// <summary>
    /// 获取当月最后一天的结束时刻。
    /// </summary>
    public static DateTime EndOfMonth(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, DateTime.DaysInMonth(dateTime.Year, dateTime.Month))
            .AddDays(1).AddMilliseconds(-1);
    }

    /// <summary>
    /// 获取当年第一天的起始时刻。
    /// </summary>
    public static DateTime StartOfYear(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, 1, 1);
    }

    /// <summary>
    /// 获取当年最后一天的结束时刻。
    /// </summary>
    public static DateTime EndOfYear(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, 12, 31).AddDays(1).AddMilliseconds(-1);
    }

    #endregion

    #region 日期比较

    /// <summary>
    /// 判断两个 DateTime 是否为同一天（比较年月日）。
    /// </summary>
    public static bool IsSameDay(this DateTime dateTime, DateTime other)
    {
        return dateTime.Date == other.Date;
    }

    /// <summary>
    /// 判断是否为今天。
    /// </summary>
    public static bool IsToday(this DateTime dateTime)
    {
        return dateTime.Date == DateTime.Today;
    }

    /// <summary>
    /// 判断是否在本周内。
    /// </summary>
    public static bool IsThisWeek(this DateTime dateTime)
    {
        DateTime todayStart = DateTime.Today;
        DateTime weekStart = todayStart.StartOfWeek();
        return dateTime.Date >= weekStart && dateTime.Date <= todayStart.EndOfWeek();
    }

    /// <summary>
    /// 判断是否在本月内。
    /// </summary>
    public static bool IsThisMonth(this DateTime dateTime)
    {
        return dateTime.Year == DateTime.Today.Year
            && dateTime.Month == DateTime.Today.Month;
    }

    /// <summary>
    /// 判断是否在本年内。
    /// </summary>
    public static bool IsThisYear(this DateTime dateTime)
    {
        return dateTime.Year == DateTime.Today.Year;
    }

    /// <summary>
    /// 判断日期是否在指定范围内（闭区间）。
    /// </summary>
    public static bool IsBetween(this DateTime dateTime, DateTime start, DateTime end)
    {
        return dateTime >= start && dateTime <= end;
    }

    /// <summary>
    /// 计算两个日期之间的天数差（只比较日期部分）。
    /// </summary>
    public static int DaysBetween(this DateTime start, DateTime end)
    {
        return (end.Date - start.Date).Days;
    }

    #endregion

    #region 日期运算

    /// <summary>
    /// 计算年龄（基于出生日期）。
    /// </summary>
    /// <param name="birthDate">出生日期</param>
    /// <param name="referenceDate">参照日期，默认为今天</param>
    /// <returns>年龄（周岁）</returns>
    public static int CalculateAge(DateTime birthDate, DateTime? referenceDate = null)
    {
        DateTime reference = referenceDate ?? DateTime.Today;
        int age = reference.Year - birthDate.Year;
        if (birthDate > reference.AddYears(-age))
            age--;
        return age;
    }

    /// <summary>
    /// 获取一个月有多少天。
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <returns>天数</returns>
    public static int DaysInMonth(int year, int month)
    {
        if (month < 1 || month > 12)
            return 0;
        return DateTime.DaysInMonth(year, month);
    }

    /// <summary>
    /// 判断是否为闰年。
    /// </summary>
    public static bool IsLeapYear(int year)
    {
        return DateTime.IsLeapYear(year);
    }

    /// <summary>
    /// 将时间重置到当天指定时刻。
    /// </summary>
    /// <param name="dateTime">原日期</param>
    /// <param name="hours">时</param>
    /// <param name="minutes">分</param>
    /// <param name="seconds">秒</param>
    /// <returns>重置后的 DateTime</returns>
    public static DateTime AtTime(this DateTime dateTime, int hours, int minutes = 0, int seconds = 0)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, hours, minutes, seconds);
    }

    /// <summary>
    /// 添加指定数量的工作日（跳过周六周日）。
    /// </summary>
    /// <param name="dateTime">起始日期</param>
    /// <param name="days">工作日数量</param>
    /// <returns>结果日期</returns>
    public static DateTime AddBusinessDays(this DateTime dateTime, int days)
    {
        int sign = Math.Sign(days);
        int remaining = Math.Abs(days);

        while (remaining > 0)
        {
            dateTime = dateTime.AddDays(sign);
            if (dateTime.DayOfWeek != DayOfWeek.Saturday && dateTime.DayOfWeek != DayOfWeek.Sunday)
                remaining--;
        }

        return dateTime;
    }

    #endregion

    #region 相对时间

    /// <summary>
    /// 获取相对于当前时间的友好描述，如"刚刚"、"3分钟前"、"昨天 14:30"等。
    /// 适合聊天消息、动态列表等 UI 展示场景。
    /// </summary>
    /// <param name="dateTime">过去的时间</param>
    /// <returns>友好时间描述</returns>
    public static string GetRelativeTimeDescription(this DateTime dateTime)
    {
        TimeSpan span = DateTime.Now - dateTime;

        if (span.TotalSeconds < 0)
            return dateTime.ToStandardString();

        if (span.TotalSeconds < 60)
            return "刚刚";

        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}分钟前";

        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours}小时前";

        if (span.TotalDays < 2)
            return $"昨天 {dateTime:HH:mm}";

        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays}天前";

        if (dateTime.Year == DateTime.Now.Year)
            return dateTime.ToString("MM-dd HH:mm");

        return dateTime.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// 将 TimeSpan 格式为中文可读的时长，如 "1小时30分钟"。
    /// </summary>
    /// <param name="span">时间间隔</param>
    /// <returns>中文时长字符串</returns>
    public static string FormatDuration(this TimeSpan span)
    {
        if (span.TotalSeconds < 0)
            return "0秒";

        if (span.TotalDays >= 1)
        {
            int days = (int)span.TotalDays;
            int hours = span.Hours;
            int minutes = span.Minutes;

            string result = $"{days}天";
            if (hours > 0) result += $"{hours}小时";
            if (minutes > 0) result += $"{minutes}分钟";
            return result;
        }

        if (span.TotalHours >= 1)
        {
            int hours = (int)span.TotalHours;
            int minutes = span.Minutes;
            int seconds = span.Seconds;

            string result = $"{hours}小时";
            if (minutes > 0) result += $"{minutes}分钟";
            if (seconds > 0 && hours == 0) result += $"{seconds}秒";
            return result;
        }

        if (span.TotalMinutes >= 1)
        {
            int minutes = (int)span.TotalMinutes;
            int seconds = span.Seconds;

            string result = $"{minutes}分钟";
            if (seconds > 0) result += $"{seconds}秒";
            return result;
        }

        return $"{(int)span.TotalSeconds}秒";
    }

    /// <summary>
    /// 将 TimeSpan 格式为倒计时格式，如 "01:30:45" 或 "30:45"。
    /// </summary>
    /// <param name="span">时间间隔</param>
    /// <param name="showHours">是否始终显示小时</param>
    /// <returns>倒计时字符串</returns>
    public static string FormatCountdown(this TimeSpan span, bool showHours = true)
    {
        if (span.TotalSeconds <= 0)
            return showHours ? "00:00:00" : "00:00";

        if (showHours || span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }

        return $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    #endregion

    #region 解析

    /// <summary>
    /// 尝试将字符串解析为 DateTime。按常用格式依次尝试，只要一个成功就返回。
    /// 支持格式："yyyy-MM-dd HH:mm:ss"、"yyyy-MM-dd"、"yyyy/MM/dd HH:mm:ss" 等。
    /// </summary>
    /// <param name="text">待解析字符串</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParse(string text, out DateTime result)
    {
        result = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "yyyyMMddHHmmss",
            "yyyyMMdd",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ssZ",
        };

        return DateTime.TryParseExact(text.Trim(), formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result);
    }

    /// <summary>
    /// 解析字符串为 DateTime，失败返回 null。
    /// </summary>
    public static DateTime? Parse(string text)
    {
        return TryParse(text, out DateTime result) ? result : (DateTime?)null;
    }

    #endregion

    #region 随机时间

    private static readonly System.Random _random = new System.Random();

    /// <summary>
    /// 生成在 [start, end) 范围内的随机时间。
    /// </summary>
    public static DateTime RandomBetween(DateTime start, DateTime end)
    {
        if (start >= end)
            return start;

        long range = (long)(end - start).TotalMilliseconds;
        long randomMs = (long)(_random.NextDouble() * range);
        return start.AddMilliseconds(randomMs);
    }

    #endregion
}