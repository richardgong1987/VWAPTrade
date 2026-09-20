using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // On top of the stack (close > daily > weekly for longs), a Strong setup must also clear two
    // ATR-normalised filters:
    //   GapX   = |daily - weekly|      / ATR14_H1  >= GapMin
    //   SlopeX = |daily - daily[N]|    / ATR14_H1  >= SlopeMin
    // Both numerators are signed by direction, so a VWAP moving against the trade is negative and
    // can never pass. A threshold of 0 switches that filter off.
    public class VwapStrongFilterTests {
        private const double Atr = 2.0;

        [Fact]
        public void a_long_passes_when_both_filters_clear_their_minimum() {
            // gap = (105 - 100) / 2 = 2.5 ; slope = (105 - 101) / 2 = 2.0
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: 101.0);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(reading, gapMin: 2.0, slopeMin: 1.5));
            Assert.Equal(2.5, VwapStack.GetGapX(reading, SignalSideModel.Buy), precision: 6);
            Assert.Equal(2.0, VwapStack.GetSlopeX(reading, SignalSideModel.Buy), precision: 6);
        }

        [Fact]
        public void a_long_is_refused_when_the_two_vwaps_are_too_close_together() {
            // gap = (100.5 - 100) / 2 = 0.25, below the 1.0 minimum.
            VwapStrongReadingModel reading = Long(daily: 100.5, weekly: 100.0, dailyBefore: 99.0);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 1.0, slopeMin: 0.0));
        }

        [Fact]
        public void a_long_is_refused_when_the_daily_vwap_is_not_rising_fast_enough() {
            // slope = (105 - 104.5) / 2 = 0.25, below the 1.0 minimum.
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: 104.5);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 1.0));
        }

        [Fact]
        public void a_long_whose_daily_vwap_is_falling_can_never_pass_the_slope_filter() {
            // The daily VWAP moved down over the lookback, so the signed slope is negative.
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: 107.0);

            Assert.Equal(-1.0, VwapStack.GetSlopeX(reading, SignalSideModel.Buy), precision: 6);
            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.1));
        }

        [Fact]
        public void a_short_measures_both_filters_the_other_way_round() {
            // gap = (100 - 95) / 2 = 2.5 ; slope = (99 - 95) / 2 = 2.0
            VwapStrongReadingModel reading = Short(daily: 95.0, weekly: 100.0, dailyBefore: 99.0);

            Assert.Equal(SignalSideModel.Sell, VwapStack.ResolveSide(reading, gapMin: 2.0, slopeMin: 1.5));
            Assert.Equal(2.5, VwapStack.GetGapX(reading, SignalSideModel.Sell), precision: 6);
            Assert.Equal(2.0, VwapStack.GetSlopeX(reading, SignalSideModel.Sell), precision: 6);
        }

        [Fact]
        public void a_short_whose_daily_vwap_is_rising_is_refused() {
            VwapStrongReadingModel reading = Short(daily: 95.0, weekly: 100.0, dailyBefore: 93.0);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.1));
        }

        [Fact]
        public void the_same_distance_counts_for_less_when_volatility_is_higher() {
            // Identical prices, ATR four times larger: both ratios fall to a quarter.
            VwapStrongReadingModel calm = Long(daily: 105.0, weekly: 100.0, dailyBefore: 101.0, atr: 2.0);
            VwapStrongReadingModel wild = Long(daily: 105.0, weekly: 100.0, dailyBefore: 101.0, atr: 8.0);

            Assert.Equal(2.5, VwapStack.GetGapX(calm, SignalSideModel.Buy), precision: 6);
            Assert.Equal(0.625, VwapStack.GetGapX(wild, SignalSideModel.Buy), precision: 6);
            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(calm, gapMin: 1.0, slopeMin: 0.0));
            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(wild, gapMin: 1.0, slopeMin: 0.0));
        }

        [Fact]
        public void a_threshold_of_zero_switches_that_filter_off() {
            // A gap and a slope that would fail any positive minimum still pass when both are 0.
            VwapStrongReadingModel reading = Long(daily: 100.001, weekly: 100.0, dailyBefore: 100.0009);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.0));
        }

        [Fact]
        public void a_missing_atr_blocks_the_trade_while_a_filter_is_on() {
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: 101.0, atr: double.NaN);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 1.0, slopeMin: 0.0));
            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 1.0));
        }

        [Fact]
        public void a_missing_atr_is_harmless_when_both_filters_are_off() {
            // Switched off means switched off: a missing ATR must not silently block every trade.
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: 101.0, atr: double.NaN);

            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.0));
        }

        [Fact]
        public void a_missing_lookback_value_blocks_the_trade_while_the_slope_filter_is_on() {
            // The detector passes NaN when the lookback bar is in an earlier session, where the
            // daily VWAP was reset and the difference would be meaningless.
            VwapStrongReadingModel reading = Long(daily: 105.0, weekly: 100.0, dailyBefore: double.NaN);

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.5));
            // The gap filter does not depend on the lookback, so it still works on its own.
            Assert.Equal(SignalSideModel.Buy, VwapStack.ResolveSide(reading, gapMin: 2.0, slopeMin: 0.0));
        }

        [Fact]
        public void the_filters_never_rescue_a_bar_that_fails_the_stack() {
            // Wide gap and steep slope, but the close sits below the daily VWAP.
            VwapStrongReadingModel reading = new() {
                Close = 99.0, DailyVwap = 105.0, WeeklyVwap = 100.0, DailyVwapBefore = 101.0, Atr14H1 = Atr
            };

            Assert.Equal(SignalSideModel.None, VwapStack.ResolveSide(reading, gapMin: 0.0, slopeMin: 0.0));
        }

        private static VwapStrongReadingModel Long(double daily, double weekly, double dailyBefore, double atr = Atr) =>
            new() { Close = daily + 1.0, DailyVwap = daily, WeeklyVwap = weekly, DailyVwapBefore = dailyBefore, Atr14H1 = atr };

        private static VwapStrongReadingModel Short(double daily, double weekly, double dailyBefore, double atr = Atr) =>
            new() { Close = daily - 1.0, DailyVwap = daily, WeeklyVwap = weekly, DailyVwapBefore = dailyBefore, Atr14H1 = atr };
    }
}
