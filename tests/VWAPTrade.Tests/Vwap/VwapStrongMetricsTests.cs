using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // The VWAP Strong V1 formulas (docs/VWAP_Strong_V1.pdf §5, §6):
    //   GapX         = direction × (D − W) / ATR
    //   SlopeRawX    = direction × (D − D[N]) / ATR
    //   SlopeRateX30 = SlopeRawX × 6/N
    // direction is +1 long, −1 short, so a value moving against the trade is negative.
    public class VwapStrongMetricsTests {
        // A long: D − W = 4.0, D − D[N] = 2.0. M5 ATR 2.0, H1 ATR 8.0.
        private static VwapStrongReadingModel Long(int lookbackN = 6,
            Atr14SourceModel selected = Atr14SourceModel.ATR14_H1, double atrM5 = 2.0, double atrH1 = 8.0) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0,
                DailyVwapBefore = 102.0, WeeklyVwapBefore = 99.0,
                Atr14M5 = atrM5, Atr14H1 = atrH1, LookbackN = lookbackN, SelectedAtrPeriod = selected
            };

        // The mirror image of the long: W − D = 4.0, D[N] − D = 2.0.
        private static VwapStrongReadingModel Short(int lookbackN = 6,
            Atr14SourceModel selected = Atr14SourceModel.ATR14_H1) =>
            new() {
                Close = 94.0, DailyVwap = 96.0, WeeklyVwap = 100.0,
                DailyVwapBefore = 98.0, WeeklyVwapBefore = 101.0,
                Atr14M5 = 2.0, Atr14H1 = 8.0, LookbackN = lookbackN, SelectedAtrPeriod = selected
            };

        [Theory]
        [InlineData(3, 2.0)] // 6/3
        [InlineData(6, 1.0)] // 6/6
        [InlineData(9, 2.0 / 3.0)] // 6/9 = 0.666…
        public void the_rate_factor_is_six_over_n(int lookbackN, double expectedFactor) {
            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(Long(lookbackN), SignalSideModel.Buy);

            // SlopeRawX on H1 = 2.0 / 8.0 = 0.25 for every N; only the factor differs.
            Assert.Equal(0.25, metrics.SlopeRawXH1, precision: 9);
            Assert.Equal(0.25 * expectedFactor, metrics.SlopeRateX30H1, precision: 9);
        }

        [Fact]
        public void the_same_real_speed_normalises_to_the_same_rate_whatever_n_is() {
            // Three windows over which the VWAP moved at the same pace: 1.0 over 3 bars,
            // 2.0 over 6 bars, 3.0 over 9 bars. All must report the same 30-minute speed.
            double rate3 = RateFor(lookbackN: 3, dailyBefore: 103.0);
            double rate6 = RateFor(lookbackN: 6, dailyBefore: 102.0);
            double rate9 = RateFor(lookbackN: 9, dailyBefore: 101.0);

            Assert.Equal(rate3, rate6, precision: 9);
            Assert.Equal(rate6, rate9, precision: 9);
        }

        private static double RateFor(int lookbackN, double dailyBefore) {
            VwapStrongReadingModel reading = Long(lookbackN);
            reading.DailyVwapBefore = dailyBefore;

            return VwapStrongMetrics.Compute(reading, SignalSideModel.Buy).SlopeRateX30H1;
        }

        [Fact]
        public void long_and_short_are_mirror_images() {
            VwapStrongMetricsModel buy = VwapStrongMetrics.Compute(Long(), SignalSideModel.Buy);
            VwapStrongMetricsModel sell = VwapStrongMetrics.Compute(Short(), SignalSideModel.Sell);

            Assert.Equal(buy.GapXH1, sell.GapXH1, precision: 9);
            Assert.Equal(buy.SlopeRawXH1, sell.SlopeRawXH1, precision: 9);
            Assert.Equal(buy.SlopeRateX30H1, sell.SlopeRateX30H1, precision: 9);
            Assert.True(buy.GapXH1 > 0.0);
        }

        [Fact]
        public void a_vwap_moving_against_the_trade_gives_a_negative_slope() {
            // Long, but the daily VWAP was HIGHER N bars ago, so it is falling.
            VwapStrongReadingModel falling = Long();
            falling.DailyVwapBefore = 107.0;

            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(falling, SignalSideModel.Buy);

            Assert.True(metrics.SlopeRawXH1 < 0.0);
            Assert.True(metrics.SlopeRateX30H1 < 0.0);
        }

        [Fact]
        public void a_short_whose_vwap_is_rising_also_goes_negative() {
            VwapStrongReadingModel rising = Short();
            rising.DailyVwapBefore = 93.0;

            Assert.True(VwapStrongMetrics.Compute(rising, SignalSideModel.Sell).SlopeRateX30H1 < 0.0);
        }

        [Fact]
        public void switching_atr_changes_only_the_denominator() {
            VwapStrongMetricsModel onH1 = VwapStrongMetrics.Compute(Long(selected: Atr14SourceModel.ATR14_H1), SignalSideModel.Buy);
            VwapStrongMetricsModel onM5 = VwapStrongMetrics.Compute(Long(selected: Atr14SourceModel.ATR14_M5), SignalSideModel.Buy);

            // Both sets are computed either way — only the *_Selected values follow the choice.
            Assert.Equal(onH1.GapXM5, onM5.GapXM5, precision: 9);
            Assert.Equal(onH1.GapXH1, onM5.GapXH1, precision: 9);

            Assert.Equal(onH1.GapXH1, onH1.GapXSelected, precision: 9);
            Assert.Equal(onM5.GapXM5, onM5.GapXSelected, precision: 9);

            // The raw price distance is 4.0 in both cases: 4/8 on H1, 4/2 on M5.
            Assert.Equal(0.5, onH1.GapXSelected, precision: 9);
            Assert.Equal(2.0, onM5.GapXSelected, precision: 9);
            Assert.Equal(4.0, onH1.GapXSelected * 8.0, precision: 9);
            Assert.Equal(4.0, onM5.GapXSelected * 2.0, precision: 9);
        }

        [Fact]
        public void the_old_slope_column_keeps_the_raw_slope_of_the_selected_atr() {
            // N=3 so the rate factor is 2 and the two values cannot coincide by accident.
            VwapStrongMetricsModel metrics =
                VwapStrongMetrics.Compute(Long(lookbackN: 3, selected: Atr14SourceModel.ATR14_M5), SignalSideModel.Buy);

            Assert.Equal(metrics.SlopeRawXM5, metrics.SlopeRawXSelected, precision: 9);
            Assert.Equal(metrics.SlopeRateX30M5, metrics.SlopeRateXSelected, precision: 9);
            Assert.Equal(metrics.SlopeRawXSelected * 2.0, metrics.SlopeRateXSelected, precision: 9);
        }

        [Fact]
        public void a_missing_unselected_atr_leaves_only_its_own_research_fields_blank() {
            // H1 selected, M5 missing: the selected values must still be computed.
            VwapStrongReadingModel reading = Long(atrM5: double.NaN);

            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(reading, SignalSideModel.Buy);

            Assert.True(double.IsNaN(metrics.GapXM5));
            Assert.True(double.IsNaN(metrics.SlopeRateX30M5));
            Assert.Equal(0.5, metrics.GapXSelected, precision: 9);
            Assert.False(double.IsNaN(metrics.SlopeRateXSelected));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void an_unusable_atr_yields_no_value_rather_than_a_wrong_one(double atr) {
            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(Long(atrH1: atr), SignalSideModel.Buy);

            Assert.True(double.IsNaN(metrics.GapXH1));
            Assert.True(double.IsNaN(metrics.SlopeRateX30H1));
        }

        [Fact]
        public void a_missing_lookback_leaves_the_slope_unknown_but_keeps_the_gap() {
            // The detector passes NaN when the lookback bar is across the daily VWAP reset.
            VwapStrongReadingModel reading = Long();
            reading.DailyVwapBefore = double.NaN;
            reading.WeeklyVwapBefore = double.NaN;

            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(reading, SignalSideModel.Buy);

            Assert.True(double.IsNaN(metrics.SlopeRawXH1));
            Assert.True(double.IsNaN(metrics.SlopeRateX30H1));
            Assert.True(double.IsNaN(metrics.GapChangeX));
            Assert.Equal(0.5, metrics.GapXSelected, precision: 9); // the gap needs no lookback
        }

        [Fact]
        public void an_invalid_n_yields_no_rate() {
            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(Long(lookbackN: 0), SignalSideModel.Buy);

            Assert.False(double.IsNaN(metrics.SlopeRawXH1)); // the raw slope does not need N
            Assert.True(double.IsNaN(metrics.SlopeRateX30H1));
        }

        [Fact]
        public void the_gap_change_uses_the_selected_atr_and_is_signed_by_direction() {
            // Long: gap now = 4.0, gap N bars ago = 102 − 99 = 3.0, change = 1.0 over an H1 ATR of 8.
            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(Long(), SignalSideModel.Buy);

            Assert.Equal(0.125, metrics.GapChangeX, precision: 9);
        }
    }
}
