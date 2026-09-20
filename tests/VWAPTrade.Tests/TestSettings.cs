using cAlgo.Robots;

namespace VWAPTrade.Tests {
    // Settings for tests that are not about the stop offset or the breakeven stop: both are
    // switched off so the geometry under test is only what the test states.
    internal static class TestSettings {
        public static TradeSettingsModel NoStopOffset() => WithStopOffsetTicks(0);

        public static TradeSettingsModel WithStopOffsetTicks(int stopOffsetTicks) =>
            new(riskPct: 1.0, takeProfitR: 2.0, stopOffsetTicks, breakevenTriggerR: 0.0, breakevenOffsetTicks: 0,
                vwapGapMin: 0.0, vwapSlopeMin: 0.0, vwapSlopeLookbackBars: 12);
    }
}
