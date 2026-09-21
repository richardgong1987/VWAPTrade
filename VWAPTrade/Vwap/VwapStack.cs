namespace cAlgo.Robots;

// 方向闸门（docs/VWAP_Strong_V1.pdf 第 8 节的第 2、5、6 步）。要放行，这一根 K 线必须同时满足：
//
//   ① 排列：收盘价 > 日 VWAP > 周 VWAP 只开多；收盘价 < 日 VWAP < 周 VWAP 只开空。
//   ② 距离：GapMin > 0 时，要求 GapX_Selected ≥ GapMin。
//   ③ 速度：SlopeRateMin > 0 时，要求 SlopeRateX_Selected ≥ SlopeRateMin。
//
// 阈值填 0 就是那一道闸门完全关闭 —— 此时即使 ATR 或回看数据缺失也不能因此挡掉交易，
// 否则「关掉的过滤器」反而成了新的过滤条件。公式见 VwapStrongMetrics。
// 纯比较，没有 cAlgo 依赖，有单元测试。
public static class VwapStack {
    public static SignalSideModel ResolveSide(VwapStrongReadingModel reading, double gapMin, double slopeRateMin) {
        if (reading == null)
            return SignalSideModel.None;

        SignalSideModel side = ResolveSide(reading.Close, reading.DailyVwap, reading.WeeklyVwap);

        if (side == SignalSideModel.None)
            return SignalSideModel.None;

        VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(reading, side);

        if (!PassesFilter(metrics.GapXSelected, gapMin))
            return SignalSideModel.None;

        if (!PassesFilter(metrics.SlopeRateXSelected, slopeRateMin))
            return SignalSideModel.None;

        return side;
    }

    // 只看排列，不看距离和速度。
    public static SignalSideModel ResolveSide(double close, double dailyVwap, double weeklyVwap) {
        if (!VwapStrongMetrics.IsUsable(close) || !VwapStrongMetrics.IsUsable(dailyVwap) ||
            !VwapStrongMetrics.IsUsable(weeklyVwap))
            return SignalSideModel.None;

        if (close > dailyVwap && dailyVwap > weeklyVwap)
            return SignalSideModel.Buy;

        if (close < dailyVwap && dailyVwap < weeklyVwap)
            return SignalSideModel.Sell;

        return SignalSideModel.None;
    }

    // 阈值 ≤ 0 就是这道闸门没开，直接放行，连 ATR 可不可用都不看。开着的时候，算不出数值
    // （ATR 缺失、回看那根跨了日切）一律拦下 —— NaN 跟任何数比较都是 false，不显式挡掉反而会漏过去。
    // 阈值相等时放行（用的是 ≥）。
    public static bool PassesFilter(double value, double minimum) {
        if (minimum <= 0.0)
            return true;

        return VwapStrongMetrics.IsUsable(value) && value >= minimum;
    }
}
