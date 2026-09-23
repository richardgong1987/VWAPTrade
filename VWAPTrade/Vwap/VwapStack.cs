namespace cAlgo.Robots;

// VWAP 过滤链（V1.1 第 6 节的第 2、4、5、6 步）。要放行，这一根 K 线必须同时满足：
//
//   ① 排列：收盘价 > 日 VWAP > 周 VWAP 只开多；收盘价 < 日 VWAP < 周 VWAP 只开空。
//   ② 距离：GapMin > 0 时，要求 GapX_Selected ≥ GapMin。
//   ③ 速度：SlopeRateMin > 0 时，要求 SlopeRateX_Selected ≥ SlopeRateMin。
//   ④ 扩口：UseGapChangeFilter 开启时，要求 Min ≤ GapChangeRateX30_Selected ≤ Max（闭区间）。
//
// ②③ 阈值填 0 就是那一道闸门完全关闭；④ 用独立开关（GapChange 允许负值，0 不能当关闭值）。
// 关闭时即使 ATR 或回看数据缺失也不能因此挡掉交易，否则「关掉的过滤器」反而成了新的过滤条件。
// 公式见 VwapStrongMetrics。方向许可（LongOnly / ShortOnly）不在这里，那是下单前的最后一道，
// 见 OrderExecutor。纯比较，没有 cAlgo 依赖，有单元测试。
public static class VwapStack {
    public static SignalSideModel ResolveSide(VwapStrongReadingModel reading, VwapFilterSettingsModel filters) {
        if (reading == null || filters == null)
            return SignalSideModel.None;

        SignalSideModel side = ResolveSide(reading.Close, reading.DailyVwap, reading.WeeklyVwap);

        return FindBlockingGate(reading, filters, side) == EntryGateModel.None ? side : SignalSideModel.None;
    }

    // The first of gates ①–④ that stops a trade on this side, or None when all of them pass.
    // ResolveSide is built on this, so the order here is the order trading uses; debug.csv reports
    // it for patterns on the yellow line that were not traded.
    public static EntryGateModel FindBlockingGate(VwapStrongReadingModel reading, VwapFilterSettingsModel filters,
        SignalSideModel side) {
        if (side == SignalSideModel.None || ResolveSide(reading.Close, reading.DailyVwap, reading.WeeklyVwap) != side)
            return EntryGateModel.Stack;

        VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(reading, side);

        if (!PassesFilter(metrics.GapXSelected, filters.GapMin))
            return EntryGateModel.GapMin;

        if (!PassesFilter(metrics.SlopeRateXSelected, filters.SlopeRateMin))
            return EntryGateModel.SlopeRateMin;

        if (!PassesGapChangeFilter(metrics.GapChangeRateX30Selected, filters))
            return EntryGateModel.GapChange;

        return EntryGateModel.None;
    }

    // 开关关着就完全放行，连数值可不可用都不看；开着时算不出数值一律拒绝（V1.1 第 4 节）。
    // 区间是闭的：等于 Min 或 Max 都算通过。
    public static bool PassesGapChangeFilter(double value, VwapFilterSettingsModel filters) {
        if (!filters.UseGapChangeFilter)
            return true;

        if (!VwapStrongMetrics.IsUsable(value))
            return false;

        return value >= filters.GapChangeRateMin && value <= filters.GapChangeRateMax;
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
