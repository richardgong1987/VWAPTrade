namespace cAlgo.Robots;

// 方向闸门。要开仓，这一根 K 线必须同时满足三件事：
//
//   ① 排列：收盘价 > 日 VWAP > 周 VWAP 只开多；收盘价 < 日 VWAP < 周 VWAP 只开空。
//   ② 间距：两条 VWAP 离得够开   GapX   = |日 − 周| / ATR14_H1 ≥ GapMin
//   ③ 斜率：日 VWAP 走得够明显   SlopeX = |日[0] − 日[N]| / ATR14_H1 ≥ SlopeMin
//
// ②③ 的分子都按方向取号，所以逆着方向走时是负数，一定过不了闸门；数值越大 = VWAP 朝正确方向
// 走得越明显。两者都除以 H1 的 ATR14 归一，同一个阈值才能在不同波动环境里通用。
//
// 阈值填 0 就是关掉那一道闸门（与 MovingAverageV1 的开口闸门同一约定）：关掉时不要求 ATR 可用，
// 免得取不到 ATR 就把所有交易挡光。纯比较，没有 cAlgo 依赖，有单元测试。
public static class VwapStack {
    public static SignalSideModel ResolveSide(VwapStrongReadingModel reading, double gapMin, double slopeMin) {
        if (reading == null)
            return SignalSideModel.None;

        SignalSideModel side = ResolveSide(reading.Close, reading.DailyVwap, reading.WeeklyVwap);

        if (side == SignalSideModel.None)
            return SignalSideModel.None;

        if (!PassesFilter(GetGapX(reading, side), gapMin))
            return SignalSideModel.None;

        if (!PassesFilter(GetSlopeX(reading, side), slopeMin))
            return SignalSideModel.None;

        return side;
    }

    // 只看排列，不看间距和斜率。
    public static SignalSideModel ResolveSide(double close, double dailyVwap, double weeklyVwap) {
        if (!IsUsable(close) || !IsUsable(dailyVwap) || !IsUsable(weeklyVwap))
            return SignalSideModel.None;

        if (close > dailyVwap && dailyVwap > weeklyVwap)
            return SignalSideModel.Buy;

        if (close < dailyVwap && dailyVwap < weeklyVwap)
            return SignalSideModel.Sell;

        return SignalSideModel.None;
    }

    public static double GetGapX(VwapStrongReadingModel reading, SignalSideModel side) {
        double gap = side == SignalSideModel.Buy
            ? reading.DailyVwap - reading.WeeklyVwap
            : reading.WeeklyVwap - reading.DailyVwap;

        return Normalize(gap, reading.Atr14H1);
    }

    public static double GetSlopeX(VwapStrongReadingModel reading, SignalSideModel side) {
        double slope = side == SignalSideModel.Buy
            ? reading.DailyVwap - reading.DailyVwapBefore
            : reading.DailyVwapBefore - reading.DailyVwap;

        return Normalize(slope, reading.Atr14H1);
    }

    private static double Normalize(double distance, double atr) {
        if (!IsUsable(atr) || atr <= 0.0)
            return double.NaN;

        return distance / atr;
    }

    // 阈值 ≤ 0 就是这道闸门没开，直接放行。开着的时候，算不出数值（ATR 缺失、回看那根跨了场）
    // 一律拦下 —— NaN 跟任何数比较都是 false，不显式挡掉的话反而会漏过去。
    private static bool PassesFilter(double value, double minimum) {
        if (minimum <= 0.0)
            return true;

        return IsUsable(value) && value >= minimum;
    }

    private static bool IsUsable(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
