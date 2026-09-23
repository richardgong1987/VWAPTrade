namespace cAlgo.Robots;

// 启动前的参数校验。有问题就返回一句给用户看的话，没问题返回 null。
//
// 抽出来是为了能单测：这些规则一旦失效，cBot 会带着一套算错的参数安静地跑完整个回测，
// 那比直接停下来糟糕得多。纯判断，没有 cAlgo 依赖。
public static class StartupCheck {
    public static string FindError(bool isFiveMinuteChart, string timeFrameName, string orderLabel, int lookbackN,
        VwapFilterSettingsModel filters, DepartureSettingsModel departure, LongBelowDailyVwapSettingsModel longBelowDailyVwap) {
        // 标签为空的话，这个品种上每一个 "_L"/"_S" 结尾的单子都会被当成本 cBot 的单。
        if (string.IsNullOrWhiteSpace(orderLabel))
            return "订单标签不能为空。";

        // SlopeRateX 的 6/N 换算写死了「M5 上 6 根 = 30 分钟」，别的周期这个系数不成立。
        if (!isFiveMinuteChart)
            return $"VWAP Strong 只支持 M5：当前周期是 {timeFrameName}。SlopeRateX 的 6/N 换算以 M5 为准。";

        if (lookbackN < 1)
            return $"斜率回看K线数 N 必须 ≥ 1：当前是 {lookbackN}。";

        // Min > Max 区间为空，一笔都不会通过。不静默对调 —— 那会跑出一个谁也没打算测的区间。
        if (filters != null && filters.IsGapChangeRangeInverted)
            return $"扩口变化最小值 {filters.GapChangeRateMin} 大于最大值 {filters.GapChangeRateMax}，区间为空。";

        // 只在这道闸门开着时才校验：关着的时候这两个值根本不参与判断。
        if (departure != null && departure.IsEnabled) {
            if (departure.ConfirmBars < 1)
                return $"离开连续确认K线数必须 ≥ 1：当前是 {departure.ConfirmBars}。";

            if (departure.MaxWaitBars < 0)
                return $"离开后最大等待K线数不能为负：当前是 {departure.MaxWaitBars}。0 表示不限制。";
        }

        if (longBelowDailyVwap != null) {
            if (longBelowDailyVwap.BlockCount < 0)
                return $"黄线下K线阻断根数不能为负：当前是 {longBelowDailyVwap.BlockCount}。0 表示关闭。";

            // When off, its lookback does not participate, matching the other 0 = off filters.
            if (longBelowDailyVwap.IsEnabled) {
                if (longBelowDailyVwap.LookbackBars < 1)
                    return $"做多前回看K线数必须 ≥ 1：当前是 {longBelowDailyVwap.LookbackBars}。";

                if (longBelowDailyVwap.BlockCount > longBelowDailyVwap.LookbackBars)
                    return $"黄线下K线阻断根数 {longBelowDailyVwap.BlockCount} 不能大于回看K线数 {longBelowDailyVwap.LookbackBars}。";
            }
        }

        return null;
    }
}
