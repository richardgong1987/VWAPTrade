using System;

namespace cAlgo.Robots;

// 什么时候允许开新单。时间一律是服务器时间，而服务器时区已设成日本时间
// （见 VWAPTrade 的 [Robot(TimeZone = TimeZones.TokyoStandardTime)]）。
//
//   一场：10:30 开盘，次日 06:00 收盘。周二到周五各开一场，周一不交易。
//   一周：周二 10:30 到周六 06:00，正好是上面四场。
//
// 06:00~10:30 的空档、周一、周末都不开新单；已经持有的仓位不受影响，照常由止损/止盈了结。
//
// 注意这里只管下单时间。VWAP 的清零点是另一套、也更早（日 06:00、周一 06:00），见 VwapPeriod —— 
// 所以 10:30 刚开盘就下单时，当日 VWAP 已经累积了四个半小时，不是从零开始。
public static class TradingSession {
    private static readonly TimeSpan OpenTime = new(10, 30, 0);
    private static readonly TimeSpan CloseTime = new(6, 0, 0);

    // 周二 10:30 到周六 06:00，共 91.5 小时。
    private static readonly TimeSpan WeekLength = TimeSpan.FromHours(91.5);

    public static bool IsInSession(DateTime time) {
        return GetDaySessionStart(time).HasValue;
    }

    // 这个时间属于哪一场，返回那一场开盘的 10:30；不属于任何一场就返回 null。
    public static DateTime? GetDaySessionStart(DateTime time) {
        TimeSpan timeOfDay = time.TimeOfDay;
        DateTime sessionStart;

        if (timeOfDay >= OpenTime)
            sessionStart = time.Date + OpenTime; // 当天 10:30 开的那一场
        else if (timeOfDay < CloseTime)
            sessionStart = time.Date.AddDays(-1) + OpenTime; // 昨天 10:30 开的那一场，还没收
        else
            return null; // 06:00~10:30 的空档

        return IsTradingDay(sessionStart.DayOfWeek) ? sessionStart : (DateTime?)null;
    }

    // 只有周二到周五 10:30 开的场才交易：周一不交易，周六周日休市。
    // 注意这也把周二凌晨挡在外面 —— 那段时间属于周一开的那一场。
    private static bool IsTradingDay(DayOfWeek dayOfWeek) {
        return dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday || dayOfWeek == DayOfWeek.Thursday ||
               dayOfWeek == DayOfWeek.Friday;
    }

    // 这个时间属于哪一周，返回那一周周二的 10:30；不在任何一周里就返回 null。
    //
    // 一周是连成一片的：周二 10:30 到周六 06:00 中间那几个 06:00~10:30 的空档仍然算在这一周里。
    // 不能拿「这个时间属于哪一天」去推「属于哪一周」—— 空档里没有「哪一天」，那样每天开盘都会被
    // 当成开新的一周，周 VWAP 就跟日 VWAP 一模一样了。
    public static DateTime? GetWeekSessionStart(DateTime time) {
        int daysSinceTuesday = ((int)time.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        DateTime weekStart = time.Date.AddDays(-daysSinceTuesday) + OpenTime;

        // 周二 10:30 之前还属于上一周。
        if (time < weekStart)
            weekStart = weekStart.AddDays(-7);

        return time < weekStart + WeekLength ? weekStart : (DateTime?)null;
    }

    public static bool IsNewDaySession(DateTime current, DateTime? previous) {
        return HasSessionChanged(GetDaySessionStart(current), previous.HasValue ? GetDaySessionStart(previous.Value) : null);
    }

    public static bool IsNewWeekSession(DateTime current, DateTime? previous) {
        return HasSessionChanged(GetWeekSessionStart(current), previous.HasValue ? GetWeekSessionStart(previous.Value) : null);
    }

    // 上一根 K 线落在空档里（previousStart 为 null）也算开新的一场：空档之后就是新的一天。
    private static bool HasSessionChanged(DateTime? currentStart, DateTime? previousStart) {
        if (!currentStart.HasValue)
            return false;

        return currentStart != previousStart;
    }
}
