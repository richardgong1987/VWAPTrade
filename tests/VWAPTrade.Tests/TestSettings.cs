using cAlgo.Robots;

namespace VWAPTrade.Tests {
    // Settings for tests that are not about the stop offset or the breakeven stop: both are
    // switched off so the geometry under test is only what the test states.
    internal static class TestSettings {
        public static TradeSettingsModel NoStopOffset() => WithStopOffsetTicks(0);

        public static TradeSettingsModel WithStopOffsetTicks(int stopOffsetTicks) =>
            new(riskPct: 1.0, takeProfitR: 2.0, stopOffsetTicks, breakevenTriggerR: 0.0, breakevenOffsetTicks: 0,
                vwapFilters: NoFilters(), vwapSlopeLookbackBars: 6, atr14Source: Atr14SourceModel.ATR14_H1,
                tradeDirectionMode: TradeDirectionModeModel.All);

        // 全部过滤关闭：被测的几何与仓位计算不该被闸门影响。
        public static VwapFilterSettingsModel NoFilters() =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: false, gapChangeRateMin: 0.0, gapChangeRateMax: 0.0);
    }
}
