using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // Expansion efficiency (docs/VWAP2.docx): ExpansionEfficiency = GapChangeRateX30_Selected / GapX_Selected.
    // It catches a gap that is still widening, but too slowly for how wide it already is. With
    // ExpansionEfficiencyMin > 0 a value below it blocks, and so does a gap of 0 or less or a missing
    // input; 0 switches the filter off completely. It is independent of the gap-change range switch.
    public class ExpansionEfficiencyFilterTests {
        // The example in the spec: Gap 10, GapChange 0.04. H1 ATR 1.0 and N = 6, so the price
        // distances are the X values themselves: 0.04 / 10 = 0.004.
        private static VwapStrongReadingModel SpecLong(Atr14SourceModel selected = Atr14SourceModel.ATR14_H1) =>
            new() {
                Close = 111.0, DailyVwap = 110.0, WeeklyVwap = 100.0,
                DailyVwapBefore = 109.96, WeeklyVwapBefore = 100.0,
                Atr14M5 = 0.25, Atr14H1 = 1.0, LookbackN = 6, SelectedAtrPeriod = selected
            };

        // Long: GapX = 4 / 8 = 0.5; the gap was 102 − 99 = 3 N bars ago, so GapChangeRateX30 = 1 / 8 = 0.125
        // and the efficiency is 0.25.
        private static VwapStrongReadingModel Long(double dailyBefore = 102.0, double weeklyBefore = 99.0,
            double weeklyVwap = 100.0, double atrH1 = 8.0) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = weeklyVwap,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = weeklyBefore,
                Atr14M5 = 2.0, Atr14H1 = atrH1, LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        private static VwapFilterSettingsModel Filters(double expansionEfficiencyMin, bool useGapChangeFilter = false,
            double gapChangeRateMin = 0.0, double gapChangeRateMax = 0.0) =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: useGapChangeFilter, gapChangeRateMin: gapChangeRateMin,
                gapChangeRateMax: gapChangeRateMax, slopeEfficiencyMin: 0.0, expansionEfficiencyMin: expansionEfficiencyMin);

        private static VwapStrongMetricsModel Metrics(VwapStrongReadingModel reading) =>
            VwapStrongMetrics.Compute(reading, SignalSideModel.Buy);

        private static EntryGateModel FailedFilter(VwapStrongReadingModel reading, VwapFilterSettingsModel filters) =>
            VwapStack.FindFailedFilter(Metrics(reading), filters);

        [Fact]
        public void the_spec_example_is_blocked_at_the_candidate_threshold() {
            VwapStrongMetricsModel metrics = Metrics(SpecLong());

            Assert.Equal(10.0, metrics.GapXSelected, precision: 6);
            Assert.Equal(0.04, metrics.GapChangeRateX30Selected, precision: 6);
            Assert.Equal(0.004, metrics.ExpansionEfficiency, precision: 6);
            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(SpecLong(), Filters(0.0045)));
            Assert.Equal(EntryGateModel.None, FailedFilter(SpecLong(), Filters(0.0035)));
        }

        [Fact]
        public void a_gap_change_inside_its_own_range_can_still_fail_on_efficiency() {
            // The case the filter exists for: 0.04 clears a gap-change minimum of 0.03, but for a gap
            // of 10 it is too slow.
            VwapFilterSettingsModel filters = Filters(0.0045, useGapChangeFilter: true, gapChangeRateMin: 0.03,
                gapChangeRateMax: 0.10);

            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(SpecLong(), filters));
        }

        [Fact]
        public void the_atr_choice_does_not_change_the_efficiency() {
            // Both X values divide by the same ATR, so it cancels out.
            double onH1 = Metrics(SpecLong(Atr14SourceModel.ATR14_H1)).ExpansionEfficiency;
            double onM5 = Metrics(SpecLong(Atr14SourceModel.ATR14_M5)).ExpansionEfficiency;

            Assert.Equal(onH1, onM5, precision: 9);
        }

        [Theory]
        [InlineData(0.25, EntryGateModel.None)] // exactly on the threshold passes
        [InlineData(0.24, EntryGateModel.None)]
        [InlineData(0.26, EntryGateModel.ExpansionEfficiency)]
        public void the_filter_uses_greater_or_equal(double expansionEfficiencyMin, EntryGateModel expected) {
            Assert.Equal(expected, FailedFilter(Long(), Filters(expansionEfficiencyMin)));
        }

        [Fact]
        public void a_narrowing_gap_gives_a_negative_efficiency_that_is_blocked() {
            // The gap was 104 − 96 = 8 N bars ago and is 4 now. No absolute value: narrowing stays negative.
            VwapStrongReadingModel narrowing = Long(dailyBefore: 104.0, weeklyBefore: 96.0);

            Assert.True(Metrics(narrowing).ExpansionEfficiency < 0.0);
            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(narrowing, Filters(0.0045)));
        }

        [Theory]
        [InlineData(104.0)] // gap 0
        [InlineData(106.0)] // gap against the trade
        public void a_gap_of_zero_or_less_is_blocked(double weeklyVwap) {
            VwapStrongReadingModel noGap = Long(weeklyVwap: weeklyVwap);

            Assert.True(double.IsNaN(Metrics(noGap).ExpansionEfficiency));
            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(noGap, Filters(0.0045)));
        }

        [Fact]
        public void an_enabled_filter_blocks_a_missing_value() {
            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(Long(dailyBefore: double.NaN), Filters(0.0045)));
            Assert.Equal(EntryGateModel.ExpansionEfficiency, FailedFilter(Long(atrH1: double.NaN), Filters(0.0045)));
        }

        [Fact]
        public void zero_switches_the_filter_off_even_when_the_value_is_missing() {
            // An off filter must never become a filter of its own.
            Assert.Equal(EntryGateModel.None, FailedFilter(SpecLong(), Filters(0.0)));
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(dailyBefore: double.NaN), Filters(0.0)));
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(weeklyVwap: 104.0), Filters(0.0)));
        }
    }
}
