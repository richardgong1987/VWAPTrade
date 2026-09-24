using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // Slope efficiency (docs/VWAP.docx): SlopeEfficiency = SlopeRateX_Selected / GapX_Selected.
    // A wide gap with a daily VWAP that has gone flat is an ageing trend. With SlopeEfficiencyMin > 0
    // a value below it blocks, and so does a gap of 0 or less; 0 switches the filter off completely.
    public class SlopeEfficiencyFilterTests {
        // The case in the spec: 2020-12-11 13:15, XAUUSD M5, S_Eng_1 short. H1 ATR 1.0, so the price
        // distances are the X values themselves: GapX 12.611813, SlopeRateX 0.109411 over N = 6.
        private static VwapStrongReadingModel SpecShort(Atr14SourceModel selected = Atr14SourceModel.ATR14_H1) =>
            new() {
                Close = 99.5, DailyVwap = 100.0, WeeklyVwap = 112.611813,
                DailyVwapBefore = 100.109411, WeeklyVwapBefore = 112.5,
                Atr14M5 = 0.25, Atr14H1 = 1.0, LookbackN = 6, SelectedAtrPeriod = selected
            };

        // Long: GapX = 4 / 8 = 0.5, SlopeRateX = 2 / 8 = 0.25, so the efficiency is 0.5.
        private static VwapStrongReadingModel Long(double dailyBefore = 102.0, double weeklyVwap = 100.0, double atrH1 = 8.0) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = weeklyVwap,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = 99.0,
                Atr14M5 = 2.0, Atr14H1 = atrH1, LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        // Only slope efficiency is ever switched on here, so a failure can only be SlopeEfficiency.
        private static VwapFilterSettingsModel Filters(double slopeEfficiencyMin) =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: false, gapChangeRateMin: 0.0, gapChangeRateMax: 0.0,
                slopeEfficiencyMin: slopeEfficiencyMin);

        private static VwapStrongMetricsModel Metrics(VwapStrongReadingModel reading, SignalSideModel side) =>
            VwapStrongMetrics.Compute(reading, side);

        private static EntryGateModel FailedFilter(VwapStrongReadingModel reading, SignalSideModel side, double slopeEfficiencyMin) =>
            VwapStack.FindFailedFilter(Metrics(reading, side), Filters(slopeEfficiencyMin));

        private static EntryGateModel FailedLongFilter(VwapStrongReadingModel reading, double slopeEfficiencyMin) =>
            FailedFilter(reading, SignalSideModel.Buy, slopeEfficiencyMin);

        [Fact]
        public void the_spec_case_is_blocked_at_the_candidate_threshold() {
            VwapStrongMetricsModel metrics = Metrics(SpecShort(), SignalSideModel.Sell);

            Assert.Equal(12.611813, metrics.GapXSelected, precision: 6);
            Assert.Equal(0.109411, metrics.SlopeRateXSelected, precision: 6);
            Assert.Equal(0.008675, metrics.SlopeEfficiency, precision: 6);
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedFilter(SpecShort(), SignalSideModel.Sell, 0.01));
            Assert.Equal(EntryGateModel.None, FailedFilter(SpecShort(), SignalSideModel.Sell, 0.008));
        }

        [Fact]
        public void the_atr_choice_does_not_change_the_efficiency() {
            // Both X values divide by the same ATR, so it cancels out.
            double onH1 = Metrics(SpecShort(Atr14SourceModel.ATR14_H1), SignalSideModel.Sell).SlopeEfficiency;
            double onM5 = Metrics(SpecShort(Atr14SourceModel.ATR14_M5), SignalSideModel.Sell).SlopeEfficiency;

            Assert.Equal(onH1, onM5, precision: 9);
        }

        [Theory]
        [InlineData(0.5, EntryGateModel.None)] // exactly on the threshold passes
        [InlineData(0.49, EntryGateModel.None)]
        [InlineData(0.51, EntryGateModel.SlopeEfficiency)]
        public void the_filter_uses_greater_or_equal(double slopeEfficiencyMin, EntryGateModel expected) {
            Assert.Equal(expected, FailedLongFilter(Long(), slopeEfficiencyMin));
        }

        [Fact]
        public void a_vwap_moving_against_the_trade_gives_a_negative_efficiency_that_is_blocked() {
            // The daily VWAP was higher N bars ago, so for a long it is falling.
            VwapStrongReadingModel falling = Long(dailyBefore: 110.0);

            Assert.True(Metrics(falling, SignalSideModel.Buy).SlopeEfficiency < 0.0);
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedLongFilter(falling, 0.001));
        }

        [Theory]
        [InlineData(104.0)] // gap 0
        [InlineData(106.0)] // gap against the trade
        public void a_gap_of_zero_or_less_is_blocked(double weeklyVwap) {
            VwapStrongReadingModel noGap = Long(weeklyVwap: weeklyVwap);

            Assert.True(double.IsNaN(Metrics(noGap, SignalSideModel.Buy).SlopeEfficiency));
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedLongFilter(noGap, 0.01));
        }

        [Fact]
        public void an_enabled_filter_blocks_a_missing_value() {
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedLongFilter(Long(dailyBefore: double.NaN), 0.01));
            Assert.Equal(EntryGateModel.SlopeEfficiency, FailedLongFilter(Long(atrH1: double.NaN), 0.01));
        }

        [Fact]
        public void zero_switches_the_filter_off_even_when_the_value_is_missing() {
            // An off filter must never become a filter of its own.
            Assert.Equal(EntryGateModel.None, FailedFilter(SpecShort(), SignalSideModel.Sell, 0.0));
            Assert.Equal(EntryGateModel.None, FailedLongFilter(Long(dailyBefore: double.NaN), 0.0));
            Assert.Equal(EntryGateModel.None, FailedLongFilter(Long(weeklyVwap: 104.0), 0.0));
        }
    }
}
