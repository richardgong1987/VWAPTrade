using System;

namespace cAlgo.Robots;

// 指标自己的周期，跟「什么时候允许下单」是两回事（下单时间见 TradingSession）。
//
//   日 VWAP：每天早上 06:00 清零，累积到次日 06:00。06:00 是外汇/黄金的日切（纽约 17:00）。
//   周 VWAP：每周一早上 06:00 清零，累积到周六 06:00。
//
// 每一根 K 线都落在某个周期里，所以 VWAP 一直在累积 —— 包括周一、以及 06:00~10:30 这段还不
// 允许下单的时间。等 10:30 开始下单时，当日 VWAP 已经累积了四个半小时的量。
//
// 纯时间比较，没有 cAlgo 依赖，有单元测试。
public static class VwapPeriod {
    // 日切：早上 06:00。
    private static readonly TimeSpan DayStartTime = new(6, 0, 0);

    // 这个时间属于哪一天，返回那一天开始累积的 06:00。
    public static DateTime GetDayStart(DateTime time) {
        DateTime dayStart = time.Date + DayStartTime;

        // 06:00 之前还算前一天，跟日切之前的行情算在一起。
        return time.TimeOfDay >= DayStartTime ? dayStart : dayStart.AddDays(-1);
    }

    // 这个时间属于哪一周，返回那一周周一的 06:00。先落到「哪一天」再回退到周一，
    // 这样周一 06:00 之前（还属于上周五那一天）不会被算成新的一周。
    public static DateTime GetWeekStart(DateTime time) {
        DateTime dayStart = GetDayStart(time);
        int daysSinceMonday = ((int)dayStart.DayOfWeek + 6) % 7;

        return dayStart.AddDays(-daysSinceMonday);
    }

    // 序列的第一根（previous 为 null）一律算开新周期：累积量本来就是空的。
    public static bool IsNewDay(DateTime current, DateTime? previous) {
        return !previous.HasValue || GetDayStart(current) != GetDayStart(previous.Value);
    }

    public static bool IsNewWeek(DateTime current, DateTime? previous) {
        return !previous.HasValue || GetWeekStart(current) != GetWeekStart(previous.Value);
    }
}
