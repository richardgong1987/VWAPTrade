using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // The filters after the stack (docs/VWAP_Strong_V1.1.docx §6, steps 4/5): GapMin against
    // GapX_Selected, then SlopeRateMin against SlopeRateX_Selected, then the gap change. A threshold
    // of 0 switches that filter off completely — even a missing ATR must not block the trade then.
    // The stack itself (Strong or not) is VwapStackTests.
    public class VwapStackFilterTests {
        // Long: gap = 4.0, slope over N=6 = 2.0. H1 ATR 8 -> GapX 0.5, SlopeRawX 0.25, rate 0.25,
        // gap change 1.0 / 8 = 0.125, slope efficiency 0.25 / 0.5 = 0.5.
        private static VwapFilterSettingsModel Filters(double gapMin = 0.0, double slopeRateMin = 0.0,
            bool useGapChangeFilter = false, double gapChangeRateMin = 0.0, double gapChangeRateMax = 0.0,
            double slopeEfficiencyMin = 0.0) =>
            new(gapMin, slopeRateMin, useGapChangeFilter, gapChangeRateMin, gapChangeRateMax, slopeEfficiencyMin);

        private static VwapStrongReadingModel Long(double atrH1 = 8.0, double dailyBefore = 102.0, int lookbackN = 6) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = 99.0,
                Atr14M5 = 2.0, Atr14H1 = atrH1, LookbackN = lookbackN,
                SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        private static EntryGateModel FailedFilter(VwapStrongReadingModel reading, VwapFilterSettingsModel filters) =>
            VwapStack.FindFailedFilter(VwapStrongMetrics.Compute(reading, SignalSideModel.Buy), filters);

        [Fact]
        public void every_filter_off_passes() {
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(), Filters()));
        }

        [Fact]
        public void a_missing_atr_cannot_block_a_trade_while_both_filters_are_off() {
            // Switched off means switched off: an unusable ATR must not become a filter of its own.
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(atrH1: double.NaN), Filters()));
        }

        [Fact]
        public void a_missing_lookback_cannot_block_a_trade_while_the_rate_filter_is_off() {
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(dailyBefore: double.NaN), Filters(gapMin: 0.3)));
        }

        [Fact]
        public void an_enabled_filter_rejects_a_missing_atr() {
            VwapStrongReadingModel noAtr = Long(atrH1: double.NaN);

            Assert.Equal(EntryGateModel.GapMin, FailedFilter(noAtr, Filters(gapMin: 0.1)));
            Assert.Equal(EntryGateModel.SlopeRateMin, FailedFilter(noAtr, Filters(slopeRateMin: 0.1)));
        }

        [Fact]
        public void an_enabled_rate_filter_rejects_a_missing_lookback() {
            Assert.Equal(EntryGateModel.SlopeRateMin, FailedFilter(Long(dailyBefore: double.NaN), Filters(slopeRateMin: 0.1)));
        }

        [Theory]
        [InlineData(0.5, EntryGateModel.None)] // exactly on the threshold passes
        [InlineData(0.49, EntryGateModel.None)]
        [InlineData(0.51, EntryGateModel.GapMin)]
        public void the_gap_filter_uses_greater_or_equal(double gapMin, EntryGateModel expected) {
            Assert.Equal(expected, FailedFilter(Long(), Filters(gapMin: gapMin)));
        }

        [Theory]
        [InlineData(0.25, EntryGateModel.None)] // exactly on the threshold passes
        [InlineData(0.24, EntryGateModel.None)]
        [InlineData(0.26, EntryGateModel.SlopeRateMin)]
        public void the_rate_filter_uses_greater_or_equal(double slopeRateMin, EntryGateModel expected) {
            Assert.Equal(expected, FailedFilter(Long(), Filters(slopeRateMin: slopeRateMin)));
        }

        [Fact]
        public void the_rate_filter_compares_the_normalised_speed_not_the_raw_slope() {
            // N=3 over the same 2.0 of movement: raw slope is still 0.25, but the 30-minute speed
            // is 0.50. A threshold of 0.4 passes only because the rate, not the raw slope, is used.
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(lookbackN: 3), Filters(slopeRateMin: 0.4)));
            Assert.Equal(EntryGateModel.SlopeRateMin, FailedFilter(Long(lookbackN: 6), Filters(slopeRateMin: 0.4)));
        }

        [Fact]
        public void a_falling_vwap_can_never_satisfy_a_positive_rate_threshold() {
            Assert.Equal(EntryGateModel.SlopeRateMin, FailedFilter(Long(dailyBefore: 110.0), Filters(slopeRateMin: 0.01)));
        }

        [Fact]
        public void each_filter_is_named_when_it_is_the_one_that_fails() {
            Assert.Equal(EntryGateModel.GapMin, FailedFilter(Long(), Filters(gapMin: 0.6)));
            Assert.Equal(EntryGateModel.SlopeRateMin, FailedFilter(Long(), Filters(slopeRateMin: 0.3)));
            Assert.Equal(EntryGateModel.GapChange,
                FailedFilter(Long(), Filters(useGapChangeFilter: true, gapChangeRateMin: 0.2, gapChangeRateMax: 0.3)));
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedFilter(Long(), Filters(slopeEfficiencyMin: 0.6)));
        }

        [Fact]
        public void when_several_filters_fail_the_first_in_trading_order_is_named() {
            // debug.csv reports this gate, so it must be the one trading actually stopped at.
            VwapFilterSettingsModel allFail = Filters(gapMin: 0.6, slopeRateMin: 0.3, useGapChangeFilter: true,
                gapChangeRateMin: 0.2, gapChangeRateMax: 0.3, slopeEfficiencyMin: 0.6);

            Assert.Equal(EntryGateModel.GapMin, FailedFilter(Long(), allFail));
        }

        [Fact]
        public void slope_efficiency_is_checked_after_the_gap_change() {
            // It is an extra check after the existing three, not a replacement for any of them.
            VwapFilterSettingsModel both = Filters(useGapChangeFilter: true, gapChangeRateMin: 0.2, gapChangeRateMax: 0.3,
                slopeEfficiencyMin: 0.6);

            Assert.Equal(EntryGateModel.GapChange, FailedFilter(Long(), both));
        }
    }
}
