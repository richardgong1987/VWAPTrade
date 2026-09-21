using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // The gate itself (docs/VWAP_Strong_V1.pdf §5.3, §6.4, §8): the stack, then GapMin against
    // GapX_Selected, then SlopeRateMin against SlopeRateX_Selected. A threshold of 0 switches that
    // filter off completely — even a missing ATR must not block the trade then.
    public class VwapStackFilterTests {
        // Long: gap = 4.0, slope over N=6 = 2.0. H1 ATR 8 -> GapX 0.5, SlopeRawX 0.25, rate 0.25.
        private static VwapFilterSettingsModel Filters(double gapMin = 0.0, double slopeRateMin = 0.0,
            bool useGapChangeFilter = false, double gapChangeRateMin = 0.0, double gapChangeRateMax = 0.0) =>
            new(gapMin, slopeRateMin, useGapChangeFilter, gapChangeRateMin, gapChangeRateMax);

        private static VwapStrongReadingModel Long(double atrH1 = 8.0, double dailyBefore = 102.0, int lookbackN = 6) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = 99.0,
                Atr14M5 = 2.0, Atr14H1 = atrH1, LookbackN = lookbackN,
                SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        [Fact]
        public void both_filters_off_lets_the_stack_decide_on_its_own() {
            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(Long(), Filters(gapMin: 0.0, slopeRateMin: 0.0)));
        }

        [Fact]
        public void a_missing_atr_cannot_block_a_trade_while_both_filters_are_off() {
            // Switched off means switched off: an unusable ATR must not become a filter of its own.
            VwapStrongReadingModel noAtr = Long(atrH1: double.NaN);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(noAtr, Filters(gapMin: 0.0, slopeRateMin: 0.0)));
        }

        [Fact]
        public void a_missing_lookback_cannot_block_a_trade_while_the_rate_filter_is_off() {
            VwapStrongReadingModel noLookback = Long(dailyBefore: double.NaN);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(noLookback, Filters(gapMin: 0.3, slopeRateMin: 0.0)));
        }

        [Fact]
        public void an_enabled_filter_rejects_a_missing_atr() {
            VwapStrongReadingModel noAtr = Long(atrH1: double.NaN);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(noAtr, Filters(gapMin: 0.1, slopeRateMin: 0.0)));
            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(noAtr, Filters(gapMin: 0.0, slopeRateMin: 0.1)));
        }

        [Fact]
        public void an_enabled_rate_filter_rejects_a_missing_lookback() {
            VwapStrongReadingModel noLookback = Long(dailyBefore: double.NaN);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(noLookback, Filters(gapMin: 0.0, slopeRateMin: 0.1)));
        }

        [Theory]
        [InlineData(0.5, SignalSideModel.Buy)] // exactly on the threshold passes
        [InlineData(0.49, SignalSideModel.Buy)]
        [InlineData(0.51, SignalSideModel.None)]
        public void the_gap_filter_uses_greater_or_equal(double gapMin, SignalSideModel expected) {
            Assert.Equal(expected, VwapStack.ResolveSide(Long(), Filters(gapMin: gapMin)));
        }

        [Theory]
        [InlineData(0.25, SignalSideModel.Buy)] // exactly on the threshold passes
        [InlineData(0.24, SignalSideModel.Buy)]
        [InlineData(0.26, SignalSideModel.None)]
        public void the_rate_filter_uses_greater_or_equal(double slopeRateMin, SignalSideModel expected) {
            Assert.Equal(expected, VwapStack.ResolveSide(Long(), Filters(slopeRateMin: slopeRateMin)));
        }

        [Fact]
        public void the_rate_filter_compares_the_normalised_speed_not_the_raw_slope() {
            // N=3 over the same 2.0 of movement: raw slope is still 0.25, but the 30-minute speed
            // is 0.50. A threshold of 0.4 passes only because the rate, not the raw slope, is used.
            VwapStrongReadingModel faster = Long(lookbackN: 3);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(faster, Filters(gapMin: 0.0, slopeRateMin: 0.4)));
            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(Long(lookbackN: 6), Filters(gapMin: 0.0, slopeRateMin: 0.4)));
        }

        [Fact]
        public void a_falling_vwap_can_never_satisfy_a_positive_rate_threshold() {
            VwapStrongReadingModel falling = Long(dailyBefore: 110.0);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(falling, Filters(gapMin: 0.0, slopeRateMin: 0.01)));
        }

        [Fact]
        public void the_filters_never_rescue_a_bar_that_fails_the_stack() {
            VwapStrongReadingModel belowVwap = Long();
            belowVwap.Close = 99.0; // close under the daily VWAP: no long

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(belowVwap, Filters(gapMin: 0.0, slopeRateMin: 0.0)));
        }

        [Fact]
        public void the_stack_still_refuses_a_crossed_pair_of_vwaps() {
            VwapStrongReadingModel crossed = Long();
            crossed.WeeklyVwap = 105.0; // daily < weekly while the close is above both

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(crossed, Filters(gapMin: 0.0, slopeRateMin: 0.0)));
        }
    }
}
