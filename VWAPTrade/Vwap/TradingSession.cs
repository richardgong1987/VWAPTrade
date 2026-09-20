using System;

namespace cAlgo.Robots;

// 这个策略的交易时段。时间一律是服务器时间，而服务器时区已设成日本时间
// （见 VWAPTrade 的 [Robot(TimeZone = TimeZones.TokyoStandardTime)]）。
//
//   一个交易日：10:00 开盘，次日 06:00 收盘。周二到周五各开一场，周一不交易。
//   一个交易周：周二 10:00 开盘，周六 06:00 收盘，正好是上面四场。
//
// 06:00~10:00 的空档、周一、周末都不属于任何一场：VWAP 不累积（取值为 NaN，线上留空），
// 也不开新单。已经持有的仓位不受影响，照常由止损/止盈了结。
//
// VWAP 什么时候清零、什么时候允许下单，共用这一份定义，免得两边各算一套而走样。
public static class TradingSession {
    private const int OpenHour = 10;
    private const int CloseHour = 6;

    public static bool IsInSession(DateTime time) {
        return GetDaySessionStart(time).HasValue;
    }

    // 这个时间属于哪一场，返回那一场开盘的 10:00；不属于任何一场就返回 null。
    public static DateTime? GetDaySessionStart(DateTime time) {
        DateTime sessionStart;

        if (time.Hour >= OpenHour)
            sessionStart = time.Date.AddHours(OpenHour); // 当天 10:00 开的那一场
        else if (time.Hour < CloseHour)
            sessionStart = time.Date.AddDays(-1).AddHours(OpenHour); // 昨天 10:00 开的那一场，还没收
        else
            return null; // 06:00~10:00 的空档

        return IsTradingDay(sessionStart.DayOfWeek) ? sessionStart : (DateTime?)null;
    }

    // 只有周二到周五 10:00 开的场才交易：周一不交易，周六周日休市。
    // 注意这也把周二凌晨挡在外面 —— 那段时间属于周一开的那一场。
    private static bool IsTradingDay(DayOfWeek dayOfWeek) {
        return dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday || dayOfWeek == DayOfWeek.Thursday ||
               dayOfWeek == DayOfWeek.Friday;
    }

    // 这个时间属于哪一周，返回那一周周二的 10:00。
    public static DateTime? GetWeekSessionStart(DateTime time) {
        DateTime? daySessionStart = GetDaySessionStart(time);

        if (!daySessionStart.HasValue)
            return null;

        int daysSinceTuesday = ((int)daySessionStart.Value.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        return daySessionStart.Value.AddDays(-daysSinceTuesday);
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
