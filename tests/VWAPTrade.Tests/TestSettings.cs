using cAlgo.Robots;

namespace VWAPTrade.Tests {
    // Settings for tests: 1% risk, 2R take profit, no stop offset, no breakeven stop and every
    // filter off, so what is under test is only what the test states.
    internal static class TestSettings {
        public static TradeSettingsModel NoStopOffset() => Create();

        public static TradeSettingsModel WithStopOffsetTicks(int stopOffsetTicks) => Create(stopOffsetTicks: stopOffsetTicks);

        public static TradeSettingsModel Create(double riskPct = 1.0, double takeProfitR = 2.0, int stopOffsetTicks = 0) =>
            new(riskPct, takeProfitR, stopOffsetTicks, breakevenTriggerR: 0.0, breakevenOffsetTicks: 0,
                vwapFilters: NoFilters(), vwapSlopeLookbackBars: 6, atr14Source: Atr14SourceModel.ATR14_H1,
                tradeDirectionMode: TradeDirectionPermissionModel.All,
                longBelowDailyVwap: new LongBelowDailyVwapSettingsModel(lookbackBars: 6, blockCount: 0));

        // 全部过滤关闭：被测的几何与仓位计算不该被闸门影响。
        public static VwapFilterSettingsModel NoFilters() =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: false, gapChangeRateMin: 0.0, gapChangeRateMax: 0.0);
    }
}
